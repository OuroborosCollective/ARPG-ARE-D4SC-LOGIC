using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Numerics;
using AreArpg.Core;

/// <summary>
/// Owner decision 2026-11-25: between the old crypt and the village lies Aschenwacht (the desecrated forecourt and the bell
/// court). The cleared crypt opens into it, the wanderer walks it on foot, may read its traces, and the bell tower's door
/// leads on to the village. Nothing fights there and nothing is skipped.
/// </summary>
internal static class CourtTests
{
    public static void Run(Action<bool, string> check)
    {
        LayoutIsSoundAndConnected(check);
        var crypt = ClearedCrypt(91);
        check(crypt.TryTraversePassage() == null, "court-crypt-passage-only-at-the-exit-arch");
        HunterEncounterTests.Move(crypt, 0, 24000);
        var court = crypt.TryTraversePassage();
        check(court != null && court.InCourt && court.Progression.EntryAct == 1 && !court.IsVillage, "court-crypt-exit-leads-into-aschenwacht");
        check(crypt.Progression.EntryAct == 1 && !crypt.Progression.InCourt, "court-transition-does-not-mutate-old-checkpoint");
        check(court.Monsters.Count == 0 && court.Npcs.Count == 0 && court.CryptCleared && court.GroundLoot.Count == 0, "court-nothing-fights-here");
        check(court.PlayerXmm == CourtLayout.SpawnXmm && court.PlayerZmm == CourtLayout.SpawnZmm && court.ChapterName.EndsWith("Der entweihte Vorhof"),
            "court-wanderer-steps-out-of-the-crypt-portal");
        check(court.Progression.Items.Count == 3 && court.Progression.LifetimeKills == 3, "court-keeps-loot-xp-and-kills");
        check(court.TryTraversePassage() == null && court.TryEnterCourt() == null, "court-no-shortcut-from-the-portal");

        // Save and load in the court: a token of its own, read back exactly; older entry tokens stay as they were.
        string save = court.Progression.SaveString();
        check(save.Contains("|1,1,c"), "court-save-names-the-court");
        check(CharacterProgression.TryLoad(save, out var loaded) && loaded.InCourt && loaded.EntryAct == 1 && loaded.StableHash == court.Progression.StableHash,
            "court-save-reload");
        var resumed = new ArenaSimulation(91, loaded, BigInteger.One, LootCatalog.Version1);
        check(resumed.InCourt && resumed.Monsters.Count == 0 && resumed.PlayerZmm == CourtLayout.SpawnZmm, "court-load-does-not-respawn-the-crypt");
        string payload = save.Substring(0, save.LastIndexOf('|'));
        check(CharacterProgression.TryLoad(Resign(payload), out _), "court-resigned-fixture-loads");
        foreach (string bad in new[] { "1,1,c0", "1,1,c256", "1,1,c01", "1,1,cx", "1,2,c", "1,1,c1000" })
            check(!CharacterProgression.TryLoad(Resign(payload.Replace("1,1,c", bad)), out _), "court-rejects-token-" + bad);

        // Reading: only within reach of a trace; it is remembered in the save while the wanderer is here.
        check(!court.ActOnHunger(HungerAction.ReadCourtTrace).Accepted || court.NearCourtTrace.HasValue, "court-reading-needs-a-trace-in-reach");
        int read = 0;
        long portalX = CourtLore.Traces[0].Xmm, portalZ = CourtLore.Traces[0].Zmm;
        check(court.NearCourtTrace.HasValue && court.NearCourtTrace.Value.Id == 0 && court.ActOnHunger(HungerAction.ReadCourtTrace).Accepted,
            "court-the-portal-can-be-read-at-once");
        read |= 1;
        var twin = new ArenaSimulation(91, LoadOrThrow(save), BigInteger.One, LootCatalog.Version1);
        twin.ActOnHunger(HungerAction.ReadCourtTrace);
        foreach (CourtTrace trace in CourtLore.Traces.Skip(1))
        {
            long[] spot = NearestFreeCell(trace);
            bool there = WalkTo(court, spot[0], spot[1], 400) && WalkTo(twin, spot[0], spot[1], 400);
            var result = court.ActOnHunger(HungerAction.ReadCourtTrace);
            twin.ActOnHunger(HungerAction.ReadCourtTrace);
            check(there && result.Accepted && result.Text.StartsWith(trace.Title), "court-trace-readable-on-foot-" + trace.Id);
            read |= 1 << trace.Id;
            check(court.Progression.CourtTraces == read && !court.AtWorldPassage, "court-trace-remembered-" + trace.Id);
        }
        check(court.ComputeWorldHash() == twin.ComputeWorldHash(), "court-walk-and-reading-deterministic");
        check(court.Progression.SaveString().Contains("1,1,c" + CourtLore.AllTraces) && CharacterProgression.TryLoad(court.Progression.SaveString(), out var all) &&
            all.CourtTraces == CourtLore.AllTraces, "court-all-traces-survive-the-save");
        check(court.ChapterName.EndsWith("Der Glockenhof"), "court-bell-court-named-in-the-north");

        // The bell tower's door: the pilgrim path leads on to the village, and only there.
        check(WalkTo(court, CourtLayout.ExitXmm, CourtLayout.ExitZmm, 400, 1500) && court.AtWorldPassage, "court-bell-tower-door-reachable");
        var village = court.TryTraversePassage();
        check(village != null && village.IsVillage && village.Progression.EntryAct == 2 && !village.Progression.InCourt && village.Progression.CourtTraces == 0,
            "court-tower-door-leads-to-the-village");
        check(village.PlayerZmm == -23000 && village.Npcs.Count == 3 && village.TryTraversePassage() == null, "court-village-arrival-unchanged");
        check(CharacterProgression.TryLoad(village.Progression.SaveString(), out var settled) && settled.EntryAct == 2 && !settled.InCourt &&
            village.Progression.SaveString().Contains("|1,2"), "court-village-save-keeps-the-old-token");
        // The legacy advance used by older checks still goes straight on; the real passage never skips the court.
        check(ClearedCrypt(92).TryAdvanceCrypt()?.IsVillage == true, "court-legacy-advance-unchanged");
    }

