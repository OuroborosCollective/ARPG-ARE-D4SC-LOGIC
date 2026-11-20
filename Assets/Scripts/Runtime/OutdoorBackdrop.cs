using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.UnityClient
{
    /// <summary>Static distant scenery outside the playable square; never supplies collision or game state.</summary>
    public sealed class OutdoorBackdrop : MonoBehaviour
    {
        private const int Segments = 64;
        /// <summary>Edge of the walked outdoor ground (a cube built by the journey scenery), in metres.</summary>
        private const float GroundMetres = 57f;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Material _land, _sky;
        private MeshRenderer _skyRenderer;
        private bool _groundTextured;
        private Light _sun;
        private Color _groundColor = Color.white;

        public void SetDaylight(float daylight)
        {
            if (_land == null) return;
            _land.SetFloat("_Exposure", Mathf.Lerp(.10f, 1f, Mathf.Clamp01(daylight)));
        }

        /// <summary>
        /// The textured land continues the lit ground, so every frame it takes the light that falls on that ground,
        /// whoever moves the sun: day, dusk and night stay seamless at the ground's edge.
        /// </summary>
        private void LateUpdate()
        {
            if (_groundTextured && _land != null) _land.SetVector("_GroundColor", GroundLight(_groundColor));
        }

        /// <summary>Linear light reflected by flat ground: the flat ambient light plus the sun or moon proxy.</summary>
        private Vector4 GroundLight(Color ground)
        {
            Color light = RenderSettings.ambientLight.linear;
            if (_sun == null || !_sun.isActiveAndEnabled) _sun = FindSun();
            if (_sun != null)
                light += _sun.color.linear * (_sun.intensity * Mathf.Max(0f, -_sun.transform.forward.y));
            Color lit = ground.linear * light;
            return new Vector4(lit.r, lit.g, lit.b, 1f);
        }

        private static Light FindSun()
        {
            Light sun = RenderSettings.sun;
            if (sun != null && sun.isActiveAndEnabled) return sun;
            sun = null;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.isActiveAndEnabled && (sun == null || light.intensity > sun.intensity))
                    sun = light;
            return sun;
        }

        public void SetSkyDomeVisible(bool visible)
        {
            if (_skyRenderer != null) _skyRenderer.enabled = visible;
        }

        public static Transform Create(int act)
        {
            var root = new GameObject("ARE_Outdoor_Backdrop");
            root.AddComponent<OutdoorBackdrop>().Build(act);
            return root.transform;
        }

        private void Build(int act)
        {
            var shader = Resources.Load<Shader>("AshenBackdrop");
            _land = new Material(shader) { name = "Ashen distant landscape", renderQueue = 2000 };
            _land.SetFloat("_FogStrength", 0);
            var ground = Resources.Load<Material>("ARE/Visuals/WorldSurfaces/OutdoorGround");
            if (ground != null && ground.HasProperty("_BaseMap") && ground.GetTexture("_BaseMap") != null)
            {
                _land.SetTexture("_BaseMap", ground.GetTexture("_BaseMap"));
                _land.SetFloat("_TextureStrength", 1);
                // Continue the walked ground beyond its edge: same repeat, same place, same brightness, same glow.
                // The ground is a cube of GroundMetres; a cube maps its top face as u = 0.5 - x, v = 0.5 - z.
                Vector2 repeat = ground.GetTextureScale("_BaseMap"), shift = ground.GetTextureOffset("_BaseMap");
                _land.SetVector("_GroundMapping", new Vector4(-repeat.x / GroundMetres, -repeat.y / GroundMetres,
                    repeat.x * .5f + shift.x, repeat.y * .5f + shift.y));
                _groundColor = ground.HasProperty("_BaseColor") ? ground.GetColor("_BaseColor") : Color.white;
                _groundTextured = true;
                _land.SetVector("_GroundColor", GroundLight(_groundColor));
                if (ground.IsKeywordEnabled("_EMISSION") && ground.HasProperty("_EmissionMap") && ground.GetTexture("_EmissionMap") != null)
                {
                    _land.SetTexture("_GlowMap", ground.GetTexture("_EmissionMap"));
                    _land.SetFloat("_GlowStrength", Mathf.Min(4f, ground.GetColor("_EmissionColor").maxColorComponent));
                }
            }
            _sky = new Material(shader) { name = "Ashen overcast sky", renderQueue = 1000 };
            _sky.SetFloat("_ZWrite", 0); _sky.SetFloat("_FogStrength", 0);

            // Ash-grey land to the horizon (owner direction 2026-11-20: an ash world, not green meadows).
            // Pale like the ash snow underfoot and cooler towards the horizon, where the land meets the sky.
            var soil = new Color(.43f, .43f, .44f);
            var near = new Color(.39f, .39f, .40f);
            var middle = new Color(.34f, .35f, .37f);
            var far = new Color(.30f, .32f, .35f);
            AddBand("Outdoor_GroundSkirt", 0, 1, soil, near, act);
            AddBand("Outdoor_NearRidge", 1, 2, near, middle, act);
            AddBand("Outdoor_FarRidge", 2, 3, middle, far, act);
            AddBand("Outdoor_OuterSlope", 3, 4, far, far, act);
            AddSky();

            // Reuse two reviewed private rock clusters, well beyond the 27-m movement boundary.
            for (int side = -1; side <= 1; side += 2)
            {
                var rock = new GameObject("Outdoor_Rocks_" + side);
                rock.transform.SetParent(transform, false);
                rock.transform.position = new Vector3(side * 39f, 0, act == 4 ? 18 : -9);
                if (!PassiveArtVisuals.TryBindGrounded(rock.transform, "AshRocks", 0))
                { Destroy(rock); continue; }
                var renderers = rock.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { Destroy(rock); continue; }
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float width = Mathf.Max(bounds.size.x, bounds.size.z);
                if (width <= 0) { Destroy(rock); continue; }
                rock.transform.localScale = Vector3.one * (7f / width);
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                rock.transform.position += new Vector3(side * 39f - bounds.center.x, .2f - bounds.min.y,
                    (act == 4 ? 18 : -9) - bounds.center.z);
            }
        }

        private static Vector3 RingPoint(int ring, float angle, int act)
        {
            float x = Mathf.Sin(angle), z = Mathf.Cos(angle);
            if (ring == 0)
            {
                // Square inner edge matches the existing ground without entering the movement area.
                float radius = 27.5f / Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                return new Vector3(x * radius, -.13f, z * radius);
            }
            float wave = .5f + .27f * Mathf.Sin(angle * 5 + act * .7f) + .18f * Mathf.Sin(angle * 9 - act);
            float radiusOut = ring == 1 ? 57 : ring == 2 ? 110 : ring == 3 ? 185 : 235;
            float height = ring == 1 ? .2f + 2 * wave * Mathf.Abs(x) :
                ring == 2 ? 2 + 6 * wave : ring == 3 ? 5 + 12 * wave : 2;
            return new Vector3(x * radiusOut, height, z * radiusOut);
        }

        private void AddBand(string label, int inner, int outer, Color innerColor, Color outerColor, int act)
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var colors = new Color[vertices.Length];
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments;
                vertices[i * 2] = RingPoint(inner, angle, act);
                vertices[i * 2 + 1] = RingPoint(outer, angle, act);
                colors[i * 2] = innerColor; colors[i * 2 + 1] = outerColor;
            }
            AddMesh(label, vertices, colors, StripTriangles(Segments), _land);
        }

        private static int[] StripTriangles(int segments)
        {
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            return triangles;
        }

        private void AddSky()
        {
            float[] heights = { -90, 0, 95, 280 };
            float[] radii = { 290, 310, 290, 0 };
            Color[] palette = { new Color(.30f, .36f, .34f), new Color(.49f, .53f, .51f),
                new Color(.29f, .37f, .43f), new Color(.15f, .23f, .31f) };
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            for (int band = 0; band < heights.Length - 1; band++)
            {
                int start = vertices.Count;
                for (int i = 0; i <= Segments; i++)
                    for (int row = band; row <= band + 1; row++)
                    {
                        float a = i * Mathf.PI * 2 / Segments;
                        vertices.Add(new Vector3(Mathf.Sin(a) * radii[row], heights[row], Mathf.Cos(a) * radii[row]));
                        colors.Add(palette[row]);
                    }
                foreach (int index in StripTriangles(Segments)) triangles.Add(start + index);
            }
            AddMesh("Outdoor_Sky", vertices.ToArray(), colors.ToArray(), triangles.ToArray(), _sky);
        }

        private void AddMesh(string label, Vector3[] vertices, Color[] colors, int[] triangles, Material material)
        {
            var mesh = new Mesh { name = label, vertices = vertices, colors = colors, triangles = triangles };
            mesh.RecalculateBounds(); _meshes.Add(mesh);
            var part = new GameObject(label); part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            if (label == "Outdoor_Sky") _skyRenderer = renderer;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes) if (mesh != null) Destroy(mesh);
            if (_land != null) Destroy(_land);
            if (_sky != null) Destroy(_sky);
        }
    }
}
