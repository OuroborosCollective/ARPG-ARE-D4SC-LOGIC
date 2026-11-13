using System;

namespace AreArpg.Core
{
    /// <summary>Authored four-metre cells: five rooms, side branches and a western loop. Shared by Core and rendering.</summary>
    public static class CryptDungeonLayout
    {
        public const int CellMm = 4000, MinCell = -6, MaxCell = 6;

        public static bool IsFloor(int depth, int x, int z)
        {
            if (depth < 1 || depth > 3 || x < -6 || x > 6 || z < -6 || z > 6) return false;
            if (depth == 2) x = -x;
            return (x >= -1 && x <= 1 && z >= -6 && z <= -4) || // entrance
                (x >= -1 && x <= 1 && z >= -2 && z <= 0) || // junction
                (x >= -5 && x <= -3 && z >= -2 && z <= 0) || // western chamber
                (x >= 3 && x <= 5 && z >= 0 && z <= 2) || // eastern chamber
                (x >= -2 && x <= 2 && z >= 3 && z <= 5) || // northern hall
                (x == 0 && (z == -3 || z == 1 || z == 2 || z == 6)) ||
                (z == -1 && x >= -3 && x <= -1) ||
                (z == 1 && x >= 0 && x <= 3) ||
                (x == -4 && z >= 0 && z <= 3) ||
                (z == 3 && x >= -4 && x <= -2);
        }

        private static bool Inside(int depth, long x, long z)
        {
            if (x < -26000 || x >= 26000 || z < -26000 || z >= 26000) return false;
            return IsFloor(depth, (int)((x + 26000) / CellMm) - 6, (int)((z + 26000) / CellMm) - 6);
        }

        public static bool CanStand(int depth, long x, long z)
        {
            if (x < -25000 || x > 25000 || z < -25000 || z > 25000) return false;
            long r = ArenaSimulation.PlayerRadiusMm;
            return Inside(depth, x-r, z-r) && Inside(depth, x+r, z-r) && Inside(depth, x-r, z+r) && Inside(depth, x+r, z+r);
        }

        public static bool HasSight(int depth, long ax, long az, long bx, long bz)
        {
            if (!Inside(depth, ax, az) || !Inside(depth, bx, bz)) return false;
            long steps = Math.Max(Math.Abs(bx-ax), Math.Abs(bz-az)) / 250 + 1;
            for (long i=0; i<=steps; i++)
                if (!Inside(depth, ax+(bx-ax)*i/steps, az+(bz-az)*i/steps)) return false;
            return true;
        }

        public static void Chest(int depth, int index, out long x, out long z)
        {
            if (index < 0 || index > 1) throw new ArgumentOutOfRangeException(nameof(index));
            x = (index == 0 ? -18000 : 18000) * (depth == 2 ? -1 : 1); z = index == 0 ? 0 : 8000;
        }

        public static MonsterState Monster(int depth, int index)
        {
            if (depth < 1 || depth > 3 || index < 0 || index > 3) throw new ArgumentOutOfRangeException();
            long[] xs = { 0, -16000, 16000, 0 }, zs = { -6000, -5000, 5000, 14000 };
            int kind = index == 1 || index == 3 ? 1 : 0;
            long x = xs[index] * (depth == 2 ? -1 : 1), z = zs[index];
            int hp = kind == 0 ? 320 + depth*40 : 480 + depth*50;
            return new MonsterState { Id = 100 + depth*10 + index, Kind = kind, Xmm = x, PreviousXmm = x,
                Zmm = z, PreviousZmm = z, Health = hp, MaxHealth = hp };
        }
    }
}
