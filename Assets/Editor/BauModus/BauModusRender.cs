using System;
using System.Collections.Generic;
using System.IO;
using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Isometric images for the browser: a sprite sheet per building block (8 yaw steps) and a background per area and
/// view direction, both rendered by Unity with the same orthographic camera (pitch 30°, 2:1 ground ratio), so
/// browser and editor show the same geometry. Cached in Library/BauModus (never versioned).
/// </summary>
internal static class BauModusRender
{
    public const int Frames = 8;
    public const int Version = 4;
    const float SpritePpm = 22f, MapPpm = 14f;
    const int SpriteLayer = 31, MaxFrame = 420, MaxMap = 2048;
    static readonly Vector3 Studio = new Vector3(9000f, -4000f, 9000f);

    [Serializable]
    public sealed class SpriteInfo
    {
        public string key = "";
        public int version, frames, frameW, frameH;
        public float ax, ay, ppm;
        public bool art, surface, alpha;
    }

    [Serializable]
    public sealed class MapInfo
    {
        public string area = "";
        public int version, view, width, height, stamp;
        public float u0, v0, ppm, yaw;
        public bool real, world, playing;
    }

    static string Dir(string sub)
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Library/BauModus/" + sub);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ------------------------------------------------------------------ sprites

    public static SpriteInfo SpriteMeta(string key, bool create)
    {
        BuildCatalogEntry entry = BuildCatalog.Find(key);
        if (entry == null) throw new InvalidOperationException("Unbekannter Baustein '" + key + "'.");
        string json = Path.Combine(Dir("sprites"), key + ".json"), png = Path.Combine(Dir("sprites"), key + ".png");
        if (File.Exists(json) && File.Exists(png))
        {
            try
            {
                SpriteInfo cached = JsonUtility.FromJson<SpriteInfo>(File.ReadAllText(json));
                if (cached != null && cached.version == Version && cached.art == HasArt(entry)) return cached;
            }
            catch (Exception) { }
        }
        if (!create) return null;
        SpriteInfo info = entry.Kind == BuildKind.Surface ? RenderSwatch(entry, png) : RenderSheet(entry, png);
        File.WriteAllText(json, JsonUtility.ToJson(info));
        return info;
    }

    public static byte[] SpritePng(string key)
    {
        SpriteMeta(key, true);
        return File.ReadAllBytes(Path.Combine(Dir("sprites"), key + ".png"));
    }

    static bool HasArt(BuildCatalogEntry entry)
    {
        return entry.Kind == BuildKind.Surface ? Resources.Load<Material>(entry.ResourcePath) != null : Resources.Load<GameObject>(entry.ResourcePath) != null;
    }

