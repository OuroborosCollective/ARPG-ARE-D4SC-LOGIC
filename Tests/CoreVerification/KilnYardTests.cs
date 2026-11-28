using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using AreArpg.Core;

/// <summary>
/// Owner decision 2026-11-25: a side gate in the east wall of Aschenwacht's forecourt leads into the Brennerhof. It is
/// optional, nothing fights there, and the wanderer returns through the same gate. The yard has eight vases that break once,
/// the gate of the kiln passage that can only be unbolted from the passage side (the puzzle), the hidden Scherbenschlupf
/// (the secret), and two weapons as fixed finds: the spear at the weapon bench, the halberd in the Scherbenschlupf.
/// </summary>
internal static class KilnYardTests
{
    public static void Run(Action<bool, string> check)
    {
        LayoutIsSound(check);
        var court = Court(93);
        check(court.InCourt && !court.InKilnYard && !court.AtKilnSideGate, "kiln-court-starts-at-the-portal");
        check(!court.TryGetRegionalRelicSource(RegionalRelic.B_Brennerspeer, out _, out _), "kiln-finds-not-in-the-court");

        // Through the side gate in the forecourt's east wall, on foot.
        check(CourtTests.WalkTo(court, SideGateStand[0], SideGateStand[1], 900) && court.AtKilnSideGate && court.AtWorldPassage,
            "kiln-side-gate-reachable-on-foot");
        string courtSave = court.Progression.SaveString();
        var yard = court.TryTraversePassage();
        check(yard != null && yard.InKilnYard && !yard.InCourt && yard.Progression.EntryAct == 1 && !yard.IsVillage, "kiln-side-gate-leads-into-the-yard");
        check(court.Progression.SaveString() == courtSave && !court.Progression.InKilnYard, "kiln-transition-does-not-mutate-the-court");
        check(yard.PlayerXmm == KilnLayout.SpawnXmm && yard.PlayerZmm == KilnLayout.SpawnZmm && yard.ChapterName == KilnYard.Title,
            "kiln-wanderer-stands-inside-the-south-gate");
        check(yard.Monsters.Count == 0 && yard.Npcs.Count == 0 && yard.GroundLoot.Count == 0, "kiln-nothing-fights-here");
        check(yard.Progression.KilnState == (KilnYard.InsideBit | KilnYard.VisitedBit) && !yard.AtWorldPassage && yard.TryTraversePassage() == null,
            "kiln-arrival-state-and-no-instant-return");

        check(yard.TryLeaveKilnYard() == null, "kiln-direct-exit-requires-the-south-gate");

        // Save and load in the yard: ",b" plus the yard's state after the court's traces; malformed states are refused.
        string save = yard.Progression.SaveString();
        check(save.Contains("|1,1,c,b3"), "kiln-save-names-the-yard");
        check(CharacterProgression.TryLoad(save, out var loaded) && loaded.InKilnYard && loaded.InCourt && loaded.StableHash == yard.Progression.StableHash,
            "kiln-save-reload");
        var resumed = new ArenaSimulation(93, loaded, BigInteger.One, LootCatalog.Version1);
        check(resumed.InKilnYard && resumed.PlayerXmm == KilnLayout.SpawnXmm && resumed.PlayerZmm == KilnLayout.SpawnZmm, "kiln-load-resumes-in-the-yard");
        string payload = save.Substring(0, save.LastIndexOf('|'));
        check(CharacterProgression.TryLoad(Resign(payload), out _), "kiln-resigned-fixture-loads");
        foreach (string bad in new[] { "1,1,c,b0", "1,1,c,b01", "1,1,c,b1", "1,1,c,b5", "1,1,c,b2048", "1,1,c,bx", "1,1,c,", "1,1,c,c3", "1,1,c,b", "1,1,c,b3,b3" })
            check(!CharacterProgression.TryLoad(Resign(payload.Replace("1,1,c,b3", bad)), out _), "kiln-rejects-token-" + bad);
        check(!CharacterProgression.TryLoad(Resign(payload.Replace("|1,1,c,b3", "|1,2,b3")), out _), "kiln-rejects-yard-outside-aschenwacht");

        // The spear lies at the weapon bench.
        check(yard.TryGetRegionalRelicSource(RegionalRelic.B_Brennerspeer, out long sx, out long sz) && sx == KilnYard.SpearXmm && sz == KilnYard.SpearZmm,
            "kiln-spear-lies-at-the-bench");
        check(WalkTo(yard, KilnYard.SpearXmm, KilnYard.SpearZmm, 500, 400) && yard.CanClaimRegionalRelic, "kiln-spear-reachable");
        var spear = yard.ActOnHunger(HungerAction.ClaimRegionalRelic);
        check(spear.Accepted && yard.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.B_Brennerspeer) && KilnYard.FoundCount(yard.Progression) == 1,
            "kiln-spear-taken");
        check(RegionalRelics.HandDefinition(RegionalRelic.B_Brennerspeer).Family == WeaponFamily.Spear &&
            RegionalRelics.HandDefinition(RegionalRelic.B_Scherbenhellebarde).TwoHanded, "kiln-weapons-are-polearms");

