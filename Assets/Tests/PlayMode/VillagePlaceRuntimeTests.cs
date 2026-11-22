using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// The houses of Aschengrenze that can be entered, in the running game. The Core says on which side of a door
    /// the player is, what lies in a room and who is in it; these tests check that the game shows exactly that.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static readonly VillagePlace[] Houses = { VillagePlace.SickRoom, VillagePlace.Smithy, VillagePlace.Barn };

        private bool StepDoors() { return (bool)typeof(ArenaRuntime).GetMethod("StepVillageDoors", Flags).Invoke(_runtime, null); }

        private static EntryWorldLayout.Footprint HouseOf(VillagePlace place)
        {
            for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
                if (EntryWorldLayout.Building(i).Id == VillagePlaceLayout.House(place)) return EntryWorldLayout.Building(i);
            throw new InvalidOperationException("No building for " + place);
        }

        /// <summary>The player walks to a point on free ground with real steps, around whatever stands in the way.</summary>
        private void WalkPlayerTo(long tx, long tz)
        {
            const long cell = 500;
            Func<long, long, long> key = (x, z) => ((x + 400000) / cell) * 100000 + (z + 400000) / cell;
            long startX = Game.PlayerXmm / cell * cell, startZ = Game.PlayerZmm / cell * cell, goalX = tx / cell * cell, goalZ = tz / cell * cell;
            var from = new Dictionary<long, long[]>(); var open = new Queue<long[]>();
            open.Enqueue(new[] { startX, startZ }); from[key(startX, startZ)] = new[] { startX, startZ };
            long[] stepX = { cell, -cell, 0, 0 }, stepZ = { 0, 0, cell, -cell };
            while (open.Count > 0 && !from.ContainsKey(key(goalX, goalZ)))
            {
                long[] at = open.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    long nx = at[0] + stepX[d], nz = at[1] + stepZ[d];
                    if (from.ContainsKey(key(nx, nz)) || !Game.CanStand(nx, nz)) continue;
                    from[key(nx, nz)] = at; open.Enqueue(new[] { nx, nz });
                }
            }
            Assert.IsTrue(from.ContainsKey(key(goalX, goalZ)), "There is a way from " + Game.PlayerXmm + "," + Game.PlayerZmm + " to " + tx + "," + tz);
            var way = new List<long[]> { new[] { tx, tz } };
            for (long[] at = { goalX, goalZ }; at[0] != startX || at[1] != startZ; at = from[key(at[0], at[1])]) way.Add(at);
            way.Reverse();
            foreach (long[] point in way)
                for (int t = 0; t < 40; t++)
                {
                    long dx = point[0] - Game.PlayerXmm, dz = point[1] - Game.PlayerZmm;
                    if (Math.Abs(dx) <= 350 && Math.Abs(dz) <= 350) break;
                    long reach = Math.Max(Math.Abs(dx), Math.Abs(dz));
                    Game.SetMoveInput(dx * 10000 / reach, dz * 10000 / reach);
                    Game.Step();
                }
            Game.SetMoveInput(0, 0);
            Assert.LessOrEqual(Math.Max(Math.Abs(Game.PlayerXmm - tx), Math.Abs(Game.PlayerZmm - tz)), 700, "The player arrives");
        }

        /// <summary>Walk up to a house and into its door; the game puts up the room.</summary>
        private void EnterHouse(VillagePlace place)
        {
            WalkPlayerTo(VillagePlaceLayout.LeaveXmm(place), VillagePlaceLayout.LeaveZmm(place));
            Game.SetMoveInput(-VillagePlaceLayout.DoorOutX(place) * 10000, -VillagePlaceLayout.DoorOutZ(place) * 10000);
            for (int t = 0; t < 12 && Game.WalkingIntoHouse == VillagePlace.None; t++) Game.Step();
            Assert.AreEqual(place, Game.WalkingIntoHouse, "The player walks into the door of " + place);
            StringAssert.Contains(VillagePlaceLayout.Name(place).ToUpperInvariant() + " · HINEINGEHEN", _runtime.VillageDoorHint);
            Assert.IsTrue(StepDoors(), "The game lets the player in");
            Game.SetMoveInput(0, 0);
            Assert.AreEqual(place, Game.InsidePlace);
        }

        private void LeaveHouse()
        {
            VillagePlace place = Game.InsidePlace;
            WalkPlayerTo(VillagePlaceLayout.EnterXmm(place), VillagePlaceLayout.EnterZmm(place));
            Game.SetMoveInput(VillagePlaceLayout.RoomOutX * 10000, VillagePlaceLayout.RoomOutZ * 10000);
            for (int t = 0; t < 12 && !Game.WalkingOutOfRoom; t++) Game.Step();
            Assert.IsTrue(Game.WalkingOutOfRoom, "The player walks out of the door");
            Assert.AreEqual("TÜR: HINAUS INS DORF", _runtime.VillageDoorHint);
            Assert.IsTrue(StepDoors(), "The game lets the player out");
            Game.SetMoveInput(0, 0);
            Assert.AreEqual(VillagePlace.None, Game.InsidePlace);
        }

        /// <summary>Put one value of the Core's state by hand, for a picture of exactly that state. The Core is not stepped meanwhile.</summary>
        private static void Stage(object target, string member, object value)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = target.GetType().GetProperty(member, all);
            if (property != null) { property.SetValue(target, value, null); return; }
            FieldInfo field = target.GetType().GetField(member, all);
            Assert.IsNotNull(field, target.GetType().Name + " has no " + member);
            field.SetValue(target, value);
        }

        private void StageInVillage(VillageResidentState resident, VillageActivity activity, VillageWork work)
        {
            Stage(resident, "Site", VillageSite.Village); Stage(resident, "Destination", VillageSite.Village); Stage(resident, "TravelTicks", 0);
            Stage(resident, "Activity", activity); Stage(resident, "Work", work);
        }

        /// <summary>
        /// Every house: sign outside, door hint, the room with exactly the Core's stock, its lights, nobody and no
        /// fire while nobody is there, the worker and the sick where the Core has them, and the village again outside.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator HousesShowTheirRoomsAsTheCoreHasThem()
        {
            StartVillageWork();
            // This test is about houses that stand; how a lot becomes a house is checked with the lots.
            RaiseHouses(Houses);
            yield return RunFrames(1);
            var camera = (Camera)typeof(ArenaRuntime).GetField("_camera", Flags).GetValue(_runtime);
            CameraClearFlags sky = camera.clearFlags; bool fog = RenderSettings.fog;
            var torches = (System.Collections.ICollection)typeof(ArenaRuntime).GetField("_villageTorches", Flags).GetValue(_runtime);
            int lit = torches.Count;
            Assert.IsNull(_runtime.VillageDoorHint, "Away from the doors nothing is offered");
            VillageResidentState first = Economy.Residents[0];
            foreach (VillagePlace place in Houses)
            {
                ArenaRuntime.VillageWorkplace room = ArenaRuntime.RoomOf(place);
                GameObject sign = Find(ArenaRuntime.HouseSignPrefix + place);
                Assert.IsNotNull(sign, "A sign hangs at the door of " + place);
                var door = new Vector3(VillagePlaceLayout.DoorXmm(place) / 1000f, sign.transform.position.y, VillagePlaceLayout.DoorZmm(place) / 1000f);
                Assert.That(Vector3.Distance(sign.transform.position, door), Is.InRange(0.9f, 2.2f), "The sign stands beside the door, not in it");

                EnterHouse(place);
                StringAssert.Contains(VillagePlaceLayout.Name(place), Message());
                StringAssert.Contains(VillagePlaceLayout.Name(place), Game.ChapterName);
                StringAssert.Contains(VillagePlaceLayout.Name(place), ArenaRuntime.DisplayChapterName(Game), "The HUD names the room");
                Assert.AreEqual(0, torches.Count, "No village torch burns in a room");
                GameObject host = Find(ArenaRuntime.VillageRoomRoot);
                Assert.IsNotNull(host, "The room of " + place + " is put up");
                Assert.Less(Vector3.Distance(host.transform.position, new Vector3(Game.PlayerXmm / 1000f, 0f, Game.PlayerZmm / 1000f)), 6f, "The room stands where the player is");
                for (int g = 0; g < room.Goods.Length; g++)
                {
                    List<Transform> units = Units(host, room.Goods[g]);
                    Assert.AreEqual(room.Units[g], units.Count, place + " room for " + room.Goods[g]);
                    int expected = Mathf.Min(Economy.Commons(room.Goods[g]), units.Count);
                    for (int i = 0; i < units.Count; i++) Assert.AreEqual(i < expected, units[i].gameObject.activeSelf, place + ": " + room.Goods[g] + " unit " + i);
                }
                Light[] lights = host.GetComponentsInChildren<Light>(true);
                Assert.GreaterOrEqual(lights.Length, 2, place + " has its lamps");
                foreach (Light light in lights) Assert.AreEqual(LightShadows.None, light.shadows);
                yield return RunFrames(1);
                Assert.AreEqual(place, Game.InsidePlace, "Standing still, nobody is put out of the door");
                Assert.IsNull(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Woodyard), "The village is not shown from inside");
                Assert.IsNull(Find(ArenaRuntime.HouseSignPrefix + place));
                Assert.IsFalse(RenderSettings.fog, "No ash fog in a room");
                Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags, "No sky in a room");
                if (room.Fire) Assert.IsFalse(Find(ArenaRuntime.FireName).activeSelf, "No fire while nobody is at it in " + place);
                foreach (VillageResidentState resident in Economy.Residents)
                    Assert.IsFalse(Figure(resident) != null && Figure(resident).activeSelf && VillageEconomyLayout.HouseOf(VillageEconomyLayout.Seen(resident, out VillageSpot s) ? s : VillageSpot.Home) != place,
                        "Nobody stands in a room the Core does not have them in");

                // A state of the Core put by hand; the Core is not stepped until it is put back.
                VillageActivity keptActivity = first.Activity; VillageWork keptWork = first.Work; VillageSite keptSite = first.Site, keptDestination = first.Destination; int keptTravel = first.TravelTicks;
                if (place == VillagePlace.Smithy)
                {
                    StageInVillage(first, VillageActivity.Working, VillageWork.SmeltOre);
                    ulong token = Economy.StableHash; ulong world = Game.ComputeWorldHash();
                    UpdateVillageWork(0f); UpdateVillageWork(600f);
                    Assert.IsTrue(Figure(first).activeSelf, "The smith is seen in the smithy");
                    var stand = new Vector3(VillageEconomyLayout.StandXmm(VillageSpot.Smithy) / 1000f, Figure(first).transform.position.y, VillageEconomyLayout.StandZmm(VillageSpot.Smithy) / 1000f);
                    Assert.Less(Vector3.Distance(Figure(first).transform.position, stand), 0.3f, "The smith stands at the anvil");
                    Assert.IsTrue(Find(ArenaRuntime.FireName).activeSelf, "The forge burns while somebody works at it");
                    Assert.IsNotNull(Find(ArenaRuntime.FireName).GetComponentInChildren<Light>(), "The fire's light burns with the fire");
                    Assert.AreEqual(token, Economy.StableHash, "Showing the smith changes nothing");
                    Assert.AreEqual(world, Game.ComputeWorldHash());
                    StageInVillage(first, VillageActivity.Idle, VillageWork.None);
                    UpdateVillageWork(0f); UpdateVillageWork(600f); UpdateVillageWork(600f);
                    Assert.IsFalse(Figure(first).activeSelf, "Done with the work, the smith leaves the room");
                    Assert.IsFalse(Find(ArenaRuntime.FireName).activeSelf);
                }
                if (place == VillagePlace.SickRoom)
                {
                    StageInVillage(first, VillageActivity.Recovering, VillageWork.None);
                    UpdateVillageWork(0f); UpdateVillageWork(600f);
                    for (int bed = 0; bed < ArenaRuntime.SickBeds; bed++)
                        Assert.AreEqual(bed == 0, Find(ArenaRuntime.PatientPrefix + bed).activeSelf, "One resident is in care: bed " + bed);
                    Assert.IsFalse(Figure(first) != null && Figure(first).activeSelf, "Who lies in bed does not stand in the room");
                    Assert.IsTrue(Find(ArenaRuntime.FireName).activeSelf, "The hearth is lit for the sick");
                }
                StageInVillage(first, keptActivity, keptWork);
                Stage(first, "Site", keptSite); Stage(first, "Destination", keptDestination); Stage(first, "TravelTicks", keptTravel);
                UpdateVillageWork(0f);

                LeaveHouse();
                StringAssert.Contains("hinaus", Message());
                // Put back at once, before any sky has had its turn: torches, fog and the camera's background.
                Assert.AreEqual(lit, torches.Count, "The village torches burn again");
                Assert.AreEqual(fog, RenderSettings.fog, "The village's fog setting is back");
                Assert.AreEqual(sky, camera.clearFlags, "The camera draws the sky again");
                yield return RunFrames(1);
                Assert.IsNull(Find(ArenaRuntime.VillageRoomRoot), "Outside, the room is gone");
                Assert.IsNotNull(Find(ArenaRuntime.HouseSignPrefix + place));
                Assert.AreEqual(VillagePlace.None, Game.InsidePlace, "Outside, nobody is pulled back in");
                Assert.IsNotNull(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Woodyard), "The village is at work again");
                Assert.AreEqual(sky, camera.clearFlags, "The sky is back");
                Assert.AreEqual("Die Aschengrenze", Game.ChapterName);
                Assert.AreEqual("Die Aschengrenze", ArenaRuntime.DisplayChapterName(Game));
            }

            // The field shows the state of its crop, as the Core's sources have it.
            var sources = (int[])typeof(VillageEconomyState).GetField("_sources", Flags).GetValue(Economy);
            int keptTicks = sources[(int)VillageSource.CropTicks], keptRipe = sources[(int)VillageSource.RipePlot];
            string[] crops = { "Sown", "Grown", "Ripe" };
            int[][] staged = { new[] { 0, 0 }, new[] { VillageEconomyRules.CropGrowthTicks, 0 }, new[] { 1, 0 }, new[] { 0, 1 } };
            for (int state = 0; state < staged.Length; state++)
            {
                sources[(int)VillageSource.CropTicks] = staged[state][0]; sources[(int)VillageSource.RipePlot] = staged[state][1];
                Assert.AreEqual(state, ArenaRuntime.FieldCropState(Economy));
                UpdateVillageWork(0f);
                for (int i = 0; i < crops.Length; i++)
                    Assert.AreEqual(state == i + 1, Find(ArenaRuntime.CropPrefix + crops[i]).activeSelf, "Crop state " + state + ": " + crops[i]);
            }
            sources[(int)VillageSource.CropTicks] = keptTicks; sources[(int)VillageSource.RipePlot] = keptRipe;
        }

        /// <summary>
        /// Pictures of the houses, their rooms and the field, taken only on request (ARE_ENTRY_EVIDENCE_DIR).
        /// Every house is shown from its four sides, so that the side the door of the rules lies on can be compared
        /// with the door the house really has.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator VillageHousePicturesOnRequest()
        {
            string directory = Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory)) Assert.Ignore("Pictures are taken only when an evidence folder is named");
            StartVillageWork();
            yield return RunFrames(2);
            // First the lots as a new game has them, the smith's with a few logs brought; then the houses that stand.
            typeof(VillageBuildingState).GetMethod("Deliver", Flags).Invoke(Game.Progression.Houses, new object[] { VillagePlace.Smithy, 0, 6 });
            UpdateVillageWork(0f);
            foreach (VillagePlace place in Houses)
            {
                EntryWorldLayout.Footprint lot = HouseOf(place);
                var middle = new Vector3(lot.X / 1000f, 0.6f, lot.Z / 1000f);
                yield return Capture("lot-" + place.ToString().ToLowerInvariant() + ".png", overridePosition: middle + new Vector3(3.5f, 7.5f, 11.5f), overrideFocus: middle + new Vector3(0f, 0f, 1.5f));
            }
            RaiseHouses(Houses);
            yield return RunFrames(2);
            foreach (VillagePlace place in Houses)
            {
                EntryWorldLayout.Footprint house = HouseOf(place);
                var centre = new Vector3(house.X / 1000f, 1.6f, house.Z / 1000f);
                string name = place.ToString().ToLowerInvariant();
                foreach (var side in new[] { ("east", new Vector3(1, 0, 0)), ("west", new Vector3(-1, 0, 0)), ("north", new Vector3(0, 0, 1)), ("south", new Vector3(0, 0, -1)) })
                    yield return Capture("house-" + name + "-" + side.Item1 + ".png", overridePosition: centre + side.Item2 * 10.5f + Vector3.up * 3.4f, overrideFocus: centre);
                var door = new Vector3(VillagePlaceLayout.DoorXmm(place) / 1000f, 1.3f, VillagePlaceLayout.DoorZmm(place) / 1000f);
                var outward = new Vector3(VillagePlaceLayout.DoorOutX(place), 0f, VillagePlaceLayout.DoorOutZ(place));
                var along = new Vector3(outward.z, 0f, -outward.x);
                yield return Capture("door-" + name + ".png", overridePosition: door + outward * 6.5f + along * 2.2f + Vector3.up * 1.6f, overrideFocus: door);
            }
            var field = new Vector3(VillageEconomyLayout.PlaceXmm(VillageSpot.Field) / 1000f, 0.4f, VillageEconomyLayout.PlaceZmm(VillageSpot.Field) / 1000f + 2f);
            yield return Capture("field.png", overridePosition: field + new Vector3(2.5f, 6.5f, -9.5f), overrideFocus: field);
            foreach (VillagePlace place in Houses)
            {
                EnterHouse(place);
                yield return RunFrames(2);
                var room = new Vector3(VillagePlaceLayout.RoomXmm(place) / 1000f, 0.9f, VillagePlaceLayout.RoomZmm / 1000f);
                float depth = VillagePlaceLayout.HalfDepthMm(place) / 1000f;
                string name = place.ToString().ToLowerInvariant();
                yield return Capture("room-" + name + ".png", overridePosition: room + new Vector3(0f, 6.2f, -depth - 5.2f), overrideFocus: room + new Vector3(0f, 0f, 0.4f));
                yield return Capture("room-" + name + "-play.png");
                LeaveHouse();
                yield return RunFrames(2);
            }
        }
    }
}
