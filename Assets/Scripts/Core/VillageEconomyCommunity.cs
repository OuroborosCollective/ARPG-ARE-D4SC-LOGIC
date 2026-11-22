using System;
using System.Globalization;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public sealed partial class VillageEconomyState
    {
        public const int DrinkThreshold = 700;
        public const string GiftContext = "village.commons.gift";
        public const string WaterAidContext = "village.household.water";

        private static void AdvanceThirst(VillageResidentState resident, ulong tick)
        {
            if (tick % 20 == 0)
                resident.Thirst = Math.Min(VillageEconomyRules.MaxNeed, resident.Thirst + 1);
        }

        private bool ShouldDrink(CharacterProgression p, VillageResidentState resident)
        {
            if (resident.Walking || resident.Site != VillageSite.Village || resident.Thirst < DrinkThreshold ||
                Commons(VillageGood.Water) < 1) return false;
            if (!resident.CanWork)
                foreach (var neighbor in _residents)
                    if (neighbor.CanWork && !neighbor.Walking && neighbor.Site == VillageSite.Village)
                        return false; // The patient receives the same real water at home when someone can carry it.
            return resident.Hunger < VillageEconomyRules.EatHunger || p.Social.CampProvisions == 0 ||
                resident.Thirst > resident.Hunger;
        }

        private int TryDrink(VillageResidentState resident)
        {
            if (resident.Walking || resident.Site != VillageSite.Village || resident.Thirst < DrinkThreshold ||
                Commons(VillageGood.Water) == 0) return 0;
            _commons[(int)VillageGood.Water]--;
            Use(VillageGood.Water, 1);
            return QuenchThirst(resident);
        }

        private static int QuenchThirst(VillageResidentState resident)
        {
            int before = resident.Thirst;
            resident.Thirst = Math.Max(0, before - 600);
            resident.Activity = VillageActivity.Drinking;
            return before - resident.Thirst;
        }

        private VillageResidentState WaitingForWater(VillageResidentState helper)
        {
            // The errand serves the sick at home. An absent, self-sufficient or already served
            // patient cannot receive this water. Stable resident order decides simultaneous needs.
            foreach (var neighbor in _residents)
                if (neighbor != helper && !neighbor.Walking && neighbor.Site == VillageSite.Village &&
                    !neighbor.CanWork && neighbor.Thirst >= DrinkThreshold)
                    return neighbor;
            return null;
        }

        private VillageWork PlanWater(CharacterProgression p, VillageResidentState resident)
        {
            if (resident.Site != VillageSite.Village || resident.Walking || !resident.CanWork) return VillageWork.None;
            var patient = WaitingForWater(resident);
            bool thirsty = resident.Thirst >= DrinkThreshold;
            if (!thirsty && patient == null) return VillageWork.None;
            if (Commons(VillageGood.Water) == 0)
                return Source(VillageSource.Well) > 0 && !AnyoneWorking(VillageWork.FetchWater)
                    ? VillageWork.FetchWater : VillageWork.None;
            if (patient != null && !ShouldDrink(p, resident) && !AnyoneWorking(VillageWork.OfferWater))
                return VillageWork.OfferWater;
            return VillageWork.None;
        }

        private int FinishWaterAid(NpcForgeState forge, VillageResidentState helper, ulong tick)
        {
            var patient = WaitingForWater(helper);
            if (patient == null || helper.Held(VillageGood.Water) != 1)
            {
                GiveUp(helper);
                return 0;
            }
            helper.Reserved[(int)VillageGood.Water]--;
            Use(VillageGood.Water, 1);
            helper.Work = VillageWork.None;
            helper.WorkTicks = 0;
            int relief = QuenchThirst(patient);
            var encounter = forge.Archive.Append(MemoryKind.Help, helper.Identity, patient.Identity, tick,
                5000, false, WaterAidContext, new RelationshipChange(trust: 350, gratitude: 800, familiarity: 300));
            var details = new EpisodeDetails("care", "village.home", "water");
            forge.Archive.Perceive(helper.Identity, encounter.Id, KnowledgeOrigin.Self, tick, details);
            forge.Archive.Perceive(patient.Identity, encounter.Id, KnowledgeOrigin.Self, tick, details);
            return relief;
        }

        internal static bool IsExternalAssignment(VillageWork work)
        {
            return (int)work > 0 && (int)work < VillageEconomyRules.WorkCount && work != VillageWork.GoHome &&
                work != VillageWork.SeekCare && VillageEconomyRules.Rule(work).Site != VillageSite.Village;
        }

        // No village stock or other worker is consulted here. Away from the board a resident has
        // only the errand taken along, the material in their hands and the accessible local site.
        private VillageWork AwayAssignment(VillageResidentState resident, bool night)
        {
            if (night || !resident.CanWork) return VillageWork.GoHome;
            if (resident.Site == VillageSite.Mill)
            {
                // Standing at the mill a resident sees whether Orsel is still there: once she has moved, the herbs go home.
                if (!CareInVillage && resident.Carried(VillageGood.Herb) > 0 && MillHerbs < VillageEconomyRules.MillHerbCap)
                    return VillageWork.BringHerbs;
                if (resident.Carried(VillageGood.Grain) >= 2 && CanStartProduction(resident, VillageWork.MillGrain))
                    return VillageWork.MillGrain;
                return VillageWork.GoHome;
            }
            VillageWork work = resident.Assignment;
            if (!IsExternalAssignment(work)) return VillageWork.GoHome;
            var rule = VillageEconomyRules.Rule(work);
            if (rule.Site != resident.Site || !CanStartProduction(resident, work) || rule.Source >= 0 && _sources[rule.Source] <= 0 ||
                resident.Load + CarriedYield(rule, resident) > VillageEconomyRules.CarryCapacity)
                return VillageWork.GoHome;
            if (work == VillageWork.FellTree && (resident.AxeUses == 0 ||
                Source(VillageSource.MatureTrees) <= VillageEconomyRules.SeedTreeReserve)) return VillageWork.GoHome;
            if (work == VillageWork.Hunt && resident.Carried(VillageGood.Bow) == 0) return VillageWork.GoHome;
            if (work == VillageWork.PlantSeedling && (NurserySeedlings == 0 ||
                Source(VillageSource.MatureTrees) + SaplingCount >= VillageEconomyRules.StandTarget))
                return VillageWork.GoHome;
            return work;
        }

        // Each of seven two-bit groups records unavailable, scarce or sufficient. Zero represents
        // the absence of a report, distinct from a report of an empty forest. Wells and fields are local.
        private static readonly VillageSource[] ForestSources =
        {
            VillageSource.MatureTrees, VillageSource.SeedCrop, VillageSource.Wildlife, VillageSource.Berries,
            VillageSource.Herbs, VillageSource.OreVein, VillageSource.Rubble
        };

        internal static bool IsForestReport(int report)
        {
            if (report < 1 || report > ushort.MaxValue) return false;
            for (int i = 0; i < ForestSources.Length; i++)
                if (((report >> (2 * i)) & 3) == 0) return false;
            return true;
        }

        /// <summary>The available quantity last reported at home: unknown (-1), depleted (0), scarce (1), sufficient (2).</summary>
        public int ReportedSupply(VillageSource source)
        {
            int index = Array.IndexOf(ForestSources, source);
            if (index < 0 || ForestReport == 0) return -1;
            return ((ForestReport >> (2 * index)) & 3) - 1;
        }

        private const int PlantingNeeded = 1 << 14;
        private const int NurseryHasSeedlings = 1 << 15;
        internal bool ReportedPlantingNeed => (ForestReport & PlantingNeeded) != 0;
        internal bool ReportedSeedlings => (ForestReport & NurseryHasSeedlings) != 0;

        // If the last visitor has been gone for three days, a new delivery can check the shelf.
        // The herbs on the carrier remain theirs until a bed actually has space.
        private int KnownMillHerbsForSupply(ulong tick)
        {
            if (ReportedMillHerbs < 0 || tick >= MillReportTick && tick - MillReportTick >= 3UL * WorldSkyClock.TicksPerDay)
                return 0;
            return ReportedMillHerbs;
        }

        private bool WantsHerbDelivery(ulong tick) => KnownMillHerbsForSupply(tick) < VillageEconomyRules.MillHerbCap;

        private bool CanSeekSource(VillageSource source, ulong tick)
        {
            int report = ReportedSupply(source);
            if (report != 0) return true;
            // A counted-out mineral deposit cannot regrow. Living sources can be visited again
            // after the written renewal interval. The first unobserved visit is a legitimate search.
            ulong revisit;
            switch (source)
            {
                case VillageSource.MatureTrees: revisit = VillageEconomyRules.SaplingGrowthTicks; break;
                case VillageSource.SeedCrop: revisit = VillageEconomyRules.SeedCropRegenTicks; break;
                case VillageSource.Wildlife: revisit = VillageEconomyRules.WildlifeRegenTicks; break;
                case VillageSource.Berries: revisit = VillageEconomyRules.BerryRegenTicks; break;
                case VillageSource.Herbs: revisit = VillageEconomyRules.HerbRegenTicks; break;
                default: return false;
            }
            return tick >= ForestReportTick && tick - ForestReportTick >= revisit;
        }

        private int SurveyForest()
        {
            int result = Source(VillageSource.MatureTrees) + SaplingCount < VillageEconomyRules.StandTarget ? PlantingNeeded : 0;
            if (NurserySeedlings > 0) result |= NurseryHasSeedlings;
            for (int i = 0; i < ForestSources.Length; i++)
            {
                VillageSource source = ForestSources[i];
                int amount = Source(source);
                int band;
                if (source == VillageSource.MatureTrees)
                    band = amount <= VillageEconomyRules.SeedTreeReserve ? 0 :
                        amount < VillageEconomyRules.StandTarget ? 1 : 2;
                else if (source == VillageSource.OreVein || source == VillageSource.Rubble)
                {
                    var work = source == VillageSource.OreVein ? VillageWork.RecoverOre : VillageWork.GatherStone;
                    band = amount < VillageEconomyRules.Rule(work).SourceUse ? 0 : amount <= 4 ? 1 : 2;
                }
                else
                    band = amount == 0 ? 0 : amount <= 2 ? 1 : 2;
                result |= (band + 1) << (i * 2);
            }
            return result;
        }

        private void UpdateSiteReport(VillageResidentState resident, bool wasWalking, ulong tick)
        {
            if (!wasWalking && resident.Walking && resident.Destination == VillageSite.Village)
            {
                if (resident.Site == VillageSite.Forest) resident.SiteSample = SurveyForest();
                else if (resident.Site == VillageSite.Mill) resident.SiteSample = 1 + MillHerbs;
                else return;
                resident.SiteSampleTick = tick;
            }
            if (wasWalking && !resident.Walking && resident.Site == VillageSite.Village && resident.SiteSample != 0)
            {
                // Two residents can leave a site in the same tick with something happening there between their
                // departures. The way home takes both equally long and they arrive in the order they left, so of
                // two reports with the same tick the one that comes in later is the newer one.
                if (IsForestReport(resident.SiteSample) && resident.SiteSampleTick >= ForestReportTick)
                {
                    ForestReport = resident.SiteSample;
                    ForestReportTick = resident.SiteSampleTick;
                    ForestReporterOrdinal = resident.Ordinal;
                }
                else if (resident.SiteSample <= 1 + VillageEconomyRules.MillHerbCap && resident.SiteSampleTick >= MillReportTick)
                {
                    ReportedMillHerbs = resident.SiteSample - 1;
                    MillReportTick = resident.SiteSampleTick;
                    MillReporterOrdinal = resident.Ordinal;
                }
                resident.SiteSample = 0;
                resident.SiteSampleTick = 0;
            }
        }
    }

    public sealed partial class ArenaSimulation
    {
        private const long VillageConversationMm = 2400;

        private bool InVillageContact(long ax, long az, long bx, long bz)
        {
            return DistanceSquared(ax, az, bx, bz) <= FixedMath.Square(VillageConversationMm) &&
                CombatSight(ax, az, bx, bz);
        }

        internal bool MaraReceivesAtStore()
        {
            if (!IsVillage) return false;
            var supplier = Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
            return supplier != null && CanConverse(supplier) &&
                InVillageContact(supplier.Xmm, supplier.Zmm, VillageEconomyLayout.StandXmm(VillageSpot.Store),
                    VillageEconomyLayout.StandZmm(VillageSpot.Store));
        }

        internal bool VillageWorkUnsafe(VillageResidentState resident)
        {
            if (!IsVillage || !SeenInVillage(resident, out var spot)) return false;
            if (!resident.Walking && resident.Work != VillageWork.None)
                spot = Progression.Houses.Outdoors(VillageEconomyLayout.SpotFor(resident.Work));
            // Somebody in a house is reached at its door: the rooms themselves lie far outside the village.
            long x = VillageEconomyLayout.VillageXmm(spot), z = VillageEconomyLayout.VillageZmm(spot);
            foreach (var npc in Npcs)
                if (npc.Memory.Health > 0 && npc.Behavior == NpcBehavior.Attack &&
                    DistanceSquared(npc.Xmm, npc.Zmm, x, z) <= FixedMath.Square(4500) && CombatSight(npc.Xmm, npc.Zmm, x, z))
                    return true;
            return false;
        }

        private void RememberVillageGift(VillageGood good, int count)
        {
            var forge = Progression.Social.Forge;
            var gift = forge.Archive.Append(MemoryKind.Help, "player", "village.commons", forge.ProcessedTicks, 4500,
                false, VillageEconomyState.GiftContext, new RelationshipChange(trust: 300, gratitude: 350, familiarity: 100));
            var details = new EpisodeDetails("supplies", "village.board",
                good.ToString().ToLowerInvariant() + "." + count.ToString(CultureInfo.InvariantCulture));
            foreach (var npc in Npcs)
                if (npc.Mind != null && npc.Memory.Health > 0 && InVillageContact(npc.Xmm, npc.Zmm, PlayerXmm, PlayerZmm))
                    forge.Archive.Perceive(npc.Mind.Id, gift.Id, KnowledgeOrigin.Witnessed, forge.ProcessedTicks, details);
            foreach (var resident in Progression.Economy.Residents)
                if (!resident.Walking && SeenInVillage(resident, out var spot) &&
                    InVillageContact(VillageEconomyLayout.VillageXmm(spot), VillageEconomyLayout.VillageZmm(spot), PlayerXmm, PlayerZmm))
                    forge.Archive.Perceive(resident.Identity, gift.Id, KnowledgeOrigin.Witnessed, forge.ProcessedTicks, details);
        }

        private static bool SupplyEvent(string context)
        {
            return context == VillageEconomyState.GiftContext || context == VillageEconomyState.WaterAidContext ||
                context == VillageEconomyState.DeliveryContext || context == VillageEconomyState.CareContext;
        }

        private static KnownMemory RecentSupplyNews(NpcMind speaker, ulong tick, bool firstHand)
        {
            if (speaker == null) return null;
            KnownMemory latest = null;
            foreach (var k in speaker.Relevant(tick))
                if ((!firstHand || k.IsFact) && (k.IsFact || k.Origin == KnowledgeOrigin.Reported) &&
                    SupplyEvent(k.Event.Context) && (latest == null || k.Event.Id > latest.Event.Id) &&
                    speaker.Cognition.Recall(k.Event.Id, tick) != null)
                    latest = k;
            return latest;
        }

        private static bool CanConverse(NpcAgent person)
        {
            return person.Mind != null && person.Memory.Health > 0 && person.Behavior != NpcBehavior.Attack &&
                person.Behavior != NpcBehavior.Flee;
        }

        private static void TellSupplyNews(MemoryArchive archive, NpcMind teller, NpcMind listener, ulong tick)
        {
            if (teller == null || listener == null || teller == listener) return;
            var news = RecentSupplyNews(teller, tick, true);
            if (news != null && listener.Knowledge(news.Event.Id) == null)
                archive.Report(teller.Id, listener.Id, news.Event.Id, tick);
        }

        private void StepVillageContacts()
        {
            var forge = Progression.Social.Forge;
            if (!IsVillage || forge == null || forge.ProcessedTicks == 0 || forge.ProcessedTicks % 100 != 0) return;
            foreach (var listener in Npcs)
            {
                if (!CanConverse(listener)) continue;
                foreach (var teller in Npcs)
                    if (teller != listener && CanConverse(teller) &&
                        InVillageContact(teller.Xmm, teller.Zmm, listener.Xmm, listener.Zmm))
                        TellSupplyNews(forge.Archive, teller.Mind, listener.Mind, forge.ProcessedTicks);
                foreach (var neighbor in Progression.Economy.Residents)
                    if (!neighbor.Walking && neighbor.Work == VillageWork.None && SeenInVillage(neighbor, out var spot) &&
                        InVillageContact(VillageEconomyLayout.VillageXmm(spot), VillageEconomyLayout.VillageZmm(spot), listener.Xmm, listener.Zmm))
                    {
                        var neighborMind = forge.Archive.Get(neighbor.Identity);
                        TellSupplyNews(forge.Archive, neighborMind, listener.Mind, forge.ProcessedTicks);
                        TellSupplyNews(forge.Archive, listener.Mind, neighborMind, forge.ProcessedTicks);
                    }
            }
            // Two free residents at the same Core workplace share their own witnessed news even
            // when neither is a named story figure. Reported knowledge is never relayed as a witness.
            var residents = Progression.Economy.Residents;
            for (int i = 0; i < residents.Count; i++)
            {
                var left = residents[i];
                if (left.Walking || left.Work != VillageWork.None || !SeenInVillage(left, out var place)) continue;
                for (int j = i + 1; j < residents.Count; j++)
                {
                    var right = residents[j];
                    if (right.Walking || right.Work != VillageWork.None || !SeenInVillage(right, out var other) ||
                        other != place) continue;
                    var leftMind = forge.Archive.Get(left.Identity);
                    var rightMind = forge.Archive.Get(right.Identity);
                    TellSupplyNews(forge.Archive, leftMind, rightMind, forge.ProcessedTicks);
                    TellSupplyNews(forge.Archive, rightMind, leftMind, forge.ProcessedTicks);
                }
            }
        }

        /// <summary>A resident's greeting can repeat only the retained news this NPC personally knows.</summary>
        public string VillageSupplyNews(int npcId)
        {
            if (!IsVillage || !Progression.Economy.Started || npcId < 0 || npcId >= VillageServicesState.BasePopulation) return "";
            var speaker = Npcs.Find(npc => npc.Id == npcId);
            if (speaker == null || !CanConverse(speaker) || speaker.Mood == NpcDisposition.Hostile) return "";
            var forge = Progression.Social.Forge;
            var known = RecentSupplyNews(forge?.Archive.Get(NpcForgeState.Identity(npcId)), forge?.ProcessedTicks ?? 0, false);
            if (known == null) return "";
            string message;
            switch (known.Event.Context)
            {
                case VillageEconomyState.GiftContext: message = "Die Reisende hat Material für das Dorf gebracht."; break;
                case VillageEconomyState.WaterAidContext: message = "Ein Nachbar hat den Kranken Wasser gebracht."; break;
                case VillageEconomyState.DeliveryContext: message = "Bei Mara wurden Rationen abgeliefert."; break;
                default: message = "Orsel hat sich um einen Verletzten gekümmert."; break;
            }
            return (known.Origin == KnowledgeOrigin.Reported ? "Mir wurde erzählt: " : "Ich war dabei. ") + message;
        }
    }
}
