using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using AreArpg.Core;

/// <summary>
/// Owner decision 2026-11-26 (answer A): the Aschbeter is an enemy by the rules of the owner's archive
/// (Spielprobe/AschbeterRegeln.gd). He notices the wanderer at 8.5 m, turns by 5 degrees every 0.3 s, shuffles at 0.32 m/s,
/// strikes from 2.12 m and his claw can kill from the front; a swing reaches him only while he turns, from the flank it
/// throws him down dead, a flatter one only throws him down. He can be avoided.
/// </summary>
internal static class AschbeterTests
{
    public static void Run(Action<bool, string> check)
    {
        Rules(check);
        Encounter(check);
    }

    private static void Rules(Action<bool, string> check)
    {
        var a = new AschbeterState(0, 0, 0);
        a.Step(0, 8600, true, false, 0, 0, true);
        check(a.Mode == AschbeterMode.Idle && a.Zmm == 0, "aschbeter-ignores-beyond-8-5-m");
        a = new AschbeterState(0, 0, 0);
        for (int i = 0; i < 10; i++) a.Step(0, 8000, true, false, 0, 0, true);
        check(a.Mode == AschbeterMode.Shuffle && a.Xmm == 0 && a.Zmm == 320, "aschbeter-shuffles-0-32-m-per-second");
        a = new AschbeterState(0, 0, 0);
        for (int i = 0; i < 10; i++) a.Step(0, 8000, true, false, 0, 0, false);
        check(a.Zmm == 0, "aschbeter-stops-where-the-ground-ends");

        a = new AschbeterState(0, 0, 0);
        // The tick that starts the turn counts as its zeroth: thirty more ticks turn him ten steps.
        for (int i = 0; i < 31; i++) a.Step(4000, -4000, true, false, 0, 0, true);
        check(a.Mode == AschbeterMode.TurnPositive && a.Heading == 10, "aschbeter-turns-5-degrees-every-0-3-s");
        check(AschbeterState.LookAt(1, 0, 0) == 18 && AschbeterState.LookAt(0, -1, 0) == 36 && AschbeterState.AngleSteps(0, 36) == 36 &&
            AschbeterState.AngleSteps(70, 2) == 4, "aschbeter-headings-are-the-archive-table");

        // The strike: from 2.12 m in front; the claw lands five ticks after the strike began (tick 1) and reaches 2.27 m
        // within 25 degrees; ten ticks after it began he rests for 13.
        a = new AschbeterState(0, 0, 0);
        bool hit = false; int contactTick = 0;
        for (int i = 1; i <= 11; i++) { a.Step(0, 2000, true, false, 0, 0, true); if (a.ClawHit) { hit = true; contactTick = i; } }
        check(hit && contactTick == 1 + AschbeterState.ContactTick && a.Mode == AschbeterMode.Idle && a.ClawCooldown == AschbeterState.RecoveryTicks,
            "aschbeter-claw-lands-on-the-fifth-tick");
        a = new AschbeterState(0, 0, 0);
        hit = false;
        for (int i = 1; i <= 11; i++) { a.Step(0, i < 5 ? 2000 : 2400, true, false, 0, 0, true); hit |= a.ClawHit; }
        check(!hit && a.ClawCooldown == AschbeterState.RecoveryTicks, "aschbeter-claw-misses-who-steps-back");
        a = new AschbeterState(0, 0, 0);
        hit = false;
        for (int i = 1; i <= 11; i++) { a.Step(i < 5 ? 0 : 1600, i < 5 ? 2000 : 1300, true, false, 0, 0, true); hit |= a.ClawHit; }
        check(!hit, "aschbeter-claw-misses-who-steps-aside");

        // Swings: only while he turns. From 45 degrees or more to the side the first one is fatal.
        a = new AschbeterState(0, 0, 0);
        a.Step(0, 1500, true, true, 36, AschbeterState.LongReachMm, true);
        check(a.LastSwing == AschbeterSwing.Deflected && a.Mode == AschbeterMode.Strike, "aschbeter-deflects-a-swing-while-not-turning");
        a = new AschbeterState(0, 0, 0);
        a.Step(2000, 0, true, true, 54, AschbeterState.LongReachMm, true);
        check(a.LastSwing == AschbeterSwing.Fatal && a.Mode == AschbeterMode.Fall, "aschbeter-flank-swing-throws-him-down");
        for (int i = 0; i < AschbeterState.FallTicks; i++) a.Step(2000, 0, true, false, 0, 0, true);
        check(a.IsDead && a.Down, "aschbeter-flank-swing-kills");
        a = new AschbeterState(0, 0, 0);
        a.Step(3000, 0, true, true, 54, AschbeterState.ShortReachMm, true);
        check(a.LastSwing == AschbeterSwing.Miss && a.Mode == AschbeterMode.TurnPositive, "aschbeter-swing-out-of-reach-misses");
        a = new AschbeterState(0, 0, 0);
        a.Step(2000, 0, true, true, 18, AschbeterState.LongReachMm, true);
        check(a.LastSwing == AschbeterSwing.Miss, "aschbeter-swing-facing-away-misses");

        // A flat hit, 35 degrees off his heading while he turns: he falls, rises again, and the second flat hit is fatal.
        a = new AschbeterState(0, 0, 0);
        long gx = 803, gz = 1147;
        a.Step(gx, gz, true, true, AschbeterState.LookAt(-gx, -gz, 0), AschbeterState.LongReachMm, true);
        check(a.LastSwing == AschbeterSwing.Staggered && a.Stamina == 1 && a.Mode == AschbeterMode.Fall, "aschbeter-flat-swing-throws-him-down");
        a.Step(gx, gz, true, true, AschbeterState.LookAt(-gx, -gz, 0), AschbeterState.LongReachMm, true);
        check(a.LastSwing == AschbeterSwing.None, "aschbeter-ignores-swings-while-down");
        for (int i = 0; i < AschbeterState.FallTicks + AschbeterState.RiseTicks - 1; i++) a.Step(gx, gz, true, false, 0, 0, true);
        check(a.Mode == AschbeterMode.Idle && !a.Down && a.ClawCooldown == AschbeterState.RecoveryTicks, "aschbeter-rises-after-a-flat-swing");
        int guard = 0;
        while (a.Mode != AschbeterMode.TurnPositive && a.Mode != AschbeterMode.TurnNegative && guard++ < 40) a.Step(-gx, gz, true, false, 0, 0, true);
        a.Step(-gx, gz, true, true, AschbeterState.LookAt(gx, -gz, 0), AschbeterState.LongReachMm, true);
        check(a.LastSwing == AschbeterSwing.Fatal && a.Stamina == 0, "aschbeter-second-flat-swing-is-fatal");

        // The short staff's swing cools down for 6 ticks, the long one for 8; swings in between are not counted.
        a = new AschbeterState(0, 0, 0);
        a.Step(0, 1500, true, true, 36, AschbeterState.ShortReachMm, true);
        a.Step(0, 1500, true, true, 36, AschbeterState.ShortReachMm, true);
        check(a.StrikeCooldown == 5 && a.LastSwing == AschbeterSwing.None, "aschbeter-swing-cooldown");

        // The same inputs always give the same encounter.
        var one = new AschbeterState(); var two = new AschbeterState();
        for (int i = 0; i < 300; i++)
        {
            long px = 6000 - i * 37, pz = 2000 + i * 23; bool swing = i % 17 == 0;
            one.Step(px, pz, true, swing, AschbeterState.LookAt(one.Xmm - px, one.Zmm - pz, 0), 2300, true);
            two.Step(px, pz, true, swing, AschbeterState.LookAt(two.Xmm - px, two.Zmm - pz, 0), 2300, true);
        }
        check(string.Join(",", one.HashFields()) == string.Join(",", two.HashFields()), "aschbeter-deterministic");
    }

