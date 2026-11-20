using System;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Placement rules for the set dressing of the procedural crypt: which way a wall faces, where the wall line
    /// needs a pillar, which walls carry relief and where bones, candles and rubble lie. Presentation only: the
    /// Core never reads any of this, and the same seed always dresses a floor the same way.
    /// </summary>
    public static class CryptDungeonDressing
    {
        /// <summary>Edge of one dungeon tile in metres (Core cells are 4000 mm).</summary>
        public const float TileMetres = 4f;
        /// <summary>Distance of a prop from the wall line. Props stay at the foot of a wall, off the walked centre.</summary>
        public const float PropInset = .8f;
        /// <summary>Largest sideways shift of a prop along its wall; keeps it clear of the corner pillars.</summary>
        public const float PropSlide = 1.1f;
        /// <summary>Empty marker nodes inside a visual: the game puts its own flame or light there.</summary>
        public const string FlameMarker = "AreFlame", LightMarker = "AreLight";
        /// <summary>Marker for the large flame of a hanging lantern (a room's light source).</summary>
        public const string LanternMarker = "AreLantern";
        /// <summary>Height of a lantern's flame and of the room light inside it, in metres.</summary>
        public const float LanternHeight = 4.3f;
        /// <summary>A shrine decorates a room only when a wall is this close to the room centre, in tiles.</summary>
        public const int ShrineReach = 2;
        /// <summary>Candle flames drawn per floor (a room's lantern always burns); fewer with reduced effects.</summary>
        public const int FlameBudget = 120, ReducedFlameBudget = 24;
        public const int West = 0, East = 1, South = 2, North = 3;

        /// <summary>Unit step from a wall into the tile it encloses.</summary>
        public static Vector3 Inward(int side)
        {
            switch (side)
            {
                case West: return Vector3.right;
                case East: return Vector3.left;
                case South: return Vector3.forward;
                case North: return Vector3.back;
                default: throw new ArgumentOutOfRangeException("side");
            }
        }

        /// <summary>
        /// Walls are built facing +Z. This turn makes the decorated face look into the tile, so relief, niches and
        /// candles never end up on the outside of a room.
        /// </summary>
        public static float WallYaw(int side)
        {
            switch (side)
            {
                case West: return 90f;
                case East: return 270f;
                case South: return 0f;
                case North: return 180f;
                default: throw new ArgumentOutOfRangeException("side");
            }
        }

        /// <summary>Centre of the wall line on one side of a tile, in metres.</summary>
        public static Vector3 WallCentre(int x, int z, int side)
        {
            return new Vector3(x * TileMetres, 0f, z * TileMetres) - Inward(side) * (TileMetres * .5f);
        }

        /// <summary>
        /// Integer mix of tile, purpose and floor seed. No engine random, no string hash: the dressing of a saved
        /// floor is identical on every device and after every reload.
        /// </summary>
        public static uint Hash(int x, int z, int salt, ulong seed)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)(seed >> 32) ^ 0x9E3779B9u;
                h = (h ^ (uint)x) * 0x85EBCA6Bu; h ^= h >> 13;
                h = (h ^ (uint)z) * 0xC2B2AE35u; h ^= h >> 16;
                h = (h ^ (uint)salt) * 0x27D4EB2Fu; h ^= h >> 15;
                return h;
            }
        }

        /// <summary>
        /// 0 is the plain wall. Most walls stay calm so that the decorated ones read as landmarks:
        /// 1 arch niche, 2 arcade, 3 skull relief (rare). A missing variant falls back to the arch niche.
        /// </summary>
        public static int WallVariant(uint hash, int decorated)
        {
            if (decorated <= 0) return 0;
            uint roll = hash % 100u;
            if (roll < 58u) return 0;
            if (roll < 78u) return 1;
            if (roll < 93u) return decorated >= 2 ? 2 : 1;
            return decorated >= 3 ? 3 : 1;
        }

        /// <summary>
        /// A pillar stands where the wall line turns. The four flags are the tiles around one grid corner
        /// (south-west, south-east, north-west, north-east): one or three floors make a corner, and so do two
        /// floors that only touch diagonally. A straight wall and open floor get none.
        /// </summary>
        public static bool CornerPillar(bool southWest, bool southEast, bool northWest, bool northEast)
        {
            int floors = (southWest ? 1 : 0) + (southEast ? 1 : 0) + (northWest ? 1 : 0) + (northEast ? 1 : 0);
            if (floors == 1 || floors == 3) return true;
            return floors == 2 && southWest == northEast;
        }

        /// <summary>Which prop lies at a wall: about three walls in ten carry one, -1 means none.</summary>
        public static int PropIndex(uint hash, int propCount)
        {
            if (propCount <= 0 || hash % 100u >= 30u) return -1;
            return (int)((hash / 100u) % (uint)propCount);
        }

        /// <summary>Position of a prop relative to its tile centre: at the foot of the wall, slid along it.</summary>
        public static Vector3 PropOffset(int side, uint hash)
        {
            Vector3 inward = Inward(side);
            var along = new Vector3(inward.z, 0f, inward.x);
            float slide = ((hash >> 8) % 1000u) / 999f * 2f * PropSlide - PropSlide;
            return -inward * (TileMetres * .5f - PropInset) + along * slide;
        }

        /// <summary>Props face the room, turned by up to 30 degrees so that repeats do not line up.</summary>
        public static float PropYaw(int side, uint hash)
        {
            return WallYaw(side) + ((hash >> 18) % 61u) - 30f;
        }

        /// <summary>
        /// Wall for a room's shrine: the nearest wall straight west, east, south or north of the room centre.
        /// Returns the tile the shrine stands in and the side of that tile the wall is on.
        /// </summary>
        public static bool ShrineSpot(Func<int, int, bool> isFloor, int x, int z, out int tileX, out int tileZ, out int side)
        {
            tileX = x; tileZ = z; side = West;
            if (isFloor == null || !isFloor(x, z)) return false;
            int best = int.MaxValue;
            for (int candidate = West; candidate <= North; candidate++)
            {
                Vector3 outward = -Inward(candidate);
                int stepX = Mathf.RoundToInt(outward.x), stepZ = Mathf.RoundToInt(outward.z);
                int cx = x, cz = z, steps = 0;
                while (steps < 13 && isFloor(cx + stepX, cz + stepZ)) { cx += stepX; cz += stepZ; steps++; }
                if (steps >= 13 || steps >= best) continue;
                best = steps; tileX = cx; tileZ = cz; side = candidate;
            }
            return best != int.MaxValue;
        }
    }
}
