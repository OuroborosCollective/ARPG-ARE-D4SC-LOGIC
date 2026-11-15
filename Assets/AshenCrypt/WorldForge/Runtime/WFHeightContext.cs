using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    public enum WFBlendMode { Add, Subtract, Max, Min, Replace }

    /// <summary>
    /// One global height grid that covers all terrain tiles. Neighbouring tiles share their edge
    /// samples, so tiles cut from this grid are seamless by construction.
    /// Index = z * Width + x. Heights are in metres above the terrain origin.
    /// </summary>
    public sealed class WFHeightContext
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float Spacing;
        public readonly float SizeX;
        public readonly float SizeZ;
        public readonly float MaxHeight;
        public readonly int Seed;
        public readonly float[] Heights;
        public readonly WorldProfile Profile;

        public WFHeightContext(WorldProfile profile)
        {
            Profile = profile;
            Width = profile.GlobalSamplesX;
            Height = profile.GlobalSamplesZ;
            Spacing = profile.SampleSpacing;
            SizeX = profile.WorldSizeX;
            SizeZ = profile.WorldSizeZ;
            MaxHeight = profile.maxHeight;
            Seed = profile.seed;
            Heights = new float[Width * Height];
        }

        public WFHeightContext(WorldProfile profile, float[] heights) : this(profile)
        {
            if (heights == null || heights.Length != Heights.Length)
                throw new ArgumentException("Height array does not match the profile grid.");
            Array.Copy(heights, Heights, heights.Length);
        }

        public float X(int ix) { return ix * Spacing; }
        public float Z(int iz) { return iz * Spacing; }

        public float SampleHeight(float worldX, float worldZ)
        {
            return WFMath.Bilinear(Heights, Width, Height, worldX / Spacing, worldZ / Spacing);
        }

        public void ClampToRange()
        {
            for (int i = 0; i < Heights.Length; i++)
            {
                float v = Heights[i];
                Heights[i] = v < 0f ? 0f : (v > MaxHeight ? MaxHeight : v);
            }
        }

        public static float Blend(float current, float value, WFBlendMode mode, float opacity)
        {
            float target;
            switch (mode)
            {
                case WFBlendMode.Subtract: target = current - value; break;
                case WFBlendMode.Max: target = Mathf.Max(current, value); break;
                case WFBlendMode.Min: target = Mathf.Min(current, value); break;
                case WFBlendMode.Replace: target = value; break;
                default: target = current + value; break;
            }
            return WFMath.Lerp(current, target, WFMath.Saturate(opacity));
        }

        /// <summary>Separable box blur of a grid (used by smoothing and path flattening).</summary>
        public static float[] BoxBlur(float[] src, int width, int height, int radius, int iterations)
        {
            float[] a = (float[])src.Clone();
            if (radius < 1 || iterations < 1) return a;
            float[] b = new float[a.Length];
            for (int it = 0; it < iterations; it++)
            {
                for (int z = 0; z < height; z++)
                {
                    int row = z * width;
                    for (int x = 0; x < width; x++)
                    {
                        int x0 = Math.Max(0, x - radius), x1 = Math.Min(width - 1, x + radius);
                        float s = 0f;
                        for (int k = x0; k <= x1; k++) s += a[row + k];
                        b[row + x] = s / (x1 - x0 + 1);
                    }
                }
                for (int x = 0; x < width; x++)
                {
                    for (int z = 0; z < height; z++)
                    {
                        int z0 = Math.Max(0, z - radius), z1 = Math.Min(height - 1, z + radius);
                        float s = 0f;
                        for (int k = z0; k <= z1; k++) s += b[k * width + x];
                        a[z * width + x] = s / (z1 - z0 + 1);
                    }
                }
            }
            return a;
        }
    }

    /// <summary>Path geometry helpers: spline subdivision and distance fields.</summary>
    public static class WFPaths
    {
        /// <summary>Returns segments as (ax, az, bx, bz).</summary>
        public static List<Vector4> BuildSegments(WFPath path)
        {
            var result = new List<Vector4>();
            if (path == null || path.points == null || path.points.Count < 2) return result;
            List<Vector2> pts = path.points;
            if (!path.smooth || pts.Count < 3)
            {
                for (int i = 0; i < pts.Count - 1; i++)
                    result.Add(new Vector4(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y));
                return result;
            }
            const int sub = 6;
            Vector2 prev = pts[0];
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 p0 = pts[Math.Max(0, i - 1)];
                Vector2 p1 = pts[i];
                Vector2 p2 = pts[i + 1];
                Vector2 p3 = pts[Math.Min(pts.Count - 1, i + 2)];
                for (int s = 1; s <= sub; s++)
                {
                    float t = s / (float)sub;
                    Vector2 p = CatmullRom(p0, p1, p2, p3, t);
                    result.Add(new Vector4(prev.x, prev.y, p.x, p.y));
                    prev = p;
                }
            }
            return result;
        }

        public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// Fills <paramref name="distance"/> with the distance (metres) to the nearest segment, capped at maxRange.
        /// Only cells within maxRange of a segment are visited, so cost scales with path length, not world size.
        /// </summary>
        public static void DistanceField(List<Vector4> segments, int width, int height, float spacing, float maxRange, float[] distance)
        {
            for (int i = 0; i < distance.Length; i++) distance[i] = maxRange;
            foreach (Vector4 s in segments)
            {
                int x0 = Math.Max(0, WFMath.FloorToInt((Mathf.Min(s.x, s.z) - maxRange) / spacing));
                int x1 = Math.Min(width - 1, WFMath.FloorToInt((Mathf.Max(s.x, s.z) + maxRange) / spacing) + 1);
                int z0 = Math.Max(0, WFMath.FloorToInt((Mathf.Min(s.y, s.w) - maxRange) / spacing));
                int z1 = Math.Min(height - 1, WFMath.FloorToInt((Mathf.Max(s.y, s.w) + maxRange) / spacing) + 1);
                for (int z = z0; z <= z1; z++)
                {
                    float wz = z * spacing;
                    int row = z * width;
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = WFMath.DistanceToSegment(x * spacing, wz, s.x, s.y, s.z, s.w);
                        if (d < distance[row + x]) distance[row + x] = d;
                    }
                }
            }
        }

        public static List<Vector4> BuildAllSegments(IList<WFPath> paths)
        {
            var all = new List<Vector4>();
            if (paths == null) return all;
            foreach (WFPath p in paths)
                if (p != null && p.enabled) all.AddRange(BuildSegments(p));
            return all;
        }
    }
}
