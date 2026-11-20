using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Swaps cosmetic mesh prefabs only. All movement, collision, attack and damage
    /// remain owned by ArenaSimulation and the unchanged placeholder transforms.
    /// Models must be reviewed visual-only prefabs under Resources/ARE/Visuals.
    /// </summary>
    public static class PassiveArtVisuals
    {
        private const string ResourceRoot = "ARE/Visuals/";
        /// <summary>Reviewed humanoid idle/move controller already used by the hero and Dorn.</summary>
        public const string SharedHumanoidLocomotionKey = "EntryMelee";
        private static readonly Dictionary<Material, Material> PipelineAdapters = new Dictionary<Material, Material>();
        private static readonly string[] BaseMapHints = { "base", "albedo", "diffuse", "color" };

        /// <summary>True when a private visual exists for this key; presentation code asks before it plans dressing.</summary>
        public static bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && Resources.Load<GameObject>(ResourceRoot + key) != null;
        }

        /// <summary>Keep the fallback primitive when a reviewed private visual is missing.</summary>
        public static bool TryBind(Transform placeholder, string key)
        {
            if (placeholder == null || string.IsNullOrEmpty(key)) return false;
            GameObject prefab = Resources.Load<GameObject>(ResourceRoot + key);
            return prefab != null && TryBindPrefab(placeholder, prefab);
        }

        /// <summary>
        /// Reject executable and physics components before Instantiate can invoke Awake.
        /// The caller owns the visual-only placeholder and its authoritative position.
        /// </summary>
        public static bool TryBindPrefab(Transform placeholder, GameObject prefab)
        {
            if (placeholder == null || prefab == null || !prefab.activeSelf) return false;
            if (!IsPassiveVisual(prefab))
            {
                Debug.LogWarning("ARE ARPG: rejected non-visual or incomplete art prefab " + prefab.name);
                return false;
            }

            GameObject instance = Object.Instantiate(prefab, placeholder, false);
            instance.name = "Art_" + prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
                animator.applyRootMotion = false;
            AdaptForeignPipelineMaterials(instance);

            Renderer originalRenderer = placeholder.GetComponent<Renderer>();
            if (originalRenderer != null) originalRenderer.enabled = false;
            return true;
        }

        /// <summary>
        /// Building-sized art: the placeholder is a stretched blockout cube, so the mesh is
        /// kept at its authored world scale and stood on the ground below the placeholder.
        /// The placeholder must be unrotated; its transform is never changed.
        /// </summary>
        public static bool TryBindGrounded(Transform placeholder, string key, float groundY)
        {
            if (placeholder == null || string.IsNullOrEmpty(key)) return false;
            GameObject prefab = Resources.Load<GameObject>(ResourceRoot + key);
            return prefab != null && TryBindGroundedPrefab(placeholder, prefab, groundY);
        }

        /// <summary>Bind a passive prefab at authored world scale while preserving the Core projection host.</summary>
        public static bool TryBindGroundedPrefab(Transform placeholder, GameObject prefab, float groundY)
        {
            if (!TryBindPrefab(placeholder, prefab)) return false;
            Transform art = placeholder.GetChild(placeholder.childCount - 1);
            EnsurePlayableHumanoidLocomotion(art.gameObject);
            Vector3 stretch = placeholder.lossyScale;
            art.localScale = new Vector3(Inverse(stretch.x), Inverse(stretch.y), Inverse(stretch.z));
            art.position = new Vector3(placeholder.position.x, groundY, placeholder.position.z);
            return true;
        }

        /// <summary>
        /// True only when the controller references at least one existing clip. A private controller
        /// whose clips were not copied into this project is present but plays nothing.
        /// </summary>
        public static bool HasPlayableClip(RuntimeAnimatorController controller)
        {
            if (controller == null) return false;
            AnimationClip[] clips = controller.animationClips;
            if (clips == null) return false;
            foreach (AnimationClip clip in clips) if (clip != null) return true;
            return false;
        }

        /// <summary>
        /// Villager rule: a missing controller is always replaced (legacy behaviour); a present but clipless one only
        /// on humanoid rigs, because the shared controller is humanoid and would not drive a generic rig.
        /// </summary>
        public static bool NeedsSharedLocomotion(Animator animator)
        {
            if (animator == null) return false;
            if (animator.runtimeAnimatorController == null) return true;
            return animator.avatar != null && animator.avatar.isHuman && !HasPlayableClip(animator.runtimeAnimatorController);
        }

        /// <summary>
        /// A humanoid Animator without a playable clip keeps the muscle default pose with the hips at the
        /// object origin, so a grounded character sinks to its waist. Give such actors the shared reviewed
        /// locomotion controller instead. Working controllers and generic rigs are never touched.
        /// </summary>
        public static int EnsurePlayableHumanoidLocomotion(GameObject instance)
        {
            if (instance == null) return 0;
            RuntimeAnimatorController shared = null;
            int replaced = 0;
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                if (animator.avatar == null || !animator.avatar.isHuman || HasPlayableClip(animator.runtimeAnimatorController)) continue;
                if (shared == null) shared = Resources.Load<RuntimeAnimatorController>(ResourceRoot + SharedHumanoidLocomotionKey);
                if (!HasPlayableClip(shared)) return replaced;
                animator.runtimeAnimatorController = shared;
                replaced++;
            }
            return replaced;
        }

        private static float Inverse(float value)
        {
            return Mathf.Approximately(value, 0f) ? 1f : 1f / value;
        }

        /// <summary>
        /// Materials authored for another render pipeline would render magenta. They are
        /// replaced on the instance by project-owned adapter materials; the source
        /// material assets are never modified. Presentation only.
        /// </summary>
        private static void AdaptForeignPipelineMaterials(GameObject instance)
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material source = materials[index];
                    if (source == null || source.GetTag("RenderPipeline", false, "").Length != 0 ||
                        HasPipelineSubshader(source.shader, pipeline.renderPipelineShaderTag)) continue;
                    Material adapter = AdapterFor(source, pipeline);
                    if (adapter == null) continue;
                    materials[index] = adapter;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        // Multi-target Shader Graphs also contain an untagged built-in subshader. Before the
        // first camera render the material's active tag can be empty even when a matching pass exists.
        private static readonly ShaderTagId RenderPipelineTag = new ShaderTagId("RenderPipeline");
        private static bool HasPipelineSubshader(Shader shader, string pipelineTag)
        {
            if (shader == null || !shader.isSupported || string.IsNullOrEmpty(pipelineTag)) return false;
            for (int i = 0; i < shader.subshaderCount; i++)
                if (shader.FindSubshaderTagValue(i, RenderPipelineTag).name == pipelineTag) return true;
            return false;
        }

        /// <summary>Cache an instance-owned pipeline conversion without changing the source material asset.</summary>
        private static Material AdapterFor(Material source, RenderPipelineAsset pipeline)
        {
            if (PipelineAdapters.TryGetValue(source, out Material cached) && cached != null) return cached;
            Shader shader = pipeline.defaultShader;
            if (shader == null) return null;
            var adapter = new Material(shader) { name = source.name + " (ARE pipeline adapter)" };
            Texture baseMap = FindBaseMap(source);
            if (baseMap != null) adapter.mainTexture = baseMap;
            if (source.HasProperty("_Color")) adapter.color = source.GetColor("_Color");
            if (source.HasProperty("_BumpMap") && adapter.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
            {
                adapter.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                adapter.EnableKeyword("_NORMALMAP");
            }
            if (adapter.HasProperty("_Smoothness"))
                adapter.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.2f);
            if (adapter.HasProperty("_Metallic"))
                adapter.SetFloat("_Metallic", source.HasProperty("_Metallic") && source.HasProperty("_Glossiness") ? source.GetFloat("_Metallic") : 0f);
            PipelineAdapters[source] = adapter;
            return adapter;
        }

        /// <summary>Prefer explicit base maps, then known texture naming hints for cosmetic conversion.</summary>
        private static Texture FindBaseMap(Material source)
        {
            if (source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null) return source.GetTexture("_MainTex");
            Texture first = null;
            foreach (string property in source.GetTexturePropertyNames())
            {
                Texture texture = source.GetTexture(property);
                if (texture == null) continue;
                if (first == null) first = texture;
                string name = texture.name.ToLowerInvariant();
                foreach (string hint in BaseMapHints)
                    if (name.Contains(hint)) return texture;
            }
            return first;
        }

        /// <summary>Reject executable or physics components before any prefab instance can awaken, and characters with
        /// an unresolved skinned mesh.</summary>
        private static bool IsPassiveVisual(GameObject prefab)
        {
            bool hasMeshRenderer = false;
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) return false; // Broken script/asset reference.
                if (component is MeshRenderer || component is SkinnedMeshRenderer)
                {
                    Renderer renderer = (Renderer)component;
                    Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh :
                        renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    bool active = true;
                    for (Transform node = renderer.transform; node != null; node = node.parent)
                    {
                        active &= node.gameObject.activeSelf;
                        if (node == prefab.transform) break;
                    }
                    // A character whose skinned part lost its mesh (vendor model without that body part) would be
                    // drawn with holes: reject it so the complete fallback model is shown instead.
                    if (renderer is SkinnedMeshRenderer && renderer.enabled && active && mesh == null) return false;
                    hasMeshRenderer |= renderer.enabled && active && mesh != null && mesh.vertexCount > 0;
                    continue;
                }
                if (component is Transform || component is MeshFilter ||
                    component is Animator || component is LODGroup)
                    continue;

                // In particular: no MonoBehaviour, Collider, Rigidbody,
                // NavMeshAgent, Camera, Light, AudioListener or ParticleSystem.
                return false;
            }
            return hasMeshRenderer;
        }
    }
}
