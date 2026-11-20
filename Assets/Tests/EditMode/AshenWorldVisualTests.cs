using System;
using System.Collections.Generic;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The ash world of the overworld: ash ground with glowing veins and ember plants. Textures, plants and the built
    /// visuals are private; without them these checks are reported as ignored, never as passed.
    /// </summary>
    public sealed class AshenWorldVisualTests
    {
        const string Work = "Assets/PrivateArt/AshenWorld/";
        const string GroundTextures = "Assets/PrivateArt/AshenWorld/Generated/";
        const string NotInstalled = "Requires the private ashen world art with built visuals (never in the repository)";

        static Type Tool()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("AreArpg.EditorTools.AshenWorldArt");
                if (type != null) return type;
            }
            Assert.Fail("AshenWorldArt is missing");
            return null;
        }

        static string[] Keys()
        {
            return (string[])Tool().GetMethod("Keys", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        static bool Owns(string key)
        {
            return (bool)Tool().GetMethod("Owns", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { key });
        }

        static List<string> Built()
        {
            var built = new List<string>();
            foreach (string key in Keys()) if (Owns(key)) built.Add(key);
            if (built.Count == 0) Assert.Ignore(NotInstalled);
            return built;
        }

        static Material Surface(string key)
        {
            var material = Resources.Load<Material>("ARE/Visuals/WorldSurfaces/" + key);
            Texture colour = material != null && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            if (colour == null || !AssetDatabase.GetAssetPath(colour).StartsWith(GroundTextures, StringComparison.Ordinal))
                Assert.Ignore(NotInstalled);
            return material;
        }

        [Test]
        public void TheSetGrowsOnTheRoadsideKeysTheGameAlreadyBinds()
        {
            var keys = new List<string>(Keys());
            CollectionAssert.Contains(keys, "RoadsideShrub0");
            CollectionAssert.Contains(keys, "RoadsideShrub1");
            CollectionAssert.AllItemsAreUnique(keys);
            Assert.IsFalse(Owns("EntryEnemy0"), "Only its own visuals belong to the set");
            Assert.IsFalse(Owns("DungeonFloor"), "The crypt set stays with its own tool");
        }

        [Test]
        public void AshGroundGlowsRepeatsAtOneScaleAndKeepsTheTextureBudget()
        {
            Material ground = Surface("OutdoorGround"), verge = Surface("GrassVerge");
            foreach (Material material in new[] { ground, verge })
            {
                Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), material.name + ": the veins glow");
                Assert.IsTrue(material.IsKeywordEnabled("_NORMALMAP"), material.name);
                var veins = material.GetTexture("_EmissionMap");
                Assert.IsNotNull(veins, material.name + ": glow map");
                Assert.Greater(material.GetColor("_EmissionColor").maxColorComponent, .5f, material.name + ": glow strength");
                Assert.LessOrEqual(Mathf.Max(veins.width, veins.height), 2048, material.name + ": glow map budget");
                Assert.IsTrue(material.IsKeywordEnabled("_DETAIL_MULX2"), material.name + ": fine grain for close views");
                Texture grain = material.GetTexture("_DetailAlbedoMap");
                Assert.IsNotNull(grain, material.name + ": grain map");
                Assert.LessOrEqual(Mathf.Max(grain.width, grain.height), 512, material.name + ": grain map budget");
                foreach (string slot in new[] { "_BaseMap", "_BumpMap" })
                {
                    Texture map = material.GetTexture(slot);
                    Assert.IsNotNull(map, material.name + slot);
                    Assert.LessOrEqual(Mathf.Max(map.width, map.height), 2048, material.name + slot + ": texture budget on a tablet");
                    Assert.AreEqual(TextureWrapMode.Repeat, map.wrapMode, material.name + slot);
                    Assert.Greater(map.mipmapCount, 1, material.name + slot + ": distant ground must not shimmer");
                }
                Assert.AreSame(ground.GetTexture("_BaseMap"), material.GetTexture("_BaseMap"), "One ash for road and verge");
            }
            // The ash ground is a 57 m square, a verge strip 4 x 36 m: the same metres per repeat on both, in both directions.
            Vector2 groundRepeat = ground.GetTextureScale("_BaseMap"), vergeRepeat = verge.GetTextureScale("_BaseMap");
            float metres = 57f / groundRepeat.x;
            Assert.That(metres, Is.InRange(8f, 20f), "One repeat of the ash ground in metres");
            Assert.AreEqual(metres, 57f / groundRepeat.y, .01f);
            Assert.AreEqual(metres, 4f / vergeRepeat.x, .05f, "Verge and ground share one scale");
            Assert.AreEqual(metres, 36f / vergeRepeat.y, .05f);
            // The verge strips lie on the ground at x = +-8. Their ash must continue the ground's pattern exactly,
            // otherwise the strips show as rectangles. The built-in cube maps its top face as u = 0.5 - x, v = 0.5 - z.
            Vector2 groundShift = ground.GetTextureOffset("_BaseMap"), vergeShift = verge.GetTextureOffset("_BaseMap");
            foreach (float centre in new[] { -8f, 8f })
                foreach (var spot in new[] { new Vector2(centre - 1.5f, -11f), new Vector2(centre + 1.9f, 7.25f) })
                {
                    float groundU = groundRepeat.x * (.5f - spot.x / 57f) + groundShift.x, groundV = groundRepeat.y * (.5f - spot.y / 57f) + groundShift.y;
                    float vergeU = vergeRepeat.x * (.5f - (spot.x - centre) / 4f) + vergeShift.x, vergeV = vergeRepeat.y * (.5f - spot.y / 36f) + vergeShift.y;
                    Assert.AreEqual(0f, Mathf.Abs(Mathf.DeltaAngle(groundU * 360f, vergeU * 360f)) / 360f, .002f, "Verge at x=" + centre + " continues the ground sideways");
                    Assert.AreEqual(0f, Mathf.Abs(Mathf.DeltaAngle(groundV * 360f, vergeV * 360f)) / 360f, .002f, "Verge at x=" + centre + " continues the ground along the road");
                }
        }

        [Test]
        public void EveryAshPlantIsOnePassiveMeshThatCostsAtMostThreeDraws()
        {
            foreach (string key in Built())
            {
                GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + key);
                Assert.IsNotNull(prefab, key + " belongs to the set but has no game visual");
                var host = new GameObject("AshPlantHost");
                try
                {
                    Assert.IsTrue(PassiveArtVisuals.TryBindPrefab(host.transform, prefab), key + " must pass the game's passive-art check");
                    Assert.IsEmpty(host.GetComponentsInChildren<Collider>(true), key + ": no collision, the Core owns movement");
                    Assert.IsEmpty(host.GetComponentsInChildren<Light>(true), key + ": glow comes from the material, lights are budgeted");
                    Assert.IsEmpty(host.GetComponentsInChildren<ParticleSystem>(true), key);
                    MeshRenderer[] renderers = host.GetComponentsInChildren<MeshRenderer>(true);
                    Assert.AreEqual(1, renderers.Length, key + ": one mesh per clump");
                    MeshRenderer renderer = renderers[0];
                    Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode, key + ": thin leaves cast no shadow");
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.IsNotNull(mesh, key);
                    StringAssert.StartsWith(Work, AssetDatabase.GetAssetPath(mesh), key + ": mesh built by the tool");
                    Assert.That(renderer.sharedMaterials.Length, Is.InRange(1, 3), key + ": ember caps, palette plants and the light on the ash");
                    Assert.AreEqual(renderer.sharedMaterials.Length, mesh.subMeshCount, key + ": one material per part");
                    long triangles = 0;
                    for (int part = 0; part < mesh.subMeshCount; part++) triangles += mesh.GetIndexCount(part) / 3;
                    Assert.That(triangles, Is.InRange(1, 5200), key + ": triangle budget of one clump");
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, key);
                        StringAssert.StartsWith(Work, AssetDatabase.GetAssetPath(material), key + ": material built by the tool");
                        StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name, key);
                        Assert.IsTrue(material.shader.isSupported, key);
                    }
                    Assert.AreEqual(1, host.GetComponentsInChildren<LODGroup>(true).Length, key + ": far away the plant is no longer drawn");
                    Bounds bounds = renderer.bounds;
                    Assert.That(bounds.min.y, Is.InRange(-.15f, .15f), key + " stands on the ground");
                    Assert.Less(bounds.size.y, 2.5f, key + ": ground plants, not trees");
                }
                finally { UnityEngine.Object.DestroyImmediate(host); }
            }
        }

        [Test]
        public void EmberPlantsGlowAndThrowLightOnTheAshCalmPlantsDoNot()
        {
            List<string> built = Built();
            foreach (string key in new[] { "AshFloraMushrooms", "AshFloraMushroomsSmall", "RoadsideShrub1" })
            {
                if (!built.Contains(key)) Assert.Ignore(NotInstalled);
                Material[] materials = Materials(key);
                Material caps = Array.Find(materials, m => m.name == "EmberCap");
                Assert.IsNotNull(caps, key + ": ember caps");
                Assert.IsTrue(caps.IsKeywordEnabled("_EMISSION"), key + ": the cracks of the caps glow");
                Assert.IsNotNull(caps.GetTexture("_EmissionMap"), key);
                Assert.Greater(caps.GetColor("_EmissionColor").maxColorComponent, 1f, key + ": bright enough for daylight");
                Material light = Array.Find(materials, m => m.name == "EmberGlow");
                Assert.IsNotNull(light, key + ": light on the ash around the cluster");
                Assert.AreEqual((int)RenderQueue.Transparent, light.renderQueue, key + ": the light is added over the ground");
                Assert.AreEqual((float)BlendMode.One, light.GetFloat("_DstBlend"), key + ": added, never darkening");
                Assert.AreEqual(0f, light.GetFloat("_ZWrite"), key);
            }
            foreach (string key in new[] { "AshFloraGrass", "AshFloraMoss" })
            {
                if (!built.Contains(key)) continue;
                Material[] materials = Materials(key);
                Assert.AreEqual(1, materials.Length, key + ": calm plants are one draw");
                Assert.AreEqual("AshFlora", materials[0].name, key);
                Assert.IsNull(Array.Find(materials, m => m.name == "EmberGlow"), key + " throws no light; it is the calm between the embers");
            }
            if (built.Contains("AshFloraRoses"))
                Assert.IsNotNull(Array.Find(Materials("AshFloraRoses"), m => m.name == "EmberGlow"), "Fire roses glow");
        }

        [Test]
        public void ThePlantPaletteStaysFlatAndTiny()
        {
            Built();
            Material plants = Materials("AshFloraGrass")[0];
            foreach (string slot in new[] { "_BaseMap", "_EmissionMap" })
            {
                Texture map = plants.GetTexture(slot);
                Assert.IsNotNull(map, slot);
                Assert.LessOrEqual(Mathf.Max(map.width, map.height), 64, slot + ": a palette, not a picture");
                Assert.AreEqual(FilterMode.Point, map.filterMode, slot + ": neighbouring colours must not bleed");
                Assert.AreEqual(1, map.mipmapCount, slot + ": smaller copies would mix the cells");
            }
            Assert.AreEqual((float)CullMode.Off, plants.GetFloat("_Cull"), "Blades and petals are seen from both sides");
        }

        static Material[] Materials(string key)
        {
            return Resources.Load<GameObject>("ARE/Visuals/" + key).GetComponentInChildren<MeshRenderer>(true).sharedMaterials;
        }
    }
}
