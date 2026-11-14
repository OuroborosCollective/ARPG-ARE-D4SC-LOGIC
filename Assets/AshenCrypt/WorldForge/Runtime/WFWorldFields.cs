using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    /// <summary>
    /// Derived analysis fields for texturing and spawning: slope, curvature and path distance.
    /// Built from the final heights (generated or hand-sculpted terrain read back from Unity).
    /// </summary>
    public sealed class WFWorldFields
    {
        public const float PathRange = 64f;

        public readonly WFHeightContext Ctx;
        public readonly float[] SlopeDeg;
        public readonly float[] Curvature;
        public readonly float[] PathDistance;

        public WFWorldFields(WFHeightContext ctx)
        {
            Ctx = ctx;
            int w = ctx.Width, h = ctx.Height, n = w * h;
            float[] hm = ctx.Heights;
            SlopeDeg = new float[n];
            Curvature = new float[n];
            PathDistance = new float[n];
            float sp = ctx.Spacing;
            int k = Math.Max(1, Mathf.RoundToInt(2f / sp));
            for (int z = 0; z < h; z++)
            {
                int zm = Math.Max(0, z - 1), zp = Math.Min(h - 1, z + 1);
                int zk0 = Math.Max(0, z - k), zk1 = Math.Min(h - 1, z + k);
                for (int x = 0; x < w; x++)
                {
                    int xm = Math.Max(0, x - 1), xp = Math.Min(w - 1, x + 1);
                    float gx = (hm[z * w + xp] - hm[z * w + xm]) / ((xp - xm) * sp);
                    float gz = (hm[zp * w + x] - hm[zm * w + x]) / ((zp - zm) * sp);
                    int i = z * w + x;
                    SlopeDeg[i] = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
                    int xk0 = Math.Max(0, x - k), xk1 = Math.Min(w - 1, x + k);
                    float lap = hm[z * w + xk0] + hm[z * w + xk1] + hm[zk0 * w + x] + hm[zk1 * w + x] - 4f * hm[i];
                    Curvature[i] = lap / (4f * k * sp);
                }
            }
            WFPaths.DistanceField(WFPaths.BuildAllSegments(ctx.Profile.paths), w, h, sp, PathRange, PathDistance);
        }

        public struct Sample
        {
            public float x, z, height, slope, curvature, pathDistance;
        }

        public Sample At(float worldX, float worldZ)
        {
            float sx = worldX / Ctx.Spacing, sz = worldZ / Ctx.Spacing;
            Sample s;
            s.x = worldX; s.z = worldZ;
            s.height = WFMath.Bilinear(Ctx.Heights, Ctx.Width, Ctx.Height, sx, sz);
            s.slope = WFMath.Bilinear(SlopeDeg, Ctx.Width, Ctx.Height, sx, sz);
            s.curvature = WFMath.Bilinear(Curvature, Ctx.Width, Ctx.Height, sx, sz);
            s.pathDistance = WFMath.Bilinear(PathDistance, Ctx.Width, Ctx.Height, sx, sz);
            return s;
        }

        public Vector3 NormalAt(float worldX, float worldZ)
        {
            float e = Ctx.Spacing;
            float hl = Ctx.SampleHeight(worldX - e, worldZ), hr = Ctx.SampleHeight(worldX + e, worldZ);
            float hd = Ctx.SampleHeight(worldX, worldZ - e), hu = Ctx.SampleHeight(worldX, worldZ + e);
            return new Vector3(hl - hr, 2f * e, hd - hu).normalized;
        }

        /// <summary>Share of the world that is steeper than the walkable limit (0..1).</summary>
        public float UnwalkableFraction(float maxWalkableSlopeDeg)
        {
            int c = 0;
            for (int i = 0; i < SlopeDeg.Length; i++) if (SlopeDeg[i] > maxWalkableSlopeDeg) c++;
            return SlopeDeg.Length == 0 ? 0f : c / (float)SlopeDeg.Length;
        }
    }

    /// <summary>Pure rule evaluation shared by the texture painter and the spawners.</summary>
    public static class WFRules
    {
        public static float EvaluateMask(WFMask m, WFWorldFields.Sample s, int worldSeed)
        {
            if (m == null) return 1f;
            float v = 1f;
            if (m.useHeight) v *= WFMath.Band(s.height, m.heightMin, m.heightMax, m.heightFalloff);
            if (v <= 0f) return 0f;
            if (m.useSlope) v *= WFMath.Band(s.slope, m.slopeMinDeg, m.slopeMaxDeg, m.slopeFalloffDeg);
            if (v <= 0f) return 0f;
            switch (m.pathMode)
            {
                case PathMode.OnlyNearPath:
                    v *= 1f - WFMath.SmoothStep(m.pathDistance, m.pathDistance + m.pathFalloff, s.pathDistance);
                    if (m.pathInnerClearance > 0f)
                        v *= WFMath.SmoothStep(m.pathInnerClearance, m.pathInnerClearance + 1f, s.pathDistance);
                    break;
                case PathMode.AvoidPath:
                    v *= WFMath.SmoothStep(m.pathDistance, m.pathDistance + m.pathFalloff, s.pathDistance);
                    break;
            }
            if (v <= 0f) return 0f;
            switch (m.curvatureMode)
            {
                case CurvatureMode.Concave: v *= WFMath.Saturate(s.curvature * m.curvatureStrength); break;
                case CurvatureMode.Convex: v *= WFMath.Saturate(-s.curvature * m.curvatureStrength); break;
            }
            if (v <= 0f) return 0f;
            if (m.useNoise)
            {
                uint seed = WFMath.MixSeed(worldSeed, 7001, m.noiseSeedOffset);
                float n = WFNoise.Fractal(NoiseType.FBm, s.x / m.noiseScale, s.z / m.noiseScale, seed, 4, 0.5f, 2f);
                if (m.invertNoise) n = 1f - n;
                v *= WFMath.Saturate((n - m.noiseThreshold) * m.noiseContrast + 0.5f);
            }
            return v;
        }

        /// <summary>
        /// Writes one normalised weight per rule into <paramref name="weights"/> (length = rules.Count).
        /// Disabled rules or rules without a TerrainLayer get 0.
        /// </summary>
        public static void TextureWeights(IList<WFTextureRule> rules, WFWorldFields.Sample s, int worldSeed, float sharpness, float[] weights)
        {
            float total = 0f; int first = -1;
            for (int r = 0; r < rules.Count; r++)
            {
                WFTextureRule rule = rules[r];
                if (rule == null || !rule.enabled || rule.terrainLayer == null) { weights[r] = 0f; continue; }
                if (first < 0) first = r;
                float v = rule.weight * EvaluateMask(rule.mask, s, worldSeed);
                if (sharpness != 1f && v > 0f) v = Mathf.Pow(v, sharpness);
                weights[r] = v;
                total += v;
            }
            if (total < 1e-5f)
            {
                for (int r = 0; r < rules.Count; r++) weights[r] = r == first ? 1f : 0f;
                return;
            }
            for (int r = 0; r < rules.Count; r++) weights[r] /= total;
        }
    }

    /// <summary>Bridson Poisson-disc sampling with a deterministic stream. Same algorithm as the Python reference.</summary>
    public static class WFPoisson
    {
        public static List<Vector2> Generate(float sizeX, float sizeZ, float minDistance, uint seed, int attempts = 20, int maxPoints = 2000000)
        {
            var points = new List<Vector2>();
            if (minDistance <= 0f || sizeX <= 0f || sizeZ <= 0f) return points;
            float cell = minDistance / Mathf.Sqrt(2f);
            int gw = Mathf.CeilToInt(sizeX / cell), gh = Mathf.CeilToInt(sizeZ / cell);
            var grid = new int[gw * gh];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;
            var active = new List<int>();
            var rng = new WFRandom(seed);
            float r2 = minDistance * minDistance;

            Vector2 first = new Vector2(rng.Range(0f, sizeX), rng.Range(0f, sizeZ));
            points.Add(first);
            active.Add(0);
            grid[Cell(first.y, cell, gh) * gw + Cell(first.x, cell, gw)] = 0;

            while (active.Count > 0 && points.Count < maxPoints)
            {
                int ai = rng.Range(0, active.Count);
                Vector2 p = points[active[ai]];
                bool found = false;
                for (int a = 0; a < attempts; a++)
                {
                    float ang = rng.NextFloat() * 6.2831853f;
                    float rad = minDistance * (1f + rng.NextFloat());
                    float qx = p.x + Mathf.Cos(ang) * rad, qz = p.y + Mathf.Sin(ang) * rad;
                    if (qx < 0f || qz < 0f || qx >= sizeX || qz >= sizeZ) continue;
                    int cx = Cell(qx, cell, gw), cz = Cell(qz, cell, gh);
                    bool ok = true;
                    for (int oz = Math.Max(0, cz - 2); oz <= Math.Min(gh - 1, cz + 2) && ok; oz++)
                        for (int ox = Math.Max(0, cx - 2); ox <= Math.Min(gw - 1, cx + 2); ox++)
                        {
                            int gi = grid[oz * gw + ox];
                            if (gi < 0) continue;
                            float dx = points[gi].x - qx, dz = points[gi].y - qz;
                            if (dx * dx + dz * dz < r2) { ok = false; break; }
                        }
                    if (!ok) continue;
                    points.Add(new Vector2(qx, qz));
                    active.Add(points.Count - 1);
                    grid[cz * gw + cx] = points.Count - 1;
                    found = true;
                    break;
                }
                if (!found)
                {
                    active[ai] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                }
            }
            return points;
        }

        private static int Cell(float v, float cell, int count)
        {
            int c = (int)(v / cell);
            return c < 0 ? 0 : (c >= count ? count - 1 : c);
        }
    }

    /// <summary>Simple circle occupancy hash so later spawn rules keep clear of earlier instances and POIs.</summary>
    public sealed class WFOccupancy
    {
        private readonly float _cell;
        private readonly Dictionary<long, List<Vector3>> _cells = new Dictionary<long, List<Vector3>>();
        private float _maxRadius;

        public WFOccupancy(float cellSize = 8f) { _cell = Mathf.Max(1f, cellSize); }

        private long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public void Add(float x, float z, float radius)
        {
            if (radius <= 0f) return;
            _maxRadius = Mathf.Max(_maxRadius, radius);
            long k = Key(WFMath.FloorToInt(x / _cell), WFMath.FloorToInt(z / _cell));
            List<Vector3> list;
            if (!_cells.TryGetValue(k, out list)) { list = new List<Vector3>(); _cells[k] = list; }
            list.Add(new Vector3(x, z, radius));
        }

        public bool IsBlocked(float x, float z, float ownRadius)
        {
            float reach = _maxRadius + ownRadius;
            int r = Mathf.CeilToInt(reach / _cell);
            int cx = WFMath.FloorToInt(x / _cell), cz = WFMath.FloorToInt(z / _cell);
            for (int oz = -r; oz <= r; oz++)
                for (int ox = -r; ox <= r; ox++)
                {
                    List<Vector3> list;
                    if (!_cells.TryGetValue(Key(cx + ox, cz + oz), out list)) continue;
                    foreach (Vector3 c in list)
                    {
                        float dx = c.x - x, dz = c.y - z, rr = c.z + ownRadius;
                        if (dx * dx + dz * dz < rr * rr) return true;
                    }
                }
            return false;
        }
    }
}
