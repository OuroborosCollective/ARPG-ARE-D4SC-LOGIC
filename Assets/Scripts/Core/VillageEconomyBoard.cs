using System;
using System.Collections.Generic;
using System.Globalization;

namespace AreArpg.Core
{
    /// <summary>
    /// The board counts locally owned stores. A messenger must actually return before any observation of the
    /// forest is shown. A dated report is never refreshed by opening the board or by a change in a distant source.
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
            string camp = "Maras Vorrat " + N(p.Social.CampProvisions) + "/" + N(SocialLedger.CampCapacity);
            if (!e.Started) { lines.Add(camp); return lines; }
            lines.Add("Werkstatt: " + Count(e, VillageGood.Wood) + " · " + Count(e, VillageGood.WoodFrame) + " · " + Count(e, VillageGood.Bow) + " · " +
                Count(e, VillageGood.Axe) + (e.Commons(VillageGood.BrokenAxe) > 0 ? " · " + Count(e, VillageGood.BrokenAxe) : "") + " · " +
                Count(e, VillageGood.Metal) + " · " + Count(e, VillageGood.ThornBerry));
            lines.Add("Küche: " + Count(e, VillageGood.Game) + " · " + Count(e, VillageGood.Water) + " · " + Count(e, VillageGood.Ration) + " · " +
                Count(e, VillageGood.Hide) + " · " + camp);
            lines.Add(ForestReport(e));
            string field = e.Source(VillageSource.RipePlot) > 0 ? "erntereif" : e.Source(VillageSource.CropTicks) > 0 ? "wächst" : "frei";
            lines.Add("Hausgarten (" + field + "): " + Count(e, VillageGood.SeedGrain) + " · " + Count(e, VillageGood.Grain) + " · " +
                Count(e, VillageGood.Straw) + " · " + Count(e, VillageGood.Compost));
            lines.Add("Back- und Pflegestube: " + Count(e, VillageGood.Flour) + " · " + Count(e, VillageGood.Herb) + " · " +
                Count(e, VillageGood.Leather) + " · " + Count(e, VillageGood.Bandage));
            lines.Add("Bau und Werkzeugpflege: " + Count(e, VillageGood.Ore) + " · " + Count(e, VillageGood.Stone) + " · " + Count(e, VillageGood.Fitting));
            if (p.Houses.Built(VillagePlace.SickRoom)) lines.Add("Krankenstube: Orsel pflegt die Verletzten im Dorf.");
            else if (p.Village.OrselTeachesUnlocked)
                lines.Add(e.ReportedMillHerbs < 0 ? "Alte Mühle: Noch kein Bericht von Orsels Vorrat eingetroffen." :
                    "Alte Mühle · " + ReturningWitness(e.MillReporterOrdinal, e.MillReportTick) + ": " +
                    N(e.ReportedMillHerbs) + "/" + N(VillageEconomyRules.MillHerbCap) + " Heilkräuter bei Orsel.");
            return lines;
        }

        private static string Count(VillageEconomyState e, VillageGood good)
        { return VillageEconomyRules.GoodName(good) + " " + N(e.Commons(good)); }

        private static string ForestReport(VillageEconomyState e)
        {
            if (e.ForestReporterOrdinal == 0) return "Umland: Noch kein Bericht vom Waldsaum eingetroffen.";
            return "Waldsaum · " + ReturningWitness(e.ForestReporterOrdinal, e.ForestReportTick) + ": " +
                Reported(e, VillageSource.MatureTrees, "Nutzholz") + " · " + Reported(e, VillageSource.Wildlife, "Wild") + " · " +
                Reported(e, VillageSource.Berries, "Dornbeeren") + " · " + Reported(e, VillageSource.Herbs, "Heilkräuter") + " · " +
                Reported(e, VillageSource.SeedCrop, "Baumsaat") + " · " + Reported(e, VillageSource.OreVein, "Erz") + " · " +
                Reported(e, VillageSource.Rubble, "Stein");
        }

        private static string ReturningWitness(int ordinal, ulong tick)
        {
            string day = (1UL + tick / WorldSkyClock.TicksPerDay).ToString(CultureInfo.InvariantCulture);
            return "Rückkehrbericht von Bewohner " + N(ordinal - VillageServicesState.BasePopulation + 1) + ", beobachtet an Tag " + day;
        }

        private static string Reported(VillageEconomyState e, VillageSource source, string name)
        {
            int level = e.ReportedSupply(source);
            return name + (level < 0 ? " unbekannt" : level == 0 ? " nicht verfügbar" : level == 1 ? " knapp" : " reichlich");
        }

        /// <summary>Requests a player can read at the village. Remote stock levels and the health of absent people stay private.</summary>
        public static List<string> LocalNeeds(CharacterProgression p)
        {
            VillageEconomyState e = p.Economy;
            var needs = new List<string>();
            // The lots come first: somebody stands there and waits for the player.
            for (int i = 1; i <= VillagePlaceLayout.PlaceCount; i++)
            {
                var place = (VillagePlace)i;
                if (p.Houses.Built(place) || !VillageBuildRules.Offered(p, place)) continue;
                needs.Add(p.Houses.Stage(place) == BuildStage.Raising ?
                    VillageBuildRules.Asker(place) + " baut: " + VillagePlaceLayout.Name(place) + ". Komm später wieder ins Dorf." :
                    VillageBuildRules.Asker(place) + " bittet am Bauplatz (" + VillagePlaceLayout.Name(place) + ") um " +
                        VillageBuildRules.MissingWords(p.Houses, place) + ".");
            }
            if (!e.Started)
            {
                needs.Add("Es fehlen freie Arbeitskräfte. Mit den Hauptaufträgen ziehen Bewohner ins Dorf.");
                return needs;
            }
            if (p.Social.CampProvisions < VillageEconomyRules.FoodTarget)
            {
                needs.Add("Mara braucht Nachschub. Jagd und Backstube liefern Rationen.");
                if (e.ReportedSupply(VillageSource.Wildlife) == 0)
                    needs.Add("Der letzte Rückkehrbericht meldet kein jagdbares Wild.");
                if (e.Commons(VillageGood.Bow) == 0 && e.Commons(VillageGood.Game) == 0)
                    needs.Add("An der Werkbank liegt kein freier Bogen. Die Dorftafel nimmt Bögen, Holzrahmen und Dornbeeren an.");
            }
            if (e.Commons(VillageGood.BrokenAxe) > 0 && e.Commons(VillageGood.Metal) == 0)
                needs.Add("In der Werkstatt wartet eine defekte Axt auf Metall. Ascheisen kannst du hier abgeben.");
            else if (e.Commons(VillageGood.Axe) == 0) needs.Add("Am Holzplatz liegt keine freie Axt.");
            if (e.ReportedSupply(VillageSource.MatureTrees) == 0)
                needs.Add("Im letzten Rückkehrbericht blieben nur Samenbäume. Die Forstpflege braucht Nachwuchs.");
            if (e.Commons(VillageGood.Water) == 0) needs.Add("Der Wasservorrat ist leer. Wasser wird am Dorfbrunnen geholt.");
            if (e.Source(VillageSource.RipePlot) > 0) needs.Add("Das Korn am Hausgarten ist reif für die Ernte.");
            if (e.Source(VillageSource.Soil) == 0) needs.Add("Der Hausgarten braucht Kompost aus Stroh und Wasser.");
            if (e.Source(VillageSource.Shelter) < VillageEconomyRules.ShelterCapacity)
                needs.Add("Für das Dach werden Holz, Stein und Beschlag gebraucht.");
            if (e.Source(VillageSource.Hearth) == 0) needs.Add("Die Feuerstelle braucht Brennholz.");
            if (p.Social.CampProvisions == SocialLedger.CampCapacity && e.Commons(VillageGood.Ration) > 0)
                needs.Add("Maras Lager ist voll. Fertige Rationen warten in der Küche.");
            bool localInjury = false, needsWaterAtHome = false;
            foreach (VillageResidentState r in e.Residents)
                if (r.Site == VillageSite.Village && !r.Walking && !r.CanWork)
                {
                    localInjury = true;
                    needsWaterAtHome |= r.Thirst >= VillageEconomyState.DrinkThreshold;
                }
            bool sickRoom = p.Houses.Built(VillagePlace.SickRoom);
            if (localInjury) needs.Add(sickRoom ? "Im Dorf ist jemand verletzt. Orsel pflegt in der Krankenstube." : p.Village.OrselTeachesUnlocked ?
                "Im Dorf ist jemand verletzt. Orsel hilft an der alten Mühle." :
                "Im Dorf ist jemand verletzt und braucht Ruhe und Versorgung zu Hause.");
            if (needsWaterAtHome) needs.Add("Eine verletzte Person am Haus braucht eine Kanne Wasser aus dem Dorfvorrat.");
            if (sickRoom && e.Commons(VillageGood.Herb) == 0) needs.Add("Orsel fehlen Heilkräuter in der Krankenstube.");
            else if (!sickRoom && p.Village.OrselTeachesUnlocked && e.ReportedMillHerbs == 0)
                needs.Add(ReturningWitness(e.MillReporterOrdinal, e.MillReportTick) + ": Orsel fehlen Heilkräuter an der alten Mühle.");
            return needs;
        }

        public static string ResidentNotice(VillageResidentState r)
        {
            if (r.Site == VillageSite.Village && !r.Walking) return ResidentLine(r);
            if (r.Walking && r.Activity == VillageActivity.Resting && VillageEconomyLayout.Seen(r, out _))
                return "Bewohner " + N(r.Ordinal - VillageServicesState.BasePopulation + 1) + " wartet am Tor auf einen sicheren Weg.";
            return "Bewohner " + N(r.Ordinal - VillageServicesState.BasePopulation + 1) + " ist unterwegs.";
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
                    case VillageActivity.Drinking: what = "trinkt am Dorfbrunnen"; break;
                    case VillageActivity.Resting: what = "ruht sich aus"; break;
                    case VillageActivity.Recovering: what = r.Site == VillageSite.Mill ? "wird an der Mühle gepflegt" : "erholt sich"; break;
                    case VillageActivity.Working:
                        what = VillageEconomyRules.WorkName(r.Work) + (r.Site == VillageSite.Forest ? " (im Wald)" : r.Site == VillageSite.Mill ? " (an der Mühle)" : "");
                        break;
                    default: what = r.Site == VillageSite.Village ? "hat gerade keine Arbeit" : "wartet draußen"; break;
                }
            string state = !r.CanWork ? ", verletzt" : r.Hunger >= VillageEconomyRules.EatHunger ? ", hungrig" : "";
            if (r.Thirst >= VillageEconomyState.DrinkThreshold) state += ", durstig";
            return "Bewohner " + N(r.Ordinal - VillageServicesState.BasePopulation + 1) + " " + what + state + ".";
        }
    }
}
