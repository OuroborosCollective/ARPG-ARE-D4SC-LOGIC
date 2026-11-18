using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>Theme of a dungeon depth; the theme only re-skins rooms, the network stays Core geometry.</summary>
    public enum DungeonTheme { Crypt = 1, Mine = 2, Catacomb = 3 }

    /// <summary>Special rooms beyond ordinary fights: a shrine, a trap room and a treasure room.</summary>
    public enum DungeonRoomKind { Ordinary = 0, Shrine = 1, Trap = 2, Treasure = 3 }

    /// <summary>
    /// Revision 2 dungeon blueprint: a room network with loops and a guaranteed solvable path
    /// entrance -> key -> gate -> end room. The gate is the end room's only connection, so removing it
    /// always disconnects the end room and the key room stays reachable without crossing it. The graph is
    /// deterministic per seed and depth; revision 1 (CryptDungeonLayout) stays readable for old saves.
    /// </summary>
    public sealed class DungeonBlueprint
    {
        public const int Revision = 2;
        public int RevisionValue { get { return Revision; } }
        public int Depth { get; internal set; }
        public DungeonTheme Theme { get; internal set; }
        public int RoomCount { get; internal set; }
        public int Entrance { get; internal set; }
        public int KeyRoom { get; internal set; }
        public int EndRoom { get; internal set; }
        public int RiskBp { get; internal set; }
        public int RewardBp { get; internal set; }
        public int GateA { get; internal set; }
        public int GateB { get; internal set; }
        internal readonly List<(int X, int Z)> Rooms = new List<(int, int)>();
        internal readonly List<(int A, int B)> Connections = new List<(int, int)>();
        private readonly Dictionary<int, DungeonRoomKind> _kinds = new Dictionary<int, DungeonRoomKind>();

        public DungeonRoomKind KindOf(int room) { DungeonRoomKind k; return _kinds.TryGetValue(room, out k) ? k : DungeonRoomKind.Ordinary; }
        internal void Mark(int room, DungeonRoomKind kind) { _kinds[room] = kind; }
        public int SpecialRoomCount { get { return _kinds.Count; } }

        public IReadOnlyList<(int A, int B)> Edges { get { return Connections; } }
        public bool IsGate(int a, int b) { return (a == GateA && b == GateB) || (a == GateB && b == GateA); }

        /// <summary>Canonical, deterministic signature of the whole network (used to prove seed stability).</summary>
        public string Signature()
        {
            var edges = Connections.OrderBy(e => e.A).ThenBy(e => e.B)
                .Select(e => e.A.ToString(CultureInfo.InvariantCulture) + "-" + e.B.ToString(CultureInfo.InvariantCulture));
            var rooms = Rooms.Select(r => r.X.ToString(CultureInfo.InvariantCulture) + "," + r.Z.ToString(CultureInfo.InvariantCulture));
            return string.Join(";", new[]
            {
                Revision.ToString(CultureInfo.InvariantCulture), Depth.ToString(CultureInfo.InvariantCulture),
                ((int)Theme).ToString(CultureInfo.InvariantCulture), RoomCount.ToString(CultureInfo.InvariantCulture),
                Entrance.ToString(CultureInfo.InvariantCulture), KeyRoom.ToString(CultureInfo.InvariantCulture),
                EndRoom.ToString(CultureInfo.InvariantCulture), GateA.ToString(CultureInfo.InvariantCulture),
                GateB.ToString(CultureInfo.InvariantCulture), RiskBp.ToString(CultureInfo.InvariantCulture),
                RewardBp.ToString(CultureInfo.InvariantCulture)
            }) + "|" + string.Join(";", rooms) + "|" + string.Join(";", edges);
        }
    }

    public static class DungeonBuilderV2
    {
        /// <summary>Build the revision-2 room network for one depth. Same seed and depth give the same layout.</summary>
        public static DungeonBlueprint Build(ulong seed, int depth)
        {
            if (depth < 1 || depth > 3) throw new ArgumentOutOfRangeException(nameof(depth));
            var rng = new DeterministicRng(seed ^ ((ulong)depth * 0x9E3779B97F4A7C15UL));
            int count = 8 + (int)(rng.Next64() % 5UL); // 8..12 rooms: room for entrance, end, key, gate and three special rooms
            var b = new DungeonBlueprint
            {
                Depth = depth,
                Theme = (DungeonTheme)depth,
                RoomCount = count,
                Entrance = 0,
                EndRoom = count - 1,
                RiskBp = 1000 * depth,
                RewardBp = 1500 * depth
            };
            for (int i = 0; i < count; i++) b.Rooms.Add((i % 4, i / 4));

            // A connected spanning tree over the non-end rooms keeps every room reachable from the entrance.
            for (int i = 1; i <= count - 2; i++) b.Connections.Add((i, (int)(rng.Next64() % (ulong)i)));
            // The end room hangs off a single edge: that edge is the gate.
            int gateParent = (int)(rng.Next64() % (ulong)(count - 1));
            b.Connections.Add((b.EndRoom, gateParent));
            b.GateA = b.EndRoom; b.GateB = gateParent;

            // Loops: extra edges among the non-end rooms, never touching the end room, so the gate stays a bridge.
            int loops = (int)(rng.Next64() % 3UL);
            for (int i = 0; i < loops; i++)
            {
                int a = (int)(rng.Next64() % (ulong)(count - 1));
                int c = (int)(rng.Next64() % (ulong)(count - 1));
                if (a == c || HasEdge(b, a, c)) continue;
                b.Connections.Add((a, c));
            }

            // Key room: reachable from the entrance without crossing the gate, never the gate parent itself.
            int keySpan = count - 2; // candidates 1..count-2
            int key = 1 + (int)(rng.Next64() % (ulong)keySpan);
            if (key == gateParent) key = key == count - 2 ? 1 : key + 1;
            b.KeyRoom = key;

            AssignSpecialRooms(b, rng);
            return b;
        }

        /// <summary>Shrine, trap and treasure rooms: distinct, and never the entrance, key, gate parent or end room.</summary>
        private static void AssignSpecialRooms(DungeonBlueprint b, DeterministicRng rng)
        {
            var candidates = Enumerable.Range(1, b.RoomCount - 2)
                .Where(r => r != b.KeyRoom && r != b.GateB).ToList();
            var kinds = new[] { DungeonRoomKind.Shrine, DungeonRoomKind.Trap, DungeonRoomKind.Treasure };
            foreach (var kind in kinds)
            {
                if (candidates.Count == 0) return;
                int pick = (int)(rng.Next64() % (ulong)candidates.Count);
                b.Mark(candidates[pick], kind);
                candidates.RemoveAt(pick);
            }
        }

        private static bool HasEdge(DungeonBlueprint b, int a, int c)
        {
            return b.Connections.Any(e => (e.A == a && e.B == c) || (e.A == c && e.B == a));
        }
    }
}