    private static void Encounter(Action<bool, string> check)
    {
        var court = Court(95, false);
        var a = court.Aschbeter;
        check(a != null && a.Xmm == AschbeterState.StartXmm && a.Zmm == AschbeterState.StartZmm && a.Heading == AschbeterState.StartHeading &&
            court.Monsters.Count == 0, "aschbeter-stands-on-the-procession-path-facing-the-portal");
        var escape = CourtLayout.Route(CourtLayout.SpawnXmm, CourtLayout.SpawnZmm, CourtLayout.ExitXmm, CourtLayout.ExitZmm,
            CourtLayout.SpawnXmm, CourtLayout.SpawnZmm, 2400);
        check(escape.Length > 2, "aschbeter-escape-when-starting-inside-avoidance-circle");
        string before = court.Progression.SaveString();

        // From the front his claw kills an unprotected wanderer.
        bool dead = Approach(court, 0, AschbeterState.StartZmm - 3500, 900) && WaitFor(court, () => court.GameOver, 120);
        check(dead && court.PlayerHealth == 0, "aschbeter-claw-from-the-front-kills");
        var again = new ArenaSimulation(95, LoadOrThrow(before), BigInteger.One, LootCatalog.Version1);
        check(again.Aschbeter != null && again.Aschbeter.Zmm == AschbeterState.StartZmm && !again.GameOver, "aschbeter-stands-again-after-the-checkpoint");

        // A dodge carries the wanderer out of the claw (combat v2).
        var quick = Court(96, true);
        bool struck = Approach(quick, 0, AschbeterState.StartZmm - 3500, 900) &&
            WaitFor(quick, () => quick.Aschbeter.Mode == AschbeterMode.Strike && quick.Aschbeter.ModeTicks == 3, 200);
        int health = quick.PlayerHealth; // the crypt's guardians may have scratched him on the way out
        check(struck && quick.InCombat, "aschbeter-active-pursuer-counts-as-combat");
        bool dodged = struck && quick.StartDodge(10000, 0);
        for (int i = 0; i < 12; i++) quick.Step();
        check(dodged && !quick.GameOver && quick.PlayerHealth == health && quick.DodgedAttacks > 0, "aschbeter-claw-can-be-dodged");

        // From behind, while he turns, one swing throws him down dead; he drops loot where he lies.
        var hunt = Court(97, false);
        int kills = hunt.Kills, loot = hunt.GroundLoot.Count;
        bool behind = CourtTests.WalkTo(hunt, 0, AschbeterState.StartZmm + 2250, 900, 300);
        var h = hunt.Aschbeter;
        // The ways keep 2.4 m from him; the last steps close in while he is still turning (no strike without facing).
        for (int i = 0; i < 4 && Sq(hunt.PlayerXmm - h.Xmm) + Sq(hunt.PlayerZmm - h.Zmm) > 1900L * 1900L; i++)
        {
            long dx = h.Xmm - hunt.PlayerXmm, dz = h.Zmm - hunt.PlayerZmm, scale = Math.Max(1, Math.Max(Math.Abs(dx), Math.Abs(dz)));
            hunt.SetMoveInput(dx * 3000 / scale, dz * 3000 / scale); hunt.Step();
        }
        hunt.SetMoveInput(0, 0);
        int error = Math.Abs(AschbeterState.AngleSteps(h.Heading, AschbeterState.LookAt(hunt.PlayerXmm - h.Xmm, hunt.PlayerZmm - h.Zmm, h.Heading)));
        check(behind && (h.Mode == AschbeterMode.TurnPositive || h.Mode == AschbeterMode.TurnNegative) && error >= AschbeterState.FlankSteps,
            "aschbeter-turns-to-the-wanderer-behind-him");
        var swing = hunt.ActivateSkill(0);
        hunt.Step();
        check(swing.Activated && h.LastSwing == AschbeterSwing.Fatal && h.Mode == AschbeterMode.Fall, "aschbeter-swing-from-behind-is-fatal");
        WaitFor(hunt, () => h.IsDead, 20);
        check(h.IsDead && hunt.Progression.AschbeterFallen && hunt.Kills == kills + 1 && hunt.GroundLoot.Count == loot + 1 &&
            hunt.GroundLoot[hunt.GroundLoot.Count - 1].Zmm == h.Zmm, "aschbeter-falls-and-drops-loot");

        // The save keeps him dead while the wanderer is in Aschenwacht; the village forgets him.
        string fallen = hunt.Progression.SaveString();
        check(fallen.Contains("|1,1,c,a"), "aschbeter-save-names-his-fall");
        var resumed = new ArenaSimulation(97, LoadOrThrow(fallen), BigInteger.One, LootCatalog.Version1);
        check(resumed.InCourt && resumed.Aschbeter == null && resumed.Progression.AschbeterFallen, "aschbeter-stays-dead-after-loading");
        string payload = fallen.Substring(0, fallen.LastIndexOf('|'));
        check(CharacterProgression.TryLoad(Resign(payload.Replace("1,1,c,a", "1,1,c,b3,a")), out var both) && both.AschbeterFallen && both.KilnVisited,
            "aschbeter-token-after-the-brennerhof");
        foreach (string bad in new[] { "1,1,c,a,a", "1,1,c,aa", "1,1,c,a,b3", "1,1,c,A" })
            check(!CharacterProgression.TryLoad(Resign(payload.Replace("1,1,c,a", bad)), out _), "aschbeter-rejects-token-" + bad);
        while (hunt.LootOnGround) if (!WalkAndCollect(hunt)) break;
        check(!hunt.LootOnGround && CourtTests.WalkTo(hunt, CourtLayout.ExitXmm, CourtLayout.ExitZmm, 1500, 400) && hunt.AtWorldPassage, "aschbeter-way-on-after-his-fall");
        var village = hunt.TryTraversePassage();
        check(village != null && village.IsVillage && !village.Progression.AschbeterFallen && village.Aschbeter == null, "aschbeter-village-forgets-him");

        // Nobody has to fight him: the bell tower's door lets the wanderer on while he still stands.
        var past = Court(98, false);
        check(CourtTests.WalkTo(past, CourtLayout.ExitXmm, CourtLayout.ExitZmm, 1500, 400) && !past.GameOver && past.Aschbeter != null &&
            !past.Aschbeter.IsDead && past.TryTraversePassage()?.IsVillage == true, "aschbeter-can-be-avoided");
    }

