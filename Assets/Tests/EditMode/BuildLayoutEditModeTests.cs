using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AreArpg.Core;
using Fadenwerk.EditorTools;
using UnityEditor;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Bau-Modus: plan format, rules, deterministic brush and the passive scenery projection.</summary>
    public sealed class BuildLayoutEditModeTests
    {
        static BuildLayoutData Plan(params BuildPiece[] pieces)
        {
            var data = BuildLayoutRules.Empty("aschengrenze");
            data.pieces.AddRange(pieces);
            return data;
        }

        static BuildPiece House(string id, float x = 0f, float z = 0f) { return new BuildPiece { id = id, key = "VillageHome2", x = x, z = z, rot = 90f, scale = 1f }; }

        [Test]
        public void CatalogKeysAreUniqueAndStoryFiguresAreNotPlaceable()
        {
            var keys = new HashSet<string>();
            foreach (BuildCatalogEntry entry in BuildCatalog.All)
            {
                Assert.IsTrue(keys.Add(entry.Key), "duplicate " + entry.Key);
                Assert.IsNotNull(BuildCatalog.Categories, entry.Key);
                Assert.IsTrue(entry.Width > 0f && entry.Depth > 0f && entry.Height > 0f, entry.Key);
            }
            // Figures, loot and world shells belong to the game rules, never to the builder.
            foreach (string owned in new[] { "EntryHero", "Hero", "EntryNpc0", "EntryNpc1", "EntryNpc2", "MillPatient0", "RoadHunter", "RoadBear",
                         "CryptRattler", "Enemy0", "EntryEnemy0", "Sword", "EntryWeapon", "EntryRation", "AshChest", "EntryVillageWorld", "EntryCryptWorld", "VillageBackdrop" })
                Assert.IsNull(BuildCatalog.Find(owned), owned + " must not be placeable");
            foreach (string key in BuildCatalog.ForestKeys) Assert.IsNotNull(BuildCatalog.Find(key), key);
        }

        [Test]
        public void PlanRoundTripsThroughJson()
        {
            var surface = new BuildPiece { id = "b0002", key = "GrassVerge", x = -3.25f, z = 4f, rot = 0f, scale = 1f, w = 6f, d = 3f };
            BuildLayoutData data = Plan(House("b0001", 1.5f, -2f), surface);
            data.revision = 7;
            BuildLayoutData back = BuildLayoutRules.Parse(BuildLayoutRules.ToJson(data), "aschengrenze");
            Assert.AreEqual(7, back.revision);
            Assert.AreEqual(2, back.pieces.Count);
            Assert.AreEqual("VillageHome2", back.pieces[0].key);
            Assert.AreEqual(1.5f, back.pieces[0].x);
            Assert.AreEqual(90f, back.pieces[0].rot);
            Assert.AreEqual(6f, back.pieces[1].w);
            Assert.AreEqual(3f, back.pieces[1].d);
        }

        [Test]
        public void ValidationRejectsUnsafePlans()
        {
            Assert.IsNull(BuildLayoutRules.Validate(Plan(House("b0001")), "aschengrenze"));
            StringAssert.Contains("Unbekannter Baustein", BuildLayoutRules.Validate(Plan(new BuildPiece { id = "b0001", key = "EntryHero" }), null));
            StringAssert.Contains("außerhalb", BuildLayoutRules.Validate(Plan(House("b0001", 500f, 0f)), null));
            StringAssert.Contains("Doppelte", BuildLayoutRules.Validate(Plan(House("b0001"), House("b0001", 5f)), null));
            var turned = House("b0001"); turned.rot = 360f;
            StringAssert.Contains("Drehung", BuildLayoutRules.Validate(Plan(turned), null));
            var huge = House("b0001"); huge.scale = 9f;
            StringAssert.Contains("Größe", BuildLayoutRules.Validate(Plan(huge), null));
            var sized = House("b0001"); sized.w = 4f;
            StringAssert.Contains("Untergründe", BuildLayoutRules.Validate(Plan(sized), null));
            var nan = House("b0001"); nan.x = float.NaN;
            StringAssert.Contains("Ungültige Zahl", BuildLayoutRules.Validate(Plan(nan), null));
            StringAssert.Contains("Ungültige Teil-ID", BuildLayoutRules.Validate(Plan(House("x1")), null));
            StringAssert.Contains("gehört zu", BuildLayoutRules.Validate(Plan(House("b0001")), "krypta"));
            Assert.Throws<FormatException>(() => BuildLayoutRules.Parse("{\"schema\":2,\"area\":\"aschengrenze\"}", "aschengrenze"));
            Assert.Throws<FormatException>(() => BuildLayoutRules.Parse("{\"schema\":1,\"area\":\"nirgendwo\"}", null));
        }

        [Test]
        public void PrepareNormalisesRequestsAndAssignsIds()
        {
            BuildArea area = BuildAreas.Find("aschengrenze");
            string error;
            BuildPiece piece = BuildLayoutRules.Prepare(area, "b0003", "WoodlandTreeA", 1.23456f, -2.0049f, -15f, 1f, 0f, 0f, out error);
            Assert.IsNull(error);
            Assert.AreEqual(1.23f, piece.x, 1e-4f);
            Assert.AreEqual(-2f, piece.z, 1e-4f);
            Assert.AreEqual(345f, piece.rot, 1e-4f);
            BuildPiece patch = BuildLayoutRules.Prepare(area, "b0004", "RoadStone", 0f, 0f, 0f, 1f, 0f, 0f, out error);
            Assert.AreEqual(4f, patch.w); Assert.AreEqual(4f, patch.d);
            Assert.IsNull(BuildLayoutRules.Prepare(area, "b0005", "VillageHome0", 99f, 0f, 0f, 1f, 0f, 0f, out error));
            StringAssert.Contains("außerhalb", error);
            Assert.AreEqual("b0001", BuildLayoutRules.NextId(Plan()));
            Assert.AreEqual("b0013", BuildLayoutRules.NextId(Plan(House("b0002"), House("b0012", 4f))));
        }

        [Test]
        public void ForestBrushIsDeterministicAndStaysInItsCircle()
        {
            List<BuildPiece> a = BuildLayoutRules.Forest(5f, -3f, 6f, 14, 12345u);
            List<BuildPiece> b = BuildLayoutRules.Forest(5f, -3f, 6f, 14, 12345u);
            List<BuildPiece> c = BuildLayoutRules.Forest(5f, -3f, 6f, 14, 54321u);
            Assert.AreEqual(14, a.Count);
            bool differs = false;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].key, b[i].key); Assert.AreEqual(a[i].x, b[i].x); Assert.AreEqual(a[i].z, b[i].z);
                Assert.AreEqual(a[i].rot, b[i].rot); Assert.AreEqual(a[i].scale, b[i].scale);
                Assert.IsTrue(Vector2.Distance(new Vector2(a[i].x, a[i].z), new Vector2(5f, -3f)) <= 6f * 1.16f, "tree outside brush");
                Assert.IsNotNull(BuildCatalog.Find(a[i].key));
                differs |= a[i].x != c[i].x || a[i].key != c[i].key;
            }
            Assert.IsTrue(differs, "another seed must give another forest");
            Assert.AreEqual(BuildLayoutRules.MaxForestCount, BuildLayoutRules.Forest(0f, 0f, 5f, 999, 1u).Count);
        }

        [Test]
        public void ProjectionIsPassiveSceneryWithOneHostPerPiece()
        {
            BuildLayoutData data = Plan(House("b0001", 2f, 3f),
                new BuildPiece { id = "b0002", key = "GrassVerge", x = 0f, z = 0f, rot = 0f, scale = 1f, w = 6f, d = 2f },
                new BuildPiece { id = "b0003", key = "Npc1", x = -4f, z = 1f, rot = 180f, scale = 1f });
            Transform root = BuildLayer.Build(data, (x, z) => 0.25f);
            try
            {
                Assert.AreEqual(3, BuildLayer.CountPieces(root));
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                    Assert.IsFalse(collider.enabled, "placed scenery must never collide: " + collider.name);
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.Fail("placed scenery must not carry scripts: " + behaviour.GetType().Name);
                Transform house = root.Find("Bau_b0001_VillageHome2");
                Assert.IsNotNull(house);
                Assert.AreEqual(new Vector3(2f, 0.25f, 3f), house.position);
                Assert.AreEqual(90f, house.eulerAngles.y, 0.01f);
                Transform grass = root.Find("Bau_b0002_GrassVerge");
                Renderer tile = grass.GetComponentInChildren<Renderer>();
                Assert.AreEqual(6f, tile.bounds.size.x, 0.01f);
                Assert.AreEqual(2f, tile.bounds.size.z, 0.01f);
                Assert.Greater(root.GetComponentsInChildren<Renderer>().Length, 2);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        [TestCase("Npc0", "Villager0")]
        [TestCase("Npc1", "Villager1")]
        [TestCase("Npc2", "Villager2")]
        [TestCase("Merchant0", "Merchant0")]
        public void ResidentPlanKeysProjectTheCharacterCatalogOrPassiveFallback(string key, string visual)
        {
            var entry = BuildCatalog.Find(key);
            Assert.AreEqual(BuildCatalog.Residents, entry.Category);
            Assert.AreEqual(BuildCatalog.PrefabRoot + visual, entry.ResourcePath);
            bool hasArt = Resources.Load<GameObject>(entry.ResourcePath) != null;
            var piece = new BuildPiece { id = "b0001", key = key, scale = 1f };
            Transform host = BuildLayer.CreatePiece(piece, null, null, 0);
            try
            {
                Assert.AreEqual(!hasArt, host.Find("Platzhalter_" + key) != null);
                Assert.IsNotNull(host.GetComponentInChildren<Renderer>());
                Assert.IsFalse(host.GetComponentsInChildren<Collider>(true).Any(c => c.enabled));
                Assert.IsEmpty(host.GetComponentsInChildren<MonoBehaviour>(true));
                TestContext.WriteLine(visual + (hasArt ? ": baked prefab" : ": passive fallback"));
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void GhostMarksItsFootprintAndIsNotAPlanPiece()
        {
            Transform ghost = BuildLayer.CreateGhost(new BuildPiece { key = "VillageWatchtower", x = 1f, z = 1f, scale = 1f }, null);
            try
            {
                Assert.AreEqual(BuildLayer.GhostName, ghost.name);
                Assert.IsNotNull(ghost.Find("Vorschau-Rahmen"));
                Assert.IsNull(BuildLayer.Load("nirgendwo"));
            }
            finally { Object.DestroyImmediate(ghost.gameObject); }
        }

        [Serializable]
        sealed class PreviewState
        {
            public bool previewVisible = false;
            public string previewArea = "";
        }

        [Test]
        public void UndoRejectsStaleBuildersAndUnrelatedDiskHistory()
        {
            const string folder = "Assets/Resources/ARE/BuildLayouts/";
            const string history = "Library/BauModus/history-";
            // Only use a vacant area. Never replace the owner's saved plan or private undo history.
            BuildArea area = BuildAreas.All.FirstOrDefault(a => !File.Exists(folder + a.Key + ".json")
                && !File.Exists(folder + a.Key + ".json.meta") && !File.Exists(history + a.Key + ".json"));
            if (area == null) Assert.Ignore("Requires a project with a vacant building area.");
            string path = folder + area.Key + ".json";
            string historyPath = history + area.Key + ".json";
            var query = new Dictionary<string, string> { { "area", area.Key } };
            var stateReply = FadenwerkAuthoringBridge.Extension("GET", "state", "", query);
            var prior = JsonUtility.FromJson<PreviewState>(Encoding.UTF8.GetString(stateReply.body));
            FadenwerkExtensionReply Post(string action, int revision)
            {
                string json = "{\"area\":\"" + area.Key + "\",\"revision\":" + revision
                    + ",\"key\":\"VillageHome2\",\"scale\":1}";
                return FadenwerkAuthoringBridge.Extension("POST", action, json, query);
            }

            try
            {
                Assert.AreEqual(200, Post("place", 0).status);
                string saved = File.ReadAllText(path);
                string undoHistory = File.ReadAllText(historyPath);
                var stale = Post("undo", 0);
                Assert.AreEqual(409, stale.status, "Builder A must not undo builder B's newer placement");
                var conflict = JsonUtility.FromJson<BuildLayoutData>(Encoding.UTF8.GetString(stale.body));
                Assert.AreEqual(1, conflict.revision);
                Assert.AreEqual(1, conflict.pieces.Count);
                Assert.AreEqual(saved, File.ReadAllText(path));
                Assert.AreEqual(undoHistory, File.ReadAllText(historyPath));

                Assert.AreEqual(200, Post("undo", 1).status);
                var restored = BuildLayoutRules.Parse(File.ReadAllText(path), area.Key);
                Assert.AreEqual(2, restored.revision);
                Assert.IsEmpty(restored.pieces);

                Assert.AreEqual(200, Post("place", 2).status);
                var external = BuildLayoutRules.Parse(File.ReadAllText(path), area.Key);
                external.pieces[0].x = 2f; // Same revision, different plan after external file replacement.
                string replaced = BuildLayoutRules.ToJson(external);
                File.WriteAllText(path, replaced);
                var reply = FadenwerkAuthoringBridge.Extension("GET", "layout", "", query);
                StringAssert.Contains("\"canUndo\":false", Encoding.UTF8.GetString(reply.body));
                Assert.AreEqual(409, Post("undo", external.revision).status);
                Assert.AreEqual(replaced, File.ReadAllText(path));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (File.Exists(historyPath)) File.Delete(historyPath);
                if (prior.previewVisible)
                    FadenwerkAuthoringBridge.Extension("POST", "preview", "{\"area\":\"" + prior.previewArea + "\"}", query);
                else
                    EditorApplication.ExecuteMenuItem("Tools/Ashen Crypt/Bau-Modus/Vorschau aus der Szene entfernen");
            }
        }

        [Test]
        public void OnlyFixedEntryAreasOwnAPlan()
        {
            Assert.IsNull(ArenaRuntime.BuildAreaKey(new ArenaSimulation(123), false), "legacy arena has no plan");
            Assert.IsNull(ArenaRuntime.BuildAreaKey(null, true));
            var entry = new ArenaSimulation(123, CharacterProgression.CreateEntryProfile());
            Assert.AreEqual("krypta", ArenaRuntime.BuildAreaKey(entry, true));
            foreach (BuildArea area in BuildAreas.All)
            {
                Assert.AreEqual(area, BuildAreas.Find(area.Key));
                Assert.Greater(area.Extent, 10f);
            }
        }
    }
}
