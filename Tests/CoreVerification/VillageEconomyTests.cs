using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>
/// Acceptance cases of the owner concept "NPC-Wirtschaft und Lebenszyklus" (section 11) against the real Core:
/// the small wood chain, reforestation, a broken tool, the store limit, hunting and eating, who knows what,
/// who can work, replay and save/load, and the ledger that proves nothing is doubled or lost.
/// </summary>
internal static class VillageEconomyTests
{
    private const int Day = (int)WorldSkyClock.TicksPerDay;

    public static void Run(Action<bool, string> check)
    {
        NothingBeforeTheFirstResident(check);
        ALastAxeReturnsBeforeOverfilling(check);
        EatBeforeLeavingForCare(check);
        AFullStockRefusesTheWholeGift(check);
        WorkIsLearnedOnlyWhenItYields(check);
        DeliveryUsesItsOwnReservedLoad(check);
        SmallWoodChain(check);
        MissingMaterialStopsTheBow(check);
        Reforestation(check);
        BrokenAxeWaitsForMetal(check);
        StoreLimitAndRepeatedCalls(check);
        OneCarriesTheRations(check);
        HuntAndMeal(check);
        OnlyThoseInvolvedKnow(check);
        InjuredResidentsDoNotWork(check);
        NightAndNeeds(check);
        OrselsCare(check);
        ReplayAndSaveLoad(check);
        LedgerOverTenDays(check);
        PlayersRefillTaskStaysThePlayers(check);
        PlayerHelps(check);
        Places(check);
        TheBoard(check);
        TamperedSavesAreRejected(check);
        WholeSaves(check);
        LiveVillage(check);
    }

    // ------------------------------------------------------------------ helpers

    private static CharacterProgression Village(int residents)
    {
        var p = CharacterProgression.CreateEntryProfile(); p.EnterVillage(); p.Social.EnableForge();
        for (int i = 0; i < residents; i++) p.Village.RecordMainQuestCompleted();
        return p;
    }

    /// <summary>The village alone: game ticks pass and the economy is evaluated, as the simulation step does.</summary>
    private static void Pass(CharacterProgression p, int ticks)
    {
        var forge = p.Social.Forge;
        for (int i = 0; i < ticks; i++)
        {
            forge.ProcessedTicks++;
            forge.Archive.AdvancePersonalTime(forge.ProcessedTicks);
            p.Economy.Advance(p);
        }
    }

    private static int PassUntil(CharacterProgression p, int limit, Func<bool> done)
    {
        for (int i = 0; i < limit; i += 10)
        {
            if (done()) return i;
            Pass(p, 10);
        }
        return done() ? limit : -1;
    }

    private static string Text(CharacterProgression p) { return p.Economy.Token(); }

    /// <summary>A written starting situation: stocks, sources and residents as the test needs them, ledger balanced.</summary>
    private sealed class Scene
    {
        public string[] Fields;
        public long[] Commons, Sources, Made, Used, Received, HandedOut, Start;
        public long Nursery, MillHerbs;
        private const int Head = 13;
        public List<long[]> Residents = new List<long[]>();

        public static Scene Of(CharacterProgression p)
        {
            if (!p.Economy.Started) Pass(p, 1);
            var s = new Scene { Fields = Text(p).Split(';') };
            s.Commons = Row(s.Fields[4]); s.Sources = Row(s.Fields[5]); s.Nursery = Row(s.Fields[6])[0]; s.MillHerbs = Row(s.Fields[6])[1];
            s.Start = Row(s.Fields[8]); s.Made = Row(s.Fields[9]); s.Used = Row(s.Fields[10]); s.Received = Row(s.Fields[11]); s.HandedOut = Row(s.Fields[12]);
            if (s.Fields[13] != "x") foreach (string r in s.Fields[13].Split('/')) s.Residents.Add(Row(r));
            return s;
        }

