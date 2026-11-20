using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static Transform[] DungeonModules()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.name.StartsWith("Dungeon_", System.StringComparison.Ordinal)).ToArray();
        }

        private static string[] DungeonLayoutPrint(Transform[] modules)
        {
            return modules.Select(t => t.name + "@" + t.position.ToString("0.00") + "/" + Mathf.RoundToInt(t.eulerAngles.y)).OrderBy(s => s, System.StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// The crypt reworked with the purchased dark fantasy set (owner task 2026-11-19/20). The set is private art:
        /// without it the dungeon keeps its previous look and this check is reported as ignored.
        /// </summary>
        [UnityTest]
        public IEnumerator DressedCryptFacesItsWallsInwardKeepsTheLaneFreeAndStaysPassive()
        {
            InstallDungeonFixture(); MoveStory(0,18000);
            yield return StoryAction(HungerAction.OpenDungeonSeal);
            MoveStory(0,24000); TravelStory(); yield return null;
            Assert.IsTrue(Game.InDungeon);
            if (!PassiveArtVisuals.Has("DungeonCorner")) Assert.Ignore("Requires the private reworked crypt set (purchased pack, never in the repository)");
            var map = Game.Progression.Dungeon.Current.Layout;
            Transform[] modules = DungeonModules();
            int walls = 0, props = 0, corners = 0, relief = 0;
            foreach (Transform module in modules)
            {
                string name = module.name;
                foreach (Collider collider in module.GetComponentsInChildren<Collider>(true))
                    Assert.IsFalse(collider.enabled, name + ": dressing never collides, the Core owns movement");
                Assert.IsEmpty(module.GetComponentsInChildren<ParticleSystem>(true), name + ": flames are the game's own quads");
                if (!name.StartsWith("Dungeon_Room_Light_", System.StringComparison.Ordinal) && !name.StartsWith("Dungeon_Portal_", System.StringComparison.Ordinal))
                    Assert.IsEmpty(module.GetComponentsInChildren<Light>(true), name + ": only rooms and passages shine");
                bool wall = name.StartsWith("Dungeon_Wall_", System.StringComparison.Ordinal), prop = name.StartsWith("Dungeon_Prop_", System.StringComparison.Ordinal);
                if (name.StartsWith("Dungeon_Corner_", System.StringComparison.Ordinal)) corners++;
                if (!wall && !prop) continue;
                string[] parts = name.Split('_');
                int side = "WESN".IndexOf(parts[2][0]), x = int.Parse(parts[3]), z = int.Parse(parts[4]);
                Assert.IsTrue(map.IsFloor(x, z), name + " belongs to a floor tile of the Core map");
                Vector3 inward = CryptDungeonDressing.Inward(side);
                Assert.IsFalse(map.IsFloor(x - Mathf.RoundToInt(inward.x), z - Mathf.RoundToInt(inward.z)), name + " stands at a real wall");
                if (wall)
                {
                    walls++;
                    Assert.AreEqual(0f, Mathf.DeltaAngle(CryptDungeonDressing.WallYaw(side), module.eulerAngles.y), .01f, name + " must face its room");
                    Assert.Less((module.position - CryptDungeonDressing.WallCentre(x, z, side)).magnitude, .001f, name + " stands on the tile border");
                    MeshFilter mesh = module.GetComponentInChildren<MeshFilter>();
                    if (mesh != null && mesh.sharedMesh.bounds.max.z > .5f)
                    {
                        relief++;
                        Assert.AreNotEqual(0, x, name + ": the middle lane keeps plain walls");
                    }
                }
                else
                {
                    props++;
                    Assert.AreNotEqual(0, x, name + ": nothing lies in the middle lane");
                    Vector3 local = module.position - new Vector3(x * CryptDungeonDressing.TileMetres, 0f, z * CryptDungeonDressing.TileMetres);
                    Assert.Less(Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.z)), CryptDungeonDressing.TileMetres * .5f, name + " stays inside its tile");
                    Assert.GreaterOrEqual(local.magnitude, 1f, name + " leaves the middle of the tile free");
                }
            }
            Assert.Greater(walls, 40); Assert.Greater(corners, 4); Assert.Greater(props, 0); Assert.Greater(relief, 0);
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.name.StartsWith("Dungeon_Room_Light_", System.StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(map.RoomCount, lights.Length, "One budgeted light per room");
            var atmosphere = (AshenAtmosphere)typeof(ArenaRuntime).GetField("_atmosphere", Flags).GetValue(_runtime);
            bool reduced = atmosphere.ReducedEffects;
            try
            {
                foreach (bool quiet in new[] { false, true })
                {
                    atmosphere.SetPreferences(atmosphere.MusicVolume, atmosphere.VoiceVolume, atmosphere.EffectsVolume, atmosphere.Muted, quiet);
                    typeof(ArenaRuntime).GetMethod("AnimateDungeonLights", Flags).Invoke(_runtime, null);
                    int lit = modules.SelectMany(m => m.GetComponentsInChildren<Light>(true)).Distinct().Count(l => l.enabled);
                    Assert.That(lit, Is.InRange(1, quiet ? ArenaRuntime.VillageLightReducedBudget : ArenaRuntime.VillageLightBudget));
                }
            }
            finally
            {
                atmosphere.SetPreferences(atmosphere.MusicVolume, atmosphere.VoiceVolume, atmosphere.EffectsVolume, atmosphere.Muted, reduced);
                typeof(ArenaRuntime).GetMethod("AnimateDungeonLights", Flags).Invoke(_runtime, null);
            }
            // Each room keeps its light over the room centre, where the fights happen, and shows its source.
            for (int room = 0; room < map.RoomCount; room++)
            {
                map.Room(room, out var centreX, out var centreZ);
                var centre = new Vector3(centreX / 1000f, 0f, centreZ / 1000f);
                Light roomLight = lights.Single(l => l.name == "Dungeon_Room_Light_" + room);
                Vector3 offset = roomLight.transform.position - centre;
                Assert.Less(new Vector2(offset.x, offset.z).magnitude, .01f, roomLight.name + " hangs over its room centre");
                GameObject lantern = GameObject.Find("Dungeon_Lantern_" + room);
                if (PassiveArtVisuals.Has("DungeonLantern"))
                {
                    Assert.IsNotNull(lantern, "Room " + room + " shows where its light comes from");
                    Assert.Less((lantern.transform.position - centre).magnitude, .01f);
                }
                GameObject shrine = GameObject.Find("Dungeon_Shrine_" + room);
                if (shrine != null)
                    Assert.LessOrEqual((shrine.transform.position - centre).magnitude, (CryptDungeonDressing.ShrineReach + 1) * CryptDungeonDressing.TileMetres,
                        shrine.name + " stands at a wall of its own room");
            }
            int passages = Game.Progression.Dungeon.Depth < 3 ? 2 : 1;
            Assert.AreEqual(passages, modules.Where(m => m.name.StartsWith("Dungeon_Portal_", System.StringComparison.Ordinal)).Sum(m => m.GetComponentsInChildren<Light>(true).Length),
                "One glow per passage frame");
            int candles = modules.Where(m => !m.name.StartsWith("Dungeon_Lantern_", System.StringComparison.Ordinal))
                .Sum(m => m.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Dungeon_Flame"));
            Assert.That(candles, Is.InRange(1, CryptDungeonDressing.FlameBudget), "Candles burn within the flame budget");
            foreach (Transform flame in modules.SelectMany(m => m.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Dungeon_Flame"))
                Assert.IsFalse(flame.GetComponent<MeshRenderer>().HasPropertyBlock(), "Flames share one material so that they draw in one batch");
            Assert.IsNotNull(GameObject.Find("Dungeon_Portal_Return"), "The way back has a frame");
            Assert.IsNotNull(GameObject.Find("ARE_Return_Passage"), "The walkable mark stays");

            // The same saved floor is dressed the same way every time it is shown.
            string[] before = DungeonLayoutPrint(modules);
            typeof(ArenaRuntime).GetMethod("BuildChapterScenery", Flags).Invoke(_runtime, null);
            yield return null;
            CollectionAssert.AreEqual(before, DungeonLayoutPrint(DungeonModules()));

            DungeonProject();
            Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();
            yield return Capture("dungeon-dressed-gameplay.png");
            // A second look from inside the first room, beside its candle shrine.
            if (map.RoomCount > 1)
            {
                map.Room(1, out var roomX, out var roomZ); DungeonWalk(roomX, roomZ);
                Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();
                yield return Capture("dungeon-dressed-room.png");
            }
        }
    }
}
