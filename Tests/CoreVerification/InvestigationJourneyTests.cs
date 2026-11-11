using System;
using System.Numerics;
using AreArpg.Core;

internal static class InvestigationJourneyTests
{
    /// <summary>Late-story fixture only; subsequent movement, actions, damage, travel and serialization are production code.</summary>
    private static ArenaSimulation Mill(HungerChoice choice)
    {
        var p = CharacterProgression.CreateEntryProfile();
        for (int i = 0; i < 4; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(choice); p.TravelHunger(4, false);
        return new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1);
    }
    private static void Move(ArenaSimulation w, long x, long z) { HunterEncounterTests.Move(w, x, z); }
    private static ArenaSimulation Reload(ArenaSimulation w)
    {
        if (!CharacterProgression.TryLoad(w.Progression.SaveString(), out var p)) throw new Exception("Invalid crypt checkpoint");
        return new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1);
    }
    private static string Alter(string save, int index, string value)
    {
        var f = save.Split('|'); var c = f[18].Split(','); c[index] = value; f[18] = string.Join(",", c);
        string payload = string.Join("|", f, 0, f.Length - 1); ulong h = 14695981039346656037UL;
        unchecked { foreach (byte b in System.Text.Encoding.UTF8.GetBytes(payload)) { h ^= b; h *= 1099511628211UL; } }
        return payload + "|" + h.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static void Run(Action<bool, string> check)
    {
        foreach (var choice in new[] { HungerChoice.Reveal, HungerChoice.Protect, HungerChoice.Replace })
        foreach (bool force in new[] { false, true })
        {
            string branch = choice + "-" + force; var w = Mill(choice);
            Move(w, 0, 24000); w = w.TryTraversePassage();
            check(w != null && w.Progression.EntryAct == 5, "missing-patients-open-store-passage-" + branch);
            check(w.Progression.SaveString().StartsWith("AC11|") && Reload(w).Progression.SaveString() == w.Progression.SaveString(), "crypt-ac11-roundtrip-" + branch);
            check(!w.CanStand(12000, 0) && !w.CanStand(-7000, -8000) && !w.CanStand(0, 21000) && w.CanStand(0, 18000), "crypt-store-geometry-" + branch);
            string untouched = w.Progression.SaveString();
            check(!w.ActOnHunger(HungerAction.StoreBandage).Accepted && untouched == w.Progression.SaveString(), "crypt-no-remote-clues-" + branch);
            Move(w, 0, 18000);
            check(!w.ActOnHunger(HungerAction.BreakSeal).Accepted, "crypt-warning-before-commit-" + branch);
            check(w.ActOnHunger(HungerAction.InspectSeal).Accepted, "crypt-seal-risk-learned-" + branch);
            untouched = w.Progression.SaveString();
            check(!w.ActOnHunger(HungerAction.OpenSeal).Accepted && untouched == w.Progression.SaveString(), "crypt-investigation-requires-three-clues-" + branch);
            Move(w, 0, 0); Move(w, -6500, 0);
            check(w.ActOnHunger(HungerAction.StoreBandage).Accepted, "crypt-local-bandage-" + branch);
            untouched = w.Progression.SaveString();
            check(!w.ActOnHunger(HungerAction.StoreBandage).Accepted && untouched == w.Progression.SaveString(), "crypt-clue-idempotent-" + branch);
            Move(w, 0, 0); Move(w, 0, -3000); Move(w, 6500, -3000);
            check(w.ActOnHunger(HungerAction.StoreTracks).Accepted, "crypt-local-tracks-" + branch);
            Move(w, 0, -3000); Move(w, 0, 18000);
            check(w.ActOnHunger(force ? HungerAction.BreakSeal : HungerAction.OpenSeal).Accepted, "crypt-seal-choice-" + branch);
            check(w.HungerForwardOpen && w.CanStand(0, 21000) && !w.CanStand(7000, 21000), "crypt-gate-opens-in-authority-" + branch);
            untouched = w.Progression.SaveString();
            check(!w.ActOnHunger(force ? HungerAction.OpenSeal : HungerAction.BreakSeal).Accepted && untouched == w.Progression.SaveString(), "crypt-choice-cannot-switch-" + branch);
            Move(w, 0, 24000); w = w.TryTraversePassage();
            check(w != null && w.Progression.EntryAct == 6 && !w.HungerForwardOpen && !w.CanStand(0, 21000), "crypt-terminal-inner-door-" + branch);
            int hp = w.PlayerHealth; Move(w, 0, -3000);
            check(force ? w.Progression.Investigation.TrapTicks > 0 : w.Progression.Investigation.TrapTicks == 0, "crypt-risk-from-selected-choice-" + branch);
            var loaded = Reload(w);
            check(loaded.PlayerXmm == w.PlayerXmm && loaded.PlayerZmm == w.PlayerZmm && loaded.PlayerHealth == hp, "crypt-checkpoint-real-position-health-" + branch);
            for (int i = 0; i < 15; i++)
            {
                w.Step(); loaded.Step();
                check(w.Progression.SaveString() == loaded.Progression.SaveString() && w.PlayerHealth == loaded.PlayerHealth, "crypt-warning-resume-future-" + branch + "-" + i);
            }
            check(w.PlayerHealth == hp - (force ? 320 : 0), "crypt-actual-branch-damage-" + branch);
            check(Reload(w).PlayerHealth == w.PlayerHealth, "crypt-no-reload-healing-" + branch);
            if (force)
            {
                check(w.ActivateSkill(2).Activated, "crypt-real-heal");
                check(Reload(w).PlayerHealth == w.PlayerHealth && Reload(w).Cooldowns[2] == w.Cooldowns[2], "crypt-immediate-heal-checkpoint");
            }
            Move(w, 0, 15000);
            check(w.ActOnHunger(HungerAction.InspectCryptTrace).Accepted && !w.Progression.Investigation.Reported, "crypt-trace-is-not-telepathic-report-" + branch);
            check(!w.ActOnHunger(HungerAction.ReportCrypt).Accepted, "crypt-cannot-report-remotely-" + branch);
            string traceSave = w.Progression.SaveString();
            foreach (var invalid in new[] { new[] { "0", "0" }, new[] { "1", "8" }, new[] { "2", "0" }, new[] { "5", "11" }, new[] { "6", "5" }, new[] { "7", "12000" }, new[] { "8", "21000" }, new[] { "9", "2147483647" }, new[] { "10", "101" }, new[] { "11", "301" }, new[] { "15", "01" } })
            {
                check(!CharacterProgression.TryLoad(Alter(traceSave, int.Parse(invalid[0]), invalid[1]), out _), "crypt-reject-signed-impossible-" + branch + "-" + invalid[0]);
            }
            hp = w.PlayerHealth;
            Move(w, 0, -24000); w = w.TryTraversePassage();
            check(w.Progression.EntryAct == 5 && w.PlayerHealth == hp && Reload(w).PlayerHealth == hp, "crypt-return-checkpoint-no-free-heal-" + branch);
            Move(w, 0, 24000); w = w.TryTraversePassage(); Move(w, 0, -3000);
            for (int i = 0; i < 15; i++) w.Step();
            check(w.PlayerHealth == hp, "crypt-spent-trap-cannot-reset-by-travel-" + branch);
            for (int act = 6; act >= 3; act--) { Move(w, 0, -24000); w = w.TryTraversePassage(); }
            check(w.Progression.EntryAct == 2 && w.Progression.SupplyChoice == choice, "crypt-return-all-way-to-ilyan-" + branch);
            check(!w.ActOnHunger(HungerAction.ReportCrypt).Accepted, "crypt-report-needs-local-contact-" + branch);
            // Explicit NPC contact fixture; player was moved only by production movement.
            var ilyan = w.Npcs.Find(n => n.Id == 0); ilyan.Xmm = w.PlayerXmm + 1000; ilyan.Zmm = w.PlayerZmm; ilyan.InteractionCooldownTicks = 0;
            string dorn = w.Progression.Social.GetMemory(2).Token();
            check(w.ActOnHunger(HungerAction.ReportCrypt).Accepted && w.Progression.Investigation.Reported, "crypt-personal-report-" + branch);
            check(dorn == w.Progression.Social.GetMemory(2).Token(), "crypt-no-uninformed-guard-memory-" + branch);
            untouched = w.Progression.SaveString();
            check(!w.ActOnHunger(HungerAction.ReportCrypt).Accepted && untouched == w.Progression.SaveString(), "crypt-report-idempotent-" + branch);
            check(!CharacterProgression.TryLoad(Alter(untouched, 3, "0"), out _), "crypt-report-requires-trace-" + branch);
        }
        var dodge = Mill(HungerChoice.Protect); Move(dodge, 0, 24000); dodge = dodge.TryTraversePassage(); Move(dodge, 0, 18000);
        dodge.ActOnHunger(HungerAction.InspectSeal); dodge.ActOnHunger(HungerAction.BreakSeal); Move(dodge, 0, 24000); dodge = dodge.TryTraversePassage();
        while (dodge.Progression.Investigation.TrapTicks == 0) { dodge.SetMoveInput(0, 10000); dodge.Step(); }
        int full = dodge.PlayerHealth; dodge.SetMoveInput(10000, 0); for (int i = 0; i < 10; i++) dodge.Step(); dodge.SetMoveInput(0, 0);
        check(dodge.Progression.Investigation.TrapTicks == -1 && dodge.PlayerHealth == full, "crypt-warning-is-avoidable-by-real-movement");
    }
}