        // The puzzle: from the Topfhof the gate is bolted; the bolt sits on the passage side.
        // Through the open gate the kiln passage is a few steps from the Topfhof; shut, the way leads all round the kilns.
        int through = Way(true, 0, KilnYard.GateZmm - 1500, 0, KilnYard.GateZmm + 1500).Count, round = Way(false, 0, KilnYard.GateZmm - 1500, 0, KilnYard.GateZmm + 1500).Count;
        check(KilnYard.InGate(KilnYard.GateXmm, KilnYard.GateZmm) && !Free(false, KilnYard.GateXmm, KilnYard.GateZmm) && through > 0 && through < 20 && round > 4 * through,
            "kiln-gate-closes-the-passage");
        check(WalkTo(yard, LockedStand[0], LockedStand[1], 500, 650) && yard.NearKilnGate, "kiln-gate-reachable-from-the-topfhof");
        var locked = yard.ActOnHunger(HungerAction.OpenKilnGate);
        check(!locked.Accepted && locked.Text == KilnYard.GateLocked && !yard.Progression.KilnGateOpen, "kiln-gate-bolted-from-the-other-side");
        check(!WalkTo(yard, LatchStand[0], LatchStand[1], 60, 300, true) && yard.PlayerZmm < KilnYard.GateZmm, "kiln-gate-cannot-be-walked-through");

