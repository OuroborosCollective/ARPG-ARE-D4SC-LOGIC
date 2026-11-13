using System;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Reuse two reviewed village shrubs as separate passive bindings, preserving the village prefab.</summary>
    public static class RoadsideShrubAuthoring
    {
        private const string Root = "Assets/Resources/ARE/Visuals/";

        [MenuItem("ARE ARPG/Art/Extract Private Roadside Shrubs")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "EntryVillageWorld.prefab");
            if (source == null) throw new InvalidOperationException("Build the private entry village first.");
            int index = 0;
            foreach (Transform child in source.transform)
            {
                if (child.name != "bush") continue;
                foreach (var component in child.GetComponentsInChildren<Component>(true))
                    if (!(component is Transform || component is MeshFilter || component is MeshRenderer))
                        throw new InvalidOperationException("Village shrub is not passive static art.");
                var copy = UnityEngine.Object.Instantiate(child.gameObject);
                try
                {
                    copy.name = "RoadsideShrub" + index;
                    copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    copy.transform.localScale = Vector3.one;
                    int vertices = 0;
                    foreach (var mesh in copy.GetComponentsInChildren<MeshFilter>()) vertices += mesh.sharedMesh.vertexCount;
                    if (vertices <= 40) throw new InvalidOperationException("Village shrub is only a blockout.");
                    if (PrefabUtility.SaveAsPrefabAsset(copy, Root + copy.name + ".prefab") == null)
                        throw new InvalidOperationException("Could not save roadside shrub.");
                    Debug.Log("ARE_ROADSIDE_SHRUB " + index + " vertices=" + vertices);
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
                if (++index == 2) break;
            }
            if (index != 2) throw new InvalidOperationException("Two reviewed village shrubs are required.");
            AssetDatabase.SaveAssets();
        }
    }
}
