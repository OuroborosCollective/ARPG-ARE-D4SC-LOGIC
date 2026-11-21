using System;
using System.Collections.Generic;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public sealed partial class SocialLedger
    {
        /// <summary>
        /// A resident hands one ration to Mara's store. No player credit; fails without loss at the limit, so the
        /// ration stays with whoever carried it.
        /// </summary>
        internal bool StoreCampRation(int limit)
        {
            if (CampProvisions >= Math.Min(limit, CampCapacity)) return false;
            CampProvisions++;
            return true;
        }

        /// <summary>A resident eats one camp ration under the established rule: hungry enough and a ration is there.</summary>
        internal bool EatAtCamp(VillageResidentState resident)
        {
            if (CampProvisions == 0 || resident.Hunger < VillageEconomyRules.EatHunger) return false;
            CampProvisions--;
            resident.Hunger = Math.Max(0, resident.Hunger - VillageEconomyRules.EatRelief);
            return true;
        }
    }

    /// <summary>
    /// The village at work (owner concept "NPC-Wirtschaft und Lebenszyklus", 2026-11-21). Residents have needs first
    /// and work second: the established personal controller of each resident chooses between eating, resting and
    /// going about the day's work; which work step is due follows from what the village lacks. Every step is a real
    /// change of stocks. Integer-only and driven by the game tick: the same save and the same ticks give the same
    /// village on every device, whether the player is there or far away.
    /// </summary>
    public sealed partial class VillageEconomyState
    {
        private const int Stride = VillageEconomyRules.Stride;

        /// <summary>Evaluate the village once for the current game tick. Safe to call every tick.</summary>
        internal void Advance(CharacterProgression p)
        {
            var forge = p.Social.Forge;
            if (forge == null) return;
            ulong tick = forge.ProcessedTicks;
            int residents = p.Village.Population - VillageServicesState.BasePopulation;
            if (!Started)
            {
                if (residents <= 0) return;
                Begin(tick);
            }
            if (tick % (ulong)Stride != 0 || tick <= LastTick) return;
            LastTick = tick;
            while (_residents.Count < residents)
                _residents.Add(new VillageResidentState { Ordinal = VillageServicesState.BasePopulation + _residents.Count, Hunger = 200, Fatigue = 100 });
            Regrow(tick);
            // The last free place in Mara's store stays the player's while her refill task is open.
            int campLimit = p.Social.CampRefilled ? SocialLedger.CampCapacity : SocialLedger.CampCapacity - 1;
            foreach (var resident in _residents) StepResident(p, forge, resident, tick, campLimit);
        }

        /// <summary>Nature's own inflows: saplings grow up, game breeds, berries and seed ripen, the well refills.</summary>
        private void Regrow(ulong tick)
        {
            while (_saplings.Count > 0 && _saplings[0].Key <= tick)
            {
                _sources[(int)VillageSource.MatureTrees] += _saplings[0].Value;
                _saplings.RemoveAt(0);
            }
            int wild = _sources[(int)VillageSource.Wildlife];
            if (tick % VillageEconomyRules.WildlifeRegenTicks == 0 && wild >= VillageEconomyRules.WildlifeBreeders && wild < VillageEconomyRules.WildlifeCap)
                _sources[(int)VillageSource.Wildlife]++;
            if (tick % VillageEconomyRules.BerryRegenTicks == 0 && _sources[(int)VillageSource.Berries] < VillageEconomyRules.BerryCap)
                _sources[(int)VillageSource.Berries]++;
            int trees = _sources[(int)VillageSource.MatureTrees];
            if (tick % VillageEconomyRules.SeedCropRegenTicks == 0 && trees >= VillageEconomyRules.SeedTreeReserve && _sources[(int)VillageSource.SeedCrop] < trees / 4)
                _sources[(int)VillageSource.SeedCrop]++;
            if (tick % VillageEconomyRules.WellRegenTicks == 0 && _sources[(int)VillageSource.Well] < VillageEconomyRules.WellCap)
                _sources[(int)VillageSource.Well]++;
            if (tick % VillageEconomyRules.HerbRegenTicks == 0 && _sources[(int)VillageSource.Herbs] < VillageEconomyRules.HerbCap)
                _sources[(int)VillageSource.Herbs]++;
        }

        private void StepResident(CharacterProgression p, NpcForgeState forge, VillageResidentState r, ulong tick, int campLimit)
        {
            if (tick % VillageEconomyRules.HungerTicks == 0) r.Hunger = Math.Min(VillageEconomyRules.MaxNeed, r.Hunger + 1);
            if (r.Hunger >= VillageEconomyRules.MaxNeed && tick % VillageEconomyRules.StarvingTicks == 0 && r.Health > VillageEconomyRules.StarvingHealthFloor)
                r.Health--;

            bool night = VillageEconomyRules.IsNight(tick);
            bool inVillage = r.Site == VillageSite.Village && !r.Walking;
            bool food = p.Social.CampProvisions > 0;
            bool hungry = r.Hunger >= VillageEconomyRules.EatHunger;
            // An injured or worn-down resident does not work: the work in hand is given up and its inputs go back.
            if (!r.CanWork && r.Work != VillageWork.None) GiveUp(r);
            // Orsel tends the sick at the old mill once her own quest is done; before that the hurt rest at home.
            bool care = p.Village.OrselTeachesUnlocked;
            bool atMill = r.Site == VillageSite.Mill && !r.Walking;
            VillageWork plan;
            if (r.Work != VillageWork.None) plan = r.Work;
            else if (atMill && care && r.Health < VillageEconomyRules.MaxHealth) plan = VillageWork.None;   // stays in her care until whole
            else if (!r.CanWork) plan = r.Walking ? VillageWork.None : inVillage ? (care ? VillageWork.SeekCare : VillageWork.None) : VillageWork.GoHome;
            else plan = Plan(p, r, night, campLimit, care);

            // The hard mask of what is possible right now; the resident's own controller chooses among it.
            // Going about the day's work is the villager's "travel" action: no new action is added to the minds.
            var legal = new List<ForgeAction>();
            if (r.Walking || plan == VillageWork.GoHome || plan == VillageWork.SeekCare)
                legal.Add(ForgeAction.Travel);        // a walk once begun is finished; hurt, idle or at night nobody camps at the forest edge
            else
            {
                legal.Add(ForgeAction.Wait);
                legal.Add(ForgeAction.Rest);
                if (plan != VillageWork.None) legal.Add(ForgeAction.Travel);
                if (hungry && food) legal.Add(inVillage ? ForgeAction.Eat : ForgeAction.SeekFood);
                if (inVillage && !night && plan == VillageWork.None) legal.Add(ForgeAction.Observe);
            }
            var mind = forge.BindResident("village." + r.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), "villager");
            // A resident who works decides with the working villager controller; the change is a recorded migration.
            var working = VillageWorkController.Artifact;
            if (mind.Controller.Hash != working.Hash) forge.Archive.TryMigrate(mind.Id, working, NpcForgeState.Registry, tick, out _);
            var frame = new ObservationFrame(ActionSet.Of(legal.ToArray()), new[]
            {
                new Observation(Feature.Hunger, r.Hunger * 10), new Observation(Feature.Fatigue, r.Fatigue * 10),
                new Observation(Feature.Injury, (VillageEconomyRules.MaxHealth - r.Health) * 10000 / VillageEconomyRules.MaxHealth),
                new Observation(Feature.Food, food ? 10000 : 0),
                new Observation(Feature.TravelCost, inVillage ? 0 : 3000)
            });
            var action = UtilityEvaluator.Decide(mind.Controller, frame, mind.DecisionState, tick, false, mind.Cognition).Action;

            int hunger = r.Hunger, fatigue = r.Fatigue, health = r.Health, effect = 0;
            switch (action)
            {
                case ForgeAction.Eat:
                    r.Activity = p.Social.EatAtCamp(r) ? VillageActivity.Eating : VillageActivity.Idle;
                    effect = hunger - r.Hunger;
                    break;
                case ForgeAction.SeekFood:
                    // Food is in the village: leave the forest work; nothing it had not finished is counted.
                    GiveUp(r);
                    Walk(r, VillageSite.Village);
                    r.Activity = VillageActivity.Walking;
                    effect = 1;
                    break;
                case ForgeAction.Rest:
                    r.Fatigue = Math.Max(0, r.Fatigue - VillageEconomyRules.RestRelief * Stride);
                    if (atMill && care) Care(forge, r, tick);
                    // Rest heals, but not on an empty stomach.
                    else if (tick % VillageEconomyRules.RestHealTicks == 0 && r.Health < VillageEconomyRules.MaxHealth && r.Hunger < VillageEconomyRules.MaxNeed) r.Health++;
                    r.Activity = !r.CanWork || atMill ? VillageActivity.Recovering : night && inVillage ? VillageActivity.Sleeping : VillageActivity.Resting;
                    effect = fatigue - r.Fatigue + r.Health - health;
                    break;
                case ForgeAction.Travel:
                    if (tick % VillageEconomyRules.WorkFatigueTicks == 0) r.Fatigue = Math.Min(VillageEconomyRules.MaxNeed, r.Fatigue + 1);
                    // The measured effect of going about the work is its progress: a stretch of the way walked, a
                    // stretch of the step done, and the yield when it ends. Without that, only rest would ever be
                    // credited and experience would teach every resident to rest.
                    if (r.Walking)
                    {
                        r.Activity = VillageActivity.Walking;
                        r.TravelTicks -= Stride;
                        if (r.TravelTicks <= 0) Arrive(r);
                        effect = 1;
                    }
                    else if (r.Work != VillageWork.None)
                    {
                        r.Activity = VillageActivity.Working;
                        r.WorkTicks -= Stride;
                        effect = 1;
                        if (r.WorkTicks <= 0)
                        {
                            effect += Finish(p, forge, r, tick, campLimit);
                            r.Activity = VillageActivity.Idle;
                        }
                    }
                    else if (StartWork(r, plan)) effect = 1;
                    break;
                default:
                    r.Activity = VillageActivity.Idle;
                    break;
            }
            mind.Cognition.RecordOutcome(action, tick, effect);
        }

        /// <summary>
        /// What the village lacks decides the next work step, in a fixed order: rations ready for Mara, game to cook,
        /// food to hunt (a bow first, built from frame and thorn berry, from wood, from a tree), a broken axe to
        /// repair, wood for the pile, then the forest's own renewal. Returns None when nothing can be done.
        /// </summary>
        internal VillageWork Plan(CharacterProgression p, VillageResidentState r, bool night, int campLimit, bool care)
        {
            bool forest = r.Site == VillageSite.Forest;
            // At the mill there is one thing to do: put the herbs on Orsel's shelf. Then home.
            if (r.Site == VillageSite.Mill)
                return r.Carry[(int)VillageGood.Herb] > 0 && _millHerbs < VillageEconomyRules.MillHerbCap ? VillageWork.BringHerbs : VillageWork.GoHome;
            if (night) return forest ? VillageWork.GoHome : VillageWork.None;
            // In the forest, what is carried counts as on its way to the commons.
            Func<VillageGood, int> have = good => _commons[(int)good] + (forest ? r.Carry[(int)good] : 0);
            bool axe = r.AxeUses > 0 || !forest && _commons[(int)VillageGood.Axe] > 0;
            bool bow = r.Carry[(int)VillageGood.Bow] > 0 || !forest && _commons[(int)VillageGood.Bow] > 0;
            int trees = _sources[(int)VillageSource.MatureTrees];
            bool canFell = axe && trees > VillageEconomyRules.SeedTreeReserve;
            int camp = p.Social.CampProvisions;
            var wish = VillageWork.None;

            if (have(VillageGood.Ration) > 0 && camp < campLimit) wish = VillageWork.DeliverRations;
            if (wish == VillageWork.None && have(VillageGood.Game) > 0)
                wish = have(VillageGood.Water) < 1 ? (_sources[(int)VillageSource.Well] > 0 ? VillageWork.FetchWater : VillageWork.None) :
                    have(VillageGood.Wood) < 1 ? (canFell ? VillageWork.FellTree : VillageWork.None) : VillageWork.Cook;
            bool hungryVillage = camp + have(VillageGood.Ration) + 2 * have(VillageGood.Game) < VillageEconomyRules.FoodTarget;
            if (wish == VillageWork.None && hungryVillage && _sources[(int)VillageSource.Wildlife] > 0)
                wish = bow ? VillageWork.Hunt :
                    have(VillageGood.WoodFrame) >= 1 && have(VillageGood.ThornBerry) >= 1 ? VillageWork.MakeBow :
                    have(VillageGood.WoodFrame) < 1 && have(VillageGood.Wood) >= 2 ? VillageWork.MakeFrame :
                    have(VillageGood.ThornBerry) < 1 && _sources[(int)VillageSource.Berries] > 0 ? VillageWork.GatherBerries :
                    have(VillageGood.WoodFrame) < 1 && canFell ? VillageWork.FellTree : VillageWork.None;
            if (wish == VillageWork.None && have(VillageGood.BrokenAxe) >= 1 && have(VillageGood.Metal) >= 1 && have(VillageGood.Wood) >= 1)
                wish = VillageWork.RepairAxe;
            // Healing herbs for Orsel's shelf: carry what lies in the commons to the mill, gather while her shelf is short.
            if (wish == VillageWork.None && care && have(VillageGood.Herb) > 0 && _millHerbs < VillageEconomyRules.MillHerbCap) wish = VillageWork.BringHerbs;
            if (wish == VillageWork.None && care && _millHerbs + have(VillageGood.Herb) < VillageEconomyRules.HerbTarget && _sources[(int)VillageSource.Herbs] > 0)
                wish = VillageWork.GatherHerbs;
            if (wish == VillageWork.None && have(VillageGood.Wood) < VillageEconomyRules.WoodTarget && canFell) wish = VillageWork.FellTree;
            if (wish == VillageWork.None && trees + SaplingCount < VillageEconomyRules.StandTarget)
                wish = _nursery >= 1 ? VillageWork.PlantSeedling : _sources[(int)VillageSource.SeedCrop] > 0 ? VillageWork.CollectSeedlings : VillageWork.None;

            if (wish == VillageWork.None) return forest ? VillageWork.GoHome : VillageWork.None;
            var rule = VillageEconomyRules.Rule(wish);
            if (!forest) return wish;
            if (rule.Site != VillageSite.Forest) return VillageWork.GoHome;       // village work, or the way to the mill, leads through the village
            // Hands full: carry the load home first.
            return r.Load + CarriedYield(rule) > VillageEconomyRules.CarryCapacity ? VillageWork.GoHome : wish;
        }

        private static int CarriedYield(VillageWorkRule rule)
        {
            if (rule.Work == VillageWork.CollectSeedlings) return 0;         // seedlings stay in the nursery at the forest edge
            int n = 0;
            foreach (var output in rule.Outputs) n += output.Count;
            return n;
        }

        private static void Walk(VillageResidentState r, VillageSite to)
        {
            if (r.Walking || r.Site == to) return;      // a walk once begun is finished first
            r.Destination = to;
            r.TravelTicks = VillageEconomyRules.TravelTicks(r.Site, to);
        }

        /// <summary>
        /// Orsel's care at the old mill: every hundred ticks a healing herb from her shelf mends ten health; without
        /// herbs her hands and bandages mend three. The first care of a game day is remembered by her and the patient.
        /// </summary>
        private void Care(NpcForgeState forge, VillageResidentState r, ulong tick)
        {
            if (r.Health >= VillageEconomyRules.MaxHealth) return;
            long day = (long)(tick / WorldSkyClock.TicksPerDay);
            if (day != r.CareDay)
            {
                r.CareDay = day;
                forge.Ensure("orsel", "healer");
                var ev = forge.Archive.Append(MemoryKind.Help, "orsel", r.Identity, tick, 5000, false, CareContext, new RelationshipChange(trust: 500, gratitude: 900, familiarity: 300));
                var words = new EpisodeDetails("care", "mill", "sickbed");
                forge.Archive.Perceive("orsel", ev.Id, KnowledgeOrigin.Self, tick, words);
                forge.Archive.Perceive(r.Identity, ev.Id, KnowledgeOrigin.Self, tick, words);
            }
            if (tick % VillageEconomyRules.CareHerbTicks != 0) return;
            if (_millHerbs > 0)
            {
                _millHerbs--; Use(VillageGood.Herb, 1);
                r.Health = Math.Min(VillageEconomyRules.MaxHealth, r.Health + VillageEconomyRules.CareHerbTicks / VillageEconomyRules.CareHealTicks);
            }
            else r.Health = Math.Min(VillageEconomyRules.MaxHealth, r.Health + VillageEconomyRules.CareHerbTicks / VillageEconomyRules.PlainCareHealTicks);
        }

        public const string CareContext = "village.care";

        /// <summary>Arriving in the village, a resident puts the load down in the commons and keeps the tools in hand.</summary>
        private void Arrive(VillageResidentState r)
        {
            r.Site = r.Destination;
            r.TravelTicks = 0;
            if (r.Site != VillageSite.Village) return;
            for (int g = 0; g < r.Carry.Length; g++)
            {
                if (g == (int)VillageGood.Axe || g == (int)VillageGood.Bow) continue;
                _commons[g] += r.Carry[g];
                r.Carry[g] = 0;
            }
        }

        /// <summary>
        /// Begin the planned step: walk to its place if needed, take the tool from the commons, and hold back the
        /// inputs. If anything is missing, nothing is taken and nothing starts.
        /// </summary>
        private bool StartWork(VillageResidentState r, VillageWork work)
        {
            r.Activity = VillageActivity.Idle;
            if (work == VillageWork.None) return false;
            if (work == VillageWork.GoHome) { Walk(r, VillageSite.Village); r.Activity = VillageActivity.Walking; return true; }
            if (work == VillageWork.SeekCare) { Walk(r, VillageSite.Mill); r.Activity = VillageActivity.Walking; return true; }
            var rule = VillageEconomyRules.Rule(work);
            if (work == VillageWork.BringHerbs && r.Site == VillageSite.Village)
            {
                // Take the herbs from the commons into the hands before the walk to the mill.
                int take = Math.Min(Math.Min(VillageEconomyRules.HerbLoad, VillageEconomyRules.CarryCapacity - r.Load), _commons[(int)VillageGood.Herb]);
                if (take <= 0 && r.Carry[(int)VillageGood.Herb] == 0) return false;
                _commons[(int)VillageGood.Herb] -= Math.Max(0, take); r.Carry[(int)VillageGood.Herb] += Math.Max(0, take);
            }
            if (work == VillageWork.BringHerbs && r.Site == VillageSite.Mill && r.Carry[(int)VillageGood.Herb] == 0) return false;
            if (work == VillageWork.DeliverRations)
            {
                // The rations are picked up for the walk to Mara. Held back is not yet handed over, but nobody
                // else sets out with the same rations.
                int load = Math.Min(VillageEconomyRules.DeliveryLoad, _commons[(int)VillageGood.Ration]);
                if (load <= 0) return false;
                _commons[(int)VillageGood.Ration] -= load; r.Reserved[(int)VillageGood.Ration] += load;
            }
            // Tools are picked up in the village before leaving.
            if (rule.Tool == (int)VillageGood.Axe && r.AxeUses == 0)
            {
                if (r.Site != VillageSite.Village || _commons[(int)VillageGood.Axe] < 1) return false;
                _commons[(int)VillageGood.Axe]--; r.Carry[(int)VillageGood.Axe]++; r.AxeUses = VillageEconomyRules.AxeUses;
            }
            if (rule.Tool == (int)VillageGood.Bow && r.Carry[(int)VillageGood.Bow] == 0)
            {
                if (r.Site != VillageSite.Village || _commons[(int)VillageGood.Bow] < 1) return false;
                _commons[(int)VillageGood.Bow]--; r.Carry[(int)VillageGood.Bow]++;
            }
            if (rule.Site != r.Site) { Walk(r, rule.Site); r.Activity = VillageActivity.Walking; return true; }
            if (rule.Source >= 0 && _sources[rule.Source] < 1) return false;
            if (work == VillageWork.FellTree && _sources[(int)VillageSource.MatureTrees] <= VillageEconomyRules.SeedTreeReserve) return false;
            if (rule.Site == VillageSite.Forest && r.Load + CarriedYield(rule) > VillageEconomyRules.CarryCapacity) return false;
            foreach (var input in rule.Inputs)
                if ((input.Good == VillageGood.Seedling ? _nursery : _commons[(int)input.Good]) < input.Count) return false;
            foreach (var input in rule.Inputs)
            {
                if (input.Good == VillageGood.Seedling) _nursery -= input.Count; else _commons[(int)input.Good] -= input.Count;
                r.Reserved[(int)input.Good] += input.Count;
            }
            r.Work = work;
            r.WorkTicks = rule.Ticks;
            r.Activity = VillageActivity.Working;
            return true;
        }

        /// <summary>Give the work in hand up: what was held back returns to where it came from; nothing is produced.</summary>
        private void GiveUp(VillageResidentState r)
        {
            for (int g = 0; g < r.Reserved.Length; g++)
            {
                if (r.Reserved[g] == 0) continue;
                if (g == (int)VillageGood.Seedling) _nursery += r.Reserved[g]; else _commons[g] += r.Reserved[g];
                r.Reserved[g] = 0;
            }
            r.Work = VillageWork.None;
            r.WorkTicks = 0;
        }

        private void Make(VillageGood good, int count) { _made[(int)good] += count; }
        private void Use(VillageGood good, int count) { _used[(int)good] += count; }

        /// <summary>
        /// The work step ends: its inputs are used up once, nature gives what the step takes, and the yield exists
        /// once - in the resident's hands in the forest, in the commons in the village. Returns the units yielded.
        /// </summary>
        private int Finish(CharacterProgression p, NpcForgeState forge, VillageResidentState r, ulong tick, int campLimit)
        {
            VillageWork work = r.Work;
            var rule = VillageEconomyRules.Rule(work);
            int yielded = 0;
            if (work == VillageWork.DeliverRations)
            {
                // Rations carried to Mara are not used up by the walk: they are put down for the hand-over itself.
                _commons[(int)VillageGood.Ration] += r.Reserved[(int)VillageGood.Ration];
                r.Reserved[(int)VillageGood.Ration] = 0;
            }
            for (int g = 0; g < r.Reserved.Length; g++)
            {
                if (r.Reserved[g] == 0) continue;
                Use((VillageGood)g, r.Reserved[g]);
                r.Reserved[g] = 0;
            }
            r.Work = VillageWork.None;
            r.WorkTicks = 0;
            switch (work)
            {
                case VillageWork.FellTree:
                    // Another resident may have felled down to the seed trees in the meantime: then this tree stays.
                    if (_sources[(int)VillageSource.MatureTrees] <= VillageEconomyRules.SeedTreeReserve || r.AxeUses == 0) return 0;
                    _sources[(int)VillageSource.MatureTrees]--;
                    r.Carry[(int)VillageGood.Wood] += 3; Make(VillageGood.Wood, 3); yielded = 3;
                    if (--r.AxeUses == 0)
                    {
                        // The axe is worn out: one axe becomes one broken axe, to be repaired with metal.
                        r.Carry[(int)VillageGood.Axe]--; Use(VillageGood.Axe, 1);
                        r.Carry[(int)VillageGood.BrokenAxe]++; Make(VillageGood.BrokenAxe, 1);
                    }
                    return yielded;
                case VillageWork.CollectSeedlings:
                    yielded = Math.Min(rule.SourceUse, _sources[(int)VillageSource.SeedCrop]);
                    _sources[(int)VillageSource.SeedCrop] -= yielded;
                    _nursery += yielded; Make(VillageGood.Seedling, yielded);
                    return yielded;
                case VillageWork.PlantSeedling:
                    AddSapling(tick + VillageEconomyRules.SaplingGrowthTicks);
                    return 1;
                case VillageWork.GatherBerries:
                    yielded = Math.Min(rule.SourceUse, _sources[(int)VillageSource.Berries]);
                    _sources[(int)VillageSource.Berries] -= yielded;
                    r.Carry[(int)VillageGood.ThornBerry] += yielded; Make(VillageGood.ThornBerry, yielded);
                    return yielded;
                case VillageWork.Hunt:
                    if (_sources[(int)VillageSource.Wildlife] < 1 || r.Carry[(int)VillageGood.Bow] < 1) return 0;
                    _sources[(int)VillageSource.Wildlife]--;
                    r.Carry[(int)VillageGood.Game]++; Make(VillageGood.Game, 1);
                    if (VillageEconomyRules.HuntInjures(tick, r.Ordinal)) r.Health = Math.Max(1, r.Health - VillageEconomyRules.HuntInjury);
                    return 1;
                case VillageWork.GatherHerbs:
                    yielded = Math.Min(rule.SourceUse, _sources[(int)VillageSource.Herbs]);
                    _sources[(int)VillageSource.Herbs] -= yielded;
                    r.Carry[(int)VillageGood.Herb] += yielded; Make(VillageGood.Herb, yielded);
                    return yielded;
                case VillageWork.BringHerbs:
                    // From the hands onto Orsel's shelf; what does not fit goes home again.
                    yielded = Math.Min(r.Carry[(int)VillageGood.Herb], VillageEconomyRules.MillHerbCap - _millHerbs);
                    r.Carry[(int)VillageGood.Herb] -= yielded; _millHerbs += yielded;
                    return yielded;
                case VillageWork.FetchWater:
                    yielded = Math.Min(rule.SourceUse, _sources[(int)VillageSource.Well]);
                    _sources[(int)VillageSource.Well] -= yielded;
                    _commons[(int)VillageGood.Water] += yielded; Make(VillageGood.Water, yielded);
                    return yielded;
                case VillageWork.DeliverRations:
                    return Deliver(p, forge, r, tick, campLimit);
                default:
                    foreach (var output in rule.Outputs)
                    {
                        _commons[(int)output.Good] += output.Count; Make(output.Good, output.Count);
                        yielded += output.Count;
                    }
                    return yielded;
            }
        }

        private void AddSapling(ulong due)
        {
            if (_saplings.Count > 0 && _saplings[_saplings.Count - 1].Key == due)
                _saplings[_saplings.Count - 1] = new KeyValuePair<ulong, int>(due, _saplings[_saplings.Count - 1].Value + 1);
            else _saplings.Add(new KeyValuePair<ulong, int>(due, 1));
        }

        /// <summary>
        /// Rations go from the commons into Mara's store, one real hand-over each. A full store takes nothing and
        /// the ration stays in the commons. The first hand-over of a game day is remembered by the two who were
        /// part of it - the resident and Mara - and by nobody else.
        /// </summary>
        private int Deliver(CharacterProgression p, NpcForgeState forge, VillageResidentState r, ulong tick, int campLimit)
        {
            int delivered = 0;
            while (delivered < VillageEconomyRules.DeliveryLoad && _commons[(int)VillageGood.Ration] > 0 && p.Social.StoreCampRation(campLimit))
            {
                _commons[(int)VillageGood.Ration]--;
                _handedOut[(int)VillageGood.Ration]++;
                delivered++;
            }
            long day = (long)(tick / WorldSkyClock.TicksPerDay);
            if (delivered == 0 || day == r.DeliveryDay) return delivered;
            r.DeliveryDay = day;
            var ev = forge.Archive.Append(MemoryKind.Help, r.Identity, "mara", tick, 3000, false, DeliveryContext,
                new RelationshipChange(trust: 300, gratitude: 400, familiarity: 200));
            var words = new EpisodeDetails("food", "village", "rations");
            forge.Archive.Perceive(r.Identity, ev.Id, KnowledgeOrigin.Self, tick, words);
            forge.Archive.Perceive("mara", ev.Id, KnowledgeOrigin.Self, tick, words);
            return delivered;
        }

        public const string DeliveryContext = "village.delivery.rations";

        /// <summary>
        /// The player puts goods into the village commons: a real hand-over from the player's own stock, counted as
        /// received. The caller has already taken the goods from the player.
        /// </summary>
        internal void Receive(VillageGood good, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (good == VillageGood.Seedling) _nursery += count; else _commons[(int)good] += count;
            _received[(int)good] += count;
        }

        /// <summary>What the village is waiting for right now, in plain words for the village board.</summary>
        public List<string> OpenNeeds(CharacterProgression p)
        {
            var needs = new List<string>();
            if (!Started) return needs;
            int camp = p.Social.CampProvisions;
            bool anyBow = _commons[(int)VillageGood.Bow] > 0, anyAxe = _commons[(int)VillageGood.Axe] > 0, anyone = false;
            foreach (var r in _residents)
            {
                anyBow |= r.Carry[(int)VillageGood.Bow] > 0; anyAxe |= r.AxeUses > 0;
                anyone |= r.CanWork;
            }
            if (_residents.Count == 0) needs.Add("Niemand im Dorf hat Zeit für Handwerk.");
            else if (!anyone) needs.Add("Alle Bewohner sind verletzt oder entkräftet. Die Arbeit ruht.");
            if (camp < VillageEconomyRules.FoodTarget)
            {
                if (_sources[(int)VillageSource.Wildlife] == 0) needs.Add("Das Wild ist erschöpft. Der Jagd fehlt die Beute.");
                else if (!anyBow) needs.Add("Der Jagd fehlt ein Bogen (Holzrahmen und Dornbeere).");
            }
            if (!anyAxe)
                needs.Add(_commons[(int)VillageGood.BrokenAxe] > 0 && _commons[(int)VillageGood.Metal] == 0 ?
                    "Die Axt ist defekt und es fehlt Metall für die Reparatur." : "Dem Holzeinschlag fehlt eine Axt.");
            if (_sources[(int)VillageSource.MatureTrees] <= VillageEconomyRules.SeedTreeReserve) needs.Add("Der Wald ist bis auf die Samenbäume geschlagen. Holz gibt es erst wieder, wenn Setzlinge herangewachsen sind.");
            if (_commons[(int)VillageGood.Game] > 0 && _commons[(int)VillageGood.Water] == 0 && _sources[(int)VillageSource.Well] == 0) needs.Add("Der Küche fehlt Wasser.");
            if (_commons[(int)VillageGood.Ration] > 0 && camp >= SocialLedger.CampCapacity) needs.Add("Maras Lager ist voll. Fertige Rationen warten.");
            bool hurt = false;
            foreach (var r in _residents) hurt |= !r.CanWork;
            if (hurt && p.Village.OrselTeachesUnlocked && _millHerbs == 0) needs.Add("Orsel fehlen Heilkräuter für die Verletzten.");
            return needs;
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The village works on wherever the player is, as long as the game itself is running.</summary>
        private void StepVillageEconomy() { Progression.Economy.Advance(Progression); }

        private void MixEconomy(ref ulong h)
        {
            // Only once residents work: profiles without an economy keep their exact earlier hash.
            if (!Progression.Economy.Started) return;
            Mix(ref h, 0x45434F4EUL);
            Mix(ref h, Progression.Economy.StableHash);
        }

        /// <summary>
        /// The player gives materials from the own pack to the village commons. Only at the village board, only what the
        /// player really has, and only goods the village works with. Nothing is created: the pack loses exactly what
        /// the commons gains.
        /// </summary>
        public SocialResult GiveToVillage(VillageGood good, int count)
        {
            var result = new SocialResult();
            if (GameOver || !IsVillage || !Progression.Economy.Started) { result.Text = "Hier nimmt niemand Material für das Dorf an."; return result; }
            if (!VillageBoardInReach) { result.Text = "Material für das Dorf gibst du an der Dorftafel ab."; return result; }
            if (count <= 0 || !Progression.Crafting.TakeForVillage(good, count))
            { result.Text = "Davon hast du nicht genug bei dir. Nichts wurde übergeben."; return result; }
            Progression.Economy.Receive(good, count);
            result.Accepted = true;
            result.Text = count + " × " + VillageEconomyRules.GoodName(good) + " liegt jetzt im Dorflager.";
            return result;
        }
    }
}