        // This reachable cell is within arm's reach of the halberd, across the west kiln wall.
        check(Way(false, KilnLayout.SpawnXmm, KilnLayout.SpawnZmm, -1375, 20375).Count > 0, "kiln-opposite-wall-reachable");
        typeof(ArenaSimulation).GetProperty(nameof(resumed.PlayerXmm)).SetValue(resumed, -1375L);
        typeof(ArenaSimulation).GetProperty(nameof(resumed.PlayerZmm)).SetValue(resumed, 20375L);
        check(!resumed.ActOnHunger(HungerAction.ClaimRegionalRelic).Accepted && !resumed.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.B_Scherbenhellebarde),
            "kiln-wall-blocks-taking-the-hidden-halberd");

        // The secret: the Scherbenschlupf behind the west kiln, where the halberd lies, and on into the kiln passage.
        check(WalkTo(yard, KilnYard.HalberdXmm, KilnYard.HalberdZmm, 900, 400) && yard.CanClaimRegionalRelic, "kiln-scherbenschlupf-reachable");
        var halberd = yard.ActOnHunger(HungerAction.ClaimRegionalRelic);
        check(halberd.Accepted && yard.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.B_Scherbenhellebarde) && KilnYard.FoundCount(yard.Progression) == 2,
            "kiln-halberd-taken");
        check(WalkTo(yard, LatchStand[0], LatchStand[1], 600, 650) && yard.NearKilnGate && KilnYard.OnLatchSide(yard.PlayerXmm, yard.PlayerZmm),
            "kiln-gate-reachable-from-the-passage-side");
        var opened = yard.ActOnHunger(HungerAction.OpenKilnGate);
        check(opened.Accepted && opened.Text == KilnYard.GateOpened && yard.Progression.KilnGateOpen && !yard.NearKilnGate, "kiln-gate-unbolted");
        check(!yard.ActOnHunger(HungerAction.OpenKilnGate).Accepted, "kiln-gate-opens-once");
        check(WalkTo(yard, LockedStand[0], LockedStand[1], 60, 650), "kiln-gate-is-a-way-once-open");

        var selection = new ArenaSimulation(93, loaded, BigInteger.One, LootCatalog.Version1);
        typeof(ArenaSimulation).GetProperty(nameof(selection.PlayerXmm)).SetValue(selection, -3950L);
        typeof(ArenaSimulation).GetProperty(nameof(selection.PlayerZmm)).SetValue(selection, -5700L);
        check(selection.NearKilnVase?.Id == 0 && selection.CanBreakWorldVase(1), "world-tap-two-vases-reachable");
        check(selection.BreakWorldVase(1).Accepted && selection.Progression.KilnVaseBroken(1) &&
            !selection.Progression.KilnVaseBroken(0), "world-tap-breaks-selected-vase-not-nearest");
        check(!selection.BreakWorldVase(1).Accepted && !selection.CanBreakWorldVase(1), "world-tap-vase-does-not-repeat");
        check(CharacterProgression.TryLoad(selection.Progression.SaveString(), out var selectionReload) &&
            selectionReload.KilnVaseBroken(1) && !selectionReload.KilnVaseBroken(0), "world-tap-vase-reloads-selected-bit");

        // The vases: the story action breaks the nearest whole one; each breaks once and stays broken.
        int vaseBits = KilnYard.AllState & ~((1 << KilnYard.VaseShift) - 1), broken = 0;
        for (int pass = 0; pass < 3 && broken != vaseBits; pass++)
            foreach (KilnVase vase in KilnYard.Vases)
            {
                if (yard.Progression.KilnVaseBroken(vase.Id)) continue;
                long[] stand = NearVase(vase);
                check(stand.Length == 2 && WalkTo(yard, stand[0], stand[1], 900, 650), "kiln-vase-reachable-" + vase.Id);
                var near = yard.NearKilnVase;
                if (!near.HasValue) continue;
                int before = yard.Progression.KilnState, bit = 1 << (KilnYard.VaseShift + near.Value.Id);
                var result = yard.ActOnHunger(HungerAction.BreakKilnVase);
                check(result.Accepted && result.Text == KilnYard.VaseBroken && (before & bit) == 0 && yard.Progression.KilnState == (before | bit),
                    "kiln-vase-breaks-once-" + near.Value.Id);
                broken |= bit;
                check(!yard.NearKilnVase.HasValue || !yard.Progression.KilnVaseBroken(yard.NearKilnVase.Value.Id), "kiln-vase-stays-broken-" + near.Value.Id);
            }
        check(broken == vaseBits, "kiln-every-vase-breaks");
        check(yard.Progression.KilnState == KilnYard.AllState, "kiln-every-bit-set");
        // With the weapons carried the save wraps the base token (AC16): the state is checked by loading it back.
        check(CharacterProgression.TryLoad(yard.Progression.SaveString(), out var full), "kiln-full-save-loads");
        check(full != null && full.KilnState == KilnYard.AllState, "kiln-full-state-survives-the-save");

        // Back through the south gate: the forecourt at the side gate, the yard remembered.
        check(WalkTo(yard, KilnLayout.ExitXmm, KilnLayout.ExitZmm, 900, 400) && yard.AtWorldPassage, "kiln-south-gate-reachable");
        var back = yard.TryTraversePassage();
        check(back != null && back.InCourt && !back.InKilnYard && back.PlayerXmm == KilnYard.CourtReturnXmm && back.PlayerZmm == KilnYard.CourtReturnZmm,
            "kiln-south-gate-leads-back-to-the-side-gate");
        check(back.Progression.KilnState == (KilnYard.AllState & ~KilnYard.InsideBit) && !back.AtWorldPassage, "kiln-yard-remembered-outside");
        check(CharacterProgression.TryLoad(back.Progression.SaveString(), out var outside) &&
            new ArenaSimulation(93, outside, BigInteger.One, LootCatalog.Version1).PlayerXmm == KilnYard.CourtReturnXmm, "kiln-court-resumes-at-the-side-gate");
        check(!back.TryGetRegionalRelicSource(RegionalRelic.B_Brennerspeer, out _, out _) && back.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.B_Scherbenhellebarde),
            "kiln-finds-carried-out");
        check(back.TrySetRegionalRelicEquipped(RegionalRelic.B_Scherbenhellebarde, true) && back.Progression.Equipment.Weapon != null &&
            back.Progression.Equipment.Weapon.Definition.Name == "Scherbenhellebarde", "kiln-halberd-can-be-wielded");

        // Once more inside: the gate stays open and the vases stay broken.
        check(CourtTests.WalkTo(back, SideGateStand[0], SideGateStand[1], 200) && back.AtKilnSideGate, "kiln-side-gate-again");
        var again = back.TryTraversePassage();
        check(again != null && again.InKilnYard && again.Progression.KilnGateOpen && again.Progression.KilnVaseBroken(7), "kiln-yard-unchanged-on-return");

        // On to the village: the yard belongs to Aschenwacht and is forgotten there; the weapons stay.
        var leave = again;
        check(WalkTo(leave, KilnLayout.ExitXmm, KilnLayout.ExitZmm, 900, 400), "kiln-leave-again");
        var forecourt = leave.TryTraversePassage();
        check(forecourt != null && CourtTests.WalkTo(forecourt, CourtLayout.ExitXmm, CourtLayout.ExitZmm, 1500, 400) && forecourt.AtWorldPassage &&
            !forecourt.AtKilnSideGate, "kiln-bell-tower-door-still-leads-on");
        var village = forecourt.TryTraversePassage();
        check(village != null && village.IsVillage && village.Progression.KilnState == 0 && village.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.B_Brennerspeer),
            "kiln-village-forgets-the-yard-keeps-the-finds");
        check(CharacterProgression.TryLoad(village.Progression.SaveString(), out var settled) && settled.EntryAct == 2 &&
            settled.Equipment.IsRegionalRelicEquipped(RegionalRelic.B_Scherbenhellebarde), "kiln-village-save-keeps-the-wielded-halberd");

        // The same walk twice gives the same world.
        var twinCourt = Court(93);
        check(CourtTests.WalkTo(twinCourt, SideGateStand[0], SideGateStand[1], 900), "kiln-twin-reaches-the-gate");
        var twin = twinCourt.TryTraversePassage();
        var first = new ArenaSimulation(93, LoadOrThrow(save), BigInteger.One, LootCatalog.Version1);
        check(twin != null && WalkTo(twin, LockedStand[0], LockedStand[1], 500, 650) && WalkTo(first, LockedStand[0], LockedStand[1], 500, 650) &&
            twin.PlayerXmm == first.PlayerXmm && twin.PlayerZmm == first.PlayerZmm, "kiln-walk-deterministic");
    }

    /// <summary>Where the wanderer stands to use the side gate: the free cell of the forecourt nearest to it.</summary>
    internal static readonly long[] SideGateStand = { 39375L, KilnYard.SideGateZmm };
    /// <summary>In front of the gate on the Topfhof side, and behind it in the kiln passage at the bolt.</summary>
    private static readonly long[] LockedStand = { KilnYard.GateXmm, KilnYard.LockedMaxZmm - 900 };
    private static readonly long[] LatchStand = { KilnYard.GateXmm, KilnYard.LatchMinZmm + 1200 };

    private static void LayoutIsSound(Action<bool, string> check)
    {
        check(KilnLayout.CanStand(KilnLayout.SpawnXmm, KilnLayout.SpawnZmm) && KilnLayout.CanStand(KilnLayout.ExitXmm, KilnLayout.ExitZmm),
            "kiln-layout-spawn-and-exit-free");
        check(DistanceSquared(KilnLayout.SpawnXmm, KilnLayout.SpawnZmm, KilnLayout.ExitXmm, KilnLayout.ExitZmm) >
            (ArenaSimulation.PassageRadiusMm + 600) * (ArenaSimulation.PassageRadiusMm + 600), "kiln-layout-spawn-outside-the-passage");
        check(CourtLayout.CanStand(SideGateStand[0], SideGateStand[1]) &&
            DistanceSquared(SideGateStand[0], SideGateStand[1], KilnYard.SideGateXmm, KilnYard.SideGateZmm) < ArenaSimulation.PassageRadiusMm * ArenaSimulation.PassageRadiusMm &&
            CourtLayout.Route(CourtLayout.SpawnXmm, CourtLayout.SpawnZmm, SideGateStand[0], SideGateStand[1]).Length > 0, "kiln-layout-side-gate-in-the-forecourt");
        check(CourtLayout.CanStand(KilnYard.CourtReturnXmm, KilnYard.CourtReturnZmm) &&
            DistanceSquared(KilnYard.CourtReturnXmm, KilnYard.CourtReturnZmm, KilnYard.SideGateXmm, KilnYard.SideGateZmm) >
            (ArenaSimulation.PassageRadiusMm + 300) * (ArenaSimulation.PassageRadiusMm + 300), "kiln-layout-court-return-outside-the-passage");
        check(KilnLayout.CanStand(KilnYard.SpearXmm, KilnYard.SpearZmm) && KilnLayout.CanStand(KilnYard.HalberdXmm, KilnYard.HalberdZmm),
            "kiln-layout-finds-on-free-cells");
        check(KilnLayout.CanStand(LockedStand[0], LockedStand[1]) && KilnLayout.CanStand(LatchStand[0], LatchStand[1]) &&
            KilnLayout.CanStand(KilnYard.GateXmm, KilnYard.GateZmm), "kiln-layout-passage-open-in-the-map");
        // With the gate bolted the halberd and the bolt are still reached, through the west alley and the Scherbenschlupf.
        check(OpenWay(null, false, KilnLayout.SpawnXmm, KilnLayout.SpawnZmm, KilnYard.HalberdXmm, KilnYard.HalberdZmm) &&
            OpenWay(null, false, KilnLayout.SpawnXmm, KilnLayout.SpawnZmm, LatchStand[0], LatchStand[1]), "kiln-layout-secret-and-bolt-reachable-with-the-gate-shut");
        foreach (KilnVase vase in KilnYard.Vases)
            check(NearVase(vase).Length == 2, "kiln-layout-vase-in-reach-" + vase.Id);
        long south = KilnLayout.HeightMm(0, -15000), upper = KilnLayout.HeightMm(0, 30000);
        check(Math.Abs(south) < 800 && Math.Abs(upper) < 1500, "kiln-layout-heights-plausible");
    }

    internal static ArenaSimulation Court(ulong seed)
    {
        var crypt = new ArenaSimulation(seed, CharacterProgression.CreateEntryProfile(), BigInteger.One, LootCatalog.Version1);
        foreach (var enemy in crypt.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
        crypt.ActivateSkill(3);
        while (crypt.LootOnGround) if (!crypt.TryCollectLoot()) throw new Exception("crypt loot out of reach");
        HunterEncounterTests.Move(crypt, 0, 24000);
        return crypt.TryTraversePassage() ?? throw new Exception("crypt did not open into Aschenwacht");
    }

    private static CharacterProgression LoadOrThrow(string save)
    {
        if (!CharacterProgression.TryLoad(save, out var p)) throw new Exception("yard save refused");
        return p;
    }

    private static bool Free(bool gateOpen, long x, long z) => KilnLayout.CanStand(x, z) && (gateOpen || !KilnYard.InGate(x, z));

    private static long Centre(long v, long min) => v - ((v - min) % KilnLayout.CellMm + KilnLayout.CellMm) % KilnLayout.CellMm + KilnLayout.CellMm / 2;

    /// <summary>
    /// The way over the yard's free cells with the gate as the rules see it (four neighbours, breadth first), as cell centres;
    /// empty when there is none.
    /// </summary>
    private static List<long[]> Way(bool gateOpen, long fromX, long fromZ, long toX, long toZ)
    {
        long sx = Centre(fromX, KilnLayout.MinXmm), sz = Centre(fromZ, KilnLayout.MinZmm), gx = Centre(toX, KilnLayout.MinXmm), gz = Centre(toZ, KilnLayout.MinZmm);
        var way = new List<long[]>();
        if (!Free(gateOpen, sx, sz) || !Free(gateOpen, gx, gz)) return way;
        var previous = new Dictionary<(long, long), (long, long)> { [(sx, sz)] = (sx, sz) };
        var queue = new Queue<(long, long)>();
        queue.Enqueue((sx, sz));
        long[] dx = { 0, 0, KilnLayout.CellMm, -KilnLayout.CellMm }, dz = { KilnLayout.CellMm, -KilnLayout.CellMm, 0, 0 };
        while (queue.Count > 0 && !previous.ContainsKey((gx, gz)))
        {
            var (x, z) = queue.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                var next = (x + dx[k], z + dz[k]);
                if (previous.ContainsKey(next) || !Free(gateOpen, next.Item1, next.Item2)) continue;
                previous[next] = (x, z); queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey((gx, gz))) return way;
        for (var at = (gx, gz); ; at = previous[at]) { way.Add(new[] { at.Item1, at.Item2 }); if (at == (sx, sz)) break; }
        way.Reverse();
        return way;
    }

    private static bool OpenWay(ArenaSimulation w, bool gateOpen, long fromX, long fromZ, long toX, long toZ) =>
        Way(w != null ? w.Progression.KilnGateOpen : gateOpen, fromX, fromZ, toX, toZ).Count > 0;

    /// <summary>
    /// Walk the way with the stick, one tick at a time, as the Aschenwacht checks do (CourtTests.WalkTo), with the gate as the
    /// rules see it. <paramref name="straight"/> pushes straight at the goal instead, to show that a closed gate holds.
    /// </summary>
    private static bool WalkTo(ArenaSimulation w, long x, long z, int limit, long stopMm, bool straight = false)
    {
        var way = straight ? new List<long[]> { new[] { x, z } } : Way(w.Progression.KilnGateOpen, w.PlayerXmm, w.PlayerZmm, x, z);
        if (way.Count == 0) return false;
        int index = 0, stuck = 0;
        long lastX = w.PlayerXmm, lastZ = w.PlayerZmm;
        for (int tick = 0; tick < limit; tick++)
        {
            if (DistanceSquared(w.PlayerXmm, w.PlayerZmm, x, z) <= stopMm * stopMm) { w.SetMoveInput(0, 0); return true; }
            while (index < way.Count - 1 && DistanceSquared(w.PlayerXmm, w.PlayerZmm, way[index][0], way[index][1]) < 700L * 700L) index++;
            int aim = Math.Min(way.Count - 1, index + (stuck > 2 ? 0 : 2));
            long ddx = way[aim][0] - w.PlayerXmm, ddz = way[aim][1] - w.PlayerZmm;
            long scale = Math.Max(1, Math.Max(Math.Abs(ddx), Math.Abs(ddz)));
            w.SetMoveInput(ddx * 10000 / scale, ddz * 10000 / scale);
            w.Step();
            stuck = w.PlayerXmm == lastX && w.PlayerZmm == lastZ ? stuck + 1 : 0;
            lastX = w.PlayerXmm; lastZ = w.PlayerZmm;
        }
        w.SetMoveInput(0, 0);
        return false;
    }

    /// <summary>A free cell within reach of the vase that the spawn reaches with the gate shut, nearest to the vase.</summary>
    private static long[] NearVase(KilnVase vase)
    {
        long reach = KilnYard.VaseReachMm - 700, best = long.MaxValue; long[] found = new long[0]; // a walker stops within 650 mm
        for (long x = vase.Xmm - reach; x <= vase.Xmm + reach; x += KilnLayout.CellMm)
            for (long z = vase.Zmm - reach; z <= vase.Zmm + reach; z += KilnLayout.CellMm)
            {
                long cx = Centre(x, KilnLayout.MinXmm), cz = Centre(z, KilnLayout.MinZmm), d = DistanceSquared(cx, cz, vase.Xmm, vase.Zmm);
                if (d >= best || d > reach * reach || !Free(false, cx, cz)) continue;
                if (KilnYard.Vases.Any(o => o.Id != vase.Id && DistanceSquared(cx, cz, o.Xmm, o.Zmm) <= d)) continue;
                if (Way(false, KilnLayout.SpawnXmm, KilnLayout.SpawnZmm, cx, cz).Count == 0) continue;
                best = d; found = new[] { cx, cz };
            }
        return found;
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
