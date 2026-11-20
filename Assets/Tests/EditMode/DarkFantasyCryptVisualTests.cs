using System;
using System.Collections.Generic;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The crypt visuals built from the purchased dark fantasy pack. The pack and the built visuals are private;
    /// without them these checks are reported as ignored, never as passed.
    /// </summary>
    public sealed class DarkFantasyCryptVisualTests
    {
        const string Work = "Assets/PrivateArt/DarkFantasy/";
        const string NotInstalled = "Requires the purchased dark fantasy pack with built crypt visuals (private, never in the repository)";

        static Type Tool()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("AreArpg.EditorTools.DarkFantasyCryptArt");
                if (type != null) return type;
            }
            Assert.Fail("DarkFantasyCryptArt is missing");
            return null;
        }

        static List<string> Built()
        {
            Type tool = Tool();
            MethodInfo owns = tool.GetMethod("Owns", BindingFlags.Public | BindingFlags.Static);
            var built = new List<string>();
            foreach (string key in (string[])tool.GetMethod("Keys", BindingFlags.Public | BindingFlags.Static).Invoke(null, null))
                if ((bool)owns.Invoke(null, new object[] { key })) built.Add(key);
            if (built.Count == 0) Assert.Ignore(NotInstalled);
            return built;
        }

        static Mesh MeshOf(GameObject prefab, string key)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Assert.AreEqual(1, filters.Length, key + ": one combined mesh");
            Assert.IsNotNull(filters[0].sharedMesh, key);
            return filters[0].sharedMesh;
        }

        [Test]
        public void TheSetNamesEveryModuleTheCryptAsksFor()
        {
            var keys = new List<string>((string[])Tool().GetMethod("Keys", BindingFlags.Public | BindingFlags.Static).Invoke(null, null));
            foreach (string key in new[] { "DungeonFloor", "DungeonWall", "DungeonWallB", "DungeonCorner", "DungeonGate", "DungeonPortal", "DungeonShrine", "DungeonLantern", "DungeonProp0" })
                CollectionAssert.Contains(keys, key);
            CollectionAssert.AllItemsAreUnique(keys);
            MethodInfo owns = Tool().GetMethod("Owns", BindingFlags.Public | BindingFlags.Static);
            Assert.IsFalse((bool)owns.Invoke(null, new object[] { "EntryEnemy0" }), "Only its own visuals belong to the set");
        }

        [Test]
        public void EveryCryptVisualIsOnePassiveMeshWithProjectMaterials()
        {
            foreach (string key in Built())
            {
                GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + key);
                Assert.IsNotNull(prefab, key + " belongs to the set but has no game visual");
                var host = new GameObject("CryptVisualHost");
                try
                {
                    Assert.IsTrue(PassiveArtVisuals.TryBindPrefab(host.transform, prefab), key + " must pass the game's passive-art check");
                    Assert.IsEmpty(host.GetComponentsInChildren<Collider>(true), key + ": no collision, the Core owns movement");
                    Assert.IsEmpty(host.GetComponentsInChildren<Light>(true), key + ": lights are budgeted by the game");
                    Assert.IsEmpty(host.GetComponentsInChildren<ParticleSystem>(true), key + ": flames are drawn by the game");
                    MeshRenderer[] renderers = host.GetComponentsInChildren<MeshRenderer>(true);
                    Assert.AreEqual(1, renderers.Length, key + ": one renderer");
                    Assert.LessOrEqual(renderers[0].sharedMaterials.Length, 6, key + ": material budget");
                    foreach (Material material in renderers[0].sharedMaterials)
                    {
                        Assert.IsNotNull(material, key);
                        StringAssert.StartsWith(Work, AssetDatabase.GetAssetPath(material), key + ": converted material, the pack's files stay untouched");
                        StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name, key + ": a material of the pack's old pipeline would render magenta");
                        Assert.IsTrue(material.shader.isSupported, key);
                    }
                    Mesh mesh = MeshOf(prefab, key);
                    StringAssert.StartsWith(Work, AssetDatabase.GetAssetPath(mesh), key);
                    Assert.AreEqual(renderers[0].sharedMaterials.Length, mesh.subMeshCount, key + ": one sub-mesh per material");
                    long triangles = 0;
                    for (int part = 0; part < mesh.subMeshCount; part++) triangles += mesh.GetIndexCount(part) / 3;
                    Assert.That(triangles, Is.InRange(1, 4500), key + ": triangle budget of one module");
                }
                finally { UnityEngine.Object.DestroyImmediate(host); }
            }
        }

        [Test]
        public void ModulesFitTheDungeonGridAndKeepTheWallLine()
        {
            List<string> built = Built();
            float tile = CryptDungeonDressing.TileMetres;
            Bounds floor = MeshOf(Resources.Load<GameObject>("ARE/Visuals/DungeonFloor"), "DungeonFloor").bounds;
            Assert.AreEqual(tile, floor.size.x, .01f); Assert.AreEqual(tile, floor.size.z, .01f);
            Assert.AreEqual(0f, floor.max.y, .01f, "The walked surface lies at height zero");
            Assert.AreEqual(0f, floor.center.x, .01f); Assert.AreEqual(0f, floor.center.z, .01f);
            foreach (string key in built)
            {
                if (!key.StartsWith("DungeonWall", StringComparison.Ordinal)) continue;
                Bounds wall = MeshOf(Resources.Load<GameObject>("ARE/Visuals/" + key), key).bounds;
                Assert.AreEqual(tile, wall.size.x, .01f, key + ": a wall closes exactly one tile edge");
                Assert.AreEqual(0f, wall.center.x, .01f, key);
                Assert.AreEqual(0f, wall.min.y, .01f, key + " stands on the floor");
                Assert.GreaterOrEqual(wall.min.z, -.25f, key + ": nothing may stick out behind the wall");
                Assert.LessOrEqual(wall.max.z, 1.05f, key + ": relief stays near the wall, off the walked tile");
                Assert.That(wall.max.y, Is.InRange(3f, 3.6f), key + ": walls share one height");
            }
            if (built.Contains("DungeonGate"))
            {
                Bounds gate = MeshOf(Resources.Load<GameObject>("ARE/Visuals/DungeonGate"), "DungeonGate").bounds;
                Assert.AreEqual(3.9f, gate.size.x, .06f, "The gate fills its passage without being stretched");
                Assert.AreEqual(2.8f, gate.size.y, .06f);
            }
        }

        [Test]
        public void CandlesCarryMarkersForTheGamesOwnFlames()
        {
            List<string> built = Built();
            if (!built.Contains("DungeonShrine")) Assert.Ignore(NotInstalled);
            GameObject shrine = Resources.Load<GameObject>("ARE/Visuals/DungeonShrine");
            int flames = 0, lights = 0;
            foreach (Transform node in shrine.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == CryptDungeonDressing.FlameMarker) flames++;
                if (node.name == CryptDungeonDressing.LightMarker) lights++;
            }
            Assert.GreaterOrEqual(flames, 3, "Each candle of the shrine gets a flame");
            Assert.GreaterOrEqual(lights, 1);
            Bounds body = MeshOf(shrine, "DungeonShrine").bounds;
            foreach (Transform node in shrine.GetComponentsInChildren<Transform>(true))
                if (node.name == CryptDungeonDressing.FlameMarker)
                    Assert.That(node.localPosition.y, Is.InRange(body.max.y - .5f, body.max.y + .2f), "A flame sits on a candle tip, not in the stone");
        }
    }
}