    private static void LayoutIsSoundAndConnected(Action<bool, string> check)
    {
        check(CourtLayout.CanStand(CourtLayout.SpawnXmm, CourtLayout.SpawnZmm) && CourtLayout.CanStand(CourtLayout.ExitXmm, CourtLayout.ExitZmm),
            "court-layout-spawn-and-exit-free");
        long[] route = CourtLayout.Route(CourtLayout.SpawnXmm, CourtLayout.SpawnZmm, CourtLayout.ExitXmm, CourtLayout.ExitZmm);
        check(route.Length > 2 * 300 && route.Length < 2 * 2000, "court-layout-portal-to-tower-on-foot");
        check(Enumerable.Range(0, route.Length / 2).All(i => CourtLayout.CanStand(route[2 * i], route[2 * i + 1])), "court-layout-route-on-free-cells");
        check(!CourtLayout.CanStand(0, -45000) && !CourtLayout.CanStand(60000, 0) && !CourtLayout.CanStand(0, 80000), "court-layout-ends-at-walls-and-edges");
        check(CourtLayout.Route(CourtLayout.SpawnXmm, CourtLayout.SpawnZmm, 0, -45000).Length == 0, "court-layout-no-route-into-walls");
        long portal = CourtLayout.HeightMm(CourtLayout.SpawnXmm, CourtLayout.SpawnZmm), yard = CourtLayout.HeightMm(0, 0), bell = CourtLayout.HeightMm(1500, 55000);
        check(portal > 900 && portal < 1600 && Math.Abs(yard) < 400 && bell > 900 && bell < 1600, "court-layout-heights-terrace-yard-and-bell-court");
        foreach (CourtTrace trace in CourtLore.Traces)
            check(NearestFreeCell(trace).Length == 2, "court-layout-trace-in-reach-" + trace.Id);
        check(CourtLore.Traces.Select(t => t.Id).SequenceEqual(Enumerable.Range(0, 8)) && CourtLore.Traces.All(t => t.Text.Length > 60 && t.Text.Length < 420),
            "court-lore-eight-authored-traces");
        check(CourtLore.Traces.All(t => { long[] spot = NearestFreeCell(t); return spot.Length == 2 &&
            DistanceSquared(spot[0], spot[1], CourtLayout.ExitXmm, CourtLayout.ExitZmm) > ArenaSimulation.PassageRadiusMm * ArenaSimulation.PassageRadiusMm; }),
            "court-lore-every-trace-readable-outside-the-passage");
    }

