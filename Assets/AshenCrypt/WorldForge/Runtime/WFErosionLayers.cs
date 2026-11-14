using System;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    /// <summary>
    /// Particle (droplet) hydraulic erosion. Carves gullies and deposits sediment in valleys.
    /// Fully deterministic: droplet start positions come from the world seed.
    /// Heights are converted to sample units internally, so results are resolution-independent.
    /// </summary>
    [Serializable]
    public class WFHydraulicErosionLayer : WFHeightLayer
    {
        [Min(0f), Tooltip("Droplets per height sample (0.3–1 is typical). 0.5 on a 513² grid ≈ 130k droplets.")] public float dropletsPerSample = 0.5f;
        [Range(4, 120)] public int lifetime = 30;
        [Range(0f, 0.95f)] public float inertia = 0.05f;
        [Min(0f)] public float sedimentCapacity = 4f;
        [Min(0f)] public float minSedimentCapacity = 0.01f;
        [Range(0f, 1f)] public float erodeSpeed = 0.3f;
        [Range(0f, 1f)] public float depositSpeed = 0.3f;
        [Range(0f, 1f)] public float evaporateSpeed = 0.01f;
        [Min(0f)] public float gravity = 4f;
        [Range(1, 8)] public int brushRadius = 3;
        [Range(0f, 1f)] public float opacity = 1f;
        public int seedOffset = 0;

        public override void Apply(WFHeightContext ctx)
        {
            int w = ctx.Width, h = ctx.Height;
            if (w < 4 || h < 4) return;
            float inv = 1f / ctx.Spacing;
            var map = new float[ctx.Heights.Length];
            for (int i = 0; i < map.Length; i++) map[i] = ctx.Heights[i] * inv;

            // Brush offsets and weights (clipped at borders during use).
            int bCount = 0;
            var bx = new int[(2 * brushRadius + 1) * (2 * brushRadius + 1)];
            var bz = new int[bx.Length];
            var bw = new float[bx.Length];
            float wsum = 0f;
            for (int oz = -brushRadius; oz <= brushRadius; oz++)
                for (int ox = -brushRadius; ox <= brushRadius; ox++)
                {
                    float d = Mathf.Sqrt(ox * ox + oz * oz);
                    if (d > brushRadius) continue;
                    bx[bCount] = ox; bz[bCount] = oz; bw[bCount] = 1f - d / brushRadius; wsum += bw[bCount]; bCount++;
                }
            for (int k = 0; k < bCount; k++) bw[k] /= wsum;

            int droplets = Mathf.RoundToInt(dropletsPerSample * w * h);
            var rng = new WFRandom(WFMath.MixSeed(ctx.Seed, 401, seedOffset));
            for (int n = 0; n < droplets; n++)
            {
                float px = rng.Range(0f, w - 1.001f), pz = rng.Range(0f, h - 1.001f);
                float dirX = 0f, dirZ = 0f, speed = 1f, water = 1f, sediment = 0f;
                for (int life = 0; life < lifetime; life++)
                {
                    int nx = (int)px, nz = (int)pz;
                    float cx = px - nx, cz = pz - nz;
                    float height, gx, gz;
                    HeightAndGradient(map, w, px, pz, out height, out gx, out gz);
                    dirX = dirX * inertia - gx * (1f - inertia);
                    dirZ = dirZ * inertia - gz * (1f - inertia);
                    float len = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
                    if (len < 1e-6f) break;
                    dirX /= len; dirZ /= len;
                    px += dirX; pz += dirZ;
                    if (px < 0f || px >= w - 1 || pz < 0f || pz >= h - 1) break;

                    float newHeight, ngx, ngz;
                    HeightAndGradient(map, w, px, pz, out newHeight, out ngx, out ngz);
                    float deltaHeight = newHeight - height;
                    float capacity = Mathf.Max(-deltaHeight * speed * water * sedimentCapacity, minSedimentCapacity);

                    if (sediment > capacity || deltaHeight > 0f)
                    {
                        float deposit = deltaHeight > 0f ? Mathf.Min(deltaHeight, sediment) : (sediment - capacity) * depositSpeed;
                        sediment -= deposit;
                        int i0 = nz * w + nx;
                        map[i0] += deposit * (1f - cx) * (1f - cz);
                        map[i0 + 1] += deposit * cx * (1f - cz);
                        map[i0 + w] += deposit * (1f - cx) * cz;
                        map[i0 + w + 1] += deposit * cx * cz;
                    }
                    else
                    {
                        float erode = Mathf.Min((capacity - sediment) * erodeSpeed, -deltaHeight);
                        for (int k = 0; k < bCount; k++)
                        {
                            int ex = nx + bx[k], ez = nz + bz[k];
                            if (ex < 0 || ez < 0 || ex >= w || ez >= h) continue;
                            int ei = ez * w + ex;
                            float amount = erode * bw[k];
                            float take = map[ei] < amount ? map[ei] : amount;
                            map[ei] -= take;
                            sediment += take;
                        }
                    }
                    speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed - deltaHeight * gravity));
                    water *= 1f - evaporateSpeed;
                }
            }
            for (int i = 0; i < map.Length; i++)
                ctx.Heights[i] = WFMath.Lerp(ctx.Heights[i], map[i] * ctx.Spacing, opacity);
        }

        private static void HeightAndGradient(float[] map, int w, float px, float pz, out float height, out float gx, out float gz)
        {
            int x = (int)px, z = (int)pz;
            float u = px - x, v = pz - z;
            int i = z * w + x;
            float nw = map[i], ne = map[i + 1], sw = map[i + w], se = map[i + w + 1];
            gx = (ne - nw) * (1f - v) + (se - sw) * v;
            gz = (sw - nw) * (1f - u) + (se - ne) * u;
            height = nw * (1f - u) * (1f - v) + ne * u * (1f - v) + sw * (1f - u) * v + se * u * v;
        }
    }

    /// <summary>Thermal erosion: material slides down where the slope exceeds the talus angle (softens cliffs, builds scree).</summary>
    [Serializable]
    public class WFThermalErosionLayer : WFHeightLayer
    {
        [Range(1, 200)] public int iterations = 20;
        [Range(10f, 80f)] public float talusAngleDeg = 38f;
        [Range(0f, 1f)] public float strength = 0.5f;

        public override void Apply(WFHeightContext ctx)
        {
            int w = ctx.Width, h = ctx.Height;
            float talus = Mathf.Tan(talusAngleDeg * Mathf.Deg2Rad) * ctx.Spacing;
            var delta = new float[ctx.Heights.Length];
            float[] map = ctx.Heights;
            for (int it = 0; it < iterations; it++)
            {
                Array.Clear(delta, 0, delta.Length);
                for (int z = 1; z < h - 1; z++)
                {
                    for (int x = 1; x < w - 1; x++)
                    {
                        int i = z * w + x;
                        float c = map[i];
                        int lowest = -1; float maxDiff = 0f;
                        CheckNeighbour(map, i - 1, c, ref lowest, ref maxDiff);
                        CheckNeighbour(map, i + 1, c, ref lowest, ref maxDiff);
                        CheckNeighbour(map, i - w, c, ref lowest, ref maxDiff);
                        CheckNeighbour(map, i + w, c, ref lowest, ref maxDiff);
                        if (lowest < 0 || maxDiff <= talus) continue;
                        float move = (maxDiff - talus) * 0.5f * strength;
                        delta[i] -= move;
                        delta[lowest] += move;
                    }
                }
                for (int i = 0; i < map.Length; i++) map[i] += delta[i];
            }
        }

        private static void CheckNeighbour(float[] map, int n, float c, ref int lowest, ref float maxDiff)
        {
            float d = c - map[n];
            if (d > maxDiff) { maxDiff = d; lowest = n; }
        }
    }
}