    private static ArenaSimulation Court(ulong seed, bool combatV2)
    {
        var profile = CharacterProgression.CreateEntryProfile();
        if (combatV2) profile.UseCombatV2();
        var crypt = new ArenaSimulation(seed, profile, BigInteger.One, LootCatalog.Version1);
        foreach (var enemy in crypt.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
        if (combatV2)
        {
            // Combat v2 lands the blow on the action's impact tick.
            crypt.ActivateSkill(3);
            for (int i = 0; i < 20 && !crypt.CryptCleared; i++) crypt.Step();
        }
        else crypt.ActivateSkill(3);
        while (crypt.LootOnGround) if (!crypt.TryCollectLoot()) throw new Exception("crypt loot out of reach");
        HunterEncounterTests.Move(crypt, 0, 24000);
        return crypt.TryTraversePassage() ?? throw new Exception("crypt did not open into Aschenwacht");
    }

    /// <summary>Walk the layout's way straight at a point, without keeping away from him.</summary>
    private static bool Approach(ArenaSimulation w, long x, long z, int limit)
    {
        long[] route = CourtLayout.Route(w.PlayerXmm, w.PlayerZmm, x, z);
        if (route.Length == 0) return false;
        int index = 0;
        for (int tick = 0; tick < limit && !w.GameOver; tick++)
        {
            long ex = x - w.PlayerXmm, ez = z - w.PlayerZmm;
            if (ex * ex + ez * ez <= 650L * 650L) { w.SetMoveInput(0, 0); return true; }
            while (index < route.Length / 2 - 1 && Sq(route[2 * index] - w.PlayerXmm) + Sq(route[2 * index + 1] - w.PlayerZmm) < 700L * 700L) index++;
            int aim = Math.Min(route.Length / 2 - 1, index + 2);
            long dx = route[2 * aim] - w.PlayerXmm, dz = route[2 * aim + 1] - w.PlayerZmm, scale = Math.Max(1, Math.Max(Math.Abs(dx), Math.Abs(dz)));
            w.SetMoveInput(dx * 10000 / scale, dz * 10000 / scale);
            w.Step();
        }
        w.SetMoveInput(0, 0);
        return w.GameOver;
    }

    private static bool WaitFor(ArenaSimulation w, Func<bool> done, int limit)
    {
        for (int i = 0; i < limit && !done(); i++) w.Step();
        return done();
    }

    private static bool WalkAndCollect(ArenaSimulation w)
    {
        var drop = w.GroundLoot[0];
        CourtTests.WalkTo(w, drop.Xmm, drop.Zmm, 400, 600);
        return w.TryCollectLoot();
    }

    private static long Sq(long v) { return v * v; }

    private static CharacterProgression LoadOrThrow(string save)
    {
        if (!CharacterProgression.TryLoad(save, out var p)) throw new Exception("Aschenwacht save refused");
        return p;
    }

    private static string Resign(string payload)
    {
        ulong hash = 14695981039346656037UL;
        unchecked { foreach (byte value in Encoding.UTF8.GetBytes(payload)) { hash ^= value; hash *= 1099511628211UL; } }
        return payload + "|" + hash.ToString("X16", CultureInfo.InvariantCulture);
    }
}
