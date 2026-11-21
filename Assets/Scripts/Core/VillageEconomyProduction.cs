using System;

namespace AreArpg.Core
{
    public sealed partial class VillageEconomyState
    {
        // The field and deposits have a single, recoverable stock equation. Apply it when
        // reading this rules revision, after both the world and the goods ledger are available.
        private bool ProductionAccountsAgree()
        {
            long growing = Source(VillageSource.CropTicks) > 0 || Source(VillageSource.RipePlot) > 0 ? 1 : 0;
            return Source(VillageSource.OreVein) + Made(VillageGood.Ore) == VillageEconomyRules.OreReserve &&
                Source(VillageSource.Rubble) + Made(VillageGood.Stone) == VillageEconomyRules.StoneReserve &&
                VillageEconomyRules.SoilCapacity + Used(VillageGood.Compost) - Used(VillageGood.SeedGrain) == Source(VillageSource.Soil) &&
                Used(VillageGood.SeedGrain) - Made(VillageGood.SeedGrain) == growing;
        }

        // The garden, workyard and household are in the village. Grain for the hand-driven
        // millstone travels with the miller. Loose rock and ore come home from the forest road.
        private void AdvanceProduction(ulong tick)
        {
            int remaining = Source(VillageSource.CropTicks);
            if (remaining > 0)
            {
                remaining -= VillageEconomyRules.Stride;
                _sources[(int)VillageSource.CropTicks] = remaining;
                if (remaining == 0) _sources[(int)VillageSource.RipePlot] = 1;
            }
            // At dawn last night's fuel has burned. Roof and hearth are household state,
            // so leaving the scene or loading a save cannot refresh them.
            if (WorldSkyClock.PhaseTicks(tick) == VillageEconomyRules.NightEnd)
                _sources[(int)VillageSource.Hearth] = 0;
            if (tick % VillageEconomyRules.ShelterWearTicks == 0 && Source(VillageSource.Shelter) > 0)
                _sources[(int)VillageSource.Shelter]--;
        }

        private int HomeRestRelief(bool homeAtNight)
        {
            if (!homeAtNight) return VillageEconomyRules.RestRelief;
            int relief = VillageEconomyRules.RestRelief;
            if (Source(VillageSource.Hearth) == 0) relief--;
            if (Source(VillageSource.Shelter) == 0) relief--;
            return relief;
        }

        private bool AnyoneWorking(VillageWork work)
        {
            foreach (var worker in _residents)
                if (worker.Work == work) return true;
            return false;
        }

        private bool CanStartProduction(VillageResidentState r, VillageWork work)
        {
            switch (work)
            {
                case VillageWork.SowField:
                    return Source(VillageSource.Soil) > 0 && Source(VillageSource.CropTicks) == 0 && Source(VillageSource.RipePlot) == 0;
                case VillageWork.HarvestField: return Source(VillageSource.RipePlot) > 0;
                case VillageWork.DressSoil: return Source(VillageSource.Soil) < VillageEconomyRules.SoilCapacity;
                case VillageWork.RecoverOre: return Source(VillageSource.OreVein) >= VillageEconomyRules.Rule(work).SourceUse;
                case VillageWork.GatherStone: return Source(VillageSource.Rubble) >= VillageEconomyRules.Rule(work).SourceUse;
                case VillageWork.RepairShelter: return Source(VillageSource.Shelter) < VillageEconomyRules.ShelterCapacity;
                case VillageWork.TendHearth: return Source(VillageSource.Hearth) == 0;
                case VillageWork.MillGrain:
                    return r.Site == VillageSite.Mill && (r.Work == work || r.Carried(VillageGood.Grain) >= VillageEconomyRules.Rule(work).Inputs[0].Count);
                default: return true;
            }
        }

