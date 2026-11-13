namespace AreArpg.Core
{
    /// <summary>Transport evidence belongs to the existing character, never to the scene or inventory.</summary>
    public sealed class TradeRouteEncounter
    {
        public bool Accepted { get; internal set; }
        internal string Token() { return "1"; }
        internal static bool TryRead(string token, out TradeRouteEncounter result)
        { result = new TradeRouteEncounter { Accepted = true }; return token == "1"; }
    }

    public sealed partial class CharacterProgression
    {
        public TradeRouteEncounter TradeRoute { get; private set; } = new TradeRouteEncounter();
    }

    public sealed partial class ArenaSimulation
    {
        private SocialResult ActOnTradeRoute(HungerAction action)
        {
            var result = new SocialResult { Text = "Sprich nach dem Brandmalbericht persoenlich mit Ilyan." };
            if (GameOver || action != HungerAction.AskTransport || Progression.EntryAct != 2 ||
                !Progression.Rattler.Reported || Progression.TradeRoute.Accepted) return result;
            var contact = HungerContact(0);
            if (contact == null) return result;
            contact.InteractionCooldownTicks = 10;
            Progression.TradeRoute.Accepted = true;
            result.Accepted = true;
            result.Text = "Ilyan: Unter Immergruen wird jede Last umgeschlagen. Nimm den Ostweg zum Aschenzoll. Dort koennten noch Transportspuren liegen. Ein Wappen verraet uns nicht, wer die Fracht bestellt hat.";
            return result;
        }
    }
}
