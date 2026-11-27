using AreArpg.Core;

namespace AreArpg.UnityClient
{
    /// <summary>Translate the current simulation location into a background art key. No travel or unlock decisions live here.</summary>
    public static class WorldHorizonSite
    {
        private static readonly string[] Areas =
        {
            null, null, "village", "ash_road", "mill", "granary", "hidden_crypt",
            "nest", "west_forest", "bear", "freight", "forest_road"
        };
        private static readonly string[] UnderMill = { null, "under_1", "under_2", "under_3" };
        private static readonly string[] SideCave = { null, "side_1", "side_2", "side_3", "side_4" };

        public static string For(ArenaSimulation game)
        {
            if (game == null || game.InsidePlace != VillagePlace.None) return null;
            if (game.InKilnYard) return "kiln";
            if (game.InCourt) return game.PlayerZmm >= CourtLayout.BellCourtZmm ? "bellcourt" : "forecourt";
            if (game.InDungeon) return UnderMill[game.Progression.Dungeon.Depth];
            // New journeys have four floors. Valid older saves can have up to six. Their deepest
            // floors share the deep-rift scenery while the original dungeon still owns their topology.
            if (game.InJourneyCave) return SideCave[System.Math.Min(game.Progression.Journey.Depth, SideCave.Length - 1)];
            int area = game.CurrentAreaId;
            return area >= 0 && area < Areas.Length ? Areas[area] : null;
        }
    }
}
