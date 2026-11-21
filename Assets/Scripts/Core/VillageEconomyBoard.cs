using System;
using System.Collections.Generic;
using System.Globalization;

namespace AreArpg.Core
{
    /// <summary>
    /// What stands on the village board. The village counts its own stores exactly; of forest, game and herbs it
    /// knows only what people see when they are out there, so the land is described in words, not counted.
    /// Everything here is read from the Core state: the board shows the village, it does not run it.
    /// </summary>
    public static class VillageBoard
    {
        /// <summary>Goods the player can carry to the village, in the order the board offers them.</summary>
        private static readonly VillageGood[] Gifts =
            { VillageGood.Wood, VillageGood.ThornBerry, VillageGood.Metal, VillageGood.Herb, VillageGood.WoodFrame, VillageGood.Bow };

        public static IReadOnlyList<VillageGood> GiftGoods { get { return Gifts; } }

        private static string N(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        /// <summary>The stores of the village: workshop, kitchen and the land around.</summary>
        public static List<string> StockLines(CharacterProgression p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var lines = new List<string>();
            VillageEconomyState e = p.Economy;
            if (!e.Started) return lines;
            lines.Add("Werkstatt: " + Count(e, VillageGood.Wood) + " · " + Count(e, VillageGood.WoodFrame) + " · " + Count(e, VillageGood.Bow) + " · " +
                Count(e, VillageGood.Axe) + (e.Commons(VillageGood.BrokenAxe) > 0 ? " · " + Count(e, VillageGood.BrokenAxe) : "") + " · " +
                Count(e, VillageGood.Metal) + " · " + Count(e, VillageGood.ThornBerry));
            lines.Add("Küche: " + Count(e, VillageGood.Game) + " · " + Count(e, VillageGood.Water) + " · " + Count(e, VillageGood.Ration) + " · " +
                Count(e, VillageGood.Hide) + " · Maras Vorrat " + N(p.Social.CampProvisions) + "/" + N(SocialLedger.CampCapacity));
            lines.Add("Umland: " + Forest(e) + " · " + Wildlife(e) + " · " + Berries(e) + (p.Village.OrselTeachesUnlocked ? " · " + Herbs(e) : ""));
            return lines;
        }

        private static string Count(VillageEconomyState e, VillageGood good)
        { return VillageEconomyRules.GoodName(good) + " " + N(e.Commons(good)); }

        private static string Forest(VillageEconomyState e)
        {
            int trees = e.Source(VillageSource.MatureTrees);
            string stand = trees <= VillageEconomyRules.SeedTreeReserve ? "Wald bis auf die Samenbäume geschlagen" :
                trees < VillageEconomyRules.StandTarget * 2 / 3 ? "Wald gelichtet" : "Wald steht dicht";
            int young = e.SaplingCount;
            return stand + (young > 0 ? ", " + N(young) + (young == 1 ? " Setzling wächst" : " Setzlinge wachsen") : ", nichts wächst nach");
        }

        private static string Wildlife(VillageEconomyState e)
        {
            int game = e.Source(VillageSource.Wildlife);
            return game == 0 ? "kein Wild mehr" : game <= VillageEconomyRules.WildlifeBreeders ? "Wild fast verschwunden" :
                game < VillageEconomyRules.WildlifeCap * 3 / 4 ? "Wild wird seltener" : "Wild reichlich";
        }

        private static string Berries(VillageEconomyState e)
        {
            int berries = e.Source(VillageSource.Berries);
            return berries == 0 ? "Dornbeeren abgeerntet" : berries < VillageEconomyRules.BerryCap / 2 ? "wenig Dornbeeren" : "Dornbeeren reif";
        }

        private static string Herbs(VillageEconomyState e)
        {
            int herbs = e.Source(VillageSource.Herbs);
            return herbs == 0 ? "Heilkräuter abgeerntet" : herbs < VillageEconomyRules.HerbCap / 2 ? "wenig Heilkräuter" : "Heilkräuter stehen gut";
        }

        /// <summary>One line about a resident: what the person does, where, and whether hunger or an injury is in the way.</summary>
        public static string ResidentLine(VillageResidentState r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            string what;
            if (r.Walking)
                what = r.Destination == VillageSite.Village ? "ist auf dem Rückweg ins Dorf" :
                    r.Destination == VillageSite.Forest ? "ist auf dem Weg in den Wald" : "ist auf dem Weg zur Mühle";
            else
                switch (r.Activity)
                {
                    case VillageActivity.Sleeping: what = "schläft"; break;
                    case VillageActivity.Eating: what = "isst an Maras Lager"; break;
                    case VillageActivity.Resting: what = "ruht sich aus"; break;
                    case VillageActivity.Recovering: what = r.Site == VillageSite.Mill ? "wird an der Mühle gepflegt" : "erholt sich"; break;
                    case VillageActivity.Working:
                        what = VillageEconomyRules.WorkName(r.Work) + (r.Site == VillageSite.Forest ? " (im Wald)" : r.Site == VillageSite.Mill ? " (an der Mühle)" : "");
                        break;
                    default: what = r.Site == VillageSite.Village ? "hat gerade keine Arbeit" : "wartet draußen"; break;
                }
            string state = !r.CanWork ? ", verletzt" : r.Hunger >= VillageEconomyRules.EatHunger ? ", hungrig" : "";
            return "Bewohner " + N(r.Ordinal - VillageServicesState.BasePopulation + 1) + " " + what + state + ".";
        }
    }
}
