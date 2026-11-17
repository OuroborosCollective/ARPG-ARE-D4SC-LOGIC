using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public sealed class VillageLightShaderTests
    {
        [Test]
        public void TorchFlameShaderImportsForUrpWithoutErrors()
        {
            var shader = Resources.Load<Shader>("AshenFlame");
            Assert.IsNotNull(shader, "The torch flame must be a real imported shader");
            Assert.IsTrue(shader.isSupported, "The shader must compile for the active graphics API");
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader), "The shader must import without compile errors");
            var material = new Material(shader);
            try
            {
                Assert.IsTrue(material.HasProperty("_Intensity") && material.HasProperty("_Seed"));
                Assert.Greater(material.renderQueue, (int)UnityEngine.Rendering.RenderQueue.Transparent - 1);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void TorchesAreDarkByDayAndFullyLitAtNight()
        {
            Assert.AreEqual(0f, AreArpg.UnityClient.ArenaRuntime.VillageLightGlow(1f), 1e-5f);
            Assert.AreEqual(0f, AreArpg.UnityClient.ArenaRuntime.VillageLightGlow(.6f), 1e-5f);
            Assert.AreEqual(1f, AreArpg.UnityClient.ArenaRuntime.VillageLightGlow(0f), 1e-5f);
            Assert.That(AreArpg.UnityClient.ArenaRuntime.VillageLightGlow(.4f), Is.InRange(.2f, .8f), "Dusk lights the torches gradually");
        }
    }
}