        private static long[] Row(string text) { return text.Split(',').Select(x => long.Parse(x, CultureInfo.InvariantCulture)).ToArray(); }
        private static string Row(long[] values) { return string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture))); }

        public long[] Resident(int index)
        {
            while (Residents.Count <= index)
            {
                var r = new long[Head + 2 * VillageEconomyRules.GoodCount];
                r[0] = VillageServicesState.BasePopulation + Residents.Count; r[1] = 200; r[2] = 100; r[3] = VillageEconomyRules.MaxHealth;
                Residents.Add(r);
            }
            return Residents[index];
        }

        public static int Carry(VillageGood good) { return Head + (int)good; }
        public static int Held(VillageGood good) { return Head + VillageEconomyRules.GoodCount + (int)good; }

        /// <summary>Write the scene into the profile. The ledger is balanced by what was made or used before.</summary>
        public void Into(CharacterProgression p)
        {
            for (int g = 0; g < VillageEconomyRules.GoodCount; g++)
            {
                long present = Commons[g] + (g == (int)VillageGood.Seedling ? Nursery : 0) + (g == (int)VillageGood.Herb ? MillHerbs : 0);
                foreach (var r in Residents) present += r[Head + g] + r[Head + VillageEconomyRules.GoodCount + g];
                long gap = Start[g] + Made[g] + Received[g] - HandedOut[g] - Used[g] - present;
                if (gap >= 0) Used[g] += gap; else Made[g] -= gap;
            }
            Fields[4] = Row(Commons); Fields[5] = Row(Sources); Fields[6] = Nursery.ToString(CultureInfo.InvariantCulture) + "," + MillHerbs.ToString(CultureInfo.InvariantCulture);
            Fields[9] = Row(Made); Fields[10] = Row(Used); Fields[11] = Row(Received); Fields[12] = Row(HandedOut);
            Fields[13] = Residents.Count == 0 ? "x" : string.Join("/", Residents.Select(Row));
            if (!VillageEconomyState.TryRead(string.Join(";", Fields), out var state)) throw new Exception("FAIL scene is not a valid economy state");
            typeof(CharacterProgression).GetField("_economy", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(p, state);
        }
    }

    private static void TakeFromCamp(CharacterProgression p, int rations)
    {
        for (int i = 0; i < rations; i++) if (!p.Social.TakeCampRation()) throw new Exception("FAIL camp ration not available");
    }

    private static void ALastAxeReturnsBeforeOverfilling(Action<bool, string> check)
    {
        var p = Village(1);
        var scene = Scene.Of(p);
        var row = scene.Resident(0);
        row[4] = row[5] = (int)VillageSite.Forest;
        row[Scene.Carry(VillageGood.Axe)] = 1;
        row[Scene.Carry(VillageGood.Wood)] = 3;
        row[9] = 1;
        scene.Commons[(int)VillageGood.Axe] = 0;
        scene.Commons[(int)VillageGood.Wood] = 0;
        scene.Into(p);
        var r = p.Economy.Residents[0];
        check(p.Economy.Plan(p, r, false, SocialLedger.CampCapacity, false) == VillageWork.GoHome,
            "last-axe-needs-room-for-wood-and-broken-tool");
        bool bounded = true;
        bool returned = false;
        for (int i = 0; i < 3000; i += VillageEconomyRules.Stride)
        {
            Pass(p, VillageEconomyRules.Stride);
            bounded &= r.Load <= VillageEconomyRules.CarryCapacity;
            if (r.Site == VillageSite.Village) returned = true;
            if (p.Economy.Made(VillageGood.BrokenAxe) > 0) break;
        }
        string save = p.SaveString();
        check(bounded && returned && p.Economy.Made(VillageGood.BrokenAxe) == 1 && CharacterProgression.TryLoad(save, out var restored) && restored.SaveString() == save,
            "last-axe-remains-within-carry-and-loads-after-breaking");
    }

    private static void EatBeforeLeavingForCare(Action<bool, string> check)
    {
        var p = MillVillage(1);
        var scene = Scene.Of(p);
        var row = scene.Resident(0);
        row[1] = VillageEconomyRules.MaxNeed;
        row[3] = VillageEconomyRules.WorkHealth - 5;
        scene.Into(p);
        var r = p.Economy.Residents[0];
        int camp = p.Social.CampProvisions;
        Pass(p, VillageEconomyRules.Stride);
        var mind = p.Social.Forge.Archive.Get(r.Identity);
        check(r.Activity == VillageActivity.Eating && r.Hunger == VillageEconomyRules.MaxNeed - VillageEconomyRules.EatRelief &&
            p.Social.CampProvisions == camp - 1 && mind.Cognition.Weight(ForgeAction.Eat, 0) > 0,
            "injured-resident-eats-real-ration-before-journey-to-orsel");
        check(PassUntil(p, 1200, () => r.Site == VillageSite.Mill && !r.Walking) >= 0,
            "fed-injured-resident-can-still-reach-orsel");
    }

    private static void AFullStockRefusesTheWholeGift(Action<bool, string> check)
    {
        var p = Village(1);
        var world = new ArenaSimulation(77, p, 1, LootCatalog.Version1);
        Pass(p, VillageEconomyRules.Stride);
        ToBoard(world);
        p.Crafting.AddMaterial(MaterialKind.Holz, int.MaxValue);
        int wood = p.Economy.Commons(VillageGood.Wood);
        long received = p.Economy.Received(VillageGood.Wood);
        var declined = world.GiveToVillage(VillageGood.Wood, int.MaxValue);
        check(!declined.Accepted && p.Crafting.MaterialCount(MaterialKind.Holz) == int.MaxValue &&
            p.Economy.Commons(VillageGood.Wood) == wood && p.Economy.Received(VillageGood.Wood) == received,
            "overflowing-player-gift-is-rejected-before-inventory-withdrawal");
        check(CharacterProgression.TryLoad(p.SaveString(), out var after) && after.SaveString() == p.SaveString(),
            "refused-outsize-gift-remains-a-loadable-character");
    }

    private static void WorkIsLearnedOnlyWhenItYields(Action<bool, string> check)
    {
        var p = Village(1);
        var scene = Scene.Of(p);
        scene.Commons[(int)VillageGood.Wood] = 0;
        scene.Into(p);
        check(PassUntil(p, 3000, () => p.Economy.Residents.Count == 1 && p.Economy.Residents[0].Work == VillageWork.FellTree) >= 0,
            "worker-starts-an-actual-tree");
        var r = p.Economy.Residents[0];
        var mind = p.Social.Forge.Archive.Get(r.Identity);
        ulong decision = mind.DecisionState.LastTick;
        int before = mind.Cognition.Weight(ForgeAction.Travel, 0);
        Pass(p, 100);
        check(mind.DecisionState.LastTick == decision && mind.Cognition.Weight(ForgeAction.Travel, 0) == before &&
            p.Economy.Made(VillageGood.Wood) == 0, "unfinished-work-does-not-train-or-overwrite-initiating-frame");
        string save = p.SaveString();
        check(CharacterProgression.TryLoad(save, out var loaded), "ongoing-work-loads-before-outcome");
        int remaining = r.WorkTicks;
        Pass(p, remaining);
        Pass(loaded, remaining);
        var other = loaded.Social.Forge.Archive.Get(r.Identity);
        int learned = mind.Cognition.Weight(ForgeAction.Travel, 0);
        check(p.Economy.Made(VillageGood.Wood) == 3 && learned > before && mind.DecisionState.LastTick == decision &&
            other.DecisionState.LastTick == decision && other.Cognition.Weight(ForgeAction.Travel, 0) == learned && p.SaveString() == loaded.SaveString(),
            "actual-work-yield-trains-the-original-decision-once-through-save");
        p.Economy.Advance(p);
        check(mind.Cognition.Weight(ForgeAction.Travel, 0) == learned, "repeat-work-tick-gives-no-second-learning");
    }

    private static void DeliveryUsesItsOwnReservedLoad(Action<bool, string> check)
    {
        var p = Village(2);
        TakeFromCamp(p, 4);
        var scene = Scene.Of(p);
        var cook = scene.Resident(0);
        cook[7] = (int)VillageWork.Cook;
        cook[8] = VillageEconomyRules.Rule(VillageWork.DeliverRations).Ticks;
        cook[10] = (int)VillageActivity.Working;
        foreach (var ingredient in VillageEconomyRules.Rule(VillageWork.Cook).Inputs)
            cook[Scene.Held(ingredient.Good)] = ingredient.Count;
        var carrier = scene.Resident(1);
        carrier[7] = (int)VillageWork.DeliverRations;
        carrier[8] = cook[8];
        carrier[10] = (int)VillageActivity.Working;
        carrier[Scene.Held(VillageGood.Ration)] = 1;
        scene.Into(p);
        int before = p.Social.CampProvisions;
        Pass(p, (int)cook[8]);
        check(p.Economy.Made(VillageGood.Ration) >= 2 && p.Social.CampProvisions == before + 1 &&
            p.Economy.HandedOut(VillageGood.Ration) == 1 && p.Economy.Commons(VillageGood.Ration) == 2,
            "carrier-delivers-only-reserved-ration-while-other-resident-finishes-cooking");
    }

    // ------------------------------------------------------------------ cases

    private static void NothingBeforeTheFirstResident(Action<bool, string> check)
    {
        var p = Village(0);
        string before = p.SaveString();
        Pass(p, 3 * Day);
        check(!p.Economy.Started && p.Economy.Residents.Count == 0, "economy-waits-for-the-first-resident");
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && !loaded.Economy.Started && !p.SaveString().Contains("economy"), "no-economy-section-before-it-starts");
        check(before.Length > 0 && p.Social.CampProvisions == 8, "three-villager-village-keeps-its-camp-without-residents");
        var legacy = CharacterProgression.CreateEntryProfile(); legacy.EnterVillage(); legacy.Village.RecordMainQuestCompleted();
        legacy.Economy.Advance(legacy);
        check(!legacy.Economy.Started, "profile-without-memory-forge-has-no-economy");
    }

    /// <summary>Acceptance 1: wood is felled once, two wood and one thorn berry become exactly one bow.</summary>
    private static void SmallWoodChain(Action<bool, string> check)
    {
        var p = Village(1);
        TakeFromCamp(p, 3);                                   // the store is short: the village needs food, so it needs a bow
        Pass(p, 1);
        var e = p.Economy;
        check(e.Started && e.Commons(VillageGood.Axe) == 2 && e.Commons(VillageGood.Wood) == 4 && e.Commons(VillageGood.Metal) == 2 &&
            e.NurserySeedlings == 4 && e.Source(VillageSource.MatureTrees) == 24 && e.Source(VillageSource.Wildlife) == 6, "stated-start-stock-and-living-forest");
        int used = PassUntil(p, 4000, () => e.Made(VillageGood.Bow) == 1);
        check(used > 0, "one-resident-builds-a-bow-within-the-first-afternoon");
        check(e.Made(VillageGood.WoodFrame) == 1 && e.Used(VillageGood.WoodFrame) == 1 && e.Used(VillageGood.ThornBerry) == 1 && e.Used(VillageGood.Wood) == 2,
            "two-wood-one-frame-one-thorn-berry-one-bow");
        check(e.Made(VillageGood.ThornBerry) == 2 && e.Source(VillageSource.Berries) <= 7, "berries-come-from-the-bushes-once");
        check(e.Audit().Count == 0, "ledger-balances-after-the-bow");
        // The bow is not rebuilt for every hunt.
        int hunted = PassUntil(p, 4000, () => e.Made(VillageGood.Game) == 1);
        // The herd of six grows by one for every 3600 ticks while it is below its limit; the hunt takes exactly one.
        int grown = (int)(p.Social.Forge.ProcessedTicks / VillageEconomyRules.WildlifeRegenTicks);
        check(hunted >= 0 && e.Made(VillageGood.Bow) == 1 && e.Source(VillageSource.Wildlife) == 6 + grown - 1, "the-same-bow-hunts-one-animal-leaves-the-herd-once");
        long fellings = e.Made(VillageGood.Wood) / 3;
        check(e.Made(VillageGood.Wood) % 3 == 0 && e.Source(VillageSource.MatureTrees) == 24 - fellings, "every-felled-tree-is-three-wood-and-one-tree-less");
        check(e.Audit().Count == 0, "ledger-balances-after-the-hunt");
    }

    /// <summary>Acceptance 1, second half: a missing material prevents the bow.</summary>
    private static void MissingMaterialStopsTheBow(Action<bool, string> check)
    {
        var p = Village(1);
        TakeFromCamp(p, 3);
        var scene = Scene.Of(p);
        scene.Sources[(int)VillageSource.Berries] = 0;
        scene.Into(p);
        var e = p.Economy;
        Pass(p, 1100);                                        // before the first berry ripens again
        check(e.Made(VillageGood.WoodFrame) == 1 && e.Made(VillageGood.Bow) == 0 && e.Commons(VillageGood.WoodFrame) == 1, "without-thorn-berries-the-frame-waits-and-no-bow-exists");
        check(e.OpenNeeds(p).Any(n => n.Contains("Bogen")), "the-missing-bow-is-a-visible-need");
        check(e.Made(VillageGood.Game) == 0 && e.Source(VillageSource.Wildlife) == 6, "no-bow-no-hunt");
        Pass(p, 4000);                                        // berries ripen again: the chain continues by itself
        check(e.Made(VillageGood.Bow) == 1 && e.Audit().Count == 0, "the-bow-follows-once-a-berry-has-ripened");
    }

    /// <summary>Acceptance 2: felled trees do not reappear; only planted seedlings grow, and only after their time.</summary>
    private static void Reforestation(Action<bool, string> check)
    {
        var p = Village(1);
        var e = p.Economy;
        int untilFirst = PassUntil(p, 4500, () => e.SaplingCount == 1);
        ulong firstPlanting = e.LastTick;
        int untilSecond = PassUntil(p, 4500 - untilFirst, () => e.SaplingCount == 2);
        ulong secondDue = e.LastTick + VillageEconomyRules.SaplingGrowthTicks;
        check(untilFirst > 0 && untilSecond > 0 && e.Source(VillageSource.MatureTrees) == 22 && e.NurserySeedlings == 2 && e.Used(VillageGood.Seedling) == 2, "two-trees-felled-two-seedlings-planted-from-the-nursery");
        ulong due = e.NextSaplingTick;
        check(due == firstPlanting + 3UL * Day && secondDue > due, "a-seedling-needs-three-game-days");
        // A quiet village for the waiting time: no game to hunt, so no wood is burnt and no further tree is felled.
        var quiet = Scene.Of(p);
        quiet.Sources[(int)VillageSource.Wildlife] = 0;
        quiet.Into(p);
        e = p.Economy;
        Pass(p, (int)(due - p.Social.Forge.ProcessedTicks) - 1);
        check(e.Source(VillageSource.MatureTrees) == 22 && e.SaplingCount == 2 && e.Used(VillageGood.Seedling) == 2, "no-tree-before-its-time");
        Pass(p, 1);
        check(e.Source(VillageSource.MatureTrees) == 23 && e.SaplingCount == 1 && e.NextSaplingTick == secondDue, "the-planted-seedling-is-a-tree-exactly-when-due");
        Pass(p, (int)(secondDue - p.Social.Forge.ProcessedTicks) - 1);
        check(e.Source(VillageSource.MatureTrees) == 23 && e.SaplingCount == 1, "no-second-tree-one-tick-before-its-own-maturity");
        Pass(p, 1);
        check(e.Source(VillageSource.MatureTrees) == 24 && e.SaplingCount == 0 && e.Audit().Count == 0, "both-seedlings-grew-up-and-the-stand-is-whole-again");

        // Without seedlings and without seed nothing is planted, and felling stops at the seed trees.
        var bare = Village(1);
        var scene = Scene.Of(bare);
        scene.Nursery = 0; scene.Sources[(int)VillageSource.SeedCrop] = 0; scene.Sources[(int)VillageSource.MatureTrees] = VillageEconomyRules.SeedTreeReserve + 1;
        scene.Commons[(int)VillageGood.Wood] = 0;
        scene.Into(bare);
        Pass(bare, 1150);
        check(bare.Economy.Source(VillageSource.MatureTrees) == VillageEconomyRules.SeedTreeReserve && bare.Economy.Made(VillageGood.Wood) == 3 && bare.Economy.SaplingCount == 0,
            "felling-stops-at-the-seed-trees-and-nothing-grows-from-nothing");
        check(bare.Economy.ForestReportTick == 0 && !bare.Economy.OpenNeeds(bare).Any(n => n.Contains("Samenbäume")),
            "the-village-does-not-yet-know-which-trees-the-forester-saw");
        check(PassUntil(bare, VillageEconomyRules.ForestTravelTicks + 100, () => bare.Economy.ForestReportTick != 0) >= 0 &&
            bare.Economy.OpenNeeds(bare).Any(n => n.Contains("Samenbäume")), "the-exhausted-forest-is-a-visible-need");
        Pass(bare, 3000);
        check(bare.Economy.Source(VillageSource.MatureTrees) == VillageEconomyRules.SeedTreeReserve && bare.Economy.Made(VillageGood.Wood) == 3, "the-seed-trees-are-never-felled");
    }

    /// <summary>Acceptance 3: a broken axe and its work wait until metal is really there.</summary>
    private static void BrokenAxeWaitsForMetal(Action<bool, string> check)
    {
        var p = Village(1);
        var scene = Scene.Of(p);
        scene.Commons[(int)VillageGood.Axe] = 0; scene.Commons[(int)VillageGood.Metal] = 0; scene.Commons[(int)VillageGood.Wood] = 0;
        var r = scene.Resident(0);
        r[Scene.Carry(VillageGood.Axe)] = 1; r[9] = 1;         // the last axe, one use left
        scene.Into(p);
        var e = p.Economy;
        int broke = PassUntil(p, 3000, () => e.Commons(VillageGood.BrokenAxe) == 1);   // walk out, fell one tree, the axe breaks, walk home
        check(broke > 0 && e.Made(VillageGood.Wood) == 3 && e.Made(VillageGood.BrokenAxe) == 1 && e.Residents[0].AxeUses == 0 && e.Residents[0].Carried(VillageGood.Axe) == 0,
            "the-worn-axe-breaks-after-its-last-tree");
        Pass(p, 2000);
        check(e.Made(VillageGood.Wood) == 3 && e.Made(VillageGood.Axe) == 0, "without-metal-no-repair-and-no-further-felling");
        check(e.OpenNeeds(p).Any(n => n.Contains("Metall")), "the-missing-metal-is-a-visible-need");
        var world = new ArenaSimulation(31, p, 1, LootCatalog.Version1);
        ToBoard(world);
        p.Crafting.AddMaterial(MaterialKind.Ascheisen, 1);
        check(world.GiveToVillage(VillageGood.Metal, 1).Accepted && e.Received(VillageGood.Metal) == 1 && p.Crafting.MaterialCount(MaterialKind.Ascheisen) == 0, "the-player-brings-one-ore");
        long metalUsed = e.Used(VillageGood.Metal), woodUsed = e.Used(VillageGood.Wood);
        int repaired = PassUntil(p, Day, () => e.Made(VillageGood.Axe) == 1);          // the night may lie in between
        check(repaired >= 0 && e.Used(VillageGood.Metal) == metalUsed + 1 && e.Used(VillageGood.Wood) >= woodUsed + 1 && e.Commons(VillageGood.BrokenAxe) == 0 &&
            e.Commons(VillageGood.Metal) == 0, "one-metal-one-wood-repair-the-axe");
        int felled = PassUntil(p, Day, () => e.Made(VillageGood.Wood) >= 6);
        check(felled >= 0 && e.Audit().Count == 0, "with-the-repaired-axe-the-felling-continues");
    }

    /// <summary>Acceptance 4: the store's capacity holds; repeated calls and reloading neither double nor lose rations.</summary>
    private static void StoreLimitAndRepeatedCalls(Action<bool, string> check)
    {
        var p = Village(1);
        TakeFromCamp(p, 2);                                   // 6 of 10
        var scene = Scene.Of(p);
        scene.Commons[(int)VillageGood.Ration] = 6;
        scene.Into(p);
        var e = p.Economy;
        Pass(p, 400);
        check(p.Social.CampProvisions == 9 && e.Commons(VillageGood.Ration) == 3 && e.HandedOut(VillageGood.Ration) == 3, "three-rations-handed-over-three-stay-in-the-commons");
        string held = Text(p); int camp = p.Social.CampProvisions;
        for (int i = 0; i < 5; i++) p.Economy.Advance(p);
        check(Text(p) == held && p.Social.CampProvisions == camp, "evaluating-the-same-tick-again-changes-nothing");
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && Text(loaded) == held && loaded.Social.CampProvisions == camp, "save-and-load-keep-stocks-exactly");
        Pass(p, 600); Pass(loaded, 600);
        check(Text(p) == Text(loaded) && p.Social.CampProvisions == loaded.Social.CampProvisions, "the-loaded-village-continues-identically");
        check(e.Commons(VillageGood.Ration) == e.Made(VillageGood.Ration) - e.HandedOut(VillageGood.Ration) && p.Social.CampProvisions <= SocialLedger.CampCapacity - 1,
            "no-ration-doubled-or-lost-at-the-full-store");
        check(e.Audit().Count == 0 && loaded.Economy.Audit().Count == 0, "ledger-balances-at-the-store-limit");
    }

    /// <summary>A delivery is a load in somebody's hands: held back, not yet handed over, and carried by one person only.</summary>
    private static void OneCarriesTheRations(Action<bool, string> check)
    {
        var p = Village(2);
        TakeFromCamp(p, 4);                                   // 4 of 10: room for more than one load
        var scene = Scene.Of(p);
        scene.Resident(0); scene.Resident(1);
        scene.Commons[(int)VillageGood.Ration] = 3;
        scene.Into(p);
        var e = p.Economy;
        int camp = p.Social.CampProvisions;
        PassUntil(p, 300, () => e.Residents.Any(r => r.Work == VillageWork.DeliverRations));
        var carrier = e.Residents.FirstOrDefault(r => r.Work == VillageWork.DeliverRations);
        check(carrier != null && e.Residents.Count(r => r.Work == VillageWork.DeliverRations) == 1, "one-resident-sets-out-with-the-rations");
        check(carrier.Held(VillageGood.Ration) == 3 && e.Commons(VillageGood.Ration) == 0 && p.Social.CampProvisions == camp && e.HandedOut(VillageGood.Ration) == 0,
            "the-load-is-held-back-not-yet-handed-over");
        check(e.Present(VillageGood.Ration) == 3 && e.Audit().Count == 0, "rations-on-the-way-are-still-the-villages");
        check(VillageBoard.ResidentLine(carrier).Contains("bringt Rationen zu Mara"), "the-board-names-the-errand");
        // Mid-way: the game is saved and loaded, and the errand ends the same in both.
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && Text(loaded) == Text(p), "a-delivery-in-progress-survives-save-and-load");
        Pass(p, 100); Pass(loaded, 100);
        check(p.Social.CampProvisions == camp + 3 && e.HandedOut(VillageGood.Ration) == 3 && e.Present(VillageGood.Ration) == 0 && e.Residents.All(r => r.Held(VillageGood.Ration) == 0),
            "three-rations-arrive-once");
        check(Text(p) == Text(loaded) && loaded.Social.CampProvisions == camp + 3, "the-loaded-errand-ends-identically");
        check(e.Residents.All(r => r.Activity != VillageActivity.Working || r.Work != VillageWork.None), "nobody-is-at-work-on-nothing");

        // Hurt on the way: the load goes back to the commons, nothing is handed over and nothing is lost.
        var hurt = Village(1);
        TakeFromCamp(hurt, 4);
        var second = Scene.Of(hurt);
        second.Resident(0);
        second.Commons[(int)VillageGood.Ration] = 2;
        second.Into(hurt);
        var walker = hurt.Economy.Residents[0];
        PassUntil(hurt, 300, () => walker.Work == VillageWork.DeliverRations);
        check(walker.Work == VillageWork.DeliverRations && walker.Held(VillageGood.Ration) == 2, "a-second-village-starts-its-own-errand");
        int before = hurt.Social.CampProvisions;
        walker.Health = VillageEconomyRules.WorkHealth - 100;
        Pass(hurt, 20);
        check(walker.Work == VillageWork.None && walker.Held(VillageGood.Ration) == 0 && hurt.Economy.Commons(VillageGood.Ration) == 2 && hurt.Social.CampProvisions == before &&
            hurt.Economy.HandedOut(VillageGood.Ration) == 0 && hurt.Economy.Audit().Count == 0, "an-abandoned-errand-returns-its-load");

        // A save that claims more than a load in one pair of hands, or a load without an errand, is refused.
        var t = Village(1);
        TakeFromCamp(t, 4);
        var third = Scene.Of(t);
        third.Resident(0);
        third.Commons[(int)VillageGood.Ration] = 4;
        third.Into(t);
        PassUntil(t, 300, () => t.Economy.Residents[0].Work == VillageWork.DeliverRations);
        string[] f = Text(t).Split(';');
        string[] row = f[13].Split(',');
        int heldAt = 13 + VillageEconomyRules.GoodCount + (int)VillageGood.Ration;
        check(row[heldAt] == "4" && row[7] == ((int)VillageWork.DeliverRations).ToString(CultureInfo.InvariantCulture), "the-errand-is-written-into-the-save");
        string[] commons = f[4].Split(',');
        row[heldAt] = "5"; commons[(int)VillageGood.Ration] = "0";
        string[] made = f[9].Split(',');
        var over = (string[])f.Clone(); over[13] = string.Join(",", row); over[4] = string.Join(",", commons);
        made[(int)VillageGood.Ration] = (long.Parse(made[(int)VillageGood.Ration], CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        over[9] = string.Join(",", made);
        check(!VillageEconomyState.TryRead(string.Join(";", over), out _), "more-than-a-load-in-one-pair-of-hands-is-refused");
        var idle = (string[])f.Clone(); string[] still = f[13].Split(',');
        still[7] = "0"; still[8] = "0";
        idle[13] = string.Join(",", still);
        check(!VillageEconomyState.TryRead(string.Join(";", idle), out _), "a-load-without-an-errand-is-refused");
    }

    /// <summary>Acceptance 5: one animal, its parts once; a meal lowers the right stock and the right person's hunger.</summary>
    private static void HuntAndMeal(Action<bool, string> check)
    {
        var p = Village(1);
        TakeFromCamp(p, 4);
        var scene = Scene.Of(p);
        scene.Sources[(int)VillageSource.Wildlife] = 1;
        var r = scene.Resident(0);
        r[4] = 1; r[5] = 1; r[Scene.Carry(VillageGood.Bow)] = 1;   // at the forest edge with a bow
        scene.Into(p);
        var e = p.Economy;
        int hunted = PassUntil(p, 2500, () => e.Made(VillageGood.Game) == 1);
        check(hunted >= 0 && e.Source(VillageSource.Wildlife) == 0, "the-last-animal-is-taken-once");
        // A hunt can end with an injury (one in twenty-four); then the meal waits until the hunter is back on the feet.
        int cooked = PassUntil(p, 4 * Day, () => e.HandedOut(VillageGood.Ration) == 2);
        check(cooked >= 0, "the-animal-becomes-a-meal-and-reaches-the-store");
        check(e.Made(VillageGood.Game) == 1 && e.Made(VillageGood.Ration) == 2 && e.Made(VillageGood.Hide) == 1 && e.Used(VillageGood.Game) == 1 &&
            e.Used(VillageGood.Water) == 1 && e.Used(VillageGood.Wood) >= 1, "one-animal-two-rations-one-hide-with-water-and-fire-wood");
        check(e.Source(VillageSource.Wildlife) == 0 && e.OpenNeeds(p).Any(n => n.Contains("Wild")), "an-emptied-hunting-ground-yields-nothing-more");
        check(e.HandedOut(VillageGood.Ration) == 2 && e.Commons(VillageGood.Ration) == 0 && e.Commons(VillageGood.Hide) == 1, "both-rations-reach-maras-store-and-the-hide-stays-in-the-commons");

        var hungry = Village(1);
        var meal = Scene.Of(hungry);
        meal.Resident(0)[1] = 800;
        meal.Into(hungry);
        int[] others = { hungry.Social.GetMemory(0).Hunger, hungry.Social.GetMemory(1).Hunger, hungry.Social.GetMemory(2).Hunger };
        Pass(hungry, 10);
        var eater = hungry.Economy.Residents[0];
        check(hungry.Social.CampProvisions == 7 && eater.Hunger == 200 && eater.Activity == VillageActivity.Eating, "one-meal-one-camp-ration-less-and-six-hundred-hunger-less");
        check(hungry.Social.GetMemory(0).Hunger == others[0] && hungry.Social.GetMemory(1).Hunger == others[1] && hungry.Social.GetMemory(2).Hunger == others[2], "nobody-else-is-fed-by-that-meal");
        var empty = Village(1);
        TakeFromCamp(empty, 8);
        var starve = Scene.Of(empty);
        starve.Resident(0)[1] = 1000; starve.Sources[(int)VillageSource.Wildlife] = 0;
        starve.Into(empty);
        Pass(empty, 5 * Day);
        check(empty.Economy.Residents[0].Health == VillageEconomyRules.StarvingHealthFloor && empty.Social.CampProvisions == 0, "hunger-wears-down-but-does-not-kill-in-this-revision");
    }

    /// <summary>Acceptance 6: a hand-over is known to those who were part of it; a report stays a report.</summary>
    private static void OnlyThoseInvolvedKnow(Action<bool, string> check)
    {
        var p = Village(1);
        TakeFromCamp(p, 3);
        var scene = Scene.Of(p);
        scene.Commons[(int)VillageGood.Ration] = 2;
        scene.Into(p);
        var forge = p.Social.Forge;
        var world = new ArenaSimulation(89, p, 1, LootCatalog.Version1);
        var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        int events = forge.Archive.Events.Count;
        // Make the local encounter at the instant of the handover, after the carrier's real journey.
        for (int i = 0; i < 200 && !p.Economy.Residents.Any(r => r.Work == VillageWork.DeliverRations && r.WorkTicks == VillageEconomyRules.Stride); i++) world.Step();
        mara.Xmm = VillageEconomyLayout.StandXmm(VillageSpot.Store);
        mara.Zmm = VillageEconomyLayout.StandZmm(VillageSpot.Store);
        for (int i = 0; i <= VillageEconomyRules.Stride && !forge.Archive.Events.Any(ev => ev.Context == VillageEconomyState.DeliveryContext); i++) world.Step();
        var deliveries = forge.Archive.Events.Where(ev => ev.Context == VillageEconomyState.DeliveryContext).ToArray();
        check(deliveries.Length == 1 && forge.Archive.Events.Count == events + 1 && deliveries[0].Actor == "resident.village.3" && deliveries[0].Target == "mara", "the-first-hand-over-of-the-day-is-one-event");
        ulong id = deliveries[0].Id;
        check(forge.Archive.Get("mara").Knowledge(id)?.Origin == KnowledgeOrigin.Self && forge.Archive.Get("resident.village.3").Knowledge(id)?.Origin == KnowledgeOrigin.Self, "giver-and-receiver-know-it-first-hand");
        check(forge.Archive.Get("dorn").Knowledge(id) == null && forge.Archive.Get("ilyan").Knowledge(id) == null, "those-who-were-not-there-do-not-know");
        check(forge.Archive.Get("mara").RelationshipTo("resident.village.3").Gratitude > 0, "mara-is-grateful-to-the-one-who-delivered");
        check(forge.Archive.Report("mara", "dorn", id, forge.ProcessedTicks) && forge.Archive.Get("dorn").Knowledge(id).Origin == KnowledgeOrigin.Reported &&
            forge.Archive.Get("dorn").Knowledge(id).Source == "mara", "a-report-stays-a-report-with-its-source");
        var again = Scene.Of(p);
        again.Commons[(int)VillageGood.Ration] = 2;
        TakeFromCamp(p, 2);
        again.Into(p);
        int second = PassUntil(p, 13000, () => p.Economy.HandedOut(VillageGood.Ration) >= 4);   // the resident may be out felling first
        check(second >= 0 && forge.ProcessedTicks < WorldSkyClock.TicksPerDay && forge.Archive.Events.Count(ev => ev.Context == VillageEconomyState.DeliveryContext) == 1,
            "further-hand-overs-the-same-day-add-no-new-memory");
        Pass(p, Day - (int)forge.ProcessedTicks);
        var morning = Scene.Of(p);
        morning.Commons[(int)VillageGood.Ration] += 1;
        TakeFromCamp(p, 1);
        morning.Into(p);
        int third = PassUntil(p, Day, () => forge.Archive.Events.Count(ev => ev.Context == VillageEconomyState.DeliveryContext) == 2);
        check(third >= 0 && forge.Archive.Events.Count(ev => ev.Context == VillageEconomyState.DeliveryContext) == 2, "the-next-day-has-its-own-memory-of-a-hand-over");
    }

    /// <summary>Acceptance 7: an injured resident does no village work; a healthy one takes over or the work waits.</summary>
    private static void InjuredResidentsDoNotWork(Action<bool, string> check)
    {
        var p = Village(1);
        TakeFromCamp(p, 3);
        var scene = Scene.Of(p);
        var hurt = scene.Resident(0);
        hurt[3] = 500; hurt[7] = (long)VillageWork.MakeFrame; hurt[8] = 200; hurt[Scene.Held(VillageGood.Wood)] = 2;
        scene.Commons[(int)VillageGood.Wood] = 2;
        scene.Into(p);
        var e = p.Economy;
        Pass(p, 10);
        check(e.Residents[0].Work == VillageWork.None && e.Commons(VillageGood.Wood) == 4 && e.Residents[0].Held(VillageGood.Wood) == 0, "the-injured-resident-lays-the-work-down-and-the-wood-goes-back");
        Pass(p, 2000);
        check(e.Made(VillageGood.WoodFrame) == 0 && e.Made(VillageGood.Wood) == 0 && e.Residents[0].Activity == VillageActivity.Recovering, "the-work-waits-while-the-only-resident-recovers");
        check(e.OpenNeeds(p).Any(n => n.Contains("verletzt")), "the-resting-village-is-a-visible-need");

        var two = Village(2);
        TakeFromCamp(two, 3);
        var pair = Scene.Of(two);
        pair.Resident(0)[3] = 500; pair.Resident(1);
        pair.Into(two);
        Pass(two, 1500);
        check(two.Economy.Made(VillageGood.WoodFrame) == 1 && two.Economy.Residents[0].Work == VillageWork.None && two.Economy.Residents[0].Activity == VillageActivity.Recovering,
            "the-healthy-resident-takes-the-work-over");

        var healing = Village(1);
        var soon = Scene.Of(healing);
        soon.Resident(0)[3] = VillageEconomyRules.WorkHealth - 2;
        soon.Into(healing);
        int back = PassUntil(healing, 1000, () => healing.Economy.Residents[0].CanWork);
        check(back > 0 && back <= 400, "rest-heals-slowly-until-the-resident-can-work-again");
        // Allowed to work is not yet eager to work: the resident's own controller rests a hurt body a while longer.
        check(PassUntil(healing, 2 * Day, () => healing.Economy.Made(VillageGood.Wood) > 0) >= 0 && healing.Economy.Residents[0].Health > VillageEconomyRules.WorkHealth, "the-recovered-resident-works-again");
    }

    private static void NightAndNeeds(Action<bool, string> check)
    {
        var p = Village(1);
        Pass(p, (int)VillageEconomyRules.NightStart + 1500);
        var r = p.Economy.Residents[0];
        check(r.Site == VillageSite.Village && !r.Walking && r.Activity == VillageActivity.Sleeping && r.Work == VillageWork.None, "at-night-the-resident-is-home-and-sleeps");
        long madeAtNight = p.Economy.Made(VillageGood.Wood) + p.Economy.Made(VillageGood.Seedling);
        Pass(p, 2000);
        check(p.Economy.Made(VillageGood.Wood) + p.Economy.Made(VillageGood.Seedling) == madeAtNight && r.Fatigue == 0, "no-work-in-the-dark-and-sleep-takes-the-fatigue");
        check(VillageEconomyRules.IsNight(VillageEconomyRules.NightStart) && !VillageEconomyRules.IsNight(VillageEconomyRules.NightEnd) && !VillageEconomyRules.IsNight(0), "night-is-the-dark-third-of-the-day");
        // Needs come first: a hungry resident eats before working although work is waiting.
        var hungry = Village(1);
        TakeFromCamp(hungry, 3);
        var scene = Scene.Of(hungry);
        scene.Resident(0)[1] = 950;
        scene.Into(hungry);
        Pass(hungry, 10);
        check(hungry.Economy.Residents[0].Activity == VillageActivity.Eating && hungry.Economy.Residents[0].Work == VillageWork.None, "a-hungry-resident-eats-before-working");
    }

    /// <summary>A village whose mill quest is done: Orsel tends the sick at the old mill.</summary>
    private static CharacterProgression MillVillage(int residents)
    {
        var p = CharacterProgression.CreateEntryProfile();
        for (int i = 0; i < 3; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect);
        p.TravelHunger(4, false);
        p.DiscoverHungerClue();
        if (!p.Village.UnlockOrselTeaches(p)) throw new Exception("FAIL mill quest not complete");
        p.Social.EnableForge();
        for (int i = 0; i < residents; i++) p.Village.RecordMainQuestCompleted();
        return p;
    }

    /// <summary>Concept cycle D: herbs, care and recovery. Orsel's care needs herbs that somebody really brought.</summary>
    private static void OrselsCare(Action<bool, string> check)
    {
        // Before Orsel's quest is done nobody is sent to her: the hurt resident rests at home.
        var early = Village(1);
        var home = Scene.Of(early);
        home.Resident(0)[3] = 500;
        home.Into(early);
        Pass(early, 2000);
        check(early.Economy.Residents[0].Site == VillageSite.Village && !early.Economy.Residents[0].Walking && early.Economy.Made(VillageGood.Herb) == 0, "without-orsels-quest-the-hurt-rest-at-home");

        var p = MillVillage(1);
        var scene = Scene.Of(p);
        scene.Resident(0)[3] = 500;
        scene.MillHerbs = 3;
        scene.Into(p);
        var e = p.Economy; var forge = p.Social.Forge;
        int arrived = PassUntil(p, 1000, () => e.Residents[0].Site == VillageSite.Mill && !e.Residents[0].Walking);
        check(arrived >= VillageEconomyRules.MillTravelTicks - 20 && arrived <= VillageEconomyRules.MillTravelTicks + 40 && e.Residents[0].Health <= 501, "the-hurt-resident-walks-to-orsel-at-the-old-mill");
        long herbsUsed = e.Used(VillageGood.Herb);
        int health = e.Residents[0].Health;
        Pass(p, 1000);
        check(e.Used(VillageGood.Herb) == herbsUsed + 3 && e.MillHerbs == 0 && e.Residents[0].Health == health + 3 * 10 + 7 * 3, "three-herbs-mend-ten-each-then-plain-care-mends-three");
        var cares = forge.Archive.Events.Where(ev => ev.Context == VillageEconomyState.CareContext).ToArray();
        check(cares.Length == 1 && cares[0].Actor == "orsel" && cares[0].Target == "resident.village.3", "one-day-of-care-is-one-memory");
        check(forge.Archive.Get("orsel").Knowledge(cares[0].Id)?.Origin == KnowledgeOrigin.Self && forge.Archive.Get("resident.village.3").Knowledge(cares[0].Id)?.Origin == KnowledgeOrigin.Self &&
            forge.Archive.Get("mara").Knowledge(cares[0].Id) == null, "healer-and-patient-know-of-the-care-mara-does-not");
        check(forge.Archive.Get("resident.village.3").RelationshipTo("orsel").Gratitude > 0, "the-patient-is-grateful-to-orsel");
        check(e.ReportedMillHerbs < 0 && !e.OpenNeeds(p).Any(n => n.Contains("Heilkräuter")),
            "an-empty-mill-shelf-does-not-inform-the-distant-board");
        check(e.Residents[0].Activity == VillageActivity.Recovering && e.Residents[0].Work == VillageWork.None && e.Made(VillageGood.Wood) == 0, "a-patient-does-no-village-work");
        int whole = PassUntil(p, 10 * Day, () => e.Residents[0].Health == VillageEconomyRules.MaxHealth);
        check(whole >= 0, "the-patient-stays-until-whole");
        int back = PassUntil(p, Day, () => e.Residents[0].Site == VillageSite.Village && !e.Residents[0].Walking);
        check(back >= 0 && e.ReportedMillHerbs == 0 && e.OpenNeeds(p).Any(n => n.Contains("Heilkräuter")), "missing-herbs-are-a-visible-need");
        check(back >= 0 && PassUntil(p, 2 * Day, () => e.Made(VillageGood.Wood) > 0 || e.Made(VillageGood.Herb) > 0) >= 0, "healed-the-resident-goes-home-and-works-again");
        check(e.Audit().Count == 0, "ledger-balances-through-care");

        // A healthy village keeps Orsel's shelf stocked: herbs are gathered at the forest edge and carried to the mill.
        var well = MillVillage(1);
        Pass(well, 1);
        var we = well.Economy;
        int stocked = PassUntil(well, 2 * Day, () => we.MillHerbs >= VillageEconomyRules.HerbTarget);
        check(stocked >= 0 && we.Made(VillageGood.Herb) >= VillageEconomyRules.HerbTarget && we.MillHerbs <= VillageEconomyRules.MillHerbCap && we.Used(VillageGood.Herb) == 0, "herbs-are-gathered-and-carried-to-orsels-shelf");
        check(we.Made(VillageGood.Herb) == we.MillHerbs + we.Commons(VillageGood.Herb) + we.Residents[0].Carried(VillageGood.Herb) && we.Audit().Count == 0, "every-herb-on-the-shelf-was-gathered");
        long made = we.Made(VillageGood.Herb);
        Pass(well, 2 * Day);
        check(we.Made(VillageGood.Herb) == made && we.Source(VillageSource.Herbs) == VillageEconomyRules.HerbCap, "a-full-shelf-needs-no-more-herbs-and-the-patch-recovers");
        check(CharacterProgression.TryLoad(well.SaveString(), out var loaded) && loaded.Economy.Token() == we.Token() && loaded.Economy.MillHerbs == we.MillHerbs, "the-shelf-survives-a-save");
        string[] f = we.Token().Split(';');
        f[6] = f[6].Split(',')[0] + ",7";
        check(!VillageEconomyState.TryRead(string.Join(";", f), out _), "more-herbs-than-the-shelf-holds-are-rejected");

        var giver = MillVillage(1);
        Pass(giver, 10);
        var world = new ArenaSimulation(41, giver, 1, LootCatalog.Version1);
        giver.Crafting.AddMaterial(MaterialKind.Ashenflieder, 2);
        check(!world.IsVillage && !world.GiveToVillage(VillageGood.Herb, 1).Accepted, "herbs-too-are-handed-over-in-the-village-only");
    }

    /// <summary>Acceptance 8: same start and ticks, same village; a save in the middle changes nothing.</summary>
    private static void ReplayAndSaveLoad(Action<bool, string> check)
    {
        var a = Village(2); var b = Village(2);
        TakeFromCamp(a, 3); TakeFromCamp(b, 3);
        bool same = true;
        for (int i = 0; i < 30; i++)
        {
            Pass(a, 1000); Pass(b, 1000);
            same &= Text(a) == Text(b) && a.SaveString() == b.SaveString();
        }
        check(same && a.Economy.Made(VillageGood.Game) > 0, "two-independent-villages-stay-identical-for-two-game-days");
        var c = Village(2);
        TakeFromCamp(c, 3);
        Pass(c, 12345);
        check(CharacterProgression.TryLoad(c.SaveString(), out var d), "a-running-village-saves-and-loads");
        Pass(d, 30000 - 12345);
        check(Text(d) == Text(a) && d.SaveString() == a.SaveString(), "a-save-in-the-middle-of-work-changes-nothing");
        check(d.Economy.StableHash == a.Economy.StableHash && d.Economy.Audit().Count == 0, "economy-hash-and-ledger-agree-after-reload");
        var first = new ArenaSimulation(55, Village(1), 1, LootCatalog.Version1);
        var second = new ArenaSimulation(55, Village(1), 1, LootCatalog.Version1);
        bool hashes = true;
        for (int i = 0; i < 1500; i++) { first.Step(); second.Step(); hashes &= first.ComputeWorldHash() == second.ComputeWorldHash(); }
        check(hashes && first.Progression.Economy.Started && first.Progression.Economy.LastTick == 1500, "world-hash-is-reproducible-with-a-working-village");
        var plain = new ArenaSimulation(55, Village(0), 1, LootCatalog.Version1);
        for (int i = 0; i < 1500; i++) plain.Step();
        check(plain.ComputeWorldHash() != first.ComputeWorldHash() && !plain.Progression.Economy.Started, "the-economy-is-part-of-the-world-hash-once-it-runs");
    }

    private static void LedgerOverTenDays(Action<bool, string> check)
    {
        var p = Village(3);
        TakeFromCamp(p, 4);
        bool balanced = true, bounded = true;
        var e = p.Economy;
        for (int i = 0; i < 10 * Day / 100; i++)
        {
            Pass(p, 100);
            balanced &= e.Audit().Count == 0;
            bounded &= e.Source(VillageSource.Wildlife) <= VillageEconomyRules.WildlifeCap && e.Source(VillageSource.Berries) <= VillageEconomyRules.BerryCap &&
                e.Source(VillageSource.Well) <= VillageEconomyRules.WellCap && e.Source(VillageSource.MatureTrees) >= VillageEconomyRules.SeedTreeReserve &&
                p.Social.CampProvisions <= SocialLedger.CampCapacity && e.Residents.All(r => r.Load <= VillageEconomyRules.CarryCapacity && r.Hunger <= 1000 && r.Health >= 1);
        }
        check(balanced, "start-plus-made-plus-received-minus-used-minus-handed-out-is-present-for-ten-days");
        check(bounded, "sources-store-and-hands-stay-inside-their-limits-for-ten-days");
        check(e.Made(VillageGood.Ration) > 0 && e.HandedOut(VillageGood.Ration) > 0 && e.Made(VillageGood.Game) * 2 == e.Made(VillageGood.Ration) + 2 * (e.Commons(VillageGood.Game) + e.Residents.Sum(r => r.Carried(VillageGood.Game) + r.Held(VillageGood.Game))),
            "every-ration-comes-from-an-animal-that-was-hunted");
        check(e.Made(VillageGood.Wood) == 3 * (24 + (long)MaturedSoFar(p) - e.Source(VillageSource.MatureTrees)), "every-wood-comes-from-a-tree-that-stood");
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && Text(loaded) == Text(p), "ten-days-of-village-life-survive-a-save");
        check(p.SaveString().Length < 64 * 1024, "the-save-stays-small-after-ten-days");
    }

    private static int MaturedSoFar(CharacterProgression p)
    {
        // A seedling counts as used when it is planted; planted seedlings that are no longer saplings have become trees.
        return (int)p.Economy.Used(VillageGood.Seedling) - p.Economy.SaplingCount;
    }

    /// <summary>The open refill task for Mara's store stays the player's: residents leave the last place free.</summary>
    private static void PlayersRefillTaskStaysThePlayers(Action<bool, string> check)
    {
        var p = Village(1);
        var scene = Scene.Of(p);
        scene.Commons[(int)VillageGood.Ration] = 6;
        scene.Into(p);
        Pass(p, 2 * Day);
        check(p.Social.CampProvisions <= SocialLedger.CampCapacity - 1 && !p.Social.CampRefilled, "residents-never-complete-the-players-refill-task");
        var forge = p.Social.Forge;
        forge.Archive.Append(MemoryKind.Quest, "player", "mara", forge.ProcessedTicks, 6000, true, SocialLedger.CampRefillContext, new RelationshipChange(gratitude: 1000, trust: 500));
        var after = Scene.Of(p);
        after.Commons[(int)VillageGood.Ration] = 6;
        after.Into(p);
        int full = PassUntil(p, Day, () => p.Social.CampProvisions == SocialLedger.CampCapacity);
        check(p.Social.CampRefilled && full >= 0, "once-the-player-refilled-it-residents-fill-the-store-completely");
    }

    private static void PlayerHelps(Action<bool, string> check)
    {
        var p = Village(1);
        Pass(p, 10);
        var world = new ArenaSimulation(77, p, 1, LootCatalog.Version1);
        p.Crafting.AddMaterial(MaterialKind.Ascheisen, 1);
        string far = Text(p);
        check(!world.VillageBoardInReach && !world.GiveToVillage(VillageGood.Metal, 1).Accepted && p.Crafting.MaterialCount(MaterialKind.Ascheisen) == 1 && Text(p) == far,
            "away-from-the-board-nothing-is-handed-over");
        ToBoard(world);
        check(world.VillageBoardInReach, "the-player-reaches-the-village-board");
        string before = Text(p);
        check(!world.GiveToVillage(VillageGood.Wood, 2).Accepted && Text(p) == before, "an-empty-pack-gives-nothing");
        p.Crafting.AddMaterial(MaterialKind.Holz, 5); p.Crafting.AddMaterial(MaterialKind.Dornbeere, 1);
        check(!world.GiveToVillage(VillageGood.Wood, 6).Accepted && p.Crafting.MaterialCount(MaterialKind.Holz) == 5 && Text(p) == before, "more-than-the-pack-holds-is-refused-without-loss");
        check(!world.GiveToVillage(VillageGood.Ration, 1).Accepted && !world.GiveToVillage(VillageGood.Water, 1).Accepted && !world.GiveToVillage(VillageGood.Wood, 0).Accepted, "only-goods-the-player-carries-can-be-given");
        int wood = p.Economy.Commons(VillageGood.Wood);
        check(world.GiveToVillage(VillageGood.Wood, 4).Accepted && p.Crafting.MaterialCount(MaterialKind.Holz) == 1 && p.Economy.Commons(VillageGood.Wood) == wood + 4 &&
            p.Economy.Received(VillageGood.Wood) == 4, "the-pack-loses-exactly-what-the-commons-gains");
        check(p.Crafting.CraftWoodenFrame() == false && world.GiveToVillage(VillageGood.ThornBerry, 1).Accepted && p.Economy.Audit().Count == 0, "gifts-keep-the-ledger-balanced");
        int metal = p.Economy.Commons(VillageGood.Metal);
        check(world.GiveToVillage(VillageGood.Metal, 1).Accepted && p.Crafting.MaterialCount(MaterialKind.Ascheisen) == 0 && p.Economy.Commons(VillageGood.Metal) == metal + 1,
            "what-was-refused-far-away-is-accepted-at-the-board");
        var road = CharacterProgression.CreateEntryProfile(); road.EnterVillage(); road.DiscoverHungerClue(); road.TravelHunger(3, false); road.Social.EnableForge();
        road.Village.RecordMainQuestCompleted();
        var away = new ArenaSimulation(78, road, 1, LootCatalog.Version1);
        away.Step();
        road.Crafting.AddMaterial(MaterialKind.Holz, 2);
        check(road.Economy.Started && !away.IsVillage && !away.GiveToVillage(VillageGood.Wood, 1).Accepted && road.Crafting.MaterialCount(MaterialKind.Holz) == 2, "outside-the-village-nothing-can-be-handed-over");
    }

    /// <summary>The player walks in a straight line through real movement steps; the village works on meanwhile.</summary>
    private static void WalkTo(ArenaSimulation world, long x, long z)
    {
        for (int t = 0; t < 1200; t++)
        {
            long dx = x - world.PlayerXmm, dz = z - world.PlayerZmm;
            if (Math.Abs(dx) < 300 && Math.Abs(dz) < 300) { world.SetMoveInput(0, 0); return; }
            long reach = Math.Max(Math.Abs(dx), Math.Abs(dz));
            world.SetMoveInput(dx * 10000 / reach, dz * 10000 / reach);
            world.Step();
        }
        throw new Exception("FAIL the player did not reach " + x + "," + z + " but stands at " + world.PlayerXmm + "," + world.PlayerZmm);
    }

    private static void ToBoard(ArenaSimulation world)
    { WalkTo(world, VillageEconomyLayout.StandXmm(VillageSpot.Board), VillageEconomyLayout.StandZmm(VillageSpot.Board)); }

    private static void Places(Action<bool, string> check)
    {
        bool standable = true, inside = true, apart = true, near = true;
        for (int a = 0; a < VillageEconomyLayout.SpotCount; a++)
        {
            var spot = (VillageSpot)a;
            long sx = VillageEconomyLayout.StandXmm(spot), sz = VillageEconomyLayout.StandZmm(spot), px = VillageEconomyLayout.PlaceXmm(spot), pz = VillageEconomyLayout.PlaceZmm(spot);
            VillagePlace house = VillageEconomyLayout.HouseOf(spot);
            if (house != VillagePlace.None)
            {
                // A place of work inside a house: the worker stands free in the room, the workplace is a piece of its furniture,
                // and in the village the worker is seen at the door.
                standable &= VillagePlaceLayout.CanStandInRoom(house, sx, sz) && !VillagePlaceLayout.CanStandInRoom(house, px, pz);
                inside &= VillagePlaceLayout.RoomAt(sx, sz) == house && VillagePlaceLayout.RoomAt(px, pz) == house;
                sx = VillageEconomyLayout.VillageXmm(spot); sz = VillageEconomyLayout.VillageZmm(spot); px = sx; pz = sz;
            }
            standable &= EntryWorldLayout.CanStand(sx, sz);
            // A workplace may stand close to a wall, but never inside a building.
            for (int building = 0; building < EntryWorldLayout.BuildingCount; building++) standable &= !EntryWorldLayout.Building(building).Contains(px, pz, 0);
            inside &= Math.Abs(sx) <= 25000 && Math.Abs(sz) <= 25000 && Math.Abs(px) <= 25000 && Math.Abs(pz) <= 25000;
            sx = VillageEconomyLayout.StandXmm(spot); sz = VillageEconomyLayout.StandZmm(spot); px = VillageEconomyLayout.PlaceXmm(spot); pz = VillageEconomyLayout.PlaceZmm(spot);
            long reach = (sx - px) * (sx - px) + (sz - pz) * (sz - pz);
            near &= reach <= 2000L * 2000L;
            for (int b = a + 1; b < VillageEconomyLayout.SpotCount; b++)
            {
                var other = (VillageSpot)b;
                long ox = VillageEconomyLayout.PlaceXmm(other) - px, oz = VillageEconomyLayout.PlaceZmm(other) - pz;
                long tx = VillageEconomyLayout.StandXmm(other) - sx, tz = VillageEconomyLayout.StandZmm(other) - sz;
                apart &= ox * ox + oz * oz >= 1500L * 1500L && tx * tx + tz * tz >= 1000L * 1000L;
            }
        }
        check(standable, "every-place-and-stand-point-is-free-of-buildings");
        check(inside, "every-place-lies-inside-the-village");
        check(near, "a-resident-stands-at-the-workplace");
        check(apart, "places-and-stand-points-do-not-overlap");
        check(VillageEconomyLayout.PlaceXmm(VillageSpot.Store) == ArenaSimulation.CampXmm && VillageEconomyLayout.PlaceZmm(VillageSpot.Store) == ArenaSimulation.CampZmm,
            "the-store-is-maras-existing-camp");
        bool threw = false;
        try { VillageEconomyLayout.StandXmm((VillageSpot)VillageEconomyLayout.SpotCount); } catch (ArgumentOutOfRangeException) { threw = true; }
        check(threw, "an-unknown-place-is-refused");

        // A real stretch of village life: where the Core says somebody is, is where that person is seen.
        var p = Village(2);
        TakeFromCamp(p, 4);
        var seenAt = new HashSet<VillageSpot>();
        bool rules = true, hiddenAway = false, hiddenAsleep = false, leaves = false, returns = false;
        for (int i = 0; i < 3 * (int)PersonalCognition.TicksPerDay; i += 10)
        {
            Pass(p, 10);
            foreach (var r in p.Economy.Residents)
            {
                bool seen = VillageEconomyLayout.Seen(r, out var spot);
                if (seen) seenAt.Add(spot);
                if (r.Walking)
                {
                    int whole = VillageEconomyRules.TravelTicks(r.Site, r.Destination);
                    bool expected = r.Site == VillageSite.Village ? r.TravelTicks > whole - VillageEconomyLayout.GateWalkTicks
                        : r.Destination == VillageSite.Village && r.TravelTicks <= VillageEconomyLayout.GateWalkTicks;
                    rules &= seen == expected && (!seen || spot == (r.Site == VillageSite.Village ? VillageSpot.Gate : VillageSpot.Woodyard));
                    leaves |= seen && r.Site == VillageSite.Village; returns |= seen && r.Site != VillageSite.Village;
                }
                else if (r.Site != VillageSite.Village) { rules &= !seen; hiddenAway = true; }
                else if (r.Activity == VillageActivity.Sleeping) { rules &= !seen; hiddenAsleep = true; }
                else
                {
                    rules &= seen;
                    if (r.Activity == VillageActivity.Working)
                        rules &= r.Work != VillageWork.None && spot == VillageEconomyLayout.SpotFor(r.Work) && VillageEconomyRules.Rule(r.Work).Site == VillageSite.Village;
                    if (r.Activity == VillageActivity.Eating) rules &= spot == VillageSpot.Store;
                }
            }
        }
        check(rules, "a-resident-is-seen-exactly-where-the-core-has-them");
        check(hiddenAway && hiddenAsleep && leaves && returns, "away-asleep-leaving-and-returning-all-occurred");
        foreach (var spot in new[] { VillageSpot.Store, VillageSpot.Kitchen, VillageSpot.Woodyard, VillageSpot.Workbench, VillageSpot.Well, VillageSpot.Home, VillageSpot.Gate })
            check(seenAt.Contains(spot), "village-life-reaches-the-" + spot.ToString().ToLowerInvariant());
    }

    private static void TheBoard(Action<bool, string> check)
    {
        var empty = Village(0);
        Pass(empty, 20);
        check(VillageBoard.StockLines(empty).Any(l => l.Contains("8/10")), "first-visit-board-reads-the-existing-camp");
        check(VillageBoard.LocalNeeds(empty).Any(l => l.Contains("Arbeitskräfte")) && !empty.Economy.Started,
            "first-visit-board-shows-the-unfilled-job-without-creating-stock-or-workers");

        var p = Village(1);
        Pass(p, 10);
        var e = p.Economy;
        var lines = VillageBoard.StockLines(p);
        check(lines.Count >= 3 && lines[0] == "Werkstatt: Holz 4 · Holzrahmen 0 · Bogen 0 · Axt 2 · Metall 2 · Dornbeeren 0" &&
            lines[1] == "Küche: Wild 0 · Wasser 2 · Rationen 0 · Häute 0 · Maras Vorrat 8/10", "the-board-counts-the-villages-own-stores");
        check(lines[2].Contains("Noch kein Bericht"), "the-land-is-unknown-until-someone-returns");
        foreach (VillageGood good in Enum.GetValues(typeof(VillageGood)))
            if (good != VillageGood.Seedling && good != VillageGood.BrokenAxe)
                check(lines.Any(l => l.Contains(VillageEconomyRules.GoodName(good) + " " + e.Commons(good))),
                    "the-village-register-accounts-for-" + good);
        string before = Text(p);
        for (int i = 0; i < 5; i++) { VillageBoard.StockLines(p); foreach (var r in e.Residents) VillageBoard.ResidentNotice(r); VillageBoard.LocalNeeds(p); }
        check(Text(p) == before, "reading-the-board-changes-nothing");

        // A broken axe in the workshop is visible. A change at the distant forest is not a report.
        var scene = Scene.Of(p);
        scene.Sources[(int)VillageSource.MatureTrees] = VillageEconomyRules.SeedTreeReserve; scene.Sources[(int)VillageSource.Wildlife] = 0;
        scene.Sources[(int)VillageSource.Berries] = 0; scene.Commons[(int)VillageGood.BrokenAxe] = 1;
        scene.Into(p);
        lines = VillageBoard.StockLines(p);
        check(lines[0].Contains("defekte Axt 1") && lines[2].Contains("Noch kein Bericht") &&
            !VillageBoard.LocalNeeds(p).Any(n => n.Contains("Wild ist erschöpft")), "only-local-shortages-reach-the-board-before-a-return");
        var mill = MillVillage(1);
        Pass(mill, 10);
        check(VillageBoard.StockLines(mill)[2].Contains("Noch kein Bericht"), "knowing-orsel-does-not-reveal-unvisited-herbs");
        var reporter = Village(1);
        int home = PassUntil(reporter, Day, () => reporter.Economy.ForestReporterOrdinal > 0);
        check(home >= 0, "a-real-returning-worker-reaches-the-board");
        string forestReport = VillageBoard.StockLines(reporter)[2];
        check(forestReport.Contains("Rückkehrbericht von Bewohner 1") && forestReport.Contains("beobachtet an Tag ") &&
            forestReport.Contains("Nutzholz") && forestReport.Contains("Wild"), "the-returned-report-names-its-source-and-time");
        var changedLand = Scene.Of(reporter);
        changedLand.Sources[(int)VillageSource.MatureTrees] = VillageEconomyRules.SeedTreeReserve;
        changedLand.Sources[(int)VillageSource.Wildlife] = 0;
        changedLand.Into(reporter);
        check(VillageBoard.StockLines(reporter)[2] == forestReport, "a-remote-change-cannot-rewrite-the-last-returned-report");

        // One line per resident, taken from the same state the rules use.
        var live = Village(2);
        TakeFromCamp(live, 4);
        var said = new HashSet<string>();
        bool numbered = true;
        for (int i = 0; i < 2 * (int)PersonalCognition.TicksPerDay; i += 10)
        {
            Pass(live, 10);
            foreach (var r in live.Economy.Residents)
            {
                string line = VillageBoard.ResidentLine(r);
                numbered &= line.StartsWith("Bewohner " + (r.Ordinal - VillageServicesState.BasePopulation + 1) + " ") && line.EndsWith(".");
                if (r.Walking) numbered &= line.Contains(r.Destination == VillageSite.Village ? "Rückweg ins Dorf" :
                    r.Destination == VillageSite.Mill ? "auf dem Weg zur Mühle" : "auf dem Weg in den Wald");
                else if (r.Activity == VillageActivity.Working) numbered &= line.Contains(VillageEconomyRules.WorkName(r.Work)) && line.Contains("(im Wald)") == (r.Site == VillageSite.Forest);
                else if (r.Activity == VillageActivity.Sleeping) numbered &= line.Contains("schläft");
                if (r.Hunger >= VillageEconomyRules.EatHunger && r.CanWork) numbered &= line.Contains("hungrig");
                if (!r.CanWork) numbered &= line.Contains("verletzt");
                if (r.Walking || r.Site != VillageSite.Village)
                    numbered &= VillageBoard.ResidentNotice(r).EndsWith("ist unterwegs.");
                said.Add(line.Substring(line.IndexOf(' ', 9) + 1));
            }
        }
        check(numbered, "every-resident-line-matches-the-residents-state");
        check(said.Count >= 8 && said.Any(l => l.StartsWith("fällt einen Baum (im Wald)")) && said.Any(l => l.StartsWith("schläft")) && said.Any(l => l.StartsWith("bereitet Rationen zu")),
            "two-days-of-village-life-read-as-many-different-lines");

        // The pack: what the board offers is exactly what the hand-over accepts.
        var giver = Village(1);
        Pass(giver, 10);
        var world = new ArenaSimulation(88, giver, 1, LootCatalog.Version1);
        ToBoard(world);
        bool offers = VillageBoard.GiftGoods.Count == 6;
        for (int g = 0; g < VillageEconomyRules.GoodCount; g++)
        {
            var good = (VillageGood)g;
            offers &= giver.Crafting.VillageGiftCount(good) == 0 && !world.GiveToVillage(good, 1).Accepted;
        }
        giver.Crafting.AddMaterial(MaterialKind.Holz, 4); giver.Crafting.AddMaterial(MaterialKind.Dornbeere, 2); giver.Crafting.AddMaterial(MaterialKind.Ascheisen, 1);
        giver.Crafting.AddMaterial(MaterialKind.Ashenflieder, 3);
        check(giver.Crafting.CraftWoodenFrame() && giver.Crafting.CraftWoodenFrame() && giver.Crafting.CraftBow(), "the-player-builds-a-frame-and-a-bow");
        int[] expected = { 0, 1, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 3 };
        for (int g = 0; g < VillageEconomyRules.GoodCount; g++)
        {
            var good = (VillageGood)g;
            int has = giver.Crafting.VillageGiftCount(good);
            offers &= has == (g < expected.Length ? expected[g] : 0) && VillageBoard.GiftGoods.Contains(good) == (has > 0 || good == VillageGood.Wood);
            if (has > 0)
            {
                int commons = giver.Economy.Commons(good);
                offers &= !world.GiveToVillage(good, has + 1).Accepted && world.GiveToVillage(good, has).Accepted && giver.Crafting.VillageGiftCount(good) == 0 &&
                    giver.Economy.Commons(good) == commons + has;
            }
        }
        check(offers && giver.Economy.Audit().Count == 0, "the-board-offers-exactly-what-the-pack-can-give");
    }

    private static void TamperedSavesAreRejected(Action<bool, string> check)
    {
        var p = Village(1);
        TakeFromCamp(p, 3);
        Pass(p, 2500);
        string token = Text(p);
        check(VillageEconomyState.TryRead(token, out var same) && same.Token() == token, "the-canonical-text-reads-back-identically");
        string[] f = token.Split(';');
        string[] commons = f[4].Split(',');
        commons[(int)VillageGood.Ration] = "7";
        string richer = string.Join(";", f.Take(4).Concat(new[] { string.Join(",", commons) }).Concat(f.Skip(5)));
        check(!VillageEconomyState.TryRead(richer, out _), "rations-written-into-the-save-are-rejected-by-the-ledger");
        check(!VillageEconomyState.TryRead(token.Replace(";", ";0", StringComparison.Ordinal), out _) && !VillageEconomyState.TryRead(token + ";", out _) &&
            !VillageEconomyState.TryRead("", out _) && !VillageEconomyState.TryRead("2" + token.Substring(1), out _), "non-canonical-or-unknown-versions-are-rejected");
        string[] sources = f[5].Split(',');
        sources[(int)VillageSource.Wildlife] = "99";
        check(!VillageEconomyState.TryRead(string.Join(";", f.Take(5).Concat(new[] { string.Join(",", sources) }).Concat(f.Skip(6))), out _), "a-herd-above-its-limit-is-rejected");
    }

    /// <summary>A whole character save as an editor of save files would write it: the same container, a valid checksum, another economy section.</summary>
    private static string Forged(CharacterProgression source, string economy)
    {
        if (!CharacterProgression.TryLoad(source.SaveString(), out var copy)) throw new Exception("FAIL the genuine save does not load");
        const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        var sections = (SortedDictionary<string, string>)typeof(CharacterProgression).GetField("_sections", hidden).GetValue(copy);
        if (economy == null) sections.Remove("economy"); else sections["economy"] = economy;
        string legacy = (string)typeof(CharacterProgression).GetMethod("WeaponMasteryPayload", hidden).Invoke(copy, null) + "|" + copy.StableHash.ToString("X16", CultureInfo.InvariantCulture);
        return (string)typeof(CharacterProgression).GetMethod("SectionContainer", hidden).Invoke(copy, new object[] { legacy });
    }

    /// <summary>
    /// The trust boundary is the stored save text. A save whose economy section does not hold a balanced, canonical
    /// ledger is refused as a whole - the game then keeps its backup - and a save from before the economy starts
    /// the economy now, without any made-up past.
    /// </summary>
    private static void WholeSaves(Action<bool, string> check)
    {
        var p = Village(1);
        TakeFromCamp(p, 3);
        Pass(p, 2500);
        string token = Text(p);
        check(CharacterProgression.TryLoad(Forged(p, token), out var genuine) && Text(genuine) == token, "a-rewritten-save-with-the-true-section-loads");
        string[] f = token.Split(';');
        string[] commons = f[4].Split(',');
        commons[(int)VillageGood.Ration] = "9";
        string richer = string.Join(";", f.Take(4).Concat(new[] { string.Join(",", commons) }).Concat(f.Skip(5)));
        check(!CharacterProgression.TryLoad(Forged(p, richer), out _), "a-save-with-rations-written-in-is-refused-as-a-whole");
        foreach (string broken in new[] { "", "x", token + " ", token.Substring(0, token.Length / 2), "1;1;0;0", token.Replace(',', '.') })
            check(!CharacterProgression.TryLoad(Forged(p, broken), out _), "a-save-with-a-broken-economy-section-is-refused-" + broken.Length);

        // Before the economy existed: a village with a resident and no economy section. It loads, and work begins now.
        string before = Forged(p, null);
        check(CharacterProgression.TryLoad(before, out var old) && !old.Economy.Started && old.Village.Population == VillageServicesState.BasePopulation + 1,
            "a-save-from-before-the-economy-loads-with-its-resident");
        int events = old.Social.Forge.Archive.Events.Count, camp = old.Social.CampProvisions;
        ulong loadedAt = old.Social.Forge.ProcessedTicks;
        Pass(old, 10);
        var e = old.Economy;
        check(e.Started && e.StartedTick > loadedAt && e.Residents.Count == 1, "its-economy-starts-at-the-first-tick-after-loading");
        bool plain = old.Social.Forge.Archive.Events.Count == events && old.Social.CampProvisions == camp;
        for (int g = 0; g < VillageEconomyRules.GoodCount; g++)
        {
            var good = (VillageGood)g;
            plain &= e.StartStock(good) == VillageEconomyRules.StartStock(good) && e.Made(good) == 0 && e.Used(good) == 0 && e.Received(good) == 0 && e.HandedOut(good) == 0;
        }
        check(plain && e.Audit().Count == 0, "no-past-work-deliveries-or-memories-are-made-up-for-an-old-save");
    }

    /// <summary>The whole simulation step in the village: villagers, player and economy together.</summary>
    private static void LiveVillage(Action<bool, string> check)
    {
        if (Environment.GetEnvironmentVariable("ARE_ECONOMY_REPORT") == "1")
            foreach (int count in new[] { 1, 2, 3 })
            {
                var rp = MillVillage(count); rp.EnterVillage(); var rw = new ArenaSimulation(7, rp, 1, LootCatalog.Version1); var re = rp.Economy;
                var share = new int[Enum.GetValues(typeof(VillageActivity)).Length];
                for (int d = 0; d < 12; d++)
                {
                    Array.Clear(share, 0, share.Length);
                    for (int i = 0; i < Day; i++) { rw.Step(); if (i % 10 == 0 && re.Residents.Count > 0) share[(int)re.Residents[0].Activity]++; }
                    Console.WriteLine("REPORT residents=" + count + " day=" + (d + 1) + " camp=" + rp.Social.CampProvisions + " rations=" + re.Made(VillageGood.Ration) + " handed=" + re.HandedOut(VillageGood.Ration) +
                        " game=" + re.Made(VillageGood.Game) + " wild=" + re.Source(VillageSource.Wildlife) + " trees=" + re.Source(VillageSource.MatureTrees) + "+" + re.SaplingCount + " wood=" + re.Commons(VillageGood.Wood) +
                        " bows=" + re.Made(VillageGood.Bow) + " axes=" + re.Commons(VillageGood.Axe) + "/" + re.Commons(VillageGood.BrokenAxe) + " metal=" + re.Commons(VillageGood.Metal) + " hides=" + re.Commons(VillageGood.Hide) +
                        " r0[hp=" + re.Residents[0].Health + " hun=" + re.Residents[0].Hunger + "] idle/work/walk/rest/sleep/eat/recover/drink=" + string.Join("/", share) +
                        " npcHunger=" + rp.Social.GetMemory(0).Hunger + "," + rp.Social.GetMemory(1).Hunger + "," + rp.Social.GetMemory(2).Hunger + " herbs=" + re.MillHerbs + "/" + re.Source(VillageSource.Herbs) + " site=" + re.Residents[0].Site + " needs=" + string.Join("|", re.OpenNeeds(rp)));
                }
                // What the save is made of once the village works: the ledger itself is small, the minds are the bulk.
                string whole = rp.SaveString();
                var parts = (SortedDictionary<string, string>)typeof(CharacterProgression).GetField("_sections", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(rp);
                Console.WriteLine("REPORT-SAVE residents=" + count + " chars=" + whole.Length + " " + string.Join(" ", parts.Select(part => part.Key + "=" + part.Value.Length)) +
                    " events=" + rp.Social.Forge.Archive.Events.Count + " known=" + rp.Social.Forge.Archive.Acquisitions.Count + " minds=" + rp.Social.Forge.Archive.Minds.Count);
            }
        if (Environment.GetEnvironmentVariable("ARE_ECONOMY_REPORT") == "1")
            foreach (int count in new[] { 0, 1, 3, 8 })
            {
                // Memory handed out and time per game tick, over one game day of the real simulation step.
                var ap = Village(count); var aw = new ArenaSimulation(7, ap, 1, LootCatalog.Version1);
                for (int i = 0; i < 2000; i++) aw.Step();
                long bytes = GC.GetAllocatedBytesForCurrentThread(); var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < Day; i++) aw.Step();
                watch.Stop(); bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
                Console.WriteLine("REPORT-COST residents=" + count + " bytes_per_tick=" + bytes / Day + " us_per_tick=" + (watch.Elapsed.TotalMilliseconds * 1000 / Day).ToString("F2", CultureInfo.InvariantCulture) +
                    " save_chars=" + ap.SaveString().Length);
            }
        var p = Village(1);
        var world = new ArenaSimulation(99, p, 1, LootCatalog.Version1);
        for (int i = 0; i < 6000; i++) world.Step();
        var e = p.Economy;
        check(e.Started && e.LastTick == 6000 && e.Residents.Count == 1 && e.Audit().Count == 0, "the-economy-runs-inside-the-real-simulation-step");
        check(p.Social.Forge.Archive.Get("resident.village.3") != null && p.Social.Forge.Archive.Get("resident.village.3").Controller.ArchetypeId == "villager", "the-resident-has-one-personal-mind");
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && loaded.Economy.Token() == e.Token() && loaded.Social.Forge.Archive.Get("resident.village.3") != null,
            "village-and-minds-survive-the-character-save");
        world.CompleteMainQuest();
        for (int i = 0; i < 20; i++) world.Step();
        check(e.Residents.Count == 2 && e.Residents[1].Ordinal == 4 && e.Audit().Count == 0, "a-second-resident-joins-the-work-after-the-next-main-quest");
    }
}
