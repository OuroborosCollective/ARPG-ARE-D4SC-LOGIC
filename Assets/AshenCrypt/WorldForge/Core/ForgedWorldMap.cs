using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace AshenCrypt.WorldForge.Core
{
    /// <summary>Same order as AshenCrypt.WorldForge.PoiType – the byte value is part of the map format.</summary>
    public enum ForgedPoiKind : byte { Landmark, Settlement, NpcHome, NpcWorkplace, SpawnZone, DungeonEntrance, Waypoint, Shrine, ResourceNode }

    [Flags]
    public enum ForgedObstacleFlags : byte { None = 0, BlocksMovement = 1, BlocksSight = 2 }

    public readonly struct ForgedObstacle
    {
        public readonly long Xmm;
        public readonly long Zmm;
        public readonly int RadiusMm;
        public readonly ForgedObstacleFlags Flags;

        public ForgedObstacle(long xMm, long zMm, int radiusMm, ForgedObstacleFlags flags)
        {
            Xmm = xMm; Zmm = zMm; RadiusMm = radiusMm; Flags = flags;
        }
    }

    public sealed class ForgedPoiRecord
    {
        public readonly string Id;
        public readonly ForgedPoiKind Kind;
        public readonly long Xmm;
        public readonly long Zmm;
        public readonly int RadiusMm;
        public readonly string Tags;

        public ForgedPoiRecord(string id, ForgedPoiKind kind, long xMm, long zMm, int radiusMm, string tags)
        {
            Id = id ?? ""; Kind = kind; Xmm = xMm; Zmm = zMm; RadiusMm = radiusMm; Tags = tags ?? "";
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            foreach (string t in Tags.Split(','))
                if (string.Equals(t.Trim(), tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public bool Contains(long xMm, long zMm)
        {
            long dx = xMm - Xmm, dz = zMm - Zmm, r = RadiusMm;
            return dx * dx + dz * dz <= r * r;
        }
    }

    /// <summary>
    /// Integer-only, engine-free world map for the authoritative simulation: heights in millimetres,
    /// walkable cells, movement/sight obstacles and points of interest. Produced by the World Forge editor
    /// ("Export Core Map"), stored as a .bytes asset with an FNV-1a 64 checksum and rejected when corrupt.
    /// Query API mirrors AreArpg.Core.WoodlandLayout (CanStand, HeightMm, HasSight). No clocks, floats or RNG.
    /// </summary>
    public sealed class ForgedWorldMap
    {
        public const int FormatVersion = 1;
        private static readonly byte[] Magic = { (byte)'W', (byte)'F', (byte)'M', (byte)'A', (byte)'P' };
        private const int BucketMm = 4000;

        public long OriginXmm { get; }
        public long OriginZmm { get; }
        public int SpacingMm { get; }
        public int NodesX { get; }
        public int NodesZ { get; }
        public int CellsX { get { return NodesX - 1; } }
        public int CellsZ { get { return NodesZ - 1; } }
        public long WidthMm { get { return (long)CellsX * SpacingMm; } }
        public long DepthMm { get { return (long)CellsZ * SpacingMm; } }
        public ulong ContentHash { get; }

        private readonly int[] _heights;
        private readonly byte[] _walk;
        private readonly ForgedObstacle[] _obstacles;
        private readonly ForgedPoiRecord[] _pois;
        private readonly int _bucketsX, _bucketsZ, _maxObstacleRadius;
        private readonly int[][] _buckets;

        public IReadOnlyList<ForgedObstacle> Obstacles { get { return _obstacles; } }
        public IReadOnlyList<ForgedPoiRecord> Pois { get { return _pois; } }

        public ForgedWorldMap(long originXmm, long originZmm, int spacingMm, int nodesX, int nodesZ, int[] heightsMm,
                              bool[] walkableCells, IList<ForgedObstacle> obstacles, IList<ForgedPoiRecord> pois)
            : this(originXmm, originZmm, spacingMm, nodesX, nodesZ, heightsMm, PackBits(walkableCells, nodesX, nodesZ), Copy(obstacles), Copy(pois))
        {
        }

        private ForgedWorldMap(long originXmm, long originZmm, int spacingMm, int nodesX, int nodesZ, int[] heightsMm,
                               byte[] walkBits, ForgedObstacle[] obstacles, ForgedPoiRecord[] pois)
        {
            if (spacingMm <= 0) throw new ArgumentOutOfRangeException(nameof(spacingMm));
            if (nodesX < 2 || nodesZ < 2) throw new ArgumentOutOfRangeException(nameof(nodesX), "At least 2x2 height nodes are required.");
            if (heightsMm == null || heightsMm.Length != nodesX * nodesZ) throw new ArgumentException("Height node count mismatch.", nameof(heightsMm));
            if (walkBits == null || walkBits.Length != ((nodesX - 1) * (nodesZ - 1) + 7) / 8) throw new ArgumentException("Walk bit count mismatch.");
            OriginXmm = originXmm; OriginZmm = originZmm; SpacingMm = spacingMm; NodesX = nodesX; NodesZ = nodesZ;
            _heights = (int[])heightsMm.Clone();
            _walk = (byte[])walkBits.Clone();
            _obstacles = obstacles;
            _pois = pois;

            _bucketsX = (int)((WidthMm + BucketMm - 1) / BucketMm) + 1;
            _bucketsZ = (int)((DepthMm + BucketMm - 1) / BucketMm) + 1;
            var lists = new List<int>[_bucketsX * _bucketsZ];
            for (int i = 0; i < _obstacles.Length; i++)
            {
                ForgedObstacle o = _obstacles[i];
                if (o.RadiusMm > _maxObstacleRadius) _maxObstacleRadius = o.RadiusMm;
                int b = Bucket(o.Xmm, o.Zmm);
                if (lists[b] == null) lists[b] = new List<int>();
                lists[b].Add(i);
            }
            _buckets = new int[lists.Length][];
            for (int i = 0; i < lists.Length; i++) _buckets[i] = lists[i] == null ? Array.Empty<int>() : lists[i].ToArray();
            ContentHash = Fnv1a64(WriteBody());
        }

        // ------------------------------------------------------------------ Queries

        public bool InBounds(long xMm, long zMm)
        {
            long x = xMm - OriginXmm, z = zMm - OriginZmm;
            return x >= 0 && z >= 0 && x <= WidthMm && z <= DepthMm;
        }

        public bool IsWalkableCell(int cx, int cz)
        {
            if (cx < 0 || cz < 0 || cx >= CellsX || cz >= CellsZ) return false;
            int i = cz * CellsX + cx;
            return (_walk[i >> 3] & (1 << (i & 7))) != 0;
        }

        public bool IsWalkablePoint(long xMm, long zMm)
        {
            if (!InBounds(xMm, zMm)) return false;
            long x = xMm - OriginXmm, z = zMm - OriginZmm;
            int cx = (int)Math.Min(CellsX - 1, x / SpacingMm), cz = (int)Math.Min(CellsZ - 1, z / SpacingMm);
            return IsWalkableCell(cx, cz);
        }

        /// <summary>Integer bilinear height (mm) – identical on every platform.</summary>
        public long HeightMm(long xMm, long zMm)
        {
            long s = SpacingMm;
            long x = Clamp(xMm - OriginXmm, 0, WidthMm), z = Clamp(zMm - OriginZmm, 0, DepthMm);
            int ix = (int)Math.Min(CellsX - 1, x / s), iz = (int)Math.Min(CellsZ - 1, z / s);
            long fx = x - ix * s, fz = z - iz * s;
            long h00 = _heights[iz * NodesX + ix], h10 = _heights[iz * NodesX + ix + 1];
            long h01 = _heights[(iz + 1) * NodesX + ix], h11 = _heights[(iz + 1) * NodesX + ix + 1];
            long a = h00 * (s - fx) + h10 * fx;
            long b = h01 * (s - fx) + h11 * fx;
            return FloorDiv(a * (s - fz) + b * fz, s * s);
        }

        /// <summary>True iff the complete circular actor footprint avoids blocked grid-cell interiors and blocking obstacles.</summary>
        public bool CanStand(long xMm, long zMm, long radiusMm)
        {
            if (radiusMm < 0) radiusMm = 0;
            // Avoid signed addition/subtraction overflow before the bounds check.
            long x = xMm - OriginXmm, z = zMm - OriginZmm;
            if (x < 0 || z < 0 || x > WidthMm || z > DepthMm ||
                radiusMm > x || radiusMm > z || radiusMm > WidthMm - x || radiusMm > DepthMm - z)
                return false;
            if (!IsWalkablePoint(xMm, zMm)) return false;
            if (radiusMm > 0)
            {
                // The bounding-square cells are only CANDIDATES: test actual disc-vs-cell overlap.
                int firstX = (int)((x - radiusMm) / SpacingMm);
                int lastX = (int)((x + radiusMm - 1) / SpacingMm);
                int firstZ = (int)((z - radiusMm) / SpacingMm);
                int lastZ = (int)((z + radiusMm - 1) / SpacingMm);
                for (int cz = firstZ; cz <= lastZ; cz++)
                {
                    long z0 = (long)cz * SpacingMm;
                    long z1 = z0 + SpacingMm;
                    long dz = z < z0 ? z0 - z : (z > z1 ? z - z1 : 0);
                    for (int cx = firstX; cx <= lastX; cx++)
                    {
                        if (IsWalkableCell(cx, cz)) continue;
                        long x0 = (long)cx * SpacingMm;
                        long x1 = x0 + SpacingMm;
                        long dx = x < x0 ? x0 - x : (x > x1 ? x - x1 : 0);
                        if (DiscOverlapsCellInterior(dx, dz, radiusMm)) return false;
                    }
                }
            }
            return !HitsObstacle(xMm, zMm, radiusMm, ForgedObstacleFlags.BlocksMovement);
        }

        // Strict inequality: a disc tangent to a blocked cell does not intrude into its interior.
        // BigInteger prevents overflows at large, otherwise valid, world dimensions.
        private static bool DiscOverlapsCellInterior(long dx, long dz, long radiusMm)
        {
            if (dx >= radiusMm || dz >= radiusMm) return false;
            if (radiusMm <= 3037000499L)
                return dx * dx + dz * dz < radiusMm * radiusMm;
            BigInteger r = new BigInteger(radiusMm);
            return (BigInteger)dx * dx + (BigInteger)dz * dz < r * r;
        }

        /// <summary>Line of sight sampled every 250 mm (same step as WoodlandLayout.HasSight).</summary>
        public bool HasSight(long axMm, long azMm, long bxMm, long bzMm)
        {
            long steps = Math.Max(Math.Abs(bxMm - axMm), Math.Abs(bzMm - azMm)) / 250 + 1;
            for (long i = 0; i <= steps; i++)
            {
                long x = axMm + (bxMm - axMm) * i / steps, z = azMm + (bzMm - azMm) * i / steps;
                if (!IsWalkablePoint(x, z)) return false;
                if (i > 0 && i < steps && HitsObstacle(x, z, 0, ForgedObstacleFlags.BlocksSight)) return false;
            }
            return true;
        }

        public ForgedPoiRecord FindPoi(string id)
        {
            foreach (ForgedPoiRecord p in _pois) if (p.Id == id) return p;
            return null;
        }

        public List<ForgedPoiRecord> FindPois(ForgedPoiKind kind)
        {
            var list = new List<ForgedPoiRecord>();
            foreach (ForgedPoiRecord p in _pois) if (p.Kind == kind) list.Add(p);
            return list;
        }

        private bool HitsObstacle(long xMm, long zMm, long radiusMm, ForgedObstacleFlags flag)
        {
            long reach = radiusMm + _maxObstacleRadius;
            int bx0 = BucketCoord(xMm - OriginXmm - reach, _bucketsX), bx1 = BucketCoord(xMm - OriginXmm + reach, _bucketsX);
            int bz0 = BucketCoord(zMm - OriginZmm - reach, _bucketsZ), bz1 = BucketCoord(zMm - OriginZmm + reach, _bucketsZ);
            for (int bz = bz0; bz <= bz1; bz++)
                for (int bx = bx0; bx <= bx1; bx++)
                    foreach (int i in _buckets[bz * _bucketsX + bx])
                    {
                        ForgedObstacle o = _obstacles[i];
                        if ((o.Flags & flag) == 0) continue;
                        long dx = o.Xmm - xMm, dz = o.Zmm - zMm, rr = o.RadiusMm + radiusMm;
                        if (dx * dx + dz * dz < rr * rr) return true;
                    }
            return false;
        }

        private int Bucket(long xMm, long zMm)
        {
            return BucketCoord(zMm - OriginZmm, _bucketsZ) * _bucketsX + BucketCoord(xMm - OriginXmm, _bucketsX);
        }

        private static int BucketCoord(long local, int count)
        {
            long b = FloorDiv(local, BucketMm);
            return (int)(b < 0 ? 0 : (b >= count ? count - 1 : b));
        }

        // ------------------------------------------------------------------ Serialisation

        public byte[] ToBytes()
        {
            byte[] body = WriteBody();
            var result = new byte[body.Length + 8];
            Buffer.BlockCopy(body, 0, result, 0, body.Length);
            ulong h = Fnv1a64(body);
            for (int i = 0; i < 8; i++) result[body.Length + i] = (byte)(h >> (8 * i));
            return result;
        }

        /// <summary>Parses and validates a map. Throws <see cref="FormatException"/> for wrong magic, version, size or checksum.</summary>
        public static ForgedWorldMap FromBytes(byte[] data)
        {
            if (data == null || data.Length < 6 + 8) throw new FormatException("World map data is missing or truncated.");
            int bodyLength = data.Length - 8;
            ulong stored = 0;
            for (int i = 0; i < 8; i++) stored |= (ulong)data[bodyLength + i] << (8 * i);
            if (Fnv1a64(data, bodyLength) != stored) throw new FormatException("World map checksum mismatch – file is corrupt.");
            try
            {
                using (var ms = new MemoryStream(data, 0, bodyLength, false))
                using (var r = new BinaryReader(ms, Encoding.UTF8))
                {
                    byte[] magic = r.ReadBytes(Magic.Length);
                    for (int i = 0; i < Magic.Length; i++)
                        if (magic.Length != Magic.Length || magic[i] != Magic[i]) throw new FormatException("Not a World Forge map (bad magic).");
                    int version = r.ReadByte();
                    if (version != FormatVersion) throw new FormatException("Unsupported World Forge map version " + version + ".");
                    long ox = r.ReadInt64(), oz = r.ReadInt64();
                    int spacing = r.ReadInt32(), nx = r.ReadInt32(), nz = r.ReadInt32();
                    if (spacing <= 0 || nx < 2 || nz < 2 || (long)nx * nz > 64L * 1024 * 1024) throw new FormatException("Invalid map dimensions.");
                    var heights = new int[nx * nz];
                    for (int i = 0; i < heights.Length; i++) heights[i] = r.ReadInt32();
                    byte[] walk = r.ReadBytes(((nx - 1) * (nz - 1) + 7) / 8);
                    int oc = r.ReadInt32();
                    if (oc < 0 || oc > 4 * 1024 * 1024) throw new FormatException("Invalid obstacle count.");
                    var obstacles = new ForgedObstacle[oc];
                    for (int i = 0; i < oc; i++)
                        obstacles[i] = new ForgedObstacle(r.ReadInt64(), r.ReadInt64(), r.ReadInt32(), (ForgedObstacleFlags)r.ReadByte());
                    int pc = r.ReadInt32();
                    if (pc < 0 || pc > 65536) throw new FormatException("Invalid POI count.");
                    var pois = new ForgedPoiRecord[pc];
                    for (int i = 0; i < pc; i++)
                    {
                        string id = ReadString(r);
                        var kind = (ForgedPoiKind)r.ReadByte();
                        long px = r.ReadInt64(), pz = r.ReadInt64();
                        int rad = r.ReadInt32();
                        pois[i] = new ForgedPoiRecord(id, kind, px, pz, rad, ReadString(r));
                    }
                    if (ms.Position != bodyLength) throw new FormatException("Trailing bytes in world map.");
                    return new ForgedWorldMap(ox, oz, spacing, nx, nz, heights, walk, obstacles, pois);
                }
            }
            catch (EndOfStreamException) { throw new FormatException("World map data is truncated."); }
            catch (ArgumentException e) { throw new FormatException("Invalid world map: " + e.Message); }
        }

        private byte[] WriteBody()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(Magic);
                w.Write((byte)FormatVersion);
                w.Write(OriginXmm); w.Write(OriginZmm);
                w.Write(SpacingMm); w.Write(NodesX); w.Write(NodesZ);
                foreach (int h in _heights) w.Write(h);
                w.Write(_walk);
                w.Write(_obstacles.Length);
                foreach (ForgedObstacle o in _obstacles) { w.Write(o.Xmm); w.Write(o.Zmm); w.Write(o.RadiusMm); w.Write((byte)o.Flags); }
                w.Write(_pois.Length);
                foreach (ForgedPoiRecord p in _pois)
                {
                    WriteString(w, p.Id); w.Write((byte)p.Kind); w.Write(p.Xmm); w.Write(p.Zmm); w.Write(p.RadiusMm); WriteString(w, p.Tags);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        private static void WriteString(BinaryWriter w, string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s ?? "");
            w.Write(b.Length);
            w.Write(b);
        }

        private static string ReadString(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > 4096) throw new FormatException("Invalid string length in world map.");
            return Encoding.UTF8.GetString(r.ReadBytes(n));
        }

        // ------------------------------------------------------------------ Helpers

        public static ulong Fnv1a64(byte[] data) { return Fnv1a64(data, data.Length); }

        public static ulong Fnv1a64(byte[] data, int count)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                for (int i = 0; i < count; i++) { h ^= data[i]; h *= 1099511628211UL; }
                return h;
            }
        }

        private static long Clamp(long v, long lo, long hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        private static byte[] PackBits(bool[] cells, int nodesX, int nodesZ)
        {
            int n = (nodesX - 1) * (nodesZ - 1);
            if (cells == null || cells.Length != n) throw new ArgumentException("Walkable cell count mismatch.", nameof(cells));
            var bits = new byte[(n + 7) / 8];
            for (int i = 0; i < n; i++) if (cells[i]) bits[i >> 3] |= (byte)(1 << (i & 7));
            return bits;
        }

        private static ForgedObstacle[] Copy(IList<ForgedObstacle> list)
        {
            if (list == null) return Array.Empty<ForgedObstacle>();
            var a = new ForgedObstacle[list.Count];
            list.CopyTo(a, 0);
            return a;
        }

        private static ForgedPoiRecord[] Copy(IList<ForgedPoiRecord> list)
        {
            if (list == null) return Array.Empty<ForgedPoiRecord>();
            var a = new ForgedPoiRecord[list.Count];
            list.CopyTo(a, 0);
            return a;
        }
    }
}
