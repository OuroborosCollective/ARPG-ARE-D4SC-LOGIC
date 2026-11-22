using System;
using System.Linq;
using System.Reflection;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>
/// Errands away from the village: a starving resident at the mill must not be held there by an errand, two reports
/// taken in the same tick must leave the newer one on the board, and the yield of an errand must reach the decision
/// that started it. Each case sets up one controlled moment of the real economy and then lets the real rules run.
/// </summary>
internal static class VillageEconomyErrandTests
{
    public static void Run(Action<bool, string> check)
    {
        StarvingAtTheMill(check);
        NewerMillReportOfTheSameTick(check);
        NewerForestReportOfTheSameTick(check);
        ErrandCreditFollowsItsYield(check);
        NoVillageWorkInTheTickThePlayerFalls(check);
    }

    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>A village whose mill quest is done (Orsel tends the sick) with the given number of residents at work.</summary>
    private static CharacterProgression MillVillage(int residents)
    {
        var p = CharacterProgression.CreateEntryProfile();
        for (int i = 0; i < 3; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect);
        p.TravelHunger(4, false);
        p.DiscoverHungerClue();
        if (!p.Village.UnlockOrselTeaches(p)) throw new Exception("FAIL the mill quest does not unlock Orsel's care");
        p.Social.EnableForge();
        for (int i = 0; i < residents; i++) p.Village.RecordMainQuestCompleted();
        for (int t = 0; t < 2 * VillageEconomyRules.Stride; t++) Tick(p);
        if (p.Economy.Residents.Count != residents) throw new Exception("FAIL the residents did not move in");
        return p;
    }

    private static void Tick(CharacterProgression p)
    {
        var forge = p.Social.Forge;
        forge.ProcessedTicks++;
        forge.Archive.AdvancePersonalTime(forge.ProcessedTicks);
        p.Economy.Advance(p);
    }

    private static int[] Commons(VillageEconomyState e) { return (int[])typeof(VillageEconomyState).GetField("_commons", Hidden).GetValue(e); }
    private static int[] Sources(VillageEconomyState e) { return (int[])typeof(VillageEconomyState).GetField("_sources", Hidden).GetValue(e); }

    /// <summary>Stand a resident at a site with free hands and nothing in progress, fed and rested.</summary>
    private static void Place(VillageResidentState r, VillageSite site)
    {
        r.Site = site; r.Destination = site; r.TravelTicks = 0; r.Work = VillageWork.None; r.WorkTicks = 0;
        r.Assignment = VillageWork.None; r.Activity = VillageActivity.Idle; r.Hunger = 100; r.Thirst = 100; r.Fatigue = 0;
        r.Health = VillageEconomyRules.MaxHealth;
    }

    /// <summary>The next tick the economy looks at the village, at least a few hours before nightfall.</summary>
    private static void WaitForDaylight(CharacterProgression p)
    {
        for (int t = 0; t < 2 * (int)WorldSkyClock.TicksPerDay; t++)
        {
            ulong phase = WorldSkyClock.PhaseTicks(p.Social.Forge.ProcessedTicks);
            if (phase % (ulong)VillageEconomyRules.Stride == 0 && (phase >= VillageEconomyRules.NightEnd + 200 || phase + 3000 < VillageEconomyRules.NightStart)) return;
            Tick(p);
        }
        throw new Exception("FAIL no daylight found");
    }

