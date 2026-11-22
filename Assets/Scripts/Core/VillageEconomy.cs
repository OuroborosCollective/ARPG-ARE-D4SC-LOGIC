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
        internal const int StockLimit = int.MaxValue / 4;
        internal const long LedgerLimit = 1000000000000L;

        /// <summary>Evaluate the village once for the current game tick. Safe to call every tick.</summary>
        internal void Advance(CharacterProgression p, ArenaSimulation area = null)
        {
            var forge = p.Social.Forge;
            if (forge == null) return;
            _houses = p.Houses;
            ulong tick = forge.ProcessedTicks;
            int residents = p.Village.Population - VillageServicesState.BasePopulation;
            if (!Started)
            {
                if (residents <= 0) return;
                Begin(tick);
            }
            if (tick % (ulong)Stride != 0 || tick <= LastTick) return;
            UpgradeRules();
            LastTick = tick;
            while (_residents.Count < residents)
                _residents.Add(new VillageResidentState { Ordinal = VillageServicesState.BasePopulation + _residents.Count, Hunger = 200, Fatigue = 100 });
            Regrow(tick);
            if (p.Village.OrselTeachesUnlocked) AdvanceProduction(tick);
            // The last free place in Mara's store stays the player's while her refill task is open.
            int campLimit = p.Social.CampRefilled ? SocialLedger.CampCapacity : SocialLedger.CampCapacity - 1;
            foreach (var resident in _residents)
            {
                bool wasWalking = resident.Walking;
                StepResident(p, forge, resident, tick, campLimit, area);
                UpdateSiteReport(resident, wasWalking, tick);
            }
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

        private void StepResident(CharacterProgression p, NpcForgeState forge, VillageResidentState r, ulong tick, int campLimit, ArenaSimulation area)
        {
            if (tick % VillageEconomyRules.HungerTicks == 0) r.Hunger = Math.Min(VillageEconomyRules.MaxNeed, r.Hunger + 1);
            AdvanceThirst(r, tick);
            if (r.Hunger >= VillageEconomyRules.MaxNeed && tick % VillageEconomyRules.StarvingTicks == 0 && r.Health > VillageEconomyRules.StarvingHealthFloor)
                r.Health--;

            bool night = VillageEconomyRules.IsNight(tick);
            bool inVillage = r.Site == VillageSite.Village && !r.Walking;
            bool food = inVillage && p.Social.CampProvisions > 0;
            bool drink = inVillage && ShouldDrink(p, r);
            // Away from home a person can seek the known store, but does not see its contents.
            // A trip to fetch ingredients must reach its source even when the camp was empty at departure.
            bool seekMeal = !inVillage && !r.Walking && r.Work == VillageWork.None && r.Assignment == VillageWork.None;
            bool needsMeal = r.Hunger >= VillageEconomyRules.EatHunger && (food || seekMeal);
            bool needsSupply = drink || needsMeal;
            var mind = forge.BindResident("village." + r.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), "villager");
            var working = VillageWorkController.Artifact;
            if (mind.Controller.Hash != working.Hash) forge.Archive.TryMigrate(mind.Id, working, NpcForgeState.Registry, tick, out _);

            if (area != null && area.VillageWorkUnsafe(r))
            {
                r.Activity = VillageActivity.Resting;
                return;
            }
            // An interruption returns reserved inputs. Until an interruption or completion the decision
            // frame belongs to the trip or job that actually started, including after a reload.
            if (r.Work != VillageWork.None && (!r.CanWork || needsSupply || night || r.Fatigue >= VillageEconomyRules.MaxNeed))
                GiveUp(r);
            if (r.Walking || r.Work != VillageWork.None)
            {
                ContinueCommitment(p, forge, r, mind, tick, campLimit, area);
                return;
            }
            // Orsel tends the sick at the old mill once her own quest is done; before that the hurt rest at home.
            bool care = p.Village.OrselTeachesUnlocked;
            bool atMill = r.Site == VillageSite.Mill && !r.Walking;
            // Once the sick room stands Orsel tends the hurt there. The mill is no place of care any more: whoever
            // still gets there finds her gone and turns home.
            bool careAtMill = atMill && !CareInVillage;
            bool atCare = careAtMill || inVillage && care && CareInVillage;
            VillageWork plan;
            // A patient stays in Orsel's care until whole. Care does not heal the starving, though, and an errand
            // bars the way home to eat: a starving resident at the mill does what the errand still allows and
            // otherwise goes home, where Mara may have rations again.
            if (careAtMill && care && r.Health < VillageEconomyRules.MaxHealth)
                plan = r.Hunger >= VillageEconomyRules.MaxNeed ? AwayAssignment(r, night) : VillageWork.None;
            else if (!r.CanWork) plan = inVillage ? (!atCare && care && (food || r.Hunger < VillageEconomyRules.EatHunger) ? VillageWork.SeekCare : VillageWork.None) : VillageWork.GoHome;
            else plan = Plan(p, r, night, campLimit, care);

            // The hard mask of what is possible right now; the resident's own controller chooses among it.
            // Going about the day's work is the villager's "travel" action: no new action is added to the minds.
            var legal = new List<ForgeAction>();
            if (needsSupply && !r.CanWork)
                legal.Add(inVillage ? ForgeAction.Eat : ForgeAction.SeekFood);
            else if (plan == VillageWork.GoHome || plan == VillageWork.SeekCare)
                legal.Add(ForgeAction.Travel);
            else
            {
                bool restCanHelp = r.Fatigue > 0 || r.Health < VillageEconomyRules.MaxHealth && r.Hunger < VillageEconomyRules.MaxNeed;
                if (plan == VillageWork.None || restCanHelp || night) legal.Add(ForgeAction.Wait);
                if (restCanHelp || night) legal.Add(ForgeAction.Rest);
                if (plan != VillageWork.None) legal.Add(ForgeAction.Travel);
                if (needsSupply) legal.Add(inVillage ? ForgeAction.Eat : ForgeAction.SeekFood);
                if (inVillage && !night && plan == VillageWork.None) legal.Add(ForgeAction.Observe);
            }
            var frame = new ObservationFrame(ActionSet.Of(legal.ToArray()), new[]
            {
                new Observation(Feature.Hunger, (drink ? Math.Max(r.Hunger, r.Thirst) : r.Hunger) * 10), new Observation(Feature.Fatigue, r.Fatigue * 10),
                new Observation(Feature.Injury, (VillageEconomyRules.MaxHealth - r.Health) * 10000 / VillageEconomyRules.MaxHealth),
                new Observation(Feature.Food, food || drink ? 10000 : 0),
                new Observation(Feature.TravelCost, inVillage ? 0 : 3000)
            });
            var action = UtilityEvaluator.Decide(mind.Controller, frame, mind.DecisionState, tick, true, mind.Cognition).Action;

            int hunger = r.Hunger, fatigue = r.Fatigue, health = r.Health, effect = 0;
            switch (action)
            {
                case ForgeAction.Eat:
                    if (drink)
                    {
                        effect = TryDrink(r);
                        r.Activity = effect > 0 ? VillageActivity.Drinking : VillageActivity.Idle;
                    }
                    else
                    {
                        r.Activity = p.Social.EatAtCamp(r) ? VillageActivity.Eating : VillageActivity.Idle;
                        effect = hunger - r.Hunger;
                    }
                    break;
                case ForgeAction.SeekFood:
                    Walk(r, VillageSite.Village);
                    r.Activity = VillageActivity.Walking;
                    break;
                case ForgeAction.Rest:
                    r.Fatigue = Math.Max(0, r.Fatigue - HomeRestRelief(night && inVillage) * Stride);
                    if (atCare && care && r.Hunger < VillageEconomyRules.MaxNeed) Care(forge, r, tick);
                    // Rest heals, but not on an empty stomach.
                    else if (tick % VillageEconomyRules.RestHealTicks == 0 && r.Health < VillageEconomyRules.MaxHealth && r.Hunger < VillageEconomyRules.MaxNeed) r.Health++;
                    r.Activity = !r.CanWork || careAtMill ? VillageActivity.Recovering : night && inVillage ? VillageActivity.Sleeping : VillageActivity.Resting;
                    effect = fatigue - r.Fatigue + r.Health - health;
                    break;
                case ForgeAction.Travel:
                    StartWork(r, plan);
                    break;
                default:
                    r.Activity = VillageActivity.Idle;
                    break;
            }
            mind.Cognition.RecordOutcome(action, tick, effect);
        }

        private void ContinueCommitment(CharacterProgression p, NpcForgeState forge, VillageResidentState r,
            NpcMind mind, ulong tick, int campLimit, ArenaSimulation area)
        {
            if (tick % VillageEconomyRules.WorkFatigueTicks == 0)
                r.Fatigue = Math.Min(VillageEconomyRules.MaxNeed, r.Fatigue + 1);
            if (r.Walking)
            {
                r.Activity = VillageActivity.Walking;
                r.TravelTicks -= Stride;
                if (r.TravelTicks > 0) return;
                // An errand to an outlying site is not done by getting there: its result is what the work brings,
                // and the work settles its own decision when it ends. A plain walk - home, to food, to Orsel -
                // is complete on arrival.
                bool errand = r.Destination != VillageSite.Village && IsExternalAssignment(r.Assignment);
                Arrive(r);
                var action = mind.DecisionState.Action;
                if (!errand && (action == ForgeAction.Travel || action == ForgeAction.SeekFood))
                    mind.Cognition.SettleCommittedOutcome(action, tick, 1);
                return;
            }
            r.Activity = VillageActivity.Working;
            r.WorkTicks -= Stride;
            if (r.WorkTicks > 0) return;
            int units = Finish(p, forge, r, tick, campLimit, area);
            if (r.Site != VillageSite.Village &&
                (units == 0 || r.Assignment != VillageWork.FellTree && r.Assignment != VillageWork.PlantSeedling))
                r.Assignment = VillageWork.None;
            r.Activity = VillageActivity.Idle;
            mind.Cognition.SettleCommittedOutcome(ForgeAction.Travel, tick, units);
        }

        /// <summary>
        /// What the village lacks decides the next work step, in a fixed order: rations ready for Mara, game to cook,
        /// food to hunt (a bow first, built from frame and thorn berry, from wood, from a tree), a broken axe to
        /// repair, wood for the pile, then the forest's own renewal. Returns None when nothing can be done.
        /// </summary>
        internal VillageWork Plan(CharacterProgression p, VillageResidentState r, bool night, int campLimit, bool care)
        {
            if (r.Site != VillageSite.Village) return AwayAssignment(r, night);
            if (night) return VillageWork.None;
            var supply = PlanWater(p, r);
            if (supply != VillageWork.None) return supply;
            bool axe = r.AxeUses > 0 || Commons(VillageGood.Axe) > 0;
            bool bow = r.Carried(VillageGood.Bow) > 0 || Commons(VillageGood.Bow) > 0;
            bool canFell = axe && CanSeekSource(VillageSource.MatureTrees, LastTick);
            int camp = p.Social.CampProvisions;
            var wish = VillageWork.None;

            if (Commons(VillageGood.Ration) > 0 && camp < campLimit) wish = VillageWork.DeliverRations;
            if (wish == VillageWork.None && Commons(VillageGood.Game) > 0)
                wish = Commons(VillageGood.Water) < 1 ? (_sources[(int)VillageSource.Well] > 0 ? VillageWork.FetchWater : VillageWork.None) :
                    Commons(VillageGood.Wood) < 1 ? (canFell ? VillageWork.FellTree : VillageWork.None) : VillageWork.Cook;
            bool hungryVillage = camp + Commons(VillageGood.Ration) + 2 * Commons(VillageGood.Game) < VillageEconomyRules.FoodTarget;
            if (wish == VillageWork.None && care) wish = PlanProduction(p, r, true);
            if (wish == VillageWork.None && (hungryVillage || care && NeedHideForCare()) && CanSeekSource(VillageSource.Wildlife, LastTick))
                wish = bow ? VillageWork.Hunt :
                    Commons(VillageGood.WoodFrame) >= 1 && Commons(VillageGood.ThornBerry) >= 1 ? VillageWork.MakeBow :
                    Commons(VillageGood.WoodFrame) < 1 && Commons(VillageGood.Wood) >= 2 ? VillageWork.MakeFrame :
                    Commons(VillageGood.ThornBerry) < 1 && CanSeekSource(VillageSource.Berries, LastTick) ? VillageWork.GatherBerries :
                    Commons(VillageGood.WoodFrame) < 1 && canFell ? VillageWork.FellTree : VillageWork.None;
            if (wish == VillageWork.None && Commons(VillageGood.BrokenAxe) >= 1 && Commons(VillageGood.Metal) >= 1 && Commons(VillageGood.Wood) >= 1)
                wish = VillageWork.RepairAxe;
            // Healing herbs for Orsel's shelf: carry what lies in the commons to the mill, gather while her shelf is short.
            // With the sick room in the village the herbs stay where they are gathered to: on its rack.
            if (wish == VillageWork.None && care && !CareInVillage && Commons(VillageGood.Herb) > 0 && WantsHerbDelivery(LastTick)) wish = VillageWork.BringHerbs;
            if (wish == VillageWork.None && care && (CareInVillage ? 0 : KnownMillHerbsForSupply(LastTick)) + Commons(VillageGood.Herb) < VillageEconomyRules.HerbTarget && CanSeekSource(VillageSource.Herbs, LastTick))
                wish = VillageWork.GatherHerbs;
            if (wish == VillageWork.None && care) wish = PlanProduction(p, r, false);
            if (wish == VillageWork.None && Commons(VillageGood.Wood) < VillageEconomyRules.WoodTarget && canFell) wish = VillageWork.FellTree;
            if (wish == VillageWork.None && ReportedPlantingNeed)
                wish = ReportedSeedlings ? VillageWork.PlantSeedling : CanSeekSource(VillageSource.SeedCrop, LastTick) ? VillageWork.CollectSeedlings : VillageWork.None;
            return wish;
        }

        private static int CarriedYield(VillageWorkRule rule, VillageResidentState r)
        {
            if (rule.Work == VillageWork.CollectSeedlings) return 0;         // seedlings stay in the nursery at the forest edge
            int n = rule.Work == VillageWork.FellTree && r.AxeUses == 1 ? 1 : 0;
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
        /// Orsel's care, at the old mill or in the sick room of the village: every hundred ticks a healing herb from
        /// her shelf mends ten health; without herbs her hands and bandages mend three. In the village her shelf is
        /// the herb rack of the sick room, the village's own herbs. The first care of a game day is remembered by
        /// her and the patient.
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
                var words = new EpisodeDetails("care", r.Site == VillageSite.Village ? "village" : "mill", "sickbed");
                forge.Archive.Perceive("orsel", ev.Id, KnowledgeOrigin.Self, tick, words);
                forge.Archive.Perceive(r.Identity, ev.Id, KnowledgeOrigin.Self, tick, words);
            }
            if (tick % VillageEconomyRules.CareHerbTicks != 0) return;
            int dressingRelief = UseDressing(r) ? 2 : 0;
            bool inSickRoom = r.Site == VillageSite.Village;
            if (inSickRoom ? _commons[(int)VillageGood.Herb] > 0 : _millHerbs > 0)
            {
                if (inSickRoom) _commons[(int)VillageGood.Herb]--; else _millHerbs--;
                Use(VillageGood.Herb, 1);
                r.Health = Math.Min(VillageEconomyRules.MaxHealth, r.Health + VillageEconomyRules.CareHerbTicks / VillageEconomyRules.CareHealTicks + dressingRelief);
            }
            else r.Health = Math.Min(VillageEconomyRules.MaxHealth, r.Health + VillageEconomyRules.CareHerbTicks / VillageEconomyRules.PlainCareHealTicks + dressingRelief);
        }

        public const string CareContext = "village.care";

        // The houses of the village as of the step in hand; they belong to the character, not to the economy.
        private VillageBuildingState _houses = new VillageBuildingState();
        private bool CareInVillage { get { return _houses.Built(VillagePlace.SickRoom); } }

        /// <summary>
        /// Orsel moves into the sick room. She brings the herbs from her shelf at the mill along, and her patients:
        /// whoever lay in her care at the mill lies in the sick room now and puts down there what they carried.
        /// </summary>
        internal void MoveCareToVillage()
        {
            _commons[(int)VillageGood.Herb] += _millHerbs;
            _millHerbs = 0;
            foreach (VillageResidentState r in _residents)
            {
                if (r.Site != VillageSite.Mill || r.Walking || r.Work != VillageWork.None || r.Health >= VillageEconomyRules.MaxHealth) continue;
                r.Destination = VillageSite.Village;
                Arrive(r);
                r.Activity = VillageActivity.Recovering;
            }
        }

        /// <summary>Arriving in the village, a resident puts the load down in the commons and keeps the tools in hand.</summary>
        private void Arrive(VillageResidentState r)
        {
            r.Site = r.Destination;
            r.TravelTicks = 0;
            if (r.Site != VillageSite.Village) return;
            r.Assignment = VillageWork.None;
            for (int g = 0; g < r.Carry.Length; g++)
            {
                if (g == (int)VillageGood.Axe || g == (int)VillageGood.Bow) continue;
                _commons[g] += r.Carry[g];
                r.Carry[g] = 0;
            }
            r.ReturningBrokenTool = false;
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
            if (work == VillageWork.SeekCare) { PackForCare(r); Walk(r, VillageSite.Mill); r.Activity = VillageActivity.Walking; return true; }
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
            if (!StartProductionTrip(r, work)) return false;
            if (rule.Site != r.Site)
            {
                if (r.Site == VillageSite.Village) r.Assignment = work;
                Walk(r, rule.Site);
                r.Activity = VillageActivity.Walking;
                return true;
            }
            if (!CanStartProduction(r, work)) return false;
            if (rule.Source >= 0 && _sources[rule.Source] < 1) return false;
            if (work == VillageWork.FellTree && _sources[(int)VillageSource.MatureTrees] <= VillageEconomyRules.SeedTreeReserve) return false;
            if (work == VillageWork.PlantSeedling && Source(VillageSource.MatureTrees) + SaplingCount >= VillageEconomyRules.StandTarget) return false;
            if (rule.Site == VillageSite.Forest && r.Load + CarriedYield(rule, r) > VillageEconomyRules.CarryCapacity) return false;
            var inputs = InputStore(r, work);
            foreach (var input in rule.Inputs)
                if ((input.Good == VillageGood.Seedling ? _nursery : inputs[(int)input.Good]) < input.Count) return false;
            foreach (var input in rule.Inputs)
            {
                if (input.Good == VillageGood.Seedling) _nursery -= input.Count; else inputs[(int)input.Good] -= input.Count;
                r.Reserved[(int)input.Good] += input.Count;
            }
            r.Work = work;
            r.WorkTicks = VillageBuildRules.WorkTicks(rule, _houses);
            r.Activity = VillageActivity.Working;
            return true;
        }

        /// <summary>Give the work in hand up: what was held back returns to where it came from; nothing is produced.</summary>
        private void GiveUp(VillageResidentState r)
        {
            var inputs = InputStore(r, r.Work);
            for (int g = 0; g < r.Reserved.Length; g++)
            {
                if (r.Reserved[g] == 0) continue;
                if (g == (int)VillageGood.Seedling) _nursery += r.Reserved[g]; else inputs[g] += r.Reserved[g];
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
        private int Finish(CharacterProgression p, NpcForgeState forge, VillageResidentState r, ulong tick, int campLimit, ArenaSimulation area)
        {
            VillageWork work = r.Work;
            var rule = VillageEconomyRules.Rule(work);
            int yielded = 0;
            // Old saves may contain a last axe stroke that can no longer fit in the worker's hands.
            if (!CanStartProduction(r, work) || !CanBookProducts(rule, r) ||
                work == VillageWork.PlantSeedling && Source(VillageSource.MatureTrees) + SaplingCount >= VillageEconomyRules.StandTarget ||
                rule.Site == VillageSite.Forest && r.Load + CarriedYield(rule, r) > VillageEconomyRules.CarryCapacity)
            { GiveUp(r); return 0; }
            if (work == VillageWork.OfferWater) return FinishWaterAid(forge, r, tick);
            if (work == VillageWork.DeliverRations)
            {
                int accepted = Deliver(p, forge, r, tick, campLimit, area != null && area.MaraReceivesAtStore());
                GiveUp(r); // put down the unaccepted part of this carrier's load
                return accepted;
            }
            for (int g = 0; g < r.Reserved.Length; g++)
            {
                if (r.Reserved[g] == 0) continue;
                Use((VillageGood)g, r.Reserved[g]);
                r.Reserved[g] = 0;
            }
            r.Work = VillageWork.None;
            r.WorkTicks = 0;
            if (work >= VillageWork.SowField && work <= VillageWork.TendHearth) return ApplyProduction(r, work);
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
                    // Orsel has moved to the village: nothing is left on an empty shelf, the herbs go home again.
                    if (CareInVillage) return 0;
                    // From the hands onto Orsel's shelf; what does not fit goes home again.
                    yielded = Math.Min(r.Carry[(int)VillageGood.Herb], VillageEconomyRules.MillHerbCap - _millHerbs);
                    r.Carry[(int)VillageGood.Herb] -= yielded; _millHerbs += yielded;
                    return yielded;
                case VillageWork.FetchWater:
                    yielded = Math.Min(rule.SourceUse, _sources[(int)VillageSource.Well]);
                    _sources[(int)VillageSource.Well] -= yielded;
                    _commons[(int)VillageGood.Water] += yielded; Make(VillageGood.Water, yielded);
                    return yielded;
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
        /// A carrier deposits the reserved rations into the store and returns any surplus to the commons.
        /// The carrier remembers the day's first delivery. Mara shares that memory when she receives it in person.
        /// </summary>
        private int Deliver(CharacterProgression p, NpcForgeState forge, VillageResidentState r, ulong tick, int campLimit, bool maraPresent)
        {
            int delivered = 0;
            while (r.Held(VillageGood.Ration) > 0 && _handedOut[(int)VillageGood.Ration] < LedgerLimit &&
                p.Social.StoreCampRation(campLimit))
            {
                r.Reserved[(int)VillageGood.Ration]--;
                _handedOut[(int)VillageGood.Ration]++;
                delivered++;
            }
            long day = (long)(tick / WorldSkyClock.TicksPerDay);
            if (delivered == 0 || day == r.DeliveryDay) return delivered;
            r.DeliveryDay = day;
            var ev = forge.Archive.Append(MemoryKind.Help, r.Identity, maraPresent ? "mara" : "village.store", tick, 3000, false, DeliveryContext,
                new RelationshipChange(trust: 300, gratitude: 400, familiarity: 200));
            var words = new EpisodeDetails("food", "village", "rations");
            forge.Archive.Perceive(r.Identity, ev.Id, KnowledgeOrigin.Self, tick, words);
            if (maraPresent) forge.Archive.Perceive("mara", ev.Id, KnowledgeOrigin.Self, tick, words);
            return delivered;
        }

        public const string DeliveryContext = "village.delivery.rations";

        private bool CanBookProducts(VillageWorkRule rule, VillageResidentState r)
        {
            foreach (var output in rule.Outputs)
                if (Present(output.Good) - r.Held(output.Good) > StockLimit - output.Count ||
                    _made[(int)output.Good] > LedgerLimit - output.Count) return false;
            if (rule.Work == VillageWork.FellTree && r.AxeUses == 1)
                return Present(VillageGood.BrokenAxe) < StockLimit && Made(VillageGood.BrokenAxe) < LedgerLimit;
            return true;
        }

        /// <summary>
        /// Check both the receiving stock and its ledger before the giver's inventory changes.
        /// </summary>
        internal bool CanReceive(VillageGood good, int count)
        {
            int g = (int)good;
            return count > 0 && g >= 0 && g < _commons.Length && Present(good) <= StockLimit - (long)count &&
                _received[g] <= LedgerLimit - count;
        }

        internal void Receive(VillageGood good, int count)
        {
            if (!CanReceive(good, count)) throw new ArgumentOutOfRangeException(nameof(count));
            UpgradeRules();
            if (good == VillageGood.Seedling) _nursery += count; else _commons[(int)good] += count;
            _received[(int)good] += count;
        }

        /// <summary>What the village is waiting for right now, in plain words for the village board.</summary>
        public List<string> OpenNeeds(CharacterProgression p) { return VillageBoard.LocalNeeds(p); }

    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The village works on wherever the player is, as long as the game itself is running.</summary>
        private void StepVillageEconomy()
        {
            Progression.Economy.Advance(Progression, this);
            if (Progression.Economy.Started) StepVillageContacts();
        }

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
            if (!Progression.Economy.CanReceive(good, count))
            { result.Text = "Diese Menge passt nicht ins Dorflager."; return result; }
            if (!Progression.Crafting.TakeForVillage(good, count))
            { result.Text = "Davon hast du nicht genug bei dir. Nichts wurde übergeben."; return result; }
            Progression.Economy.Receive(good, count);
            RememberVillageGift(good, count);
            result.Accepted = true;
            result.Text = count + " × " + VillageEconomyRules.GoodName(good) + " liegt jetzt im Dorflager.";
            return result;
        }
    }
}
