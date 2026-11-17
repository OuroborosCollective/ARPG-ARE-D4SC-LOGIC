using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public sealed class AshenRegionalVfxShaderTests
    {
        [Test]
        public void SharedUrpParticleShaderSupportsBoundedRegionalGlow()
        {
            var shader = Resources.Load<Shader>("AshenAtmosphere");
            Assert.IsNotNull(shader, "The existing real URP shader must load");
            var material = new Material(shader);
            try
            {
                Assert.IsTrue(material.HasProperty("_Glow"), "Regional light must be a native material property, not a second simulation system");
                Assert.That(material.GetFloat("_Glow"), Is.InRange(0f, 1f));
                Assert.IsTrue(shader.isSupported, "The shader must compile for the active graphics API");
                Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader), "The shader must import without compile errors");
                Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, material.renderQueue);
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