        // Select work from goods that are actually here. Forest supplies arriving tomorrow
        // may not be consumed by a recipe today. Urgent food is offered before a fresh hunt.
        private VillageWork PlanProduction(CharacterProgression p, VillageResidentState r, bool urgent)
        {
            if (!p.Village.OrselTeachesUnlocked || r.Site != VillageSite.Village) return VillageWork.None;
            if (urgent)
            {
                bool shortOfFood = (long)p.Social.CampProvisions + Commons(VillageGood.Ration) < VillageEconomyRules.FoodTarget;
                if (shortOfFood && Commons(VillageGood.Flour) > 0 && Commons(VillageGood.Wood) > 0)
                    return WaterFor(VillageWork.BakeBread);
                if (shortOfFood && Commons(VillageGood.Grain) >= VillageEconomyRules.Rule(VillageWork.MillGrain).Inputs[0].Count)
                    return VillageWork.MillGrain;
                if (Source(VillageSource.RipePlot) > 0 && !AnyoneWorking(VillageWork.HarvestField))
                    return VillageWork.HarvestField;
                // A lone worker must sow before the last ration runs out: a plot takes half a day
                // to ripen. Once sown, the worker can hunt, repair or bring herbs while it grows.
                return PlanSowing(r);
            }
            if (Source(VillageSource.Hearth) == 0 && Commons(VillageGood.Wood) > 0 && !AnyoneWorking(VillageWork.TendHearth))
                return VillageWork.TendHearth;
            if (Commons(VillageGood.BrokenAxe) > 0 && Commons(VillageGood.Metal) == 0)
            {
                VillageWork supply = MetalSupply();
                if (supply != VillageWork.None) return supply;
            }
            if (Commons(VillageGood.Grain) >= 2 && Commons(VillageGood.Flour) < 2)
                return VillageWork.MillGrain;
            if (Source(VillageSource.Shelter) < VillageEconomyRules.ShelterCapacity && !AnyoneWorking(VillageWork.RepairShelter))
            {
                VillageWork repair = ShelterSupply();
                if (repair != VillageWork.None) return repair;
            }
            if (Commons(VillageGood.Bandage) == 0 && Commons(VillageGood.Straw) > 0 && !AnyoneWorking(VillageWork.MakeBandage))
            {
                if (Commons(VillageGood.Leather) > 0) return WaterFor(VillageWork.MakeBandage);
                if (Commons(VillageGood.Hide) > 0) return WaterFor(VillageWork.TanHide);
            }
            return VillageWork.None;
        }

        private VillageWork PlanSowing(VillageResidentState r)
        {
            if (Source(VillageSource.CropTicks) != 0 || Source(VillageSource.RipePlot) != 0 ||
                Commons(VillageGood.SeedGrain) == 0 || Commons(VillageGood.Grain) + (long)Commons(VillageGood.Flour) >= 6 ||
                AnyoneWorking(VillageWork.SowField)) return VillageWork.None;
            if (Source(VillageSource.Soil) == 0)
            {
                if (Commons(VillageGood.Compost) > 0 && !AnyoneWorking(VillageWork.DressSoil)) return VillageWork.DressSoil;
                if (Commons(VillageGood.Straw) > 0 && !AnyoneWorking(VillageWork.MakeCompost) && !AnyoneWorking(VillageWork.DressSoil))
                    return WaterFor(VillageWork.MakeCompost);
                return VillageWork.None;
            }
            return Commons(VillageGood.Axe) > 0 || r.AxeUses > 0 ? WaterFor(VillageWork.SowField) : VillageWork.None;
        }

        private VillageWork WaterFor(VillageWork work)
        {
            if (Commons(VillageGood.Water) > 0) return work;
            return Source(VillageSource.Well) > 0 ? VillageWork.FetchWater : VillageWork.None;
        }

        // Bread can cover all food demand. The workshop may still need a real hide for a
        // dressing. Place one hunting errand, using the same bow, wildlife and kitchen chain.
        private bool NeedHideForCare()
        {
            if (Commons(VillageGood.Straw) == 0 || Commons(VillageGood.Bandage) > 0 || Commons(VillageGood.Leather) > 0 ||
                Commons(VillageGood.Hide) > 0 || Commons(VillageGood.Game) > 0 || AnyoneWorking(VillageWork.MakeBandage) ||
                AnyoneWorking(VillageWork.TanHide) || AnyoneWorking(VillageWork.Cook)) return false;
            foreach (var worker in _residents)
                if (worker.Assignment == VillageWork.Hunt) return false;
            return true;
        }

