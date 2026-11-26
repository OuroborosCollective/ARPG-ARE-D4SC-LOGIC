using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Protect scene semantics: art remapping must never recolour interactive signals or actors.</summary>
    public sealed class WorldSurfacePresentationTests
    {
        [TestCase("Wanderer")]
        [TestCase("ARE_NPC_0")]
        [TestCase("ARE_World_Passage")]
        [TestCase("ARE_Return_Passage")]
        [TestCase("HiddenCrypt_FallWarning")]
        [TestCase("OldStore_Door_Seal")]
        [TestCase("Ground")]
        [TestCase("OldMill_Patient_0")]
        [TestCase("OldMill_Torn_Bandage")]
        [TestCase(null)]
        public void ActorsUiAndSignalsDoNotReceiveSceneryMaterials(string label)
        {
            Assert.IsNull(WorldSurfacePresentation.KeyFor(label));
        }

        [TestCase("AshRoad_Ground", "OutdoorGround")]
        [TestCase("OldMill_Ground", "OutdoorGround")]
        [TestCase("HiddenCrypt_Ground", "CryptSubfloor")]
        [TestCase("OldMill_Wheel", "MillWood")]
        [TestCase("OldMill_Bed_0", "BedWood")]
        [TestCase("Grass verge", "GrassVerge")]
        [TestCase("Mill wooden board", "MillBoard")]
        [TestCase("Mill shallow puddle", "MillWater")]
        [TestCase("Ember_Rift_Basalt_-7_7", "BasaltStructure")]
        public void ExistingSceneryHasExplicitSurfaceIdentity(string label, string expected)
        {
            Assert.AreEqual(expected, WorldSurfacePresentation.KeyFor(label));
        }

        [TestCase("Ember_Rift_Ground", "BasaltGround", 5.4f)]
        [TestCase("Ember_Rift_Causeway", "BasaltRoad", 1.65f)]
        [TestCase("Ember_Rift_Basalt_7_7", "BasaltStructure", 2.8f)]
        public void RiftBasaltKeepsAuthoredSurfacesOrUsesTexturedPublicSlate(string label, string key, float metres)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var renderer = shape.GetComponent<Renderer>();
                Assert.IsTrue(WorldSurfacePresentation.TryApply(renderer, label));
                var authored = Resources.Load<Material>("ARE/Visuals/WorldSurfaces/" + key);
                if (authored != null) Assert.AreSame(authored, renderer.sharedMaterial);
                else
                {
                    Assert.AreSame(Resources.Load<Material>("ARE/Borderland/RoughSlate"), renderer.sharedMaterial);
                    Assert.IsNotNull(renderer.sharedMaterial.GetTexture("_Mask"));
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    Assert.AreEqual(metres, properties.GetFloat("_Metres"), .001f);
                    Assert.That(properties.GetVector("_StoneTint").x, Is.InRange(.1f, .8f));
                }
            }
            finally { Object.DestroyImmediate(shape); }
        }
    }
}
