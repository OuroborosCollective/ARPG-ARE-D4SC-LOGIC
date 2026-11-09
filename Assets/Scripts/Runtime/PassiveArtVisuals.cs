using UnityEngine;

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
                Debug.LogWarning("ARE ARPG: rejected non-visual art prefab " + prefab.name);
                return false;
            }

            GameObject instance = Object.Instantiate(prefab, placeholder, false);
            instance.name = "Art_" + prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
                animator.applyRootMotion = false;

            Renderer originalRenderer = placeholder.GetComponent<Renderer>();
            if (originalRenderer != null) originalRenderer.enabled = false;
            return true;
        }

        private static bool IsPassiveVisual(GameObject prefab)
        {
            bool hasMeshRenderer = false;
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) return false; // Broken script/asset reference.
                if (component is MeshRenderer || component is SkinnedMeshRenderer)
                {
                    hasMeshRenderer = true;
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
