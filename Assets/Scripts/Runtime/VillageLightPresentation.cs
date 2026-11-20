using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Warm torch-lit islands in the village after dusk. Presentation only: brightness follows the daylight
    /// that the sky already derives from the persisted Core tick; nothing here is read by the simulation.
    /// Budget: four unshadowed point lights (two with reduced effects), four additive flame quads, one material.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string VillageLightRootName = "ARE_Village_LightIslands";
        public const int VillageLightBudget = 4;
        public const int VillageLightReducedBudget = 2;
        private const float TorchIntensity = 5.5f;

        private sealed class VillageTorch
        {
            public Light Light;
            public MeshRenderer Flame;
            public MaterialPropertyBlock Block;
            public float Seed;
        }

        // Metres. Beside the road, the gate and the camp; never on a walked lane. The gate pair comes first
        // so that the reduced budget keeps the entrance readable.
        private static readonly Vector3[] VillageTorchPosts =
        {
            new Vector3(-3.3f, 0f, 22.6f), new Vector3(3.3f, 0f, 22.6f),
            new Vector3(-6.6f, 0f, -4.4f), new Vector3(5.7f, 0f, -15.4f),
        };

        private readonly List<VillageTorch> _villageTorches = new List<VillageTorch>();
        private Material _villageFlameMaterial, _villagePostMaterial;
        private Mesh _villageFlameMesh;
        private float _villageLightGlow;

        /// <summary>Night share in 0..1: zero in daylight, one once the sky is dark.</summary>
        public static float VillageLightGlow(float daylight)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.22f, .58f, daylight));
        }

        private void BuildVillageLights()
        {
            _villageTorches.Clear();
            var shader = Resources.Load<Shader>("AshenFlame");
            if (shader == null || !shader.isSupported) return;
            if (_villageFlameMaterial == null) _villageFlameMaterial = new Material(shader) { name = "ARE torch flame (presentation only)" };
            if (_villagePostMaterial == null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) return;
                _villagePostMaterial = new Material(lit) { name = "ARE torch post", color = new Color(.16f, .11f, .075f) };
            }
            if (_villageFlameMesh == null) _villageFlameMesh = FlameQuad();
            var root = new GameObject(VillageLightRootName).transform;
            _chapterScenery.Add(root);
            for (int i = 0; i < VillageTorchPosts.Length; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Torch post " + i;
                DestroyImmediate(post.GetComponent<Collider>()); // never alive for a physics step
                post.transform.SetParent(root, false);
                post.transform.position = VillageTorchPosts[i] + Vector3.up * 1.15f;
                post.transform.localScale = new Vector3(.09f, 1.15f, .09f);
                var postRenderer = post.GetComponent<MeshRenderer>();
                postRenderer.sharedMaterial = _villagePostMaterial;
                postRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                head.name = "Torch basket " + i;
                DestroyImmediate(head.GetComponent<Collider>());
                head.transform.SetParent(root, false);
                head.transform.position = VillageTorchPosts[i] + Vector3.up * 2.36f;
                head.transform.localScale = new Vector3(.2f, .09f, .2f);
                var headRenderer = head.GetComponent<MeshRenderer>();
                headRenderer.sharedMaterial = _villagePostMaterial;
                headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var flame = new GameObject("Torch flame " + i);
                flame.transform.SetParent(root, false);
                flame.transform.position = VillageTorchPosts[i] + Vector3.up * 2.12f;
                flame.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                flame.AddComponent<MeshFilter>().sharedMesh = _villageFlameMesh;
                var renderer = flame.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _villageFlameMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var lamp = new GameObject("Torch light " + i);
                lamp.transform.SetParent(root, false);
                lamp.transform.position = VillageTorchPosts[i] + Vector3.up * 2.45f;
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point; light.shadows = LightShadows.None;
                light.color = new Color(1f, .62f, .28f); light.range = 11f; light.intensity = 0f; light.enabled = false;

                var block = new MaterialPropertyBlock();
                block.SetFloat("_Seed", i * 2.37f); block.SetFloat("_Intensity", 0f);
                renderer.SetPropertyBlock(block);
                renderer.enabled = false;
                _villageTorches.Add(new VillageTorch { Light = light, Flame = renderer, Block = block, Seed = i * 2.37f });
            }
            _villageLightGlow = -1f;
        }

        /// <summary>The shared flame look for other presentation code (crypt candles); false when the shader is unavailable.</summary>
        private bool TryFlameAssets(out Material material, out Mesh mesh)
        {
            material = null; mesh = null;
            var shader = Resources.Load<Shader>("AshenFlame");
            if (shader == null || !shader.isSupported) return false;
            if (_villageFlameMaterial == null) _villageFlameMaterial = new Material(shader) { name = "ARE torch flame (presentation only)" };
            if (_villageFlameMesh == null) _villageFlameMesh = FlameQuad();
            material = _villageFlameMaterial; mesh = _villageFlameMesh;
            return true;
        }

        /// <summary>A unit quad standing on its bottom edge, wide enough in bounds to stay visible while billboarding.</summary>
        private static Mesh FlameQuad()
        {
            var mesh = new Mesh { name = "ARE torch flame quad" };
            mesh.vertices = new[] { new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0), new Vector3(-.5f, 1, 0), new Vector3(.5f, 1, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(new Vector3(0, .5f, 0), new Vector3(1.2f, 1.2f, 1.2f));
            return mesh;
        }

        /// <summary>Called by the sky with the daylight of the saved tick; idempotent for an unchanged phase.</summary>
        private void RefreshVillageLights(float daylight, bool reduced)
        {
            float glow = VillageLightGlow(daylight);
            _villageLightGlow = glow;
            int budget = reduced ? VillageLightReducedBudget : VillageLightBudget;
            for (int i = 0; i < _villageTorches.Count; i++)
            {
                var torch = _villageTorches[i];
                if (torch.Light == null) continue;
                bool lit = glow > .01f;
                torch.Flame.enabled = lit;
                torch.Block.SetFloat("_Intensity", glow);
                torch.Flame.SetPropertyBlock(torch.Block);
                torch.Light.enabled = lit && i < budget;
                torch.Light.intensity = TorchIntensity * glow;
            }
        }

        /// <summary>Cosmetic flicker between Core ticks; reduced effects keep a steady flame light.</summary>
        private void AnimateVillageLights()
        {
            if (_villageLightGlow <= .01f || _villageTorches.Count == 0) return;
            if (_atmosphere != null && _atmosphere.ReducedEffects) return;
            float time = Time.time;
            for (int i = 0; i < _villageTorches.Count; i++)
            {
                var torch = _villageTorches[i];
                if (torch.Light == null || !torch.Light.enabled) continue;
                float flicker = .84f + .16f * Mathf.PerlinNoise(time * 3.3f, torch.Seed) + .05f * Mathf.Sin(time * 17f + torch.Seed);
                torch.Light.intensity = TorchIntensity * _villageLightGlow * flicker;
            }
        }

        private void DisposeVillageLights()
        {
            _villageTorches.Clear();
            if (_villageFlameMaterial != null) Destroy(_villageFlameMaterial);
            if (_villagePostMaterial != null) Destroy(_villagePostMaterial);
            if (_villageFlameMesh != null) Destroy(_villageFlameMesh);
            _villageFlameMaterial = _villagePostMaterial = null; _villageFlameMesh = null;
        }
    }
}
