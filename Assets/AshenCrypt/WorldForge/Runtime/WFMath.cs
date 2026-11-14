using System;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    /// <summary>Small, allocation-free math helpers shared by all generators.</summary>
    public static class WFMath
    {
        public static float Saturate(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        public static float SmoothStep(float edge0, float edge1, float x)
        {
            if (Mathf.Approximately(edge0, edge1)) return x < edge0 ? 0f : 1f;
            float t = Saturate((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>1 inside [min, max], smooth fade to 0 over <paramref name="falloff"/> outside.</summary>
        public static float Band(float x, float min, float max, float falloff)
        {
            if (falloff <= 0f) return (x >= min && x <= max) ? 1f : 0f;
            return SmoothStep(min - falloff, min, x) * (1f - SmoothStep(max, max + falloff, x));
        }

        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        public static int FloorToInt(float v) { return (int)Math.Floor(v); }

        /// <summary>Bilinear sample of a row-major grid (index = z * width + x), coordinates in samples.</summary>
        public static float Bilinear(float[] grid, int width, int height, float sx, float sz)
        {
            if (sx < 0f) sx = 0f; if (sz < 0f) sz = 0f;
            float maxX = width - 1, maxZ = height - 1;
            if (sx > maxX) sx = maxX; if (sz > maxZ) sz = maxZ;
            int x0 = (int)sx, z0 = (int)sz;
            int x1 = x0 < width - 1 ? x0 + 1 : x0;
            int z1 = z0 < height - 1 ? z0 + 1 : z0;
            float fx = sx - x0, fz = sz - z0;
            float a = grid[z0 * width + x0], b = grid[z0 * width + x1];
            float c = grid[z1 * width + x0], d = grid[z1 * width + x1];
            return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fz);
        }

        public static float DistanceToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            float vx = bx - ax, vz = bz - az;
            float den = vx * vx + vz * vz;
            float t = den <= 1e-12f ? 0f : Saturate(((px - ax) * vx + (pz - az) * vz) / den);
            float dx = px - (ax + t * vx), dz = pz - (az + t * vz);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Stable 32-bit seed mixing so every system gets its own independent stream.</summary>
        public static uint MixSeed(int seed, int a, int b = 0, int c = 0)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)a + 0x7F4A7C15u + (h << 6) + (h >> 2);
                h ^= (uint)b + 0x94D049BBu + (h << 6) + (h >> 2);
                h ^= (uint)c + 0xBF58476Du + (h << 6) + (h >> 2);
                h ^= h >> 16; h *= 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
                return h;
            }
        }
    }

    /// <summary>
    /// Deterministic SplitMix64 random stream. Never uses UnityEngine.Random or time,
    /// so the same seed produces the same world on every machine.
    /// </summary>
    public struct WFRandom
    {
        private ulong _state;

        public WFRandom(uint seed)
        {
            unchecked { _state = ((ulong)seed << 1) ^ 0x9E3779B97F4A7C15UL; }
        }

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() { return (NextULong() >> 40) * (1f / 16777216f); }

        public float Range(float min, float max) { return min + (max - min) * NextFloat(); }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));
        }
    }

    public enum NoiseType { FBm, Ridged, Billow }

    /// <summary>Integer-hashed 2D gradient noise. Identical algorithm to Reference/af_reference.py.</summary>
    public static class WFNoise
    {
        private static uint Hash(int x, int z, uint seed)
        {
            unchecked
            {
                uint h = seed;
                h ^= (uint)x * 0x27D4EB2Du;
                h = (h ^ (h >> 15)) * 0x85EBCA6Bu;
                h ^= (uint)z * 0x165667B1u;
                h = (h ^ (h >> 13)) * 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        private static float Grad(uint h, float dx, float dz)
        {
            switch (h & 7u)
            {
                case 0u: return dx + dz;
                case 1u: return -dx + dz;
                case 2u: return dx - dz;
                case 3u: return -dx - dz;
                case 4u: return dx;
                case 5u: return -dx;
                case 6u: return dz;
                default: return -dz;
            }
        }

        private static float Fade(float t) { return t * t * t * (t * (t * 6f - 15f) + 10f); }

        /// <summary>Gradient noise, roughly in [-1, 1].</summary>
        public static float Gradient(float x, float z, uint seed)
        {
            int x0 = WFMath.FloorToInt(x), z0 = WFMath.FloorToInt(z);
            float fx = x - x0, fz = z - z0;
            float u = Fade(fx), v = Fade(fz);
            float n00 = Grad(Hash(x0, z0, seed), fx, fz);
            float n10 = Grad(Hash(x0 + 1, z0, seed), fx - 1f, fz);
            float n01 = Grad(Hash(x0, z0 + 1, seed), fx, fz - 1f);
            float n11 = Grad(Hash(x0 + 1, z0 + 1, seed), fx - 1f, fz - 1f);
            return WFMath.Lerp(WFMath.Lerp(n00, n10, u), WFMath.Lerp(n01, n11, u), v);
        }

        /// <summary>Fractal noise normalised to [0, 1].</summary>
        public static float Fractal(NoiseType type, float x, float z, uint seed, int octaves, float persistence, float lacunarity)
        {
            if (octaves < 1) octaves = 1;
            float amp = 1f, freq = 1f, sum = 0f, norm = 0f, weight = 1f;
            for (int o = 0; o < octaves; o++)
            {
                uint s = unchecked(seed + (uint)o * 0x9E3779B9u);
                float n = Gradient(x * freq, z * freq, s);
                float value;
                switch (type)
                {
                    case NoiseType.Ridged:
                        float r = 1f - Mathf.Abs(n);
                        r *= r;
                        value = r * weight;
                        weight = WFMath.Saturate(r * 1.5f);
                        break;
                    case NoiseType.Billow:
                        value = Mathf.Abs(n);
                        break;
                    default:
                        value = n * 0.5f + 0.5f;
                        break;
                }
                sum += value * amp;
                norm += amp;
                amp *= persistence;
                freq *= lacunarity;
            }
            return norm > 0f ? WFMath.Saturate(sum / norm) : 0f;
        }
    }
}
