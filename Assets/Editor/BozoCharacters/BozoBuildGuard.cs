using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// The purchased BoZo pack keeps all its parts (meshes, 2048 textures) in Resources folders so its creator can
    /// load them by name. The game only ships baked figures, so those folders must not enter a player build
    /// (they would add the whole pack to the APK). They are renamed for the duration of a build and restored after.
    /// </summary>
    public sealed class BozoBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string VendorRoot = "Assets/BoZo_StylizedModularCharacters";
        public const string Hidden = "ResourcesNotInBuild";
        public int callbackOrder { get { return -1000; } }

        public void OnPreprocessBuild(BuildReport report) { Exclude(); }
        public void OnPostprocessBuild(BuildReport report) { Restore(); }

        /// <summary>A failed or cancelled build never reaches the postprocess step: restore on the next editor load.</summary>
        [InitializeOnLoadMethod]
        static void RestoreAfterInterruptedBuild()
        {
            EditorApplication.delayCall += () => { if (!BuildPipeline.isBuildingPlayer) Restore(); };
        }

        public static string[] Folders(string name)
        {
            var found = new List<string>();
            if (!AssetDatabase.IsValidFolder(VendorRoot)) return found.ToArray();
            foreach (string dir in Directory.GetDirectories(VendorRoot, name, SearchOption.AllDirectories))
                found.Add(dir.Replace('\\', '/'));
            found.Sort(StringComparer.Ordinal);
            return found.ToArray();
        }

        /// <summary>Rename every vendor Resources folder; returns how many were hidden.</summary>
        public static int Exclude() { return Rename("Resources", Hidden); }

        /// <summary>Give the vendor Resources folders their name back; returns how many were restored.</summary>
        public static int Restore() { return Rename(Hidden, "Resources"); }

        static int Rename(string from, string to)
        {
            string[] folders = Folders(from);
            if (folders.Length == 0) return 0;
            // Children must still be reachable under their recorded path when RenameAsset runs.
            Array.Sort(folders, (a, b) =>
            {
                int depth = b.Split('/').Length.CompareTo(a.Split('/').Length);
                return depth != 0 ? depth : StringComparer.Ordinal.Compare(a, b);
            });
            int count = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string folder in folders)
                {
                    string error = AssetDatabase.RenameAsset(folder, to);
                    if (string.IsNullOrEmpty(error)) count++;
                    else Debug.LogError("BoZo build guard could not rename " + folder + ": " + error);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            if (count != folders.Length) throw new BuildFailedException("BoZo build guard: " + (folders.Length - count) + " folder(s) could not be renamed.");
            return count;
        }
    }
}
