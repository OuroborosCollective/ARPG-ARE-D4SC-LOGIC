using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    /// <summary>
    /// One non-destructive step of the height stack. Layers are evaluated top to bottom on a
    /// fresh grid every time, so any layer can be edited, disabled or reordered later.
    /// </summary>
    [Serializable]
    public abstract class WFHeightLayer
    {
        public bool enabled = true;
        public string label = "";

        public abstract void Apply(WFHeightContext ctx);

        public virtual string DisplayName
        {
            get { return string.IsNullOrEmpty(label) ? GetType().Name.Replace("WF", "").Replace("Layer", "") : label; }
        }
    }

    // ------------------------------------------------------------------ Noise

    [Serializable]
    public class WFNoiseLayer : WFHeightLayer
    {
        public NoiseType noiseType = NoiseType.FBm;
        [Min(1f), Tooltip("Feature size in metres.")] public float scale = 80f;
        [Range(1, 10)] public int octaves = 5;
        [Range(0.05f, 0.95f)] public float persistence = 0.5f;
        [Range(1.2f, 3.5f)] public float lacunarity = 2f;
        [Tooltip("Height of the noise in metres (0..1 noise is mapped to baseHeight..baseHeight+amplitude).")]
        public float amplitude = 6f;
        public float baseHeight = 0f;
        public Vector2 offset = Vector2.zero;
        [Min(0f), Tooltip("Domain warp in metres; breaks up the grid-like look of plain noise.")]
        public float warp = 0f;
        public int seedOffset = 0;
        public WFBlendMode blend = WFBlendMode.Add;
        [Range(0f, 1f)] public float opacity = 1f;

        public override void Apply(WFHeightContext ctx)
        {
            uint seed = WFMath.MixSeed(ctx.Seed, 101, seedOffset);
            uint warpSeedX = WFMath.MixSeed(ctx.Seed, 102, seedOffset);
            uint warpSeedZ = WFMath.MixSeed(ctx.Seed, 103, seedOffset);
            float inv = 1f / Mathf.Max(1f, scale);
            for (int z = 0; z < ctx.Height; z++)
            {
                for (int x = 0; x < ctx.Width; x++)
                {
                    float wx = ctx.X(x) + offset.x, wz = ctx.Z(z) + offset.y;
                    if (warp > 0f)
                    {
                        float qx = WFNoise.Fractal(NoiseType.FBm, wx * inv, wz * inv, warpSeedX, 3, 0.5f, 2f) * 2f - 1f;
                        float qz = WFNoise.Fractal(NoiseType.FBm, wx * inv, wz * inv, warpSeedZ, 3, 0.5f, 2f) * 2f - 1f;
                        wx += qx * warp; wz += qz * warp;
                    }
                    float n = WFNoise.Fractal(noiseType, wx * inv, wz * inv, seed, octaves, persistence, lacunarity);
                    int i = z * ctx.Width + x;
                    ctx.Heights[i] = WFHeightContext.Blend(ctx.Heights[i], baseHeight + amplitude * n, blend, opacity);
                }
            }
        }
    }

    // ------------------------------------------------------------------ Stamps

    public enum StampShape { Mound, Basin, Crater, RingRidge, Plateau, RidgeLine, TextureStamp }

    [Serializable]
    public class WFStampLayer : WFHeightLayer
    {
        public StampShape shape = StampShape.Mound;
        [Tooltip("World X/Z in metres.")] public Vector2 center = new Vector2(128f, 128f);
        [Min(0.5f)] public float radius = 30f;
        [Min(0.05f), Tooltip("Ellipse: Z radius = radius * this.")] public float radiusZScale = 1f;
        [Range(-180f, 180f)] public float rotationDeg = 0f;
        [Tooltip("Positive height of the feature in metres.")] public float height = 8f;
        [Tooltip("Crater only: depth of the bowl in metres.")] public float depth = 3f;
        [Range(0.1f, 0.95f)] public float innerRadiusRatio = 0.72f;
        [Min(0.25f), Tooltip("Rim / ridge width in metres.")] public float ringWidth = 8f;
        [Min(1f), Tooltip("RidgeLine only: length in metres.")] public float length = 120f;
        [Range(0.02f, 1f), Tooltip("Plateau only: soft edge as fraction of radius.")] public float edgeSoftness = 0.25f;
        [Range(0f, 1f), Tooltip("Noise distortion of the outline.")] public float breakup = 0.15f;
        [Min(1f)] public float breakupScale = 18f;
        [Tooltip("TextureStamp only: greyscale height texture (Read/Write is enabled automatically by the editor).")]
        public Texture2D texture;
        public WFBlendMode blend = WFBlendMode.Add;
        [Tooltip("Max/Min/Replace: absolute base height in metres that the shape is added to.")]
        public float baseHeight = 0f;
        [Range(0f, 1f)] public float opacity = 1f;
        public int seedOffset = 0;

        public override void Apply(WFHeightContext ctx)
        {
            if (shape == StampShape.TextureStamp && (texture == null || !texture.isReadable))
            {
                Debug.LogWarning("[WorldForge] Texture stamp '" + DisplayName + "' skipped: texture missing or not readable.");
                return;
            }
            uint seed = WFMath.MixSeed(ctx.Seed, 201, seedOffset);
            float rad = rotationDeg * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            float reach = Reach();
            int x0 = Math.Max(0, WFMath.FloorToInt((center.x - reach) / ctx.Spacing));
            int x1 = Math.Min(ctx.Width - 1, WFMath.FloorToInt((center.x + reach) / ctx.Spacing) + 1);
            int z0 = Math.Max(0, WFMath.FloorToInt((center.y - reach) / ctx.Spacing));
            int z1 = Math.Min(ctx.Height - 1, WFMath.FloorToInt((center.y + reach) / ctx.Spacing) + 1);
            float absHeight = Mathf.Abs(height) + Mathf.Abs(depth) + 1e-4f;

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float wx = ctx.X(x), wz = ctx.Z(z);
                    float dx = wx - center.x, dz = wz - center.y;
                    float lx = dx * cs + dz * sn;
                    float lz = -dx * sn + dz * cs;
                    float jitter = 0f;
                    if (breakup > 0f)
                        jitter = (WFNoise.Fractal(NoiseType.FBm, wx / breakupScale, wz / breakupScale, seed, 3, 0.5f, 2f) * 2f - 1f) * breakup;
                    float shapeValue = Evaluate(lx, lz, jitter);
                    if (Mathf.Abs(shapeValue) < 1e-5f) continue;
                    int i = z * ctx.Width + x;
                    float current = ctx.Heights[i];
                    switch (blend)
                    {
                        case WFBlendMode.Add:
                        case WFBlendMode.Subtract:
                            ctx.Heights[i] = WFHeightContext.Blend(current, shapeValue, blend, opacity);
                            break;
                        default:
                            float mask = WFMath.Saturate(Mathf.Abs(shapeValue) / absHeight);
                            float mixed = WFHeightContext.Blend(current, baseHeight + shapeValue, blend, 1f);
                            ctx.Heights[i] = WFMath.Lerp(current, mixed, mask * opacity);
                            break;
                    }
                }
            }
        }

        private float Reach()
        {
            float r = Mathf.Max(radius, radius * radiusZScale);
            switch (shape)
            {
                case StampShape.Mound:
                case StampShape.Basin: return r * 2.6f;
                case StampShape.Crater:
                case StampShape.RingRidge: return r + ringWidth * 3f;
                case StampShape.RidgeLine: return length * 0.5f + ringWidth * 3f;
                default: return r * 1.5f;
            }
        }

        private float Evaluate(float lx, float lz, float jitter)
        {
            switch (shape)
            {
                case StampShape.RidgeLine:
                {
                    float half = length * 0.5f;
                    float cx = Mathf.Clamp(lx, -half, half);
                    float d = Mathf.Sqrt((lx - cx) * (lx - cx) + lz * lz) + jitter * ringWidth;
                    return height * Mathf.Exp(-(d / ringWidth) * (d / ringWidth));
                }
                case StampShape.TextureStamp:
                {
                    float u = lx / (2f * radius) + 0.5f;
                    float v = lz / (2f * radius * radiusZScale) + 0.5f;
                    if (u < 0f || u > 1f || v < 0f || v > 1f) return 0f;
                    float edge = WFMath.SmoothStep(0f, 0.06f, Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)));
                    return height * texture.GetPixelBilinear(u, v).grayscale * edge;
                }
            }

            float ez = lz / Mathf.Max(0.05f, radiusZScale);
            float r = Mathf.Sqrt(lx * lx + ez * ez) + jitter * radius * 0.25f;
            if (r < 0f) r = 0f;
            switch (shape)
            {
                case StampShape.Mound: return height * Mathf.Exp(-(r / radius) * (r / radius));
                case StampShape.Basin: return -height * Mathf.Exp(-(r / radius) * (r / radius));
                case StampShape.RingRidge:
                {
                    float q = (r - radius) / ringWidth;
                    return height * Mathf.Exp(-q * q);
                }
                case StampShape.Crater:
                {
                    float q = (r - radius) / ringWidth;
                    float inner = radius * innerRadiusRatio;
                    return height * Mathf.Exp(-q * q) - depth * Mathf.Exp(-(r / inner) * (r / inner));
                }
                case StampShape.Plateau:
                    return height * (1f - WFMath.SmoothStep(radius * (1f - edgeSoftness), radius, r));
            }
            return 0f;
        }
    }

    // ------------------------------------------------------------------ Import

    [Serializable]
    public class WFImportRawLayer : WFHeightLayer
    {
        [Tooltip("Project-relative (Assets/...) or absolute path to a 16-bit RAW heightmap covering the whole world.")]
        public string rawPath = "Assets/Studio/Nature/AshenKrypt/World/Heightmaps/AK_World_256m_Global_513.raw";
        [Tooltip("Samples per side; 0 = derive from file size (square maps).")]
        public int resolution = 0;
        public bool littleEndian = true;
        [Tooltip("RAW rows are stored south to north (Blender/Ashen Krypt export). Disable for north-to-south files.")]
        public bool rowsSouthToNorth = true;
        [Tooltip("Height in metres that the 16-bit value 65535 represents.")]
        public float heightScale = 32f;
        public WFBlendMode blend = WFBlendMode.Replace;
        [Range(0f, 1f)] public float opacity = 1f;

        public override void Apply(WFHeightContext ctx)
        {
            string path = rawPath;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, rawPath);
            if (!File.Exists(path))
            {
                Debug.LogWarning("[WorldForge] RAW import skipped, file not found: " + rawPath);
                return;
            }
            byte[] bytes = File.ReadAllBytes(path);
            int res = resolution;
            if (res <= 0) res = Mathf.RoundToInt(Mathf.Sqrt(bytes.Length / 2f));
            if (res * res * 2 != bytes.Length)
            {
                Debug.LogWarning("[WorldForge] RAW import skipped: " + bytes.Length + " bytes do not match " + res + "x" + res + " 16-bit samples.");
                return;
            }
            var src = new float[res * res];
            for (int i = 0; i < src.Length; i++)
            {
                int b0 = bytes[i * 2], b1 = bytes[i * 2 + 1];
                int raw = littleEndian ? (b0 | (b1 << 8)) : ((b0 << 8) | b1);
                src[i] = raw / 65535f * heightScale;
            }
            for (int z = 0; z < ctx.Height; z++)
            {
                float v = ctx.Height > 1 ? z / (float)(ctx.Height - 1) : 0f;
                float sz = (rowsSouthToNorth ? v : 1f - v) * (res - 1);
                for (int x = 0; x < ctx.Width; x++)
                {
                    float sx = (ctx.Width > 1 ? x / (float)(ctx.Width - 1) : 0f) * (res - 1);
                    int i = z * ctx.Width + x;
                    ctx.Heights[i] = WFHeightContext.Blend(ctx.Heights[i], WFMath.Bilinear(src, res, res, sx, sz), blend, opacity);
                }
            }
        }
    }

    // ------------------------------------------------------------------ Shaping utilities

    [Serializable]
    public class WFRemapLayer : WFHeightLayer
    {
        public float clampMin = 0f;
        public float clampMax = 1000f;
        public bool useCurve = false;
        [Tooltip("x and y are normalised to 0..maxHeight.")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float scale = 1f;
        public float offset = 0f;

        public override void Apply(WFHeightContext ctx)
        {
            float mh = Mathf.Max(0.001f, ctx.MaxHeight);
            for (int i = 0; i < ctx.Heights.Length; i++)
            {
                float h = ctx.Heights[i] * scale + offset;
                if (useCurve && curve != null) h = curve.Evaluate(h / mh) * mh;
                ctx.Heights[i] = Mathf.Clamp(h, clampMin, clampMax);
            }
        }
    }

    [Serializable]
    public class WFTerraceLayer : WFHeightLayer
    {
        [Min(0.1f)] public float stepHeight = 2.5f;
        [Range(1f, 12f)] public float sharpness = 4f;
        [Range(0f, 1f)] public float opacity = 0.6f;

        public override void Apply(WFHeightContext ctx)
        {
            for (int i = 0; i < ctx.Heights.Length; i++)
            {
                float h = ctx.Heights[i];
                float k = h / stepHeight;
                float fl = Mathf.Floor(k);
                float t = Mathf.Pow(k - fl, sharpness);
                ctx.Heights[i] = WFMath.Lerp(h, (fl + t) * stepHeight, opacity);
            }
        }
    }

    [Serializable]
    public class WFSmoothLayer : WFHeightLayer
    {
        [Range(1, 16)] public int radiusSamples = 2;
        [Range(1, 8)] public int iterations = 1;
        [Range(0f, 1f)] public float opacity = 1f;

        public override void Apply(WFHeightContext ctx)
        {
            float[] blurred = WFHeightContext.BoxBlur(ctx.Heights, ctx.Width, ctx.Height, radiusSamples, iterations);
            for (int i = 0; i < ctx.Heights.Length; i++)
                ctx.Heights[i] = WFMath.Lerp(ctx.Heights[i], blurred[i], opacity);
        }
    }

    // ------------------------------------------------------------------ Gameplay shaping

    /// <summary>Carves all enabled profile paths into the terrain: flattened bed, sunken track and raised shoulders.</summary>
    [Serializable]
    public class WFPathCarveLayer : WFHeightLayer
    {
        [Range(0f, 2f)] public float strength = 1f;
        [Min(0f), Tooltip("Extra smoothing (metres) of the road grade: roads cut notches through ridges instead of climbing them.")]
        public float gradeSmoothing = 10f;

        public override void Apply(WFHeightContext ctx)
        {
            List<WFPath> paths = ctx.Profile.paths;
            if (paths == null || paths.Count == 0) return;
            float maxWidth = 1f;
            foreach (WFPath p in paths) if (p != null && p.enabled) maxWidth = Mathf.Max(maxWidth, p.width);
            int blurRadius = Mathf.Max(1, Mathf.RoundToInt((maxWidth + gradeSmoothing) / ctx.Spacing));
            float[] blurred = WFHeightContext.BoxBlur(ctx.Heights, ctx.Width, ctx.Height, blurRadius, 2);
            var dist = new float[ctx.Heights.Length];
            foreach (WFPath p in paths)
            {
                if (p == null || !p.enabled || p.points == null || p.points.Count < 2) continue;
                float half = p.width * 0.5f;
                float range = half * 2.5f + 2f;
                WFPaths.DistanceField(WFPaths.BuildSegments(p), ctx.Width, ctx.Height, ctx.Spacing, range, dist);
                for (int i = 0; i < dist.Length; i++)
                {
                    float d = dist[i];
                    if (d >= range) continue;
                    float core = 1f - WFMath.SmoothStep(half * 0.6f, half * 1.15f, d);
                    float h = WFMath.Lerp(ctx.Heights[i], blurred[i], core * p.flatten);
                    h -= p.carveDepth * core * strength;
                    float sq = (d - half * 1.2f) / (half * 0.35f + 0.5f);
                    h += p.shoulderHeight * Mathf.Exp(-sq * sq) * strength;
                    ctx.Heights[i] = h;
                }
            }
        }
    }

    /// <summary>Flattens the ground around POIs that have a flatten radius (camps, villages, dungeon entrances).</summary>
    [Serializable]
    public class WFFlattenPoiLayer : WFHeightLayer
    {
        [Range(0f, 1f)] public float opacity = 1f;

        public override void Apply(WFHeightContext ctx)
        {
            List<WFPoiDefinition> pois = ctx.Profile.pois;
            if (pois == null) return;
            foreach (WFPoiDefinition poi in pois)
            {
                if (poi == null || poi.flattenRadius <= 0f) continue;
                float inner = poi.flattenRadius, outer = poi.flattenRadius + Mathf.Max(0.01f, poi.flattenFalloff);
                int x0 = Math.Max(0, WFMath.FloorToInt((poi.position.x - outer) / ctx.Spacing));
                int x1 = Math.Min(ctx.Width - 1, WFMath.FloorToInt((poi.position.x + outer) / ctx.Spacing) + 1);
                int z0 = Math.Max(0, WFMath.FloorToInt((poi.position.y - outer) / ctx.Spacing));
                int z1 = Math.Min(ctx.Height - 1, WFMath.FloorToInt((poi.position.y + outer) / ctx.Spacing) + 1);
                double sum = 0; int count = 0;
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = ctx.X(x) - poi.position.x, dz = ctx.Z(z) - poi.position.y;
                        if (dx * dx + dz * dz <= inner * inner) { sum += ctx.Heights[z * ctx.Width + x]; count++; }
                    }
                if (count == 0) continue;
                float target = (float)(sum / count);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = ctx.X(x) - poi.position.x, dz = ctx.Z(z) - poi.position.y;
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        float w = 1f - WFMath.SmoothStep(inner, outer, d);
                        if (w <= 0f) continue;
                        int i = z * ctx.Width + x;
                        ctx.Heights[i] = WFMath.Lerp(ctx.Heights[i], target, w * opacity);
                    }
            }
        }
    }

    public enum BoundaryMode { RaiseCliffs, Sink }

    /// <summary>
    /// ARPG world border: raises jagged cliffs along the map edge so players stay inside the playable area.
    /// Paths that leave the map get a gap, so they become natural exits/zone transitions.
    /// </summary>
    [Serializable]
    public class WFBoundaryLayer : WFHeightLayer
    {
        public BoundaryMode mode = BoundaryMode.RaiseCliffs;
        [Min(1f)] public float width = 14f;
        public float height = 10f;
        [Min(0f)] public float jaggedness = 6f;
        [Min(1f)] public float noiseScale = 22f;
        public bool gapAtPaths = true;
        [Min(1f)] public float gapWidth = 10f;
        public int seedOffset = 0;

        public override void Apply(WFHeightContext ctx)
        {
            uint seed = WFMath.MixSeed(ctx.Seed, 301, seedOffset);
            float[] pathDist = null;
            if (gapAtPaths)
            {
                pathDist = new float[ctx.Heights.Length];
                WFPaths.DistanceField(WFPaths.BuildAllSegments(ctx.Profile.paths), ctx.Width, ctx.Height, ctx.Spacing, gapWidth + 12f, pathDist);
            }
            for (int z = 0; z < ctx.Height; z++)
            {
                for (int x = 0; x < ctx.Width; x++)
                {
                    float wx = ctx.X(x), wz = ctx.Z(z);
                    float edge = Mathf.Min(Mathf.Min(wx, wz), Mathf.Min(ctx.SizeX - wx, ctx.SizeZ - wz));
                    if (edge > width + jaggedness) continue;
                    float n = WFNoise.Fractal(NoiseType.FBm, wx / noiseScale, wz / noiseScale, seed, 4, 0.5f, 2f);
                    float t = 1f - WFMath.SmoothStep(0f, width, edge - (n - 0.5f) * 2f * jaggedness);
                    t = t * t * (3f - 2f * t);
                    int i = z * ctx.Width + x;
                    if (pathDist != null)
                        t *= WFMath.SmoothStep(gapWidth * 0.5f, gapWidth * 0.5f + 8f, pathDist[i]);
                    float amount = height * t * (0.75f + 0.5f * n);
                    ctx.Heights[i] += mode == BoundaryMode.RaiseCliffs ? amount : -amount;
                }
            }
        }
    }
}
