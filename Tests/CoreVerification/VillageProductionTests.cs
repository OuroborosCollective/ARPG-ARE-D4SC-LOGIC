using System;
using System.Linq;
using System.Globalization;
using AreArpg.Core;

internal static class VillageProductionTests
{
    public static void Run(Action<bool, string> check)
    {
        var p = CharacterProgression.CreateEntryProfile();
        for (int i = 0; i < 3; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect);
        p.TravelHunger(4, false);
        p.DiscoverHungerClue();
        check(p.Village.UnlockOrselTeaches(p), "mill-production-follows-the-existing-mill-quest");
        p.EnterVillage(); p.Social.EnableForge();
        for (int i = 0; i < 3; i++) p.Village.RecordMainQuestCompleted();
        var world = new ArenaSimulation(79, p, 1, LootCatalog.Version1);
        var economy = p.Economy;
        ulong harvestDue = 0, firstHarvest = 0;
        bool grainWentOut = false, flourCameHome = false, midMillReload = false, repaired = false;
        int lastShelter = VillageEconomyRules.ShelterCapacity;
        for (int t = 0; t < 10 * (int)WorldSkyClock.TicksPerDay; t++)
        {
            world.Step();
            if (!economy.Started || (t + 1) % VillageEconomyRules.Stride != 0) continue;
            int growing = economy.Source(VillageSource.CropTicks);
            if (harvestDue == 0 && growing > 0) harvestDue = economy.LastTick + (ulong)growing;
            if (firstHarvest == 0 && economy.Made(VillageGood.Grain) > 0) firstHarvest = economy.LastTick;
            int shelter = economy.Source(VillageSource.Shelter);
            if (shelter > lastShelter) repaired = true;
            lastShelter = shelter;
            foreach (var r in economy.Residents)
            {
                grainWentOut |= r.Walking && r.Destination == VillageSite.Mill && r.Carried(VillageGood.Grain) >= 2;
                flourCameHome |= r.Walking && r.Destination == VillageSite.Village && r.Carried(VillageGood.Flour) >= 2;
                if (!midMillReload && r.Work == VillageWork.MillGrain && r.Held(VillageGood.Grain) == 2)
                {
                    string save = p.SaveString();
                    check(CharacterProgression.TryLoad(save, out var restored) && restored.SaveString() == save &&
                        restored.Economy.Token() == economy.Token(), "actual-miller-keeps-grain-reservation-in-reloaded-save");
                    midMillReload = true;
                }
            }
        }
        check(harvestDue != 0 && firstHarvest >= harvestDue + (ulong)VillageEconomyRules.Rule(VillageWork.HarvestField).Ticks,
            "village-field-matures-for-its-full-game-time-before-harvest");
        check(grainWentOut && flourCameHome && midMillReload && economy.Made(VillageGood.Flour) == economy.Used(VillageGood.Grain),
            "grain-is-carried-to-the-mill-and-the-flour-is-carried-back");
        check(economy.Made(VillageGood.SeedGrain) > 1 && economy.Made(VillageGood.Grain) == 4 * economy.Made(VillageGood.SeedGrain) &&
            economy.Used(VillageGood.Compost) > 0 && VillageEconomyRules.SoilCapacity + economy.Used(VillageGood.Compost) - economy.Used(VillageGood.SeedGrain) == economy.Source(VillageSource.Soil),
            "seed-is-kept-and-compost-restores-the-soil-consumed-by-each-sowing");
        check(economy.Used(VillageGood.Flour) > 0 && economy.Made(VillageGood.Ration) ==
            2 * economy.Used(VillageGood.Game) + 3 * economy.Used(VillageGood.Flour) && economy.HandedOut(VillageGood.Ration) > 0,
            "bread-and-game-reach-the-same-real-ration-and-store-accounts");
        check(repaired && economy.Used(VillageGood.Fitting) > 0 && economy.Used(VillageGood.Stone) > 0 &&
            economy.Made(VillageGood.Stone) == VillageEconomyRules.StoneReserve - economy.Source(VillageSource.Rubble),
            "shelter-wears-and-returns-to-service-through-smith-and-builder");
        check(economy.Made(VillageGood.Ore) > 0 && economy.Made(VillageGood.Ore) == VillageEconomyRules.OreReserve - economy.Source(VillageSource.OreVein) &&
            economy.Made(VillageGood.Metal) == economy.Used(VillageGood.Ore) && economy.Made(VillageGood.Fitting) == economy.Used(VillageGood.Stone) + economy.Present(VillageGood.Fitting),
            "the-shared-smith-uses-finite-ore-for-fittings-and-tools");
        check(economy.Made(VillageGood.Bandage) > 0 && economy.Used(VillageGood.Hide) == economy.Made(VillageGood.Leather) &&
            economy.Used(VillageGood.Leather) == economy.Made(VillageGood.Bandage),
            "real-hunt-hides-and-harvest-fibre-supply-the-care-workshop");
        check(economy.Audit().Count == 0 && !economy.Residents.Any(r => r.Load > VillageEconomyRules.CarryCapacity),
            "all-workchains-balance-with-bounded-carriers-after-ten-game-days");

        string[] fields = economy.Token().Split(';');
        string originalSources = fields[5];
        string[] sources = originalSources.Split(',');
        int ore = economy.Source(VillageSource.OreVein);
        sources[(int)VillageSource.OreVein] = (ore == 0 ? 1 : ore - 1).ToString(CultureInfo.InvariantCulture);
        fields[5] = string.Join(",", sources);
        check(!VillageEconomyState.TryRead(string.Join(";", fields), out _), "finite-seam-cannot-be-refilled-or-drained-by-editing-the-save");
        sources = originalSources.Split(',');
        bool bare = economy.Source(VillageSource.CropTicks) + economy.Source(VillageSource.RipePlot) == 0;
        sources[(int)VillageSource.CropTicks] = "0";
        sources[(int)VillageSource.RipePlot] = bare ? "1" : "0";
        fields[5] = string.Join(",", sources);
        check(!VillageEconomyState.TryRead(string.Join(";", fields), out _), "a-field-without-its-sowing-history-is-rejected");

        DressingReachesThePatient(world, check);
    }

