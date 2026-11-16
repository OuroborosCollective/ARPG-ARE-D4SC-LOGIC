using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Tests the actual imported URP sky shader, not a substitute material.</summary>
    public class WorldSkyEditModeTests
    {
        [Test]
        public void MobileSkyShaderImportsWithSunCloudAndStarControls()
        {
            var shader = Resources.Load<Shader>("AshenWorldSky");
            Assert.IsNotNull(shader, "A real sky shader must be packed into Resources for Android");
            Assert.IsTrue(shader.isSupported, "URP sky shader must be supported by this Unity graphics context");
            var material = new Material(shader);
            try
            {
                Assert.IsTrue(material.HasProperty("_SunDirection"), "Directional sunlight comes from the tracked Core clock");
                Assert.IsTrue(material.HasProperty("_CloudAmount"), "The mobile reduction toggle must lower cloud cost");
                Assert.IsTrue(material.HasProperty("_StarAmount"), "Night needs visible stars without any simulation authority");
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
