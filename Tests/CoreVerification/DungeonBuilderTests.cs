using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;

/// <summary>Dungeon Builder v2: room network with loops, a guaranteed entrance -> key -> gate -> end path,
/// themes per depth and depth modifiers. 10,000 seeds must all be solvable with no unreachable room.</summary>
internal static class DungeonBuilderTests
{
    private static HashSet<int> Reachable(DungeonBlueprint b, int start, Func<int, int, bool> passable)
    {
        var seen = new HashSet<int> { start };
        var queue = new Queue<int>();
        queue.Enqueue(start);
        while (queue.Count != 0)
        {
            int room = queue.Dequeue();
            foreach (var edge in b.Edges)
            {
                int other = edge.Item1 == room ? edge.Item2 : edge.Item2 == room ? edge.Item1 : -1;
                if (other < 0 || seen.Contains(other) || !passable(room, other)) continue;
                seen.Add(other);
                queue.Enqueue(other);
            }
        }
        return seen;
    }

    private static bool IsEdge(DungeonBlueprint b, int a, int c) =>
        b.Edges.Any(e => (e.Item1 == a && e.Item2 == c) || (e.Item1 == c && e.Item2 == a));

    public static void Run(Action<bool, string> require)
    {
        var sample = DungeonBuilderV2.Build(12345UL, 1);
        require(sample.RevisionValue == 2, "dungeon-v2-revision");
        require(sample.RoomCount >= 8 && sample.RoomCount <= 12, "dungeon-v2-room-count");
        require(sample.Entrance == 0, "dungeon-v2-entrance");
        require(sample.KeyRoom != sample.Entrance && sample.EndRoom != sample.Entrance &&
            sample.KeyRoom != sample.EndRoom, "dungeon-v2-distinct-key-end");
        require(sample.Theme == DungeonTheme.Crypt, "dungeon-v2-theme-depth1");
        require(DungeonBuilderV2.Build(12345UL, 2).Theme == DungeonTheme.Mine, "dungeon-v2-theme-depth2");
        require(DungeonBuilderV2.Build(12345UL, 3).Theme == DungeonTheme.Catacomb, "dungeon-v2-theme-depth3");

        // Same seed yields the same layout; a different seed yields a different one.
        require(DungeonBuilderV2.Build(777UL, 2).Signature() == DungeonBuilderV2.Build(777UL, 2).Signature(),
            "dungeon-v2-seed-deterministic");
        require(DungeonBuilderV2.Build(777UL, 2).Signature() != DungeonBuilderV2.Build(778UL, 2).Signature(),
            "dungeon-v2-different-seed-different-layout");

        // 10,000 seeds: every room reachable, and the key/gate/end path is guaranteed.
        for (ulong seed = 0; seed < 10000UL; seed++)
        {
            int depth = 1 + (int)(seed % 3);
            var b = DungeonBuilderV2.Build(seed, depth);
            if (b.RoomCount < 6 || b.RoomCount > 12) { require(false, "dungeon-v2-rooms-" + seed); return; }
            // All rooms reachable from the entrance with every door open.
            var all = Reachable(b, b.Entrance, (x, y) => true);
            if (all.Count != b.RoomCount) { require(false, "dungeon-v2-unreachable-room-" + seed); return; }
            // The gate is a bridge: without the gate, the end room is unreachable.
            var withoutGate = Reachable(b, b.Entrance, (x, y) => !b.IsGate(x, y));
            if (withoutGate.Contains(b.EndRoom)) { require(false, "dungeon-v2-gate-bypassed-" + seed); return; }
            if (!withoutGate.Contains(b.KeyRoom)) { require(false, "dungeon-v2-key-before-gate-" + seed); return; }
            if (IsEdge(b, b.KeyRoom, b.EndRoom) && b.IsGate(b.KeyRoom, b.EndRoom)) { require(false, "dungeon-v2-key-not-end-" + seed); return; }
            // With the gate open, the end room is reachable.
            if (!all.Contains(b.EndRoom)) { require(false, "dungeon-v2-end-unreachable-" + seed); return; }
        }
        require(true, "dungeon-v2-10000-seeds-solvable");

        // Depth modifiers: deeper floors carry more risk and reward.
        require(DungeonBuilderV2.Build(1UL, 3).RiskBp > DungeonBuilderV2.Build(1UL, 1).RiskBp,
            "dungeon-v2-deeper-risk");
        require(DungeonBuilderV2.Build(1UL, 3).RewardBp > DungeonBuilderV2.Build(1UL, 1).RewardBp,
            "dungeon-v2-deeper-reward");

        // Special rooms: a shrine, a trap and a treasure room, never the entrance, key, gate parent or end.
        int shrine = 0, trap = 0, treasure = 0;
        for (ulong seed = 0; seed < 10000UL; seed++)
        {
            var b = DungeonBuilderV2.Build(seed, 1);
            for (int r = 0; r < b.RoomCount; r++)
            {
                var k = b.KindOf(r);
                if (k == DungeonRoomKind.Ordinary) continue;
                if (r == b.Entrance || r == b.KeyRoom || r == b.EndRoom || r == b.GateB) { require(false, "dungeon-v2-special-role-clash-" + seed); return; }
                if (k == DungeonRoomKind.Shrine) shrine++;
                else if (k == DungeonRoomKind.Trap) trap++;
                else if (k == DungeonRoomKind.Treasure) treasure++;
            }
        }
        require(shrine == 10000 && trap == 10000 && treasure == 10000, "dungeon-v2-special-rooms-each-depth");

        // Revision 1 stays readable for old saves: the original layout still builds and is walkable.
        var legacy = new CryptDungeonLayout(4242UL, 1);
        require(legacy.RoomCount >= 5 && legacy.CanStand(0, -20000), "dungeon-v2-revision1-preserved");
        require(CryptDungeonLayout.CellMm == 4000 && legacy.Depth == 1, "dungeon-v2-revision1-constants");
    }
}
