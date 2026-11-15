#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;

namespace AshenCrypt.WorldForge.Nature.EditorTools
{

/// <summary>
/// Imports Ashen Krypt FBXs, maps the shared ground/bark PBR materials and builds tile/prop prefabs.
/// Run from Tools > Ashen Krypt > Generate Materials and Prefabs after importing this package.
/// </summary>
public static class AshenKryptAssetSetup
{
    private const string GroundBase = "ashen_ground_basecolor.png";
    private const string GroundNormal = "ashen_ground_normal.png";
    private const string GroundRough = "ashen_ground_roughness.png";
    private const string BarkBase = "ashen_bark_basecolor.png";
    private const string BarkNormal = "ashen_bark_normal.png";
    private const string BarkRough = "ashen_bark_roughness.png";
    private static readonly int[] FourRotations = { 0, 90, 180, 270 };

    private static readonly Dictionary<string, int[]> TileRotations = new Dictionary<string, int[]>
    {
        { "ashen_path_straight_ns_4m_a", new[] { 0, 90 } },
        { "ashen_path_cross_4m_a", new[] { 0 } },
        { "ashen_path_t_junction_4m_a", FourRotations },
        { "ashen_path_corner_4m_a", FourRotations },
        { "ashen_path_dead_end_4m_a", FourRotations },
        { "ashen_nature_shrub_mushroom_4m_a", FourRotations },
        { "ashen_nature_bush_fungus_4m_b", FourRotations },
        { "ashen_mushroom_only_4m_a", FourRotations },
        { "ashen_mushroom_only_4m_b", FourRotations }
    };

    [MenuItem("Tools/World Forge/Ashen Krypt Nature/Generate Materials and Prefabs")]
    public static void Generate()
    {
        string packageRoot = FindPackageRoot();
        if (string.IsNullOrEmpty(packageRoot))
        {
            EditorUtility.DisplayDialog("Ashen Krypt setup", "Could not locate the AshenKrypt package folder. Import the package folder, including its Editor subfolder, into Assets first.", "OK");
            return;
        }

        string modelDir = packageRoot + "/Models";
        string textureDir = packageRoot + "/Textures";
        string materialDir = packageRoot + "/Materials/Generated";
        string prefabDir = packageRoot + "/Prefabs/Generated";
        EnsureFolder(packageRoot, "Materials");
        EnsureFolder(packageRoot + "/Materials", "Generated");
        EnsureFolder(packageRoot, "Prefabs");
        EnsureFolder(packageRoot + "/Prefabs", "Generated");

        try
        {
            ConfigureTexture(textureDir + "/" + GroundBase, TextureImporterType.Default, true, false);
            ConfigureTexture(textureDir + "/" + GroundNormal, TextureImporterType.NormalMap, false, false);
            ConfigureTexture(textureDir + "/" + GroundRough, TextureImporterType.Default, false, true);
            ConfigureTexture(textureDir + "/" + BarkBase, TextureImporterType.Default, true, false);
            ConfigureTexture(textureDir + "/" + BarkNormal, TextureImporterType.NormalMap, false, false);
            ConfigureTexture(textureDir + "/" + BarkRough, TextureImporterType.Default, false, true);
            AssetDatabase.Refresh();

            string groundSmooth = BuildSmoothnessMap(textureDir + "/" + GroundRough, materialDir + "/ashen_ground_smoothness.png");
            string barkSmooth = BuildSmoothnessMap(textureDir + "/" + BarkRough, materialDir + "/ashen_bark_smoothness.png");
            AssetDatabase.Refresh();

            Material groundMaterial = CreatePbrMaterial(
                materialDir + "/AK_Ground_AshenPBR.mat", "AK_Ground_AshenPBR",
                textureDir + "/" + GroundBase, textureDir + "/" + GroundNormal, groundSmooth);
            Material barkMaterial = CreatePbrMaterial(
                materialDir + "/AK_Bark_CharredPBR.mat", "AK_Bark_CharredPBR",
                textureDir + "/" + BarkBase, textureDir + "/" + BarkNormal, barkSmooth);
            AssetDatabase.SaveAssets();

            string[] fbxPaths = Directory.Exists(ToAbsolute(modelDir))
                ? Directory.GetFiles(ToAbsolute(modelDir), "*.fbx", SearchOption.TopDirectoryOnly)
                    .Select(p => ToAssetPath(p)).OrderBy(p => p, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            if (fbxPaths.Length == 0)
                throw new InvalidOperationException("No FBX files found in " + modelDir);

            int mapped = 0;
            foreach (string fbxPath in fbxPaths)
                mapped += ConfigureFbxAndRemap(fbxPath, groundMaterial, barkMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int prefabs = 0;
            foreach (string fbxPath in fbxPaths)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (model == null)
                    throw new InvalidOperationException("Unity did not load the imported model: " + fbxPath);

                string id = Path.GetFileNameWithoutExtension(fbxPath);
                bool tile = IsTile(id);
                int[] rotations = TileRotations.TryGetValue(id, out int[] configured) ? configured : new[] { 0 };
                foreach (int degrees in rotations)
                {
                    string suffix = degrees == 0 ? "" : "_r" + degrees.ToString("000");
                    string prefabPath = prefabDir + "/" + id + suffix + ".prefab";
                    SavePrefab(model, prefabPath, id + suffix, degrees, tile);
                    prefabs++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Ashen Krypt setup complete: {fbxPaths.Length} FBXs, {mapped} external PBR material remaps, {prefabs} prefabs. Generated smoothness maps invert the supplied roughness maps into Unity's smoothness channel.");
            EditorUtility.DisplayDialog("Ashen Krypt setup complete", $"Imported and processed {fbxPaths.Length} FBX models.\nPBR material remaps: {mapped}\nGenerated prefabs: {prefabs}\n\nInspect the console and Generated folders before committing to your project.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Ashen Krypt setup failed", ex.Message + "\nSee the Unity Console for details.", "OK");
        }
    }

    private static string FindPackageRoot()
    {
        string[] guids = AssetDatabase.FindAssets("AshenKryptAssetSetup t:MonoScript");
        foreach (string guid in guids)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!scriptPath.Equals("Assets/Studio/Nature/AshenKrypt/Editor/AshenKryptAssetSetup.cs", StringComparison.Ordinal)) continue;
            return Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)).Replace('\\', '/');
        }
        return null;
    }