    /// <summary>
    /// Review finding: a resident who reached the mill with an errand, a scratch and an empty stomach was kept
    /// there. The errand barred the way home to eat, the scratch asked for care first, and care does not heal the
    /// starving. Such a resident now does what the errand still allows and otherwise goes home.
    /// </summary>
    private static void StarvingAtTheMill(Action<bool, string> check)
    {
        var p = MillVillage(1);
        WaitForDaylight(p);
        var e = p.Economy;
        var r = e.Residents[0];
        while (p.Social.CampProvisions > 0) if (!p.Social.TakeCampRation()) throw new Exception("FAIL the camp cannot be emptied");
        Place(r, VillageSite.Mill);
        r.Assignment = VillageWork.MillGrain;                 // set out with a milling errand; there is nothing left to mill
        r.Hunger = VillageEconomyRules.MaxNeed;                // the store was empty at departure and the way was long
        r.Health = VillageEconomyRules.MaxHealth - 2;          // starving has begun to tell
        for (int t = 0; t < 600 && r.Site == VillageSite.Mill && !r.Walking; t++) Tick(p);
        check(r.Walking && r.Destination == VillageSite.Village, "a-starving-resident-is-not-kept-at-the-mill-by-an-errand");
        for (int t = 0; t < VillageEconomyRules.MillTravelTicks + 20 && r.Walking; t++) Tick(p);
        check(r.Site == VillageSite.Village && !r.Walking && r.Assignment == VillageWork.None, "the-way-home-ends-in-the-village-and-ends-the-errand");
        // Mara has rations again: the same resident eats at home.
        check(p.Social.StoreCampRation(SocialLedger.CampCapacity) && p.Social.StoreCampRation(SocialLedger.CampCapacity), "mara-has-two-rations-again");
        int hunger = r.Hunger;
        for (int t = 0; t < 400 && r.Hunger >= hunger; t++) Tick(p);
        check(r.Hunger < hunger && p.Social.CampProvisions == 1, "home-again-the-resident-eats-as-soon-as-there-is-food");

        // A resident who can still work and has the grain for it finishes the milling first, then goes home.
        var q = MillVillage(1);
        WaitForDaylight(q);
        var miller = q.Economy.Residents[0];
        while (q.Social.CampProvisions > 0) if (!q.Social.TakeCampRation()) throw new Exception("FAIL the camp cannot be emptied");
        q.Economy.Receive(VillageGood.Grain, 2);
        Place(miller, VillageSite.Mill);
        Commons(q.Economy)[(int)VillageGood.Grain] -= 2; miller.Carry[(int)VillageGood.Grain] = 2;
        miller.Assignment = VillageWork.MillGrain;
        miller.Hunger = VillageEconomyRules.MaxNeed;
        miller.Health = VillageEconomyRules.MaxHealth - 2;
        bool milled = false;
        for (int t = 0; t < 900 && !(miller.Walking && miller.Destination == VillageSite.Village); t++)
        {
            Tick(q);
            milled |= miller.Work == VillageWork.MillGrain;
        }
        check(milled && q.Economy.Made(VillageGood.Flour) == 2 && miller.Walking && miller.Destination == VillageSite.Village,
            "a-hungry-but-able-resident-finishes-the-milling-and-then-goes-home");
        check(q.Economy.Audit().Count == 0, "the-ledger-balances-after-the-hungry-milling");

        // Unchanged: below starving, a scratched resident with an errand is tended first and works afterwards.
        var s = MillVillage(1);
        WaitForDaylight(s);
        var patient = s.Economy.Residents[0];
        Place(patient, VillageSite.Mill);
        patient.Assignment = VillageWork.MillGrain;
        patient.Health = VillageEconomyRules.MaxHealth - 2;
        for (int t = 0; t < 400 && patient.Health < VillageEconomyRules.MaxHealth; t++) Tick(s);
        check(patient.Health == VillageEconomyRules.MaxHealth && patient.Site == VillageSite.Mill && patient.CareDay >= 0,
            "a-fed-resident-with-a-scratch-is-still-tended-at-the-mill-first");
    }

