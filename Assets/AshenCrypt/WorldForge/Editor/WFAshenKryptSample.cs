using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    /// <summary>
    /// Creates a ready-to-generate Ashen Krypt world (profile, biome, TerrainLayers, terrain-tree wrappers)
    /// from the Ashen Krypt nature package that lives in Assets/Studio/Nature/AshenKrypt.
    /// The numbers come from WFAshenKryptSampleData.cs (generated from Reference/ashen_krypt_sample.py).
    /// </summary>
    public static class WFAshenKryptSample
    {
        private const string AK = "Assets/Studio/Nature/AshenKrypt";
        private const string Data = "Assets/WorldForgeData/AshenKrypt";

        [MenuItem("Tools/World Forge/Create Ashen Krypt Sample World", priority = 20)]
        public static void CreateAndOpen()
        {
            WorldProfile p = CreateSample();
            if (p != null) WFWorldDesignerWindow.Open(p);
        }

        public static WorldProfile CreateSample()
        {
            var missing = new List<string>();
            WFEditorUtil.EnsureFolder(Data + "/TerrainLayers");
            WFEditorUtil.EnsureFolder(Data + "/Prefabs");
            WFEditorUtil.EnsureFolder(Data + "/Generated");

            if (!AssetDatabase.IsValidFolder(AK + "/Prefabs/Generated")) TryRunAshenKryptSetup();

            string tex = AK + "/World/Textures/";
            string normal = tex + "ashen_world_ground_normal.png";
            var layers = new Dictionary<string, TerrainLayer>
            {
                { "Ash", MakeLayer("Ash", tex + "ashen_world_ground_basecolor.png", normal, 14f, 0.06f, missing) },
                { "Basalt", MakeLayer("Basalt", tex + "ashen_world_basalt_color.png", normal, 10f, 0.10f, missing) },
                { "LichenCrust", MakeLayer("LichenCrust", tex + "ashen_world_lichen_crust_color.png", normal, 9f, 0.05f, missing) },
                { "PathScoria", MakeLayer("PathScoria", tex + "ashen_world_path_scoria_color.png", normal, 6f, 0.12f, missing) }
            };

            Func<string, GameObject> objectPrefab = id =>
            {
                GameObject go = WFEditorUtil.LoadFirst<GameObject>(AK + "/Prefabs/Generated/" + id + ".prefab", AK + "/Models/" + id + ".fbx");
                if (go == null) missing.Add(AK + "/Models/" + id + ".fbx");
                return go;
            };
            Func<string, GameObject> treePrefab = id =>
            {
                GameObject model = objectPrefab(id);
                return model != null ? WFEditorUtil.CreateTreeWrapper(model, Data + "/Prefabs") : null;
            };
            Func<string, Texture2D> detailTexture = id =>
            {
                string path = FindAssetPath(id, "t:Texture2D");
                if (path == null) { missing.Add(id + ".png (WorldForge/Content/Grass)"); return null; }
                return WFEditorUtil.ConfigureTexture(path, false, true);
            };

            BiomeProfile biome = LoadOrCreate<BiomeProfile>(Data + "/WF_Biome_AshenKrypt.asset");
            WFAshenKryptSampleData.FillTextureRules(biome, n => layers[n]);
            WFAshenKryptSampleData.FillSpawnRules(biome, objectPrefab, treePrefab, detailTexture);
            EditorUtility.SetDirty(biome);

            WorldProfile p = LoadOrCreate<WorldProfile>(Data + "/WF_World_AshenKrypt_256m.asset");
            p.tilesX = 4; p.tilesZ = 4; p.tileSize = 64f; p.maxHeight = 32f;
            p.heightmapResolution = 129; p.alphamapResolution = 128; p.detailResolution = 128; p.detailResolutionPerPatch = 32;
            p.seed = WFAshenKryptSampleData.Seed;
            p.quality = QualityPreset.PC_ThirdPerson;
            p.maxWalkableSlope = 38f;
            p.createPoiObjects = true;
            p.rootObjectName = "AshenKrypt_World_256m";
            p.outputFolder = Data + "/Generated";
            p.biome = biome;
            WFAshenKryptSampleData.FillHeightLayers(p);
            WFAshenKryptSampleData.FillPaths(p);
            WFAshenKryptSampleData.FillPois(p);
            // Optional: start from the old Blender v004 heightfield instead of the stamps (disabled by default).
            p.heightLayers.Insert(0, new WFImportRawLayer { enabled = false, label = "Legacy Blender heightmap v004 (optional)" });
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            Selection.activeObject = p;

            if (missing.Count > 0)
            {
                var sb = new StringBuilder("The sample profile was created, but these assets were not found:\n\n");
                foreach (string m in new HashSet<string>(missing)) sb.AppendLine("• " + m);
                sb.AppendLine("\nImport the Ashen Krypt nature package into " + AK + " and run this command again.");
                EditorUtility.DisplayDialog("World Forge sample", sb.ToString(), "OK");
            }
            return p;
        }

        private static TerrainLayer MakeLayer(string name, string diffusePath, string normalPath, float tile, float smoothness, List<string> missing)
        {
            string path = Data + "/TerrainLayers/WF_TL_" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            Texture2D diffuse = WFEditorUtil.ConfigureTexture(diffusePath, false);
            Texture2D normal = WFEditorUtil.ConfigureTexture(normalPath, true);
            if (diffuse == null) missing.Add(diffusePath);
            if (normal == null) missing.Add(normalPath);
            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = normal;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tile, tile);
            layer.tileOffset = Vector2.zero;
            layer.smoothness = smoothness;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        private static string FindAssetPath(string fileName, string filter)
        {
            foreach (string guid in AssetDatabase.FindAssets(fileName + " " + filter))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == fileName) return p;
            }
            return null;
        }

        /// <summary>Runs the v003 'Generate Materials and Prefabs' tool (PBR material remap) if it is in the project.</summary>
        private static void TryRunAshenKryptSetup()
        {
            Type t = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("AshenCrypt.WorldForge.Nature.EditorTools.AshenKryptAssetSetup");
                if (t != null) break;
            }
            if (t == null) return;
            MethodInfo m = t.GetMethod("Generate", BindingFlags.Public | BindingFlags.Static);
            if (m == null) return;
            if (EditorUtility.DisplayDialog("World Forge sample",
                    "The Ashen Krypt materials/prefabs have not been generated yet. Run 'Tools ▸ World Forge ▸ Ashen Krypt Nature ▸ Generate Materials and Prefabs' now? (recommended – gives the trees and props their PBR bark/ground materials)",
                    "Run it", "Skip"))
                m.Invoke(null, null);
        }
    }
}