    private static string ToAbsolute(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string ToAssetPath(string absolutePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        if (!full.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generated asset path is outside the Unity project: " + full);
        return full.Substring(projectRoot.Length + 1);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static void ConfigureTexture(string assetPath, TextureImporterType type, bool srgb, bool readable)
    {
        if (!File.Exists(ToAbsolute(assetPath)))
            throw new FileNotFoundException("Required PBR texture is missing", ToAbsolute(assetPath));
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Unity texture importer is unavailable for " + assetPath);
        bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.isReadable != readable || !importer.mipmapEnabled;
        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.isReadable = readable;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = 2048;
        if (changed) importer.SaveAndReimport();
    }

    private static string BuildSmoothnessMap(string roughnessPath, string outputPath)
    {
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);
        if (source == null) throw new InvalidOperationException("Could not load roughness map: " + roughnessPath);
        var importer = AssetImporter.GetAtPath(roughnessPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Could not access roughness texture importer: " + roughnessPath);
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            source = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);
        }

        Color[] input = source.GetPixels();
        Color[] output = new Color[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            float smoothness = 1f - Mathf.Clamp01(input[i].grayscale);
            // Unity Lit metallic map: metallic in RGB, smoothness in alpha. HDRP Mask Map uses AO=1 in G.
            output[i] = new Color(0f, 1f, 0f, smoothness);
        }
        var packed = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, true);
        packed.SetPixels(output);
        packed.Apply(true, false);
        string absoluteOutput = ToAbsolute(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput));
        File.WriteAllBytes(absoluteOutput, packed.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(packed);
        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

        importer = AssetImporter.GetAtPath(roughnessPath) as TextureImporter;
        if (importer != null && importer.isReadable)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
        var outputImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;
        if (outputImporter != null)
        {
            outputImporter.textureType = TextureImporterType.Default;
            outputImporter.sRGBTexture = false;
            outputImporter.isReadable = false;
            outputImporter.mipmapEnabled = true;
            outputImporter.SaveAndReimport();
        }
        return outputPath;
    }

