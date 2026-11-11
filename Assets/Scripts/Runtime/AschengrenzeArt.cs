using System;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Presentation table for the Aschengrenze village. It names which blockout landmark
    /// may be replaced by a reviewed passive mesh and which blockout parts that mesh makes
    /// redundant. It never names Core-owned projections (rations, camp, passage, NPCs).
    /// </summary>
    public static class AschengrenzeArt
    {
        public const string BackdropLabel = "Aschengrenze_Backdrop";
        public const float GroundY = 0f;

        private const string Hall = "Aschengrenze_Supply_Hall";
        private const string Tower = "Aschengrenze_Watchtower";
        private const string Canopy = "Aschengrenze_Market_Canopy_";
        private const string MarketPost = "Aschengrenze_Market_Post_";

        /// <summary>Visual key under Resources/ARE/Visuals for a landmark, or null.</summary>
        public static string KeyFor(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            if (label == Hall) return "VillageSupplyHall";
            if (label == Tower) return "VillageWatchtower";
            if (label == BackdropLabel) return "VillageBackdrop";
            if (label.StartsWith(Canopy, StringComparison.Ordinal)) return "VillageMarketStall";
            return null;
        }

        /// <summary>Landmark whose bound mesh replaces this blockout part, or null.</summary>
        public static string OwnerOf(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            if (label.StartsWith("Aschengrenze_Hall_Roof_", StringComparison.Ordinal) ||
                label == "Aschengrenze_Hall_Door")
                return Hall;
            if (label.StartsWith("Aschengrenze_Watch_Post_", StringComparison.Ordinal) ||
                label == "Aschengrenze_Signal_Beacon")
                return Tower;
            if (label.StartsWith(MarketPost, StringComparison.Ordinal))
            {
                int end = label.IndexOf('_', MarketPost.Length);
                if (end > MarketPost.Length)
                    return Canopy + label.Substring(MarketPost.Length, end - MarketPost.Length);
            }
            return null;
        }
    }
}
