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
        SmallWoodChain(check);
        MissingMaterialStopsTheBow(check);
        Reforestation(check);
        BrokenAxeWaitsForMetal(check);
        StoreLimitAndRepeatedCalls(check);
        HuntAndMeal(check);
        OnlyThoseInvolvedKnow(check);
        InjuredResidentsDoNotWork(check);
        NightAndNeeds(check);
        ReplayAndSaveLoad(check);
        LedgerOverTenDays(check);
        PlayersRefillTaskStaysThePlayers(check);
        PlayerHelps(check);
        TamperedSavesAreRejected(check);
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
        public long Nursery;
        public List<long[]> Residents = new List<long[]>();

        public static Scene Of(CharacterProgression p)
        {
            if (!p.Economy.Started) Pass(p, 1);
            var s = new Scene { Fields = Text(p).Split(';') };
            s.Commons = Row(s.Fields[4]); s.Sources = Row(s.Fields[5]); s.Nursery = long.Parse(s.Fields[6], CultureInfo.InvariantCulture);
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
                var r = new long[12 + 2 * VillageEconomyRules.GoodCount];
                r[0] = VillageServicesState.BasePopulation + Residents.Count; r[1] = 200; r[2] = 100; r[3] = VillageEconomyRules.MaxHealth;
                Residents.Add(r);
            }
            return Residents[index];
        }

        public static int Carry(VillageGood good) { return 12 + (int)good; }
        public static int Held(VillageGood good) { return 12 + VillageEconomyRules.GoodCount + (int)good; }

        /// <summary>Write the scene into the profile. The ledger is balanced by what was made or used before.</summary>
        public void Into(CharacterProgression p)
        {
            for (int g = 0; g < VillageEconomyRules.GoodCount; g++)
            {
                long present = Commons[g] + (g == (int)VillageGood.Seedling ? Nursery : 0);
                foreach (var r in Residents) present += r[12 + g] + r[12 + VillageEconomyRules.GoodCount + g];
                long gap = Start[g] + Made[g] + Received[g] - HandedOut[g] - Used[g] - present;
                if (gap >= 0) Used[g] += gap; else Made[g] -= gap;
            }
            Fields[4] = Row(Commons); Fields[5] = Row(Sources); Fields[6] = Nursery.ToString(CultureInfo.InvariantCulture);
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
        int until = PassUntil(p, 4500, () => e.SaplingCount == 2);
        check(until > 0 && e.Source(VillageSource.MatureTrees) == 22 && e.NurserySeedlings == 2 && e.Used(VillageGood.Seedling) == 2, "two-trees-felled-two-seedlings-planted-from-the-nursery");
        ulong due = e.NextSaplingTick;
        check(due > p.Social.Forge.ProcessedTicks + 2UL * (ulong)Day && due <= p.Social.Forge.ProcessedTicks + VillageEconomyRules.SaplingGrowthTicks, "a-seedling-needs-three-game-days");
        // A quiet village for the waiting time: no game to hunt, so no wood is burnt and no further tree is felled.
        var quiet = Scene.Of(p);
        quiet.Sources[(int)VillageSource.Wildlife] = 0;
        quiet.Into(p);
        e = p.Economy;
        Pass(p, (int)(due - p.Social.Forge.ProcessedTicks) - 10);
        check(e.Source(VillageSource.MatureTrees) == 22 && e.SaplingCount == 2, "no-tree-before-its-time");
        Pass(p, 10);
        check(e.Source(VillageSource.MatureTrees) == 23 && e.SaplingCount == 1, "the-planted-seedling-is-a-tree-exactly-when-due");
        Pass(p, 400);
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
        check(bare.Economy.OpenNeeds(bare).Any(n => n.Contains("Samenbäume")), "the-exhausted-forest-is-a-visible-need");
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
        int events = forge.Archive.Events.Count;
        Pass(p, 200);
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
        string before = Text(p);
        check(!world.GiveToVillage(VillageGood.Wood, 2).Accepted && Text(p) == before, "an-empty-pack-gives-nothing");
        p.Crafting.AddMaterial(MaterialKind.Holz, 5); p.Crafting.AddMaterial(MaterialKind.Dornbeere, 1);
        check(!world.GiveToVillage(VillageGood.Wood, 6).Accepted && p.Crafting.MaterialCount(MaterialKind.Holz) == 5 && Text(p) == before, "more-than-the-pack-holds-is-refused-without-loss");
        check(!world.GiveToVillage(VillageGood.Ration, 1).Accepted && !world.GiveToVillage(VillageGood.Water, 1).Accepted && !world.GiveToVillage(VillageGood.Wood, 0).Accepted, "only-goods-the-player-carries-can-be-given");
        int wood = p.Economy.Commons(VillageGood.Wood);
        check(world.GiveToVillage(VillageGood.Wood, 4).Accepted && p.Crafting.MaterialCount(MaterialKind.Holz) == 1 && p.Economy.Commons(VillageGood.Wood) == wood + 4 &&
            p.Economy.Received(VillageGood.Wood) == 4, "the-pack-loses-exactly-what-the-commons-gains");
        check(p.Crafting.CraftWoodenFrame() == false && world.GiveToVillage(VillageGood.ThornBerry, 1).Accepted && p.Economy.Audit().Count == 0, "gifts-keep-the-ledger-balanced");
        var road = CharacterProgression.CreateEntryProfile(); road.EnterVillage(); road.DiscoverHungerClue(); road.TravelHunger(3, false); road.Social.EnableForge();
        road.Village.RecordMainQuestCompleted();
        var away = new ArenaSimulation(78, road, 1, LootCatalog.Version1);
        away.Step();
        road.Crafting.AddMaterial(MaterialKind.Holz, 2);
        check(road.Economy.Started && !away.IsVillage && !away.GiveToVillage(VillageGood.Wood, 1).Accepted && road.Crafting.MaterialCount(MaterialKind.Holz) == 2, "outside-the-village-nothing-can-be-handed-over");
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

    /// <summary>The whole simulation step in the village: villagers, player and economy together.</summary>
    private static void LiveVillage(Action<bool, string> check)
    {
        if (Environment.GetEnvironmentVariable("ARE_ECONOMY_REPORT") == "1")
            foreach (int count in new[] { 1, 2, 3 })
            {
                var rp = Village(count); var rw = new ArenaSimulation(7, rp, 1, LootCatalog.Version1); var re = rp.Economy;
                var share = new int[7];
                for (int d = 0; d < 12; d++)
                {
                    Array.Clear(share, 0, share.Length);
                    for (int i = 0; i < Day; i++) { rw.Step(); if (i % 10 == 0 && re.Residents.Count > 0) share[(int)re.Residents[0].Activity]++; }
                    Console.WriteLine("REPORT residents=" + count + " day=" + (d + 1) + " camp=" + rp.Social.CampProvisions + " rations=" + re.Made(VillageGood.Ration) + " handed=" + re.HandedOut(VillageGood.Ration) +
                        " game=" + re.Made(VillageGood.Game) + " wild=" + re.Source(VillageSource.Wildlife) + " trees=" + re.Source(VillageSource.MatureTrees) + "+" + re.SaplingCount + " wood=" + re.Commons(VillageGood.Wood) +
                        " bows=" + re.Made(VillageGood.Bow) + " axes=" + re.Commons(VillageGood.Axe) + "/" + re.Commons(VillageGood.BrokenAxe) + " metal=" + re.Commons(VillageGood.Metal) + " hides=" + re.Commons(VillageGood.Hide) +
                        " r0[hp=" + re.Residents[0].Health + " hun=" + re.Residents[0].Hunger + "] idle/work/walk/rest/sleep/eat/recover=" + string.Join("/", share) +
                        " npcHunger=" + rp.Social.GetMemory(0).Hunger + "," + rp.Social.GetMemory(1).Hunger + "," + rp.Social.GetMemory(2).Hunger + " needs=" + string.Join("|", re.OpenNeeds(rp)));
                }
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