    private static Shader FindLitShader()
    {
        string activePipeline = GraphicsSettings.currentRenderPipeline != null
            ? GraphicsSettings.currentRenderPipeline.GetType().FullName
            : string.Empty;
        if (activePipeline.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Shader hdrp = Shader.Find("HDRP/Lit");
            if (hdrp != null) return hdrp;
        }
        if (activePipeline.IndexOf("UniversalRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null) return urp;
        }
        string[] candidates = { "Universal Render Pipeline/Lit", "HDRP/Lit", "Standard" };
        foreach (string candidate in candidates)
        {
            Shader shader = Shader.Find(candidate);
            if (shader != null) return shader;
        }
        throw new InvalidOperationException("No supported Lit shader found. Install URP/HDRP or use the Built-in Render Pipeline Standard shader.");
    }

    private static Material CreatePbrMaterial(string assetPath, string materialName, string basePath, string normalPath, string smoothnessPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            material = new Material(FindLitShader()) { name = materialName };
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else material.shader = FindLitShader();
        material.name = materialName;
        Texture baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
        Texture normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture smoothMap = AssetDatabase.LoadAssetAtPath<Texture2D>(smoothnessPath);
        SetTexture(material, baseMap, "_BaseMap", "_BaseColorMap", "_MainTex");
        SetTexture(material, normalMap, "_BumpMap", "_NormalMap");
        SetTexture(material, smoothMap, "_MetallicGlossMap", "_MaskMap");
        SetColor(material, Color.white, "_BaseColor", "_Color");
        SetFloat(material, 0.72f, "_Smoothness", "_Glossiness");
        SetFloat(material, 0f, "_Metallic");
        if (material.HasProperty("_BumpMap") || material.HasProperty("_NormalMap")) material.EnableKeyword("_NORMALMAP");
        if (material.HasProperty("_MetallicGlossMap")) material.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetTexture(Material material, Texture texture, params string[] properties)
    {
        foreach (string property in properties)
            if (material.HasProperty(property)) material.SetTexture(property, texture);
    }

    private static void SetColor(Material material, Color color, params string[] properties)
    {
        foreach (string property in properties)
            if (material.HasProperty(property)) material.SetColor(property, color);
    }

    private static void SetFloat(Material material, float value, params string[] properties)
    {
        foreach (string property in properties)
            if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static int ConfigureFbxAndRemap(string fbxPath, Material groundMaterial, Material barkMaterial)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("ModelImporter unavailable for " + fbxPath);
        importer.importAnimation = false;
        importer.addCollider = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.SaveAndReimport();

        importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        int mapped = 0;
        foreach (Material source in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>())
        {
            Material target = source.name == "AK_Ground_AshenPBR" ? groundMaterial :
                              source.name == "AK_Bark_CharredPBR" ? barkMaterial : null;
            if (target == null) continue;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), target);
            mapped++;
        }
        if (mapped > 0)
        {
            AssetDatabase.WriteImportSettingsIfDirty(fbxPath);
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
        }
        return mapped;
    }

    private static bool IsTile(string id)
    {
        return id.Contains("_4m_") || id == "ashen_ground_4m_cracked_a" || id == "ashen_forest_ground_4m_tile_a";
    }

    private static void SavePrefab(GameObject model, string prefabPath, string prefabName, int rotationY, bool tile)
    {
        var root = new GameObject(prefabName);
        try
        {
            var nested = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (nested == null) throw new InvalidOperationException("Could not instantiate model prefab: " + model.name);
            nested.name = model.name + "_Model";
            nested.transform.SetParent(root.transform, false);
            Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);
            nested.transform.localRotation = rotation;
            if (tile)
            {
                Vector3 centre = new Vector3(2f, 0f, 2f);
                nested.transform.localPosition = centre - rotation * centre;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(2f, 0f, 2f);
                collider.size = new Vector3(4f, 0.30f, 4f);
            }
            else
            {
                nested.transform.localPosition = Vector3.zero;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
}
#endif
