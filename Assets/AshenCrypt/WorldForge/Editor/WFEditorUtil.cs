using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    internal static class WFEditorUtil
    {
        /// <summary>Creates every missing folder of an "Assets/..." path.</summary>
        public static void EnsureFolder(string assetFolder)
        {
            assetFolder = assetFolder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string[] parts = assetFolder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new ArgumentException("Folder must start with 'Assets': " + assetFolder);
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public static string SafeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Unnamed";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        public static Texture2D ConfigureTexture(string path, bool normalMap, bool alphaTransparency = false, bool readable = false)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                TextureImporterType wanted = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != wanted) { importer.textureType = wanted; changed = true; }
                if (importer.sRGBTexture == normalMap) { importer.sRGBTexture = !normalMap; changed = true; }
                if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
                if (alphaTransparency && !importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    changed = true;
                }
                if (readable && !importer.isReadable) { importer.isReadable = true; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Makes sure texture stamps can be read on the CPU.</summary>
        public static void EnsureReadable(Texture2D tex)
        {
            if (tex == null || tex.isReadable) return;
            string path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Wraps a model in a prefab with a LODGroup on the root, which is what Unity terrain trees require.
        /// Returns the existing wrapper if it is already there.
        /// </summary>
        public static GameObject CreateTreeWrapper(GameObject model, string folder, float cullScreenHeight = 0.012f)
        {
            if (model == null) return null;
            EnsureFolder(folder);
            string path = folder + "/WF_Tree_" + SafeName(model.name) + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("WF_Tree_" + model.name);
            try
            {
                var inst = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (inst == null) inst = UnityEngine.Object.Instantiate(model);
                inst.transform.SetParent(root.transform, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
                var lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(cullScreenHeight, renderers) });
                lod.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static bool IsValidTreePrototype(GameObject prefab, out string reason)
        {
            reason = null;
            if (prefab == null) { reason = "missing prefab"; return false; }
            if (prefab.GetComponent<LODGroup>() != null) return true;
            if (prefab.GetComponent<MeshFilter>() != null && prefab.GetComponent<MeshRenderer>() != null) return true;
            reason = prefab.name + " needs a LODGroup or MeshFilter+MeshRenderer on its root (use 'Wrap as terrain tree').";
            return false;
        }

        public static T LoadFirst<T>(params string[] candidatePaths) where T : UnityEngine.Object
        {
            foreach (string p in candidatePaths)
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(p);
                if (a != null) return a;
            }
            return null;
        }

        public static List<T> FindAssets<T>(string filter, string folder) where T : UnityEngine.Object
        {
            var list = new List<T>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list;
        }
    }
}
