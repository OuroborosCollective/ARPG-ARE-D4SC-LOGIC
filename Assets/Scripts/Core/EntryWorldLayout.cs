using System;

namespace AreArpg.Core
{
    /// <summary>Revision-one village footprints in millimetres, shared with the private art composer.</summary>
    public static class EntryWorldLayout
    {
        public readonly struct Footprint
        {
            public readonly string Id;
            public readonly long X, Z, HalfWidth, HalfDepth;
            public Footprint(string id, long x, long z, long halfWidth, long halfDepth)
            { Id = id; X = x; Z = z; HalfWidth = halfWidth; HalfDepth = halfDepth; }
            public bool Contains(long x, long z, long margin)
            { return Math.Abs(x - X) < HalfWidth + margin && Math.Abs(z - Z) < HalfDepth + margin; }
        }

        private static readonly Footprint[] Buildings = {
            new Footprint("supply-hall", -12500, -8700, 2700, 2500),
            new Footprint("watchtower", -14500, -1800, 2050, 2050),
            new Footprint("market-west", -9200, -13000, 1600, 1000),
            new Footprint("market-east", -4100, -13000, 1600, 1000),
            new Footprint("home-east-south", 12500, -15500, 3200, 3200),
            new Footprint("home-east-centre", 14500, -3000, 3200, 3200),
            new Footprint("home-east-north", 12500, 11000, 3200, 3200),
            new Footprint("home-west-south", -21500, -20000, 3200, 3200),
            new Footprint("home-west-centre", -22000, -6000, 3200, 3200),
            new Footprint("home-west-north", -21000, 10000, 3200, 3200)
        };
        public static int BuildingCount { get { return Buildings.Length; } }
        public static Footprint Building(int index) { return Buildings[index]; }

        /// <summary>Canonical village collision; vendor physics never decides walkability.</summary>
        public static bool CanStand(long x, long z)
        {
            foreach (var building in Buildings)
                if (building.Contains(x, z, ArenaSimulation.PlayerRadiusMm)) return false;
            return true;
        }

        /// <summary>Integer segment/slab intersection against the same opaque building footprints.</summary>
        public static bool HasSight(long ax, long az, long bx, long bz)
        {
            // Positions are bounded by the existing 27m arena; cross-products fit signed 64 bits.
            foreach (var b in Buildings)
            {
                long left = b.X - b.HalfWidth, right = b.X + b.HalfWidth;
                long bottom = b.Z - b.HalfDepth, top = b.Z + b.HalfDepth;
                if (b.Contains(ax, az, 0) || b.Contains(bx, bz, 0) ||
                    Intersects(ax, az, bx, bz, left, bottom, right, bottom) ||
                    Intersects(ax, az, bx, bz, right, bottom, right, top) ||
                    Intersects(ax, az, bx, bz, right, top, left, top) ||
                    Intersects(ax, az, bx, bz, left, top, left, bottom)) return false;
            }
            return true;
        }

        private static long Cross(long ax, long az, long bx, long bz, long cx, long cz)
        { return (bx - ax) * (cz - az) - (bz - az) * (cx - ax); }
        /// <summary>Closed segments intersect when both straddle and their bounding intervals overlap.</summary>
        private static bool Intersects(long ax, long az, long bx, long bz, long cx, long cz, long dx, long dz)
        {
            if (Math.Max(ax, bx) < Math.Min(cx, dx) || Math.Max(cx, dx) < Math.Min(ax, bx) ||
                Math.Max(az, bz) < Math.Min(cz, dz) || Math.Max(cz, dz) < Math.Min(az, bz)) return false;
            return Math.Sign(Cross(ax, az, bx, bz, cx, cz)) * Math.Sign(Cross(ax, az, bx, bz, dx, dz)) <= 0 &&
                Math.Sign(Cross(cx, cz, dx, dz, ax, az)) * Math.Sign(Cross(cx, cz, dx, dz, bx, bz)) <= 0;
        }
    }
}
