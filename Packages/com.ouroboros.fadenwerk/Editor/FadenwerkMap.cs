using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fadenwerk.EditorTools
{
    /// <summary>
    /// Rendert eine Draufsicht der geladenen Szenen als Hintergrund für die Browser-Karte.
    /// Das Bild liegt in Library/Fadenwerk (nicht in der Versionsverwaltung) und übersteht Neukompilieren.
    /// </summary>
    internal static class FadenwerkMap
    {
        [Serializable]
        class MapInfo
        {
            public float minU, minV, maxU, maxV;
            public bool planeXY;
            public int rev;
            public string scene = "";
        }

        static MapInfo _info;
        static byte[] _image;

        static string Dir { get { return Path.Combine(Directory.GetCurrentDirectory(), "Library/Fadenwerk"); } }
        static string ImagePath { get { return Path.Combine(Dir, "map.jpg"); } }
        static string InfoPath { get { return Path.Combine(Dir, "map.json"); } }

        static void Load()
        {
            if (_info != null) return;
            try
            {
                if (File.Exists(InfoPath) && File.Exists(ImagePath))
                {
                    _info = JsonUtility.FromJson<MapInfo>(File.ReadAllText(InfoPath));
                    _image = File.ReadAllBytes(ImagePath);
                }
            }
            catch (Exception) { _info = null; _image = null; }
        }

        /// <summary>Gibt das Kartenbild zurück, wenn es zur aktuellen Ebene passt.</summary>
        public static bool TryGet(out byte[] image, out float minU, out float minV, out float maxU, out float maxV, out int rev)
        {
            Load();
            image = null; minU = minV = maxU = maxV = 0f; rev = 0;
            if (_info == null || _image == null || _info.planeXY != FadenwerkSettings.PlaneXY) return false;
            image = _image; minU = _info.minU; minV = _info.minV; maxU = _info.maxU; maxV = _info.maxV; rev = _info.rev;
            return true;
        }

        /// <summary>Kartenbereich aus Terrain, Renderern, NPCs und Markern der geladenen Szenen.</summary>
        public static Rect AutoBounds()
        {
            bool any = false;
            float minU = 0, minV = 0, maxU = 0, maxV = 0;
            Action<Vector3> add = p =>
            {
                Vector2 m = FadenwerkEditorData.ToMap(p);
                if (!any) { minU = maxU = m.x; minV = maxV = m.y; any = true; return; }
                minU = Mathf.Min(minU, m.x); maxU = Mathf.Max(maxU, m.x);
                minV = Mathf.Min(minV, m.y); maxV = Mathf.Max(maxV, m.y);
            };
#if FADENWERK_TERRAIN
            foreach (Terrain t in FadenwerkEditorData.FindInScenes<Terrain>())
            {
                if (t.terrainData == null) continue;
                add(t.transform.position);
                add(t.transform.position + t.terrainData.size);
            }
#endif
            foreach (Renderer r in FadenwerkEditorData.FindInScenes<Renderer>())
            {
                if (!r.enabled || r is TrailRenderer || r is LineRenderer) continue;
#if FADENWERK_PARTICLES
                if (r is ParticleSystemRenderer) continue;
#endif
                Bounds b = r.bounds;
                if (b.size.sqrMagnitude > 4.0e8f) continue; // Himmelskugeln und Ähnliches ignorieren
                add(b.min); add(b.max);
            }
            foreach (FadenwerkNpc n in FadenwerkEditorData.Npcs()) add(n.transform.position);
            foreach (FadenwerkMarker m in FadenwerkEditorData.Markers()) add(m.transform.position);
            if (!any) return new Rect(-25f, -25f, 50f, 50f);
            float pad = Mathf.Max(2f, Mathf.Max(maxU - minU, maxV - minV) * 0.04f);
            return Rect.MinMaxRect(minU - pad, minV - pad, maxU + pad, maxV + pad);
        }

        /// <summary>Rendert den Bereich. Gibt null bei Erfolg zurück, sonst eine Fehlermeldung.</summary>
        public static string Render(Rect area, int maxPixels)
        {
            Load();
            if (area.width < 0.5f || area.height < 0.5f) return "Der Kartenbereich ist zu klein.";
            if (area.width > 20000f || area.height > 20000f) return "Der Kartenbereich ist zu groß (max. 20000 Einheiten).";
            maxPixels = Mathf.Clamp(maxPixels, 256, 2048);
            int w, h;
            if (area.width >= area.height) { w = maxPixels; h = Mathf.Max(64, Mathf.RoundToInt(maxPixels * area.height / area.width)); }
            else { h = maxPixels; w = Mathf.Max(64, Mathf.RoundToInt(maxPixels * area.width / area.height)); }

            bool xy = FadenwerkSettings.PlaneXY;
            float top = 0f, bottom = 0f;
            HeightRange(area, xy, out bottom, out top);

            var go = new GameObject("Fadenwerk Map Camera") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = area.height * 0.5f;
                cam.aspect = (float)w / h;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.07f, 0.07f, 0.09f, 1f);
                cam.cullingMask = ~0;
                float depth = Mathf.Max(10f, top - bottom);
                if (xy)
                {
                    // Blick entlang +Z auf die XY-Ebene, oben = +Y
                    go.transform.position = new Vector3(area.center.x, area.center.y, bottom - 10f);
                    go.transform.rotation = Quaternion.identity;
                }
                else
                {
                    // Blick senkrecht nach unten, oben im Bild = +Z, rechts = +X
                    go.transform.position = new Vector3(area.center.x, top + 10f, area.center.y);
                    go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                }
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = depth + 30f;

                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                rt.Create();
                cam.targetTexture = rt;
                RenderCamera(cam, rt);

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply(false);
                byte[] jpg = tex.EncodeToJPG(82);
                cam.targetTexture = null;

                Directory.CreateDirectory(Dir);
                int rev = (_info != null ? _info.rev : 0) + 1;
                var info = new MapInfo
                {
                    minU = area.xMin, minV = area.yMin, maxU = area.xMax, maxV = area.yMax,
                    planeXY = xy, rev = rev,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path
                };
                File.WriteAllBytes(ImagePath, jpg);
                File.WriteAllText(InfoPath, JsonUtility.ToJson(info));
                _info = info;
                _image = jpg;
                return null;
            }
            catch (Exception e)
            {
                return "Karte konnte nicht gerendert werden: " + e.Message;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void RenderCamera(Camera cam, RenderTexture rt)
        {
            // Built-in-Pipeline: Camera.Render. URP/HDRP: offizielle Render-Anfrage, sonst Rückfall auf Camera.Render.
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                try
                {
                    var req = new RenderPipeline.StandardRequest { destination = rt };
                    if (RenderPipeline.SupportsRenderRequest(cam, req))
                    {
                        RenderPipeline.SubmitRenderRequest(cam, req);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Fadenwerk] Render-Anfrage der Pipeline fehlgeschlagen, nutze Camera.Render: " + e.Message);
                }
            }
            cam.Render();
        }

        static void HeightRange(Rect area, bool xy, out float min, out float max)
        {
            bool any = false;
            min = 0f; max = 0f;
            foreach (Renderer r in FadenwerkEditorData.FindInScenes<Renderer>())
            {
                if (!r.enabled) continue;
                Bounds b = r.bounds;
                if (b.size.sqrMagnitude > 4.0e8f) continue;
                Vector2 bmin = FadenwerkEditorData.ToMap(b.min), bmax = FadenwerkEditorData.ToMap(b.max);
                if (bmax.x < area.xMin || bmin.x > area.xMax || bmax.y < area.yMin || bmin.y > area.yMax) continue;
                float lo = xy ? b.min.z : b.min.y, hi = xy ? b.max.z : b.max.y;
                if (!any) { min = lo; max = hi; any = true; }
                else { min = Mathf.Min(min, lo); max = Mathf.Max(max, hi); }
            }
#if FADENWERK_TERRAIN
            foreach (Terrain t in FadenwerkEditorData.FindInScenes<Terrain>())
            {
                if (t.terrainData == null || xy) continue;
                float lo = t.transform.position.y, hi = lo + t.terrainData.size.y;
                if (!any) { min = lo; max = hi; any = true; }
                else { min = Mathf.Min(min, lo); max = Mathf.Max(max, hi); }
            }
#endif
            if (!any) { min = -10f; max = 10f; }
            // Sehr hohe Objekte (Himmel, Wolken) nicht über die Szene legen: Kamera sitzt über dem höchsten Objekt.
            max = Mathf.Min(max, min + 2000f);
        }

        public static void Clear()
        {
            _info = null; _image = null;
            try { if (File.Exists(ImagePath)) File.Delete(ImagePath); if (File.Exists(InfoPath)) File.Delete(InfoPath); }
            catch (Exception) { }
        }
    }
}