    private static ArenaSimulation ClearedCrypt(ulong seed)
    {
        var crypt = new ArenaSimulation(seed, CharacterProgression.CreateEntryProfile(), BigInteger.One, LootCatalog.Version1);
        foreach (var enemy in crypt.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
        crypt.ActivateSkill(3);
        while (crypt.LootOnGround) if (!crypt.TryCollectLoot()) throw new Exception("crypt loot out of reach");
        return crypt;
    }

    private static CharacterProgression LoadOrThrow(string save)
    {
        if (!CharacterProgression.TryLoad(save, out var p)) throw new Exception("court save refused");
        return p;
    }

    /// <summary>The free cell nearest to a trace that the spawn reaches, as {x, z}; empty when none lies within its reach.</summary>
    internal static long[] NearestFreeCell(CourtTrace trace)
    {
        long best = long.MaxValue; long[] found = new long[0];
        for (long x = trace.Xmm - trace.RadiusMm; x <= trace.Xmm + trace.RadiusMm; x += CourtLayout.CellMm)
            for (long z = trace.Zmm - trace.RadiusMm; z <= trace.Zmm + trace.RadiusMm; z += CourtLayout.CellMm)
            {
                long cx = x - ((x - CourtLayout.MinXmm) % CourtLayout.CellMm + CourtLayout.CellMm) % CourtLayout.CellMm + CourtLayout.CellMm / 2;
                long cz = z - ((z - CourtLayout.MinZmm) % CourtLayout.CellMm + CourtLayout.CellMm) % CourtLayout.CellMm + CourtLayout.CellMm / 2;
                long d = DistanceSquared(cx, cz, trace.Xmm, trace.Zmm);
                if (d >= best || d > (trace.RadiusMm - 300) * (trace.RadiusMm - 300) || !CourtLayout.CanStand(cx, cz)) continue;
                // Never within the Aschbeter's strike: his trace is read from a few steps away.
                if (DistanceSquared(cx, cz, AschbeterState.StartXmm, AschbeterState.StartZmm) < AschbeterSafeMm * AschbeterSafeMm) continue;
                if (CourtLayout.Route(CourtLayout.SpawnXmm, CourtLayout.SpawnZmm, cx, cz).Length == 0) continue;
                best = d; found = new[] { cx, cz };
            }
        return found;
    }

    /// <summary>
    /// Walk the route the layout gives, as a player would with the stick: aim a little ahead on the way, step one tick at a
    /// time and let the rules decide every move. True once within <paramref name="stopMm"/> of the goal; the default is one
    /// step of 620 mm, because a goal next to an obstacle cannot be overshot and approached from the far side.
    /// </summary>
    /// <summary>How far the walkers keep from the living Aschbeter: beyond his strike (2.12 m) with a step to spare.</summary>
    internal const long AschbeterSafeMm = 2800;
    /// <summary>How far a way keeps from him while it passes: still beyond his strike, a little closer than a place to stand.</summary>
    internal const long AschbeterPassMm = 2400;

    /// <summary>The layout's way, round the living Aschbeter where he stands now.</summary>
    internal static long[] RouteFor(ArenaSimulation w, long x, long z)
    {
        var a = w.Aschbeter;
        return a == null || a.Down ? CourtLayout.Route(w.PlayerXmm, w.PlayerZmm, x, z)
            : CourtLayout.Route(w.PlayerXmm, w.PlayerZmm, x, z, a.Xmm, a.Zmm, AschbeterPassMm);
    }

    internal static bool WalkTo(ArenaSimulation w, long x, long z, int limit, long stopMm = 650)
    {
        long[] route = RouteFor(w, x, z);
        if (route.Length == 0) return false;
        int points = route.Length / 2, index = 0, stuck = 0;
        long lastX = w.PlayerXmm, lastZ = w.PlayerZmm;
        for (int tick = 0; tick < limit; tick++)
        {
            if (DistanceSquared(w.PlayerXmm, w.PlayerZmm, x, z) <= stopMm * stopMm) { w.SetMoveInput(0, 0); return true; }
            // He shuffles after the wanderer: the way is planned round him again every few steps.
            if (tick > 0 && tick % 8 == 0 && w.Aschbeter != null && !w.Aschbeter.Down)
            {
                long[] again = RouteFor(w, x, z);
                if (again.Length > 0) { route = again; points = route.Length / 2; index = 0; }
            }
            while (index < points - 1 && DistanceSquared(w.PlayerXmm, w.PlayerZmm, route[2 * index], route[2 * index + 1]) < 700L * 700L) index++;
            int aim = Math.Min(points - 1, index + (stuck > 2 ? 0 : 2));
            long dx = route[2 * aim] - w.PlayerXmm, dz = route[2 * aim + 1] - w.PlayerZmm;
            long scale = Math.Max(1, Math.Max(Math.Abs(dx), Math.Abs(dz)));
            w.SetMoveInput(dx * 10000 / scale, dz * 10000 / scale);
            w.Step();
            stuck = w.PlayerXmm == lastX && w.PlayerZmm == lastZ ? stuck + 1 : 0;
            lastX = w.PlayerXmm; lastZ = w.PlayerZmm;
        }
        w.SetMoveInput(0, 0);
        return false;
    }

    /// <summary>The save's FNV-1a seal over a deliberately altered payload, so only the altered field can make a load fail.</summary>
    private static string Resign(string payload)
    {
        ulong hash = 14695981039346656037UL;
        unchecked { foreach (byte value in Encoding.UTF8.GetBytes(payload)) { hash ^= value; hash *= 1099511628211UL; } }
        return payload + "|" + hash.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static long DistanceSquared(long ax, long az, long bx, long bz) { long dx = ax - bx, dz = az - bz; return dx * dx + dz * dz; }
}
