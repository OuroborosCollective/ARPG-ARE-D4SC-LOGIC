using System;
using System.IO;
using AshenCrypt.WorldForge.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.Tests
{
    /// <summary>
    /// EditMode checks for World Forge. Golden numbers come from the Python reference
    /// (Reference/run_reference.py); integer results must match exactly, floats within tolerance.
    /// </summary>
    public class WorldForgeEditModeTests
    {
        [Test]
        public void NatureSetupUsesItsOwnAssetsAndDoesNotOverrideExistingMenu()
        {
            Type setup = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                setup = assembly.GetType("AshenCrypt.WorldForge.Nature.EditorTools.AshenKryptAssetSetup");
                if (setup != null) break;
            }
            Assert.IsNotNull(setup, "World Forge nature setup is unavailable");
            var findRoot = setup.GetMethod("FindPackageRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(findRoot);
            Assert.AreEqual("Assets/Studio/Nature/AshenKrypt", findRoot.Invoke(null, null));
            var menu = (MenuItem)setup.GetMethod("Generate").GetCustomAttributes(typeof(MenuItem), false)[0];
            Assert.AreEqual("Tools/World Forge/Ashen Krypt Nature/Generate Materials and Prefabs", menu.menuItem);
        }

        [Test]
        public void SeedMixingMatchesReference()
        {
            Assert.AreEqual(469904480u, WFMath.MixSeed(731904, 101));
            Assert.AreEqual(1872650673u, WFMath.MixSeed(731904, 5002, 7, 3));
            Assert.AreEqual(2273271095u, WFMath.MixSeed(-5, 1, 2, 3));
        }

        [Test]
        public void RandomStreamMatchesReference()
        {
            var r = new WFRandom(731904u);
            Assert.AreEqual(1922086512497815603UL, r.NextULong());
            Assert.AreEqual(4485461027660863648UL, r.NextULong());
            Assert.AreEqual(14623927727984564176UL, r.NextULong());
            Assert.AreEqual(7881853568235666527UL, r.NextULong());
            Assert.AreEqual(0.7009047865867615f, r.NextFloat(), 1e-7f);
        }

        [Test]
        public void NoiseMatchesReference()
        {
            Assert.AreEqual(-0.163818359375f, WFNoise.Gradient(0.5f, 0.25f, 12345u), 1e-5f);
            Assert.AreEqual(-0.009056806564331055f, WFNoise.Gradient(13.37f, -4.2f, 731904u), 1e-5f);
            Assert.AreEqual(-0.008544921875f, WFNoise.Gradient(-7.75f, 99.5f, 4000000000u), 1e-5f);
            Assert.AreEqual(0.499550998210907f, WFNoise.Fractal(NoiseType.FBm, 1.3f, 2.7f, 731904u, 5, 0.5f, 2f), 1e-4f);
            Assert.AreEqual(0.8503516316413879f, WFNoise.Fractal(NoiseType.Ridged, 1.3f, 2.7f, 731904u, 5, 0.5f, 2f), 1e-4f);
            Assert.AreEqual(0.0982508584856987f, WFNoise.Fractal(NoiseType.Billow, -3.1f, 0.4f, 731904u, 5, 0.5f, 2f), 1e-4f);
        }

        [Test]
        public void PoissonIsDeterministicAndRespectsSpacing()
        {
            var a = WFPoisson.Generate(64f, 64f, 3f, 99u);
            var b = WFPoisson.Generate(64f, 64f, 3f, 99u);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i], b[i]);
            Assert.AreEqual(13.1149f, a[0].x, 2e-3f); Assert.AreEqual(50.3525f, a[0].y, 2e-3f);
            Assert.AreEqual(12.3334f, a[1].x, 2e-3f); Assert.AreEqual(44.5958f, a[1].y, 2e-3f);
            Assert.AreEqual(8.9274f, a[2].x, 2e-3f); Assert.AreEqual(42.1247f, a[2].y, 2e-3f);
            for (int i = 0; i < a.Count; i++)
                for (int j = i + 1; j < a.Count; j++)
                    Assert.GreaterOrEqual((a[i] - a[j]).magnitude, 3f - 1e-4f);
        }

        [Test]
        public void HeightStackIsDeterministicSeedSensitiveAndInRange()
        {
            WorldProfile p = SmallProfile(731904);
            WorldProfile q = SmallProfile(731905);
            try
            {
                float[] h1 = p.EvaluateHeights().Heights;
                float[] h2 = p.EvaluateHeights().Heights;
                float[] h3 = q.EvaluateHeights().Heights;
                CollectionAssert.AreEqual(h1, h2, "same seed must give identical heights");
                double diff = 0; for (int i = 0; i < h1.Length; i++) diff += Math.Abs(h1[i] - h3[i]);
                Assert.Greater(diff / h1.Length, 0.01, "a different seed must change the world");
                foreach (float h in h1) { Assert.GreaterOrEqual(h, 0f); Assert.LessOrEqual(h, p.maxHeight); }
            }
            finally { UnityEngine.Object.DestroyImmediate(p); UnityEngine.Object.DestroyImmediate(q); }
        }

        [Test]
        public void TilesCutFromTheGlobalGridShareEdges()
        {
            WorldProfile p = SmallProfile(1);
            try
            {
                WFHeightContext ctx = p.EvaluateHeights();
                int res = p.heightmapResolution;
                for (int tz = 0; tz < p.tilesZ; tz++)
                    for (int tx = 0; tx + 1 < p.tilesX; tx++)
                        for (int y = 0; y < res; y++)
                        {
                            int gz = tz * (res - 1) + y;
                            float rightEdgeOfLeftTile = ctx.Heights[gz * ctx.Width + tx * (res - 1) + res - 1];
                            float leftEdgeOfRightTile = ctx.Heights[gz * ctx.Width + (tx + 1) * (res - 1)];
                            Assert.AreEqual(rightEdgeOfLeftTile, leftEdgeOfRightTile);
                        }
            }
            finally { UnityEngine.Object.DestroyImmediate(p); }
        }

        [Test]
        public void CoreMapRoundTripsAndAnswersQueries()
        {
            // 3x3 nodes, 1 m spacing, a 1 m slope in X, one blocked cell and one obstacle.
            int[] heights = { 0, 500, 1000, 0, 500, 1000, 0, 500, 1000 };
            bool[] walk = { true, true, true, false };
            var obstacles = new[] { new ForgedObstacle(500, 500, 100, ForgedObstacleFlags.BlocksMovement | ForgedObstacleFlags.BlocksSight) };
            var pois = new[] { new ForgedPoiRecord("camp", ForgedPoiKind.Settlement, 1500, 500, 400, "safezone, faction:ashborn") };
            var map = new ForgedWorldMap(0, 0, 1000, 3, 3, heights, walk, obstacles, pois);

            ForgedWorldMap back = ForgedWorldMap.FromBytes(map.ToBytes());
            Assert.AreEqual(map.ContentHash, back.ContentHash);
            Assert.AreEqual(250, back.HeightMm(500, 700));
            Assert.AreEqual(750, back.HeightMm(1500, 100));
            Assert.AreEqual(1000, back.HeightMm(99999, 0), "queries outside are clamped to the border");
            Assert.IsFalse(back.CanStand(500, 500, 50), "obstacle blocks");
            Assert.IsTrue(back.CanStand(1500, 500, 300));
            Assert.IsFalse(back.CanStand(1500, 1500, 100), "unwalkable cell blocks");
            Assert.IsFalse(back.HasSight(100, 500, 900, 500), "obstacle blocks sight");
            Assert.IsTrue(back.HasSight(1100, 200, 1900, 800));
            Assert.IsTrue(back.FindPoi("camp").HasTag("safezone"));
            Assert.IsTrue(back.FindPoi("camp").Contains(1600, 600));
        }

        [Test]
        public void CircularActorCannotIntrudeIntoBlockedCellBetweenFiveOldSamples()
        {
            // Block cell x=[1000,2000), z=[2000,3000). All 5 old sample points
            // (center + bounding-square corners) are walkable, but the disc overlaps it.
            var walk = new[] { true, true, true, true, true, true, true, false, true };
            // CellsX=3: blocked cell has index 2*3+1 = 7.
            var map = new ForgedWorldMap(0, 0, 1000, 4, 4, new int[16], walk, null, null);
            Assert.IsFalse(map.CanStand(1500, 1500, 700));
        }

        [Test]
        public void CircularActorCanTouchButCannotEnterBlockedCell()
        {
            var walk = new[] { true, true, true, true, true, true, true, false, true };
            var map = new ForgedWorldMap(0, 0, 1000, 4, 4, new int[16], walk, null, null);
            Assert.IsTrue(map.CanStand(1500, 1500, 500), "exact tangency should remain walkable");
            Assert.IsFalse(map.CanStand(1500, 1500, 501), "one millimetre intrusion is blocked");
            Assert.IsTrue(map.CanStand(1500, 1500, 0), "zero-radius point is walkable");
        }

        [Test]
        public void CoreMapRejectsCorruptOrTruncatedData()
        {
            var map = new ForgedWorldMap(0, 0, 1000, 2, 2, new[] { 0, 0, 0, 0 }, new[] { true }, null, null);
            byte[] ok = map.ToBytes();
            byte[] flipped = (byte[])ok.Clone(); flipped[10] ^= 0x40;
            Assert.Throws<FormatException>(() => ForgedWorldMap.FromBytes(flipped));
            byte[] cut = new byte[ok.Length - 3]; Array.Copy(ok, cut, cut.Length);
            Assert.Throws<FormatException>(() => ForgedWorldMap.FromBytes(cut));
            Assert.Throws<FormatException>(() => ForgedWorldMap.FromBytes(new byte[3]));
        }

        [Test]
        public void ReferenceCoreMapMatchesPythonExactly()
        {
            string[] guids = AssetDatabase.FindAssets("ashen_krypt_reference_coremap");
            Assert.IsNotEmpty(guids, "Tests/Editor/Data/ashen_krypt_reference_coremap.bytes missing");
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, AssetDatabase.GUIDToAssetPath(guids[0]));
            ForgedWorldMap map = ForgedWorldMap.FromBytes(File.ReadAllBytes(path));
            Assert.AreEqual(WFReferenceGolden.ContentHash, map.ContentHash);
            Assert.AreEqual(WFReferenceGolden.NodesX, map.NodesX);
            Assert.AreEqual(WFReferenceGolden.NodesZ, map.NodesZ);
            Assert.AreEqual(WFReferenceGolden.SpacingMm, map.SpacingMm);
            Assert.AreEqual(WFReferenceGolden.Obstacles, map.Obstacles.Count);
            Assert.AreEqual(WFReferenceGolden.Pois, map.Pois.Count);
            for (int i = 0; i < WFReferenceGolden.PointX.Length; i++)
            {
                long x = WFReferenceGolden.PointX[i], z = WFReferenceGolden.PointZ[i];
                Assert.AreEqual(WFReferenceGolden.HeightMm[i], map.HeightMm(x, z), "HeightMm #" + i);
                Assert.AreEqual(WFReferenceGolden.CanStand450[i], map.CanStand(x, z, 450), "CanStand #" + i);
            }
            for (int i = 0; i < WFReferenceGolden.SightAX.Length; i++)
                Assert.AreEqual(WFReferenceGolden.HasSight[i],
                    map.HasSight(WFReferenceGolden.SightAX[i], WFReferenceGolden.SightAZ[i], WFReferenceGolden.SightBX[i], WFReferenceGolden.SightBZ[i]),
                    "HasSight #" + i);
        }

        private static WorldProfile SmallProfile(int seed)
        {
            var p = ScriptableObject.CreateInstance<WorldProfile>();
            p.tilesX = 2; p.tilesZ = 2; p.tileSize = 32f; p.maxHeight = 24f; p.heightmapResolution = 33; p.seed = seed;
            p.paths.Add(new WFPath { name = "Test road", points = { new Vector2(2f, 30f), new Vector2(32f, 34f), new Vector2(62f, 28f) }, width = 4f });
            p.pois.Add(new WFPoiDefinition { id = "camp", position = new Vector2(20f, 20f), flattenRadius = 5f });
            p.heightLayers.Add(new WFNoiseLayer { noiseType = NoiseType.Ridged, scale = 24f, amplitude = 6f, baseHeight = 3f, warp = 4f });
            p.heightLayers.Add(new WFStampLayer { shape = StampShape.Crater, center = new Vector2(40f, 36f), radius = 12f, ringWidth = 3f, height = 5f, depth = 2f });
            p.heightLayers.Add(new WFHydraulicErosionLayer { dropletsPerSample = 0.3f });
            p.heightLayers.Add(new WFThermalErosionLayer { iterations = 5 });
            p.heightLayers.Add(new WFPathCarveLayer());
            p.heightLayers.Add(new WFFlattenPoiLayer());
            p.heightLayers.Add(new WFBoundaryLayer { width = 6f, height = 5f });
            return p;
        }
    }
}
