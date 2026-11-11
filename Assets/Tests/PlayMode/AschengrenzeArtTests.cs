using AreArpg.UnityClient;
using NUnit.Framework;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>The village art table is presentation only and may never claim Core-owned projections.</summary>
    public sealed class AschengrenzeArtTests
    {
        [Test]
        public void LandmarksMapToReviewedVillageVisualKeys()
        {
            Assert.AreEqual("VillageSupplyHall", AschengrenzeArt.KeyFor("Aschengrenze_Supply_Hall"));
            Assert.AreEqual("VillageWatchtower", AschengrenzeArt.KeyFor("Aschengrenze_Watchtower"));
            Assert.AreEqual("VillageMarketStall", AschengrenzeArt.KeyFor("Aschengrenze_Market_Canopy_0"));
            Assert.AreEqual("VillageMarketStall", AschengrenzeArt.KeyFor("Aschengrenze_Market_Canopy_1"));
            Assert.AreEqual("VillageBackdrop", AschengrenzeArt.KeyFor(AschengrenzeArt.BackdropLabel));
        }

        [Test]
        /// <summary>Hide a development detail only when its corresponding complete landmark has successfully bound.</summary>
        public void BlockoutPartsAreAbsorbedOnlyByTheirOwnLandmark()
        {
            Assert.AreEqual("Aschengrenze_Supply_Hall", AschengrenzeArt.OwnerOf("Aschengrenze_Hall_Roof_Left"));
            Assert.AreEqual("Aschengrenze_Supply_Hall", AschengrenzeArt.OwnerOf("Aschengrenze_Hall_Roof_Right"));
            Assert.AreEqual("Aschengrenze_Supply_Hall", AschengrenzeArt.OwnerOf("Aschengrenze_Hall_Door"));
            Assert.AreEqual("Aschengrenze_Watchtower", AschengrenzeArt.OwnerOf("Aschengrenze_Watch_Post_-1_1"));
            Assert.AreEqual("Aschengrenze_Watchtower", AschengrenzeArt.OwnerOf("Aschengrenze_Signal_Beacon"));
            Assert.AreEqual("Aschengrenze_Market_Canopy_0", AschengrenzeArt.OwnerOf("Aschengrenze_Market_Post_0_-1"));
            Assert.AreEqual("Aschengrenze_Market_Canopy_1", AschengrenzeArt.OwnerOf("Aschengrenze_Market_Post_1_1"));
        }

        [Test]
        /// <summary>Leave supplies, passage and actor hosts outside cosmetic landmark replacement.</summary>
        public void CoreOwnedProjectionsAreNeverReplacedOrAbsorbed()
        {
            string[] coreProjections =
            {
                "Aschengrenze_Stored_Ration_0", "Aschengrenze_Stored_Ration_9", "Aschengrenze_Missing_Rations",
                "ARE_Camp_Supply", "ARE_World_Passage", "Passage_Arch", "Passage_Stone",
                "ARE_NPC_0", "Wanderer", "Aschengrenze_Market_Counter_0", "Aschengrenze_Watch_Fence_West", null, ""
            };
            foreach (string label in coreProjections)
            {
                Assert.IsNull(AschengrenzeArt.KeyFor(label), "key for " + label);
                Assert.IsNull(AschengrenzeArt.OwnerOf(label), "owner of " + label);
            }
        }
    }
}
