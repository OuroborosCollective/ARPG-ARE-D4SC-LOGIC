using System;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Keep the private neck mark at physical scale despite imported bone units.</summary>
    public static class RattlerBrandAuthoring
    {
        private const string PrefabPath = "Assets/Resources/ARE/Visuals/CryptRattler.prefab";

        [MenuItem("ARE ARPG/Art/Fit Private Rattler Brand")]
        public static void Apply()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("Import the reviewed private CryptRattler prefab first.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform brand = Array.Find(root.GetComponentsInChildren<Transform>(true),
                    part => part.name == "Immergruen_Royal_Brand");
                Renderer renderer = brand == null ? null : brand.GetComponent<Renderer>();
                if (renderer == null) throw new InvalidOperationException("The private neck mark is missing.");
                Vector3 size = renderer.bounds.size;
                float longest = Mathf.Max(size.x, size.y, size.z);
                if (float.IsNaN(longest) || float.IsInfinity(longest) || longest <= 0f)
                    throw new InvalidOperationException("The neck mark has invalid world bounds.");
                // Uniformly resize the mark alone: retain its neck attachment, pose and silhouette.
                brand.localScale *= .24f / longest;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save the private neck mark.");
                AssetDatabase.SaveAssets();
                Debug.Log("ARE: private Rattler neck mark fitted to 0.24 m in the authored pose.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