    /// <summary>
    /// Review finding: two residents left the mill in the same tick, a third put herbs on Orsel's shelf between
    /// their departures. Both came home in the same tick; the second, newer report was dropped by a strict
    /// comparison and the board kept the older shelf.
    /// </summary>
    private static void NewerMillReportOfTheSameTick(Action<bool, string> check)
    {
        var p = MillVillage(3);
        WaitForDaylight(p);
        var e = p.Economy;
        var first = e.Residents[0]; var carrier = e.Residents[1]; var second = e.Residents[2];
        // Three herbs given to the village: one lies on Orsel's shelf already, two are in the carrier's hands at the mill.
        e.Receive(VillageGood.Herb, 3);
        Commons(e)[(int)VillageGood.Herb] -= 3;
        typeof(VillageEconomyState).GetField("_millHerbs", Hidden).SetValue(e, 1);
        foreach (var r in e.Residents) Place(r, VillageSite.Mill);
        carrier.Carry[(int)VillageGood.Herb] = 2;
        carrier.Work = VillageWork.BringHerbs; carrier.WorkTicks = VillageEconomyRules.Stride; carrier.Activity = VillageActivity.Working;
        check(e.Audit().Count == 0 && e.MillHerbs == 1, "three-herbs-are-accounted-for-before-the-departures");
        for (int t = 0; t < VillageEconomyRules.Stride; t++) Tick(p);
        check(first.Walking && second.Walking && first.SiteSampleTick == second.SiteSampleTick && first.SiteSample == 2 && second.SiteSample == 4 && e.MillHerbs == 3,
            "both-leave-in-the-same-tick-and-only-the-second-saw-the-full-shelf");
        for (int t = 0; t < VillageEconomyRules.MillTravelTicks + 20 && (first.Walking || second.Walking); t++) Tick(p);
        check(e.ReportedMillHerbs == 3 && e.MillReporterOrdinal == second.Ordinal, "the-newer-of-two-same-tick-mill-reports-stands-on-the-board");
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && loaded.Economy.ReportedMillHerbs == 3, "the-newer-mill-report-survives-save-and-load");
    }

    /// <summary>The same for the forest: the last tree above the seed trees fell between two departures.</summary>
    private static void NewerForestReportOfTheSameTick(Action<bool, string> check)
    {
        var p = MillVillage(3);
        WaitForDaylight(p);
        var e = p.Economy;
        var first = e.Residents[0]; var feller = e.Residents[1]; var second = e.Residents[2];
        foreach (var r in e.Residents) Place(r, VillageSite.Forest);
        Sources(e)[(int)VillageSource.MatureTrees] = VillageEconomyRules.SeedTreeReserve + 1;
        // The feller holds one of the village's axes and is at the last stroke of a tree.
        Commons(e)[(int)VillageGood.Axe] -= 1; feller.Carry[(int)VillageGood.Axe] = 1; feller.AxeUses = 5;
        feller.Assignment = VillageWork.FellTree;
        feller.Work = VillageWork.FellTree; feller.WorkTicks = VillageEconomyRules.Stride; feller.Activity = VillageActivity.Working;
        for (int t = 0; t < VillageEconomyRules.Stride; t++) Tick(p);
        check(first.Walking && second.Walking && first.SiteSampleTick == second.SiteSampleTick && first.SiteSample != second.SiteSample &&
            e.Source(VillageSource.MatureTrees) == VillageEconomyRules.SeedTreeReserve, "the-tree-fell-between-two-departures-of-the-same-tick");
        for (int t = 0; t < VillageEconomyRules.ForestTravelTicks + 20 && (first.Walking || second.Walking); t++) Tick(p);
        check(e.ReportedSupply(VillageSource.MatureTrees) == 0 && e.ForestReporterOrdinal == second.Ordinal,
            "the-newer-of-two-same-tick-forest-reports-stands-on-the-board");
    }

    /// <summary>
    /// Review finding: the decision to set out on an errand was credited on arrival, whatever the errand brought.
    /// The credit now waits for the work: a tree felled teaches the decision that started the errand, an errand
    /// that brings nothing teaches nothing, and a plain walk (home, to food) is still credited on arrival.
    /// </summary>
    private static void ErrandCreditFollowsItsYield(Action<bool, string> check)
    {
        // A plain village in the morning: the wood pile is short, so the first resident sets out to fell a tree.
        var p = PlainVillage();
        var e = p.Economy;
        var r = e.Residents[0];
        var mind = p.Social.Forge.Archive.Get(r.Identity);
        for (int t = 0; t < 400 && !(r.Walking && r.Destination == VillageSite.Forest); t++) Tick(p);
        check(r.Walking && r.Destination == VillageSite.Forest && r.Assignment == VillageWork.FellTree && mind.DecisionState.Action == ForgeAction.Travel,
            "the-resident-sets-out-to-fell-a-tree");
        string leaving = Learned(mind);
        for (int t = 0; t < VillageEconomyRules.ForestTravelTicks + 5 && r.Walking; t++) Tick(p);
        check(r.Site == VillageSite.Forest && !r.Walking && Learned(mind) == leaving, "arriving-at-the-forest-with-an-errand-is-not-yet-a-result");
        long wood = e.Made(VillageGood.Wood);
        for (int t = 0; t < 900 && e.Made(VillageGood.Wood) == wood; t++) Tick(p);
        check(e.Made(VillageGood.Wood) == wood + 3 && Learned(mind) != leaving, "the-felled-tree-teaches-the-resident");

        // An errand to a forest with nothing left to fell: the way out teaches nothing, the plain walk home does.
        var q = PlainVillage();
        var walker = q.Economy.Residents[0];
        var walkerMind = q.Social.Forge.Archive.Get(walker.Identity);
        for (int t = 0; t < 400 && !(walker.Walking && walker.Destination == VillageSite.Forest); t++) Tick(q);
        check(walker.Walking && walker.Assignment == VillageWork.FellTree, "a-second-resident-sets-out-too");
        Sources(q.Economy)[(int)VillageSource.MatureTrees] = VillageEconomyRules.SeedTreeReserve;   // somebody else felled the rest
        string before = Learned(walkerMind);
        for (int t = 0; t < VillageEconomyRules.ForestTravelTicks + 5 && walker.Walking; t++) Tick(q);
        check(walker.Site == VillageSite.Forest && !walker.Walking && Learned(walkerMind) == before, "a-fruitless-errand-teaches-nothing-on-arrival");
        for (int t = 0; t < 200 && !(walker.Walking && walker.Destination == VillageSite.Village); t++) Tick(q);
        string turning = Learned(walkerMind);
        check(walker.Walking && walker.Destination == VillageSite.Village && q.Economy.Made(VillageGood.Wood) == 0 && turning == before,
            "with-nothing-to-fell-the-resident-turns-home-and-has-still-learned-nothing");
        for (int t = 0; t < VillageEconomyRules.ForestTravelTicks + 5 && walker.Walking; t++) Tick(q);
        check(walker.Site == VillageSite.Village && !walker.Walking && Learned(walkerMind) != turning, "coming-home-is-credited-on-arrival");
    }

    /// <summary>A village before the mill quest: one resident, the stated start stock, early in the day.</summary>
    private static CharacterProgression PlainVillage()
    {
        var p = CharacterProgression.CreateEntryProfile();
        p.EnterVillage();
        p.Social.EnableForge();
        p.Village.RecordMainQuestCompleted();
        for (int t = 0; t < 2 * VillageEconomyRules.Stride; t++) Tick(p);
        if (p.Economy.Residents.Count != 1) throw new Exception("FAIL the resident did not move in");
        return p;
    }

    /// <summary>
    /// Review finding: the game stops for a fallen player, but in the very tick a villager struck the player down
    /// the village still worked on: the check for defeat stood before the blow. Now the economy rests from that
    /// tick on, like everything else, until the player is back.
    /// </summary>
    private static void NoVillageWorkInTheTickThePlayerFalls(Action<bool, string> check)
    {
        var profile = CharacterProgression.CreateEntryProfile();
        profile.EnterVillage();
        profile.Social.EnableForge();
        var world = new ArenaSimulation(77, profile, 1, LootCatalog.Version1);
        check(world.CompleteArrivalScene(), "the-guard-fixture-completes-arrival-before-the-fight");
        world.CompleteMainQuest();
        for (int i = 0; i < 40; i++) world.Step();
        var e = profile.Economy;
        var forge = profile.Social.Forge;
        check(e.Started && e.Residents.Count == 1 && e.LastTick == forge.ProcessedTicks / 10 * 10, "the-village-is-at-work-before-the-fight");
        // The player attacks Dorn, who strikes back from where he stands beside the player.
        var dorn = world.Npcs.Single(n => n.Id == 2);
        dorn.Xmm = world.PlayerXmm; dorn.Zmm = world.PlayerZmm;
        check(world.InteractNpc(2, NpcAction.Attack).Accepted, "the-player-starts-a-fight-with-the-guard");
        for (int i = 0; i < 30 && dorn.Behavior != NpcBehavior.Attack; i++) world.Step();
        check(dorn.Behavior == NpcBehavior.Attack, "the-guard-fights-back");
        var health = typeof(ArenaSimulation).GetProperty("PlayerHealth");
        ulong fell = 0;
        for (int attempt = 0; attempt < 400 && !world.GameOver; attempt++)
        {
            // Hold the last blow back until the tick the village is due to look at its work.
            bool due = (forge.ProcessedTicks + 1) % (ulong)VillageEconomyRules.Stride == 0;
            health.SetValue(world, due ? 1 : world.MaxPlayerHealth);
            dorn.AttackCooldownTicks = due ? 0 : 5;
            dorn.Xmm = world.PlayerXmm; dorn.Zmm = world.PlayerZmm;
            world.Step();
            if (world.GameOver) fell = forge.ProcessedTicks;
        }
        check(world.GameOver && fell % (ulong)VillageEconomyRules.Stride == 0, "the-player-falls-on-a-tick-the-village-is-due");
        check(e.LastTick < fell, "the-village-does-not-work-on-in-the-tick-the-player-falls");
        string resting = e.Token();
        for (int i = 0; i < 50; i++) world.Step();
        check(e.Token() == resting && forge.ProcessedTicks == fell + 50, "while-the-player-is-down-the-village-rests");
    }

    /// <summary>The learned weights of one mind as text; equal text means nothing was learned in between.</summary>
    private static string Learned(NpcMind mind)
    {
        var weights = (sbyte[])typeof(PersonalCognition).GetField("_weights", Hidden).GetValue(mind.Cognition);
        return weights == null ? "" : string.Join(",", weights);
    }
}
