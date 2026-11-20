using AreArpg.Core;
using AshenCrypt.WorldForge.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace AreArpg.EditMode.Tests
{
    public sealed class BorderlandLandscapeTests
    {
        [TestCase(RegionNetwork.Aschengrenze)]
        [TestCase(RegionNetwork.Aschenstrasse)]
        [TestCase(RegionNetwork.AlteMuehle)]
        [TestCase(RegionNetwork.Waldweg)]
        [TestCase(RegionNetwork.Aschenzoll)]
        public void ExteriorHasItsOwn256MetreBakedLandscape(string region)
        {
            var data = Resources.Load<TextAsset>("ARE/RegionalMaps/" + region);
            Assert.IsNotNull(data, region);
            var map = ForgedWorldMap.FromBytes(data.bytes);
            Assert.AreEqual(256000, map.WidthMm);
            Assert.AreEqual(256000, map.DepthMm);
            Assert.Greater(map.Obstacles.Count, 50, "authored vegetation anchors");
            foreach (string id in new[] { "arrival","onward","centre" })
            {
                var poi = map.FindPoi(id);
                Assert.IsNotNull(poi, id);
                Assert.IsTrue(map.CanStand(poi.Xmm, poi.Zmm, ArenaSimulation.PlayerRadiusMm), id);
            }
        }

        [Test]
        public void LandscapeShaderCompilesInTheActivePipeline()
        {
            var shader = Resources.Load<Shader>("BorderlandGround");
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
        }
    }
}
