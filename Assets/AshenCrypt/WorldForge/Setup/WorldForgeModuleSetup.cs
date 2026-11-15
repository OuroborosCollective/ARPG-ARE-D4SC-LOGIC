using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace AshenCrypt.WorldForge.Setup
{
    /// <summary>
    /// World Forge needs Unity's built-in Terrain and Terrain Physics modules. The terrain-dependent assemblies
    /// are compiled only when both are present, so importing the package can never break the project build.
    /// This tiny editor assembly has no terrain dependency and offers to enable the modules.
    /// </summary>
    [InitializeOnLoad]
    internal static class WorldForgeModuleSetup
    {
        private const string Terrain = "com.unity.modules.terrain";
        private const string TerrainPhysics = "com.unity.modules.terrainphysics";
        private const string AskedKey = "WorldForge.ModuleSetup.Asked";

        static WorldForgeModuleSetup()
        {
#if !(WF_HAS_TERRAIN && WF_HAS_TERRAINPHYSICS)
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(AskedKey, false)) return;
                SessionState.SetBool(AskedKey, true);
                if (EditorUtility.DisplayDialog("World Forge",
                        "World Forge needs Unity's built-in 'Terrain' and 'Terrain Physics' modules, which are not enabled in this project.\n\n" +
                        "Enable them now? (adds com.unity.modules.terrain and com.unity.modules.terrainphysics to Packages/manifest.json)",
                        "Enable modules", "Later"))
                    EnableModules();
            };
#endif
        }

        [MenuItem("Tools/World Forge/Enable Terrain Modules (required)", priority = 100)]
        private static void EnableModules()
        {
            Client.AddAndRemove(new[] { Terrain, TerrainPhysics }, null);
            Debug.Log("[WorldForge] Requested " + Terrain + " and " + TerrainPhysics + ". Unity recompiles; then open Tools ▸ World Forge ▸ World Designer.");
        }

        [MenuItem("Tools/World Forge/Enable Terrain Modules (required)", true)]
        private static bool EnableModulesValidate()
        {
#if WF_HAS_TERRAIN && WF_HAS_TERRAINPHYSICS
            return false;
#else
            return true;
#endif
        }
    }
}
