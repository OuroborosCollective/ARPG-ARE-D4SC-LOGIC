using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditorTools
{
    public static class BorderlandPrefabBaker
    {
        const string Source = "Assets/AshenCrypt/BorderlandArt";
        const string Destination = "Assets/Resources/ARE/Borderland";

        [MenuItem("Tools/World Forge/Grenzregion/Projektions-Prefabs backen")]
        public static void Bake()
        {
            string[] paths = AssetDatabase.FindAssets("t:Model", new[] { Source })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            if (paths.Length == 0) throw new InvalidOperationException("Keine WorldForge-FBX im Grenzregion-Kit.");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/BorderlandAtlas.png");
            if (texture == null) throw new InvalidOperationException("Der gemeinsame Farbatlas fehlt.");
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("URP Simple Lit fehlt.");
            const string matPath = Source + "/Borderland.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Borderland" };
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .17f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            if (!AssetDatabase.IsValidFolder(Destination))
                AssetDatabase.CreateFolder("Assets/Resources/ARE", "Borderland");
            var luminous = AssetDatabase.LoadAssetAtPath<Material>(Source + "/BorderlandEmbers.mat");
            if (luminous == null)
            {
                luminous = new Material(material);
                AssetDatabase.CreateAsset(luminous, Source + "/BorderlandEmbers.mat");
            }
            luminous.CopyPropertiesFromMaterial(material);
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/BorderlandEmission.png");
            if (emission == null) throw new InvalidOperationException("Emissionsatlas fehlt.");
            luminous.SetTexture("_EmissionMap", emission);
            luminous.SetColor("_EmissionColor", Color.white * .92f);
            luminous.EnableKeyword("_EMISSION");
            luminous.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(luminous);
            AssetDatabase.SaveAssetIfDirty(luminous);
            BakeGround();
            foreach (string path in paths)
            {
                string key = System.IO.Path.GetFileNameWithoutExtension(path);
                BakeModel(path, key == "BloodcapBank" || key == "Brazier" ? luminous : material);
            }
            Debug.Log("BORDERLAND_BAKED " + paths.Length);
        }

        private static void BakeGround()
        {
            string maskPath = Source + "/AshSnowMask.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            if (importer == null) throw new InvalidOperationException("Schneeasche-Textur fehlt.");
            if (importer.sRGBTexture || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
            var shader = Resources.Load<Shader>("BorderlandGround");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Schneeasche-Shader fehlerhaft.");
            string path = Destination + "/AshSnow.mat";
            var ground = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (ground == null)
            {
                ground = new Material(shader);
                AssetDatabase.CreateAsset(ground, path);
            }
            ground.shader = shader;
            ground.SetTexture("_Mask", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            EditorUtility.SetDirty(ground);
            AssetDatabase.SaveAssetIfDirty(ground);
        }

        static void BakeModel(string path, Material material)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            // The outer tree line combines only the lowest pine LOD into four ordinary renderers.
            bool readForCanopy = path.EndsWith("/AshPine.fbx", StringComparison.Ordinal);
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation ||
                importer.addCollider || importer.isReadable != readForCanopy)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.addCollider = false;
                importer.isReadable = readForCanopy;
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = new GameObject(model.name);
            try
            {
                var copy = UnityEngine.Object.Instantiate(model, root.transform);
                copy.name = "Meshes";
                var renderers = copy.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length != 3) throw new InvalidOperationException(path + " requires three mesh renderers.");
                var lods = new LOD[3];
                float[] thresholds = { .22f, .075f, .009f };
                for (int i = 0; i < lods.Length; i++)
                {
                    string expected = model.name + "Detail" + i;
                    var renderer = renderers.Single(r => r.name == expected);
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = i == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                    renderer.receiveShadows = true;
                    lods[i] = new LOD(thresholds[i], new Renderer[] { renderer });
                }
                var group = copy.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();
                var bounds = lods[0].renderers[0].bounds;
                var shape = root.AddComponent<BoxCollider>();
                shape.center = bounds.center;
                shape.size = bounds.size;
                shape.enabled = false;
                if (!PrefabUtility.SaveAsPrefabAsset(root, Destination + "/" + model.name + ".prefab"))
                    throw new InvalidOperationException("Prefab konnte nicht gespeichert werden: " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