    // Arrange an injured resident in the working settlement. All resources for this case were
    // produced in the preceding ten days. Travel, care, consumption and knowledge run in the game.
    private static void DressingReachesThePatient(ArenaSimulation world, Action<bool, string> check)
    {
        var e = world.Progression.Economy;
        VillageResidentState patient = null;
        for (int t = 0; t < 2 * (int)WorldSkyClock.TicksPerDay && patient == null; t++)
        {
            world.Step();
            if (e.Commons(VillageGood.Bandage) == 0 || e.Commons(VillageGood.Water) == 0 || e.Residents.Any(r => !r.CanWork)) continue;
            patient = e.Residents.FirstOrDefault(r => r.Site == VillageSite.Village && !r.Walking && r.Work == VillageWork.None &&
                r.Hunger < VillageEconomyRules.EatHunger && r.Load <= VillageEconomyRules.CarryCapacity - 2);
        }
        check(patient != null, "a-produced-dressing-and-clean-water-are-available-to-the-household");
        if (patient == null) return;
        int injury = VillageEconomyRules.WorkHealth - 40;
        patient.Health = injury;
        long before = e.Used(VillageGood.Bandage);
        bool carried = false;
        for (int t = 0; t < VillageEconomyRules.MillTravelTicks + (int)WorldSkyClock.TicksPerDay && e.Used(VillageGood.Bandage) == before; t++)
        {
            world.Step();
            carried |= patient.Walking && patient.Destination == VillageSite.Mill &&
                patient.Carried(VillageGood.Bandage) == 1 && patient.Carried(VillageGood.Water) >= 1;
        }
        check(carried && patient.Site == VillageSite.Mill && patient.Health > injury && patient.Carried(VillageGood.Bandage) == 0 &&
            e.Used(VillageGood.Bandage) == before + 1 && e.Audit().Count == 0,
            "the-dressing-is-carried-to-orsel-and-consumed-on-the-patient-once");
    }
}
