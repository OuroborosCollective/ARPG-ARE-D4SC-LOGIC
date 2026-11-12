using System;
using System.Numerics;
using AreArpg.Core;

internal static class RattlerEncounterTests
{
    // Authored late-story fixture; perception, combat, travel and save execution below are real Core code.
    internal static ArenaSimulation Door()
    {
        var p = CharacterProgression.CreateEntryProfile();
        for (int i = 0; i < 4; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect); p.TravelHunger(6, false);
        p.Investigation.Clues = 7; p.Investigation.Approach = SealApproach.Investigated;
        var w = new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1);
        HunterEncounterTests.Move(w, 0, 15000); w.ActOnHunger(HungerAction.InspectCryptTrace);
        return w;
    }
    internal static ArenaSimulation Den()
    {
        var w = Door(); w.ActOnHunger(HungerAction.OpenInnerDoor);
        HunterEncounterTests.Move(w, 0, 24000); return w.TryTraversePassage();
    }
    private static ArenaSimulation Reload(ArenaSimulation w)
    {
        if (!CharacterProgression.TryLoad(w.Progression.SaveString(), out var p)) throw new Exception("Invalid rattler save: " + w.Progression.SaveString());
        return new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1);
    }
    internal static void Fight(ArenaSimulation w, bool dodge)
    {
        var r = w.Progression.Rattler; var m = r.Creature;
        if (dodge && m.BossWindupTicks > 0)
        {
            // Retreat south from the frozen target; the arena offers enough room for this bounded fight.
            w.SetMoveInput(0, -10000);
        }
        else
        {
            long dx = m.Xmm - w.PlayerXmm, dz = m.Zmm - w.PlayerZmm;
            w.SetMoveInput(0, Math.Abs(dz) > 1800 ? Math.Sign(dz) * 10000 : 0);
            if (Math.Abs(dx) > 1000) w.SetMoveInput(Math.Sign(dx) * 10000, 0);
            w.ActivateSkill(3); w.ActivateSkill(1); w.ActivateSkill(0);
        }
        w.Step();
    }
    private static string Alter(string save, int field, string value)
    {
        var f = save.Split('|'); var c = f[19].Split(','); c[field] = value; f[19] = string.Join(",", c);
        string payload = string.Join("|", f, 0, f.Length - 1); ulong h = 14695981039346656037UL;
        unchecked { foreach (byte b in System.Text.Encoding.UTF8.GetBytes(payload)) { h ^= b; h *= 1099511628211UL; } }
        return payload + "|" + h.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }
    public static void Run(Action<bool, string> check)
    {
        var door = Door(); string old = door.Progression.SaveString();
        check(old.StartsWith("AC11|") && Reload(door).Progression.SaveString() == old, "rattler-unused-ac11-byte-stable");
        check(door.ActOnHunger(HungerAction.OpenInnerDoor).Accepted && door.HungerForwardOpen && door.CanStand(0, 21000), "rattler-explicit-inner-door-opens-authority");
        check(!door.ActOnHunger(HungerAction.OpenInnerDoor).Accepted && !door.ActOnHunger(HungerAction.InspectRoyalBrand).Accepted, "rattler-no-repeat-door-or-remote-brand");
        var quiet = Den(); check(quiet.Progression.EntryAct == 7 && quiet.Monsters.Count == 1, "rattler-one-real-combat-entity");
        check(quiet.Progression.SaveString().StartsWith("AC12|") && Reload(quiet).Progression.SaveString() == quiet.Progression.SaveString(), "rattler-ac12-checkpoint");
        quiet.ActOnHunger(HungerAction.ToggleSneak); HunterEncounterTests.Move(quiet, 0, 0);
        check(quiet.Progression.Rattler.MemoryTicks == 0 && quiet.Progression.Rattler.Creature.Zmm == 8000, "rattler-quiet-visible-player-does-not-trigger-sight");
        quiet.ActOnHunger(HungerAction.ToggleSneak); quiet.SetMoveInput(10000, 0); quiet.Step(); quiet.SetMoveInput(0, 0);
        check(quiet.Progression.Rattler.MemoryTicks == 60 && quiet.Progression.Rattler.TargetX == quiet.PlayerXmm, "rattler-actual-step-is-heard");
        long heard = quiet.Progression.Rattler.TargetX;
        quiet.ActOnHunger(HungerAction.ToggleSneak); quiet.SetMoveInput(10000, 0); for (int i = 0; i < 6; i++) quiet.Step(); quiet.SetMoveInput(0, 0);
        check(quiet.Progression.Rattler.TargetX == heard && quiet.PlayerXmm != heard, "rattler-quiet-movement-does-not-retarget-old-sound");
        var scent = Den(); scent.ActOnHunger(HungerAction.ToggleSneak); HunterEncounterTests.Move(scent, 0, 5800);
        check(scent.Progression.Rattler.MemoryTicks > 0, "rattler-near-scent-detects-sneaking-player");
        var attack = Den(); attack.ActOnHunger(HungerAction.ToggleSneak); HunterEncounterTests.Move(attack, 0, 2000);
        check(attack.ActivateSkill(3).Activated && attack.Progression.Rattler.MemoryTicks == 60, "rattler-successful-attack-emits-noise");
        check(Reload(attack).Progression.SaveString() == attack.Progression.SaveString(), "rattler-save-immediately-after-hit-includes-health-rng-and-cooldown");
        var a = Den(); var b = Den();
        bool warning = false; int ticks = 0;
        for (; ticks < 900 && !a.GameOver && !a.Progression.Rattler.Creature.IsDead; ticks++)
        {
            Fight(a, true); Fight(b, true); warning |= a.Progression.Rattler.Creature.BossWindupTicks > 0;
            check(a.ComputeWorldHash() == b.ComputeWorldHash(), "rattler-deterministic-replay-" + ticks);
            if (ticks == 80) b = Reload(b);
            // World hash includes session tick; compare continuation's canonical persisted effects after reload.
            if (ticks == 80) break;
        }
        for (int i = 0; i < 900 && !a.GameOver && !a.Progression.Rattler.Creature.IsDead; i++)
        {
            Fight(a, true); Fight(b, true); warning |= a.Progression.Rattler.Creature.BossWindupTicks > 0;
            check(a.Progression.SaveString() == b.Progression.SaveString(), "rattler-reload-same-combat-future-" + i);
        }
        check(warning && !a.GameOver && a.Progression.Rattler.Creature.IsDead, "rattler-full-health-tactical-victory");
        var tank = Den(); for (int i = 0; i < 900 && !tank.GameOver && !tank.Progression.Rattler.Creature.IsDead; i++) Fight(tank, false);
        check(tank.GameOver || tank.PlayerHealth < a.PlayerHealth, "rattler-standing-in-swipes-costs-health");
        var r = a.Progression.Rattler; HunterEncounterTests.Move(a, r.Creature.Xmm, r.Creature.Zmm);
        check(a.ActOnHunger(HungerAction.InspectRoyalBrand).Accepted && !a.ActOnHunger(HungerAction.ReportRoyalBrand).Accepted, "rattler-brand-is-local-discovery-not-telepathic-report");
        var kills = a.Progression.LifetimeKills; var loot = a.Progression.LootCollected;
        a = Reload(a); for (int i = 0; i < 30; i++) { a.ActivateSkill(3); a.Step(); }
        check(a.Progression.LifetimeKills == kills && a.Progression.LootCollected == loot && loot == 1, "rattler-death-reward-reload-idempotent");
        foreach (var bad in new[] { new[] {"0","0"}, new[] {"1","2"}, new[] {"4","12000"}, new[] {"5","21000"}, new[] {"6","1"}, new[] {"7","15"}, new[] {"8","1"}, new[] {"9","0"}, new[] {"10","61"}, new[] {"11","999999"}, new[] {"13","01"} })
            check(!CharacterProgression.TryLoad(Alter(a.Progression.SaveString(), int.Parse(bad[0]), bad[1]), out _), "rattler-reject-rechecksummed-invalid-" + bad[0]);
        for (int act = 7; act >= 3; act--) { HunterEncounterTests.Move(a, 0, -24000); a = a.TryTraversePassage(); check(a != null, "rattler-return-passage-" + act); }
        var ilyan = a.Npcs.Find(n => n.Id == 0); ilyan.Xmm = a.PlayerXmm + 1000; ilyan.Zmm = a.PlayerZmm; ilyan.InteractionCooldownTicks = 0;
        check(a.ActOnHunger(HungerAction.ReportRoyalBrand).Accepted && a.Progression.Rattler.Reported, "rattler-real-local-report-to-ilyan");
        check(!a.ActOnHunger(HungerAction.ReportRoyalBrand).Accepted && Reload(a).Progression.Rattler.Reported, "rattler-report-persists-once");
        var revisit = Den(); HunterEncounterTests.Move(revisit, 0, -24000); revisit = revisit.TryTraversePassage();
        check(revisit.Progression.EntryAct == 6 && revisit.CanStand(revisit.PlayerXmm, revisit.PlayerZmm), "rattler-return-arrival-is-walkable");
    }
}
