using AreArpg.UnityClient;
using NUnit.Framework;

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
    }
}
