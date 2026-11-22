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
    /// The lots of the village houses in the running game. The Core decides who asks, what is taken and when a house
    /// stands; these tests check that the game shows exactly that: no house on a lot, the piles of what was brought,
    /// the asker with their words, and the house once the player has come to the village again.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        private VillageBuildingState VillageHouses { get { return Game.Progression.Houses; } }

        /// <summary>Put the picture of the village up again from the Core, as an arrival does.</summary>
        private void RebuildVillagePicture()
        {
            foreach (string build in new[] { "BuildChapterScenery", "BuildPlacedLayer", "BuildVillageEconomy", "BuildVillageLights" })
            {
                MethodInfo method = typeof(ArenaRuntime).GetMethod(build, Flags);
                Assert.IsNotNull(method, build);
                // Optional parameters take their defaults, as in the game's own calls.
                method.Invoke(_runtime, method.GetParameters().Select(p => p.DefaultValue).ToArray());
            }
        }

        /// <summary>Houses that stand without the way there: for tests that are about the houses, not about how they were built.</summary>
        private void RaiseHouses(params VillagePlace[] places)
        {
            MethodInfo complete = typeof(VillageBuildingState).GetMethod("Complete", Flags);
            foreach (VillagePlace place in places) complete.Invoke(VillageHouses, new object[] { place });
            // Orsel's sick room stands only in a village whose mill quest is done; a save would refuse anything else.
            if (places.Contains(VillagePlace.SickRoom))
                typeof(VillageServicesState).GetProperty("OrselTeachesUnlocked").SetValue(Game.Progression.Village, true, null);
            RebuildVillagePicture();
        }

        /// <summary>Everything of the village's own scenery that is drawn on the ground of a house and rises above it.</summary>
        private List<string> DrawnOnTheGroundOf(VillagePlace place)
        {
            EntryWorldLayout.Footprint f = HouseOf(place);
            var drawn = new List<string>();
            GameObject work = Find(ArenaRuntime.VillageWorkRoot), people = Find(ArenaRuntime.VillageResidentsRoot);
            foreach (GameObject root in _scene.GetRootGameObjects())
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                    Transform t = renderer.transform;
                    if ((work != null && t.IsChildOf(work.transform)) || (people != null && t.IsChildOf(people.transform))) continue;
                    bool person = false;
                    for (Transform up = t; up != null && !person; up = up.parent) person = up.name == "Wanderer" || up.name.StartsWith("ARE_NPC_", StringComparison.Ordinal);
                    if (person) continue;
                    Bounds b = renderer.bounds;
                    if (b.size.x > 16f || b.size.z > 16f || b.max.y < 0.6f) continue;
                    if (Mathf.Abs(b.center.x - f.X / 1000f) <= f.HalfWidth / 1000f && Mathf.Abs(b.center.z - f.Z / 1000f) <= f.HalfDepth / 1000f)
                        drawn.Add(renderer.name + " in " + root.name);
                }
            return drawn;
        }

        private string SiteText(VillagePlace place)
        { return (string)typeof(ArenaRuntime).GetMethod("VillageSiteText", Flags).Invoke(_runtime, new object[] { place }); }

        private void TouchSite(ArenaHudLayout layout, Vector2 point)
        { typeof(ArenaRuntime).GetMethod("VillageSiteTouch", Flags).Invoke(_runtime, new object[] { layout, TouchPhase.Began, point }); }

        private int ShownPieces(VillagePlace place, VillageGood good)
        { return Units(Find(ArenaRuntime.BuildSitePrefix + place), good).Count(u => u.gameObject.activeSelf); }

        [UnityTest, Timeout(900000)]
        public IEnumerator LotsShowWhatWasBroughtAndBecomeHousesWhenThePlayerComesAgain()
        {
            // The first visit, before anybody has moved in: the lots and the smith are already there.
            EnterVillageAfterScene();
            yield return RunFrames(1);
            VillagePlace smithy = VillagePlace.Smithy;
            foreach (VillagePlace place in Houses)
            {
                Assert.IsNotNull(Find(ArenaRuntime.BuildSitePrefix + place), "The lot of " + place + " is staked out");
                Assert.Less(Mathf.Abs(Find(ArenaRuntime.BuildSitePrefix + place).transform.position.y), 0.06f, "The lot of " + place + " lies on the ground, not on the porch of a house that is not there");
                Assert.IsNull(Find(ArenaRuntime.HouseSignPrefix + place), "A lot has no sign");
                Assert.IsEmpty(DrawnOnTheGroundOf(place), "No house stands on the lot of " + place);
                foreach (VillageGood good in ArenaRuntime.VillageSiteModel.Goods) Assert.AreEqual(0, ShownPieces(place, good), "Nothing lies at the lot of " + place);
            }
            GameObject smith = Find(ArenaRuntime.SiteAskerPrefix + smithy);
            Assert.IsTrue(smith != null && smith.activeSelf, "The smith stands at his lot");
            Assert.IsNull(Find(ArenaRuntime.SiteAskerPrefix + VillagePlace.Barn), "The farmer asks only once the smithy stands");
            Assert.IsNull(Find(ArenaRuntime.SiteAskerPrefix + VillagePlace.SickRoom), "Orsel has not come yet");
            var asker = new Vector3(VillageBuildRules.AskerXmm(smithy) / 1000f, smith.transform.position.y, VillageBuildRules.AskerZmm(smithy) / 1000f);
            Assert.Less(Vector3.Distance(smith.transform.position, asker), 0.2f, "He stands where the rules have him");
            Assert.Less(Mathf.Abs(smith.transform.position.y - 1f), 0.06f, "He stands on the ground");
            Assert.IsNull(_runtime.VillageSiteHint, "Away from the lots nothing is offered");
            Assert.IsNull(_runtime.VillageDoorHint);

            // With the smith: the hint, his words, and what lies there.
            WalkPlayerTo(VillageBuildRules.AskerXmm(smithy) + 900, VillageBuildRules.AskerZmm(smithy) + 500);
            Assert.AreEqual(smithy, Game.SiteInReach);
            Assert.AreEqual("BAUPLATZ: SCHMIEDE · DER SCHMIED ANSPRECHEN", _runtime.VillageSiteHint);
            StringAssert.Contains("Der Schmied: Ich schmiede unter freiem Himmel", SiteText(smithy));
            StringAssert.Contains("Holz: 0 von 10 gebracht · du hast 0 bei dir", SiteText(smithy));
            StringAssert.Contains("Ascheisen: 0 von 6 gebracht · du hast 0 bei dir", SiteText(smithy));

            // Handing over goes through the panel and the game tick; the Core takes what the pack holds.
            var layout = new ArenaHudLayout(Screen.width, Screen.height, Screen.safeArea);
            Game.Progression.Crafting.AddMaterial(MaterialKind.Holz, 4);
            TouchSite(layout, ArenaRuntime.VillageSiteToggleRect(layout).center);
            TouchSite(layout, ArenaRuntime.VillageSiteGiftRect(layout, 1).center);
            yield return RunFrames(3);
            Assert.AreEqual(0, VillageHouses.Delivered(smithy, 1), "A button for something the pack does not hold does nothing");
            TouchSite(layout, ArenaRuntime.VillageSiteGiftRect(layout, 0).center);
            yield return RunFrames(3);
            Assert.AreEqual(4, VillageHouses.Delivered(smithy, 0), "The four logs lie at the lot");
            Assert.AreEqual(0, Game.Progression.Crafting.MaterialCount(MaterialKind.Holz), "The pack lost exactly those");
            StringAssert.Contains("4 × Holz", Message());
            Assert.AreEqual(4, ShownPieces(smithy, VillageGood.Wood), "The pile shows what the Core has");
            Assert.AreEqual(0, ShownPieces(smithy, VillageGood.Metal));
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), NativeJourneyMaps, out var saved) && saved.Houses.Delivered(smithy, 0) == 4,
                "An accepted hand-over is saved with the lot");

            // What is shown is no stock: showing every piece by hand changes nothing the Core knows, and the next frame is the truth again.
            ulong world = Game.ComputeWorldHash(); string save = Game.Progression.SaveString();
            foreach (Transform unit in Units(Find(ArenaRuntime.BuildSitePrefix + smithy), VillageGood.Wood)) unit.gameObject.SetActive(true);
            smith.SetActive(false);
            Assert.AreEqual(world, Game.ComputeWorldHash()); Assert.AreEqual(save, Game.Progression.SaveString());
            UpdateVillageWork(0f);
            Assert.AreEqual(4, ShownPieces(smithy, VillageGood.Wood));
            Assert.IsTrue(smith.activeSelf, "The smith is back in the picture");
            Assert.AreEqual(world, Game.ComputeWorldHash());

            // The rest. With the last piece the smith says thanks and sets to work.
            Game.Progression.Crafting.AddMaterial(MaterialKind.Holz, 9); Game.Progression.Crafting.AddMaterial(MaterialKind.Ascheisen, 6);
            TouchSite(layout, ArenaRuntime.VillageSiteGiftRect(layout, 0).center);
            yield return RunFrames(3);
            Assert.AreEqual(10, VillageHouses.Delivered(smithy, 0));
            Assert.AreEqual(3, Game.Progression.Crafting.MaterialCount(MaterialKind.Holz), "Nothing is taken beyond the need");
            TouchSite(layout, ArenaRuntime.VillageSiteGiftRect(layout, 1).center);
            yield return RunFrames(3);
            Assert.AreEqual(BuildStage.Raising, VillageHouses.Stage(smithy));
            Assert.AreEqual("Der Schmied: Vielen Dank. Ich mache mich an die Arbeit. Komm später wieder.", Message());
            StringAssert.Contains("Vielen Dank. Ich mache mich an die Arbeit. Komm später wieder.", SiteText(smithy));
            Assert.AreEqual(10, ShownPieces(smithy, VillageGood.Wood)); Assert.AreEqual(6, ShownPieces(smithy, VillageGood.Metal));
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), NativeJourneyMaps, out saved) && saved.Houses.Stage(smithy) == BuildStage.Raising);

            // The house does not appear out of nothing: as long as the player stays, the lot stays a lot.
            yield return RunFrames(3);
            UpdateVillageWork(600f);
            Assert.IsNotNull(Find(ArenaRuntime.BuildSitePrefix + smithy));
            Assert.IsEmpty(DrawnOnTheGroundOf(smithy), "No house while the player is there");
            Assert.IsNull(Find(ArenaRuntime.HouseSignPrefix + smithy));
            Assert.IsTrue(smith.activeSelf, "The smith is still at his lot");

            // The player comes to the village again. The Core raises the house on arrival (its own tests travel for
            // real); here the arrival is called by hand and the picture is put up as after a change of area.
            typeof(CharacterProgression).GetMethod("ArriveInVillage", Flags).Invoke(Game.Progression, null);
            Assert.IsTrue(VillageHouses.Built(smithy));
            RebuildVillagePicture();
            yield return RunFrames(1);
            Assert.IsNull(Find(ArenaRuntime.BuildSitePrefix + smithy), "The lot is gone");
            Assert.IsNull(Find(ArenaRuntime.SiteAskerPrefix + smithy), "Nobody asks at a house that stands");
            Assert.IsNotEmpty(DrawnOnTheGroundOf(smithy), "The smithy stands");
            Assert.IsNotNull(Find(ArenaRuntime.HouseSignPrefix + smithy), "Its sign hangs at the door");
            GameObject farmer = Find(ArenaRuntime.SiteAskerPrefix + VillagePlace.Barn);
            Assert.IsTrue(farmer != null && farmer.activeSelf, "Now the farmer asks for the barn");
            Assert.IsEmpty(DrawnOnTheGroundOf(VillagePlace.Barn), "The barn is still a lot");
            Assert.IsNull(_runtime.VillageSiteHint, "Coming out of the picture of the lot, nothing is offered at the smithy");
            EnterHouse(smithy);
            yield return RunFrames(1);
            Assert.IsNotNull(Find(ArenaRuntime.VillageRoomRoot), "The finished smithy has its room");
        }
    }
}