    static SpriteInfo RenderSheet(BuildCatalogEntry entry, string pngPath)
    {
        var piece = new BuildPiece { id = "b0", key = entry.Key, scale = 1f };
        Transform host = BuildLayer.CreatePiece(piece, null, null, 0);
        try
        {
            Prepare(host);
            Quaternion iso = Quaternion.Euler(30f, 45f, 0f);
            Vector3 right = iso * Vector3.right, up = iso * Vector3.up;
            // Union of the projected bounds over all eight yaws: one frame size and one anchor for the sheet.
            float uMin = float.MaxValue, uMax = float.MinValue, vMin = float.MaxValue, vMax = float.MinValue;
            for (int k = 0; k < Frames; k++)
            {
                host.rotation = Quaternion.Euler(0f, k * 45f, 0f);
                foreach (Renderer r in host.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = r.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 p = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z) - Studio;
                        float u = Vector3.Dot(p, right), v = Vector3.Dot(p, up);
                        uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u); vMin = Mathf.Min(vMin, v); vMax = Mathf.Max(vMax, v);
                    }
                }
            }
            if (uMin > uMax) { uMin = -0.5f; uMax = 0.5f; vMin = -0.5f; vMax = 0.5f; }
            uMin = Mathf.Min(uMin, 0f) - 0.15f; uMax = Mathf.Max(uMax, 0f) + 0.15f;
            vMin = Mathf.Min(vMin, 0f) - 0.15f; vMax = Mathf.Max(vMax, 0f) + 0.15f;
            float ppm = SpritePpm;
            float big = Mathf.Max(uMax - uMin, vMax - vMin);
            if (big * ppm > MaxFrame) ppm = MaxFrame / big;
            int w = Mathf.CeilToInt((uMax - uMin) * ppm), h = Mathf.CeilToInt((vMax - vMin) * ppm);
            var sheet = new Texture2D(w * Frames, h, TextureFormat.RGBA32, false);
            bool alpha = true;
            using (var light = new StudioLights())
            {
                for (int k = 0; k < Frames; k++)
                {
                    host.rotation = Quaternion.Euler(0f, k * 45f, 0f);
                    float uc = (uMin + uMax) * 0.5f, vc = (vMin + vMax) * 0.5f;
                    Color[] pixels = Render(iso, Studio + right * uc + up * vc, (vMax - vMin) * 0.5f, w, h, 1 << SpriteLayer, true, ref alpha);
                    sheet.SetPixels(k * w, 0, w, h, pixels);
                }
            }
            sheet.Apply(false);
            File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return new SpriteInfo
            {
                key = entry.Key, version = Version, frames = Frames, frameW = w, frameH = h, ppm = ppm,
                ax = -uMin * ppm, ay = vMax * ppm, art = HasArt(entry), surface = false, alpha = alpha
            };
        }
        finally { Object.DestroyImmediate(host.gameObject); }
    }

    /// <summary>Surfaces: a top-down swatch of a 4 m patch; the browser lays it out as an isometric pattern.</summary>
    static SpriteInfo RenderSwatch(BuildCatalogEntry entry, string pngPath)
    {
        var piece = new BuildPiece { id = "b0", key = entry.Key, scale = 1f, w = 4f, d = 4f };
        Transform host = BuildLayer.CreatePiece(piece, null, null, 0);
        try
        {
            Prepare(host);
            const int size = 128;
            bool alpha = false;
            Color[] pixels;
            using (new StudioLights())
                pixels = Render(Quaternion.Euler(90f, 0f, 0f), Studio + Vector3.up * 0.04f, 1.9f, size, size, 1 << SpriteLayer, false, ref alpha);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply(false);
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return new SpriteInfo { key = entry.Key, version = Version, frames = 1, frameW = size, frameH = size, ppm = size / 3.8f, art = HasArt(entry), surface = true };
        }
        finally { Object.DestroyImmediate(host.gameObject); }
    }

    static void Prepare(Transform host)
    {
        host.position = Studio;
        foreach (Transform t in host.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = SpriteLayer;
            t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }
        foreach (Animator animator in host.GetComponentsInChildren<Animator>(true))
        {
            // Show figures in their idle pose instead of the bind pose.
            if (animator.runtimeAnimatorController == null) continue;
            animator.Rebind();
            animator.Update(0.4f);
        }
    }

    /// <summary>Key lights for the sprite studio; other scene lights are kept off the studio layer meanwhile.</summary>
    sealed class StudioLights : IDisposable
    {
        readonly List<KeyValuePair<Light, int>> _masks = new List<KeyValuePair<Light, int>>();
        readonly List<GameObject> _own = new List<GameObject>();

        public StudioLights()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                _masks.Add(new KeyValuePair<Light, int>(light, light.cullingMask));
                light.cullingMask &= ~(1 << SpriteLayer);
            }
            Add(new Color(1f, 0.93f, 0.82f), 1.25f, Quaternion.Euler(50f, -28f, 0f));
            Add(new Color(0.60f, 0.68f, 0.85f), 0.55f, Quaternion.Euler(35f, 150f, 0f));
        }

        void Add(Color color, float intensity, Quaternion rotation)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags("BauModus Studiolicht", HideFlags.HideAndDontSave, typeof(Light));
            go.transform.rotation = rotation;
            Light light = go.GetComponent<Light>();
            light.type = LightType.Directional; light.color = color; light.intensity = intensity;
            light.shadows = LightShadows.None; light.cullingMask = 1 << SpriteLayer;
            _own.Add(go);
        }

        public void Dispose()
        {
            foreach (var pair in _masks) if (pair.Key != null) pair.Key.cullingMask = pair.Value;
            foreach (GameObject go in _own) Object.DestroyImmediate(go);
        }
    }

    // ------------------------------------------------------------------ area backgrounds

    public static string MapPath(string area, int view, bool real, string ext)
    {
        return Path.Combine(Dir("maps"), area + "-r" + view + (real ? "-echt" : "") + "." + ext);
    }

    public static MapInfo MapMeta(string area, int view, bool real)
    {
        string json = MapPath(area, view, real, "json");
        if (!File.Exists(json) || !File.Exists(MapPath(area, view, real, "jpg"))) return null;
        try
        {
            MapInfo info = JsonUtility.FromJson<MapInfo>(File.ReadAllText(json));
            return info != null && info.version == Version ? info : null;
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Render the isometric background of an area: in Edit Mode from the editor preview, in Play Mode from the
    /// running game (only for the area the game currently shows). Placed pieces are hidden unless real is set.
    /// </summary>
    public static MapInfo RenderMap(string area, int view, bool real, IList<Transform> buildObjects)
    {
        BuildArea site = BauModusStore.Area(area);
        view = ((view % 4) + 4) % 4;
        float yaw = 45f + 90f * view;
        Quaternion iso = Quaternion.Euler(30f, yaw, 0f);
        Vector3 right = iso * Vector3.right, up = iso * Vector3.up;
        float e = site.Extent, uMin = float.MaxValue, uMax = float.MinValue, vMin = float.MaxValue, vMax = float.MinValue;
        foreach (float x in new[] { -e, e })
            foreach (float z in new[] { -e, e })
                foreach (float y in new[] { -2f, 16f })
                {
                    var p = new Vector3(x, y, z);
                    float u = Vector3.Dot(p, right), v = Vector3.Dot(p, up);
                    uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u); vMin = Mathf.Min(vMin, v); vMax = Mathf.Max(vMax, v);
                }
        float ppm = MapPpm;
        float big = Mathf.Max(uMax - uMin, vMax - vMin);
        if (big * ppm > MaxMap) ppm = MaxMap / big;
        int w = Mathf.CeilToInt((uMax - uMin) * ppm), h = Mathf.CeilToInt((vMax - vMin) * ppm);

        var hidden = new List<GameObject>();
        if (!real && buildObjects != null)
            foreach (Transform t in buildObjects)
                if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
        bool fog = RenderSettings.fog;
        if (fog) RenderSettings.fog = false;
        Texture2D tex = null;
        try
        {
            bool alpha = false;
            float uc = (uMin + uMax) * 0.5f, vc = (vMin + vMax) * 0.5f;
            Color[] pixels = Render(iso, right * uc + up * vc, (vMax - vMin) * 0.5f, w, h, ~((1 << SpriteLayer) | (1 << 5)), false, ref alpha);
            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels(pixels);
            tex.Apply(false);
            File.WriteAllBytes(MapPath(area, view, real, "jpg"), tex.EncodeToJPG(84));
        }
        finally
        {
            if (fog) RenderSettings.fog = true;
            foreach (GameObject go in hidden) if (go != null) go.SetActive(true);
            if (tex != null) Object.DestroyImmediate(tex);
        }
        var info = new MapInfo
        {
            area = area, version = Version, view = view, width = w, height = h, u0 = uMin, v0 = vMax, ppm = ppm, yaw = yaw,
            real = real, world = EditorApplication.isPlaying || BauModusPreview.HasWorld, playing = EditorApplication.isPlaying,
            stamp = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000000L)
        };
        File.WriteAllText(MapPath(area, view, real, "json"), JsonUtility.ToJson(info));
        return info;
    }

    // ------------------------------------------------------------------ camera

    /// <summary>
    /// Orthographic render; centre is the point on the view plane through the image centre. With transparent set,
    /// the background is cleared to alpha 0; if the pipeline drops alpha, a magenta key is used instead.
    /// </summary>
    static Color[] Render(Quaternion rotation, Vector3 centre, float halfHeight, int w, int h, int mask, bool transparent, ref bool alphaOk)
    {
        Color[] pixels = RenderOnce(rotation, centre, halfHeight, w, h, mask, transparent ? new Color(0f, 0f, 0f, 0f) : new Color(0.07f, 0.08f, 0.10f, 1f));
        if (!transparent) return pixels;
        bool hasHole = false, hasSolid = false;
        foreach (Color c in pixels) { if (c.a < 0.02f) hasHole = true; else if (c.a > 0.98f) hasSolid = true; if (hasHole && hasSolid) break; }
        if (hasHole && hasSolid) { alphaOk = true; return pixels; }
        if (!hasSolid && hasHole) { alphaOk = true; return pixels; }
        alphaOk = false;
        Color key = new Color(1f, 0f, 1f, 1f);
        pixels = RenderOnce(rotation, centre, halfHeight, w, h, mask, key);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            if (c.r > 0.92f && c.g < 0.08f && c.b > 0.92f) pixels[i] = new Color(0f, 0f, 0f, 0f);
            else { c.a = 1f; pixels[i] = c; }
        }
        return pixels;
    }

    static Color[] RenderOnce(Quaternion rotation, Vector3 centre, float halfHeight, int w, int h, int mask, Color clear)
    {
        var go = EditorUtility.CreateGameObjectWithHideFlags("BauModus Kamera", HideFlags.HideAndDontSave, typeof(Camera));
        RenderTexture rt = null;
        RenderTexture previous = RenderTexture.active;
        Texture2D read = null;
        try
        {
            Camera cam = go.GetComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = halfHeight;
            cam.aspect = (float)w / h;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = clear;
            cam.cullingMask = mask;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            go.transform.rotation = rotation;
            go.transform.position = centre - (rotation * Vector3.forward) * 300f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 700f;
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            cam.targetTexture = rt;
            bool done = false;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) { RenderPipeline.SubmitRenderRequest(cam, request); done = true; }
            }
            if (!done) cam.Render();
            RenderTexture.active = rt;
            read = new Texture2D(w, h, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            read.Apply(false);
            cam.targetTexture = null;
            return read.GetPixels();
        }
        finally
        {
            RenderTexture.active = previous;
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (read != null) Object.DestroyImmediate(read);
            Object.DestroyImmediate(go);
        }
    }
}