        private VillageWork MetalSupply()
        {
            if (Commons(VillageGood.Ore) > 0)
                return Commons(VillageGood.Wood) > 0 ? VillageWork.SmeltOre : VillageWork.None;
            return CanSeekSource(VillageSource.OreVein, LastTick) ?
                VillageWork.RecoverOre : VillageWork.None;
        }

        private VillageWork ShelterSupply()
        {
            if (Commons(VillageGood.Fitting) == 0)
            {
                if (Commons(VillageGood.Metal) == 0) return MetalSupply();
                if (Commons(VillageGood.Wood) > 0) return WaterFor(VillageWork.ForgeFitting);
                return VillageWork.None;
            }
            if (Commons(VillageGood.Stone) == 0)
                return CanSeekSource(VillageSource.Rubble, LastTick) ?
                    VillageWork.GatherStone : VillageWork.None;
            return Commons(VillageGood.Wood) >= 2 ? VillageWork.RepairShelter : VillageWork.None;
        }

        private int[] InputStore(VillageResidentState r, VillageWork work)
        {
            return work == VillageWork.MillGrain ? r.Carry : _commons;
        }

        // All the grain is picked up before departure. Interruption at the mill leaves it
        // in the miller's hands. It returns to the commons only after the return journey.
        private bool StartProductionTrip(VillageResidentState r, VillageWork work)
        {
            if (work != VillageWork.MillGrain || r.Site != VillageSite.Village) return true;
            int take = Math.Max(0, VillageEconomyRules.Rule(work).Inputs[0].Count - r.Carried(VillageGood.Grain));
            if (Commons(VillageGood.Grain) < take || r.Load + take > VillageEconomyRules.CarryCapacity) return false;
            _commons[(int)VillageGood.Grain] -= take;
            r.Carry[(int)VillageGood.Grain] += take;
            return true;
        }

        private int ApplyProduction(VillageResidentState r, VillageWork work)
        {
            var rule = VillageEconomyRules.Rule(work);
            if (rule.Source >= 0) _sources[rule.Source] -= rule.SourceUse;
            switch (work)
            {
                case VillageWork.SowField:
                    _sources[(int)VillageSource.CropTicks] = VillageEconomyRules.CropGrowthTicks;
                    return 1;
                case VillageWork.DressSoil:
                    _sources[(int)VillageSource.Soil]++;
                    return 1;
                case VillageWork.RepairShelter:
                    _sources[(int)VillageSource.Shelter] = VillageEconomyRules.ShelterCapacity;
                    return 1;
                case VillageWork.TendHearth:
                    _sources[(int)VillageSource.Hearth] = 1;
                    return 1;
            }
            int[] destination = rule.Site == VillageSite.Village ? _commons : r.Carry;
            int amount = 0;
            foreach (var output in rule.Outputs)
            {
                destination[(int)output.Good] += output.Count;
                Make(output.Good, output.Count);
                amount += output.Count;
            }
            return amount;
        }

        private void PackForCare(VillageResidentState r)
        {
            if (r.Site != VillageSite.Village || r.Carried(VillageGood.Bandage) > 0 ||
                Commons(VillageGood.Bandage) == 0 || Commons(VillageGood.Water) == 0 || r.Load > VillageEconomyRules.CarryCapacity - 2) return;
            _commons[(int)VillageGood.Bandage]--; r.Carry[(int)VillageGood.Bandage]++;
            _commons[(int)VillageGood.Water]--; r.Carry[(int)VillageGood.Water]++;
        }

        private bool UseDressing(VillageResidentState r)
        {
            if (r.Site != VillageSite.Mill || r.Carried(VillageGood.Bandage) == 0 || r.Carried(VillageGood.Water) == 0) return false;
            r.Carry[(int)VillageGood.Bandage]--; Use(VillageGood.Bandage, 1);
            r.Carry[(int)VillageGood.Water]--; Use(VillageGood.Water, 1);
            return true;
        }
    }
}
