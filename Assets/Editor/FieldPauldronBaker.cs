using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AreArpg.Editor
{
    public static class FieldPauldronBaker
    {
        private const string Folder = "Assets/Resources/Equipment/Feldschulter/";
        private const string ModelPath = Folder + "Feldschulter.fbx";
        private const string MaterialPath = Folder + "Feldschulter_Atlas.mat";
        private const string PrefabPath = Folder + "Feldschulter_Wearable.prefab";

        [MenuItem("Ashen Crypt/Ausrüstung/Feldschulter backen")]
        public static void Bake()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("FBX fehlt: " + ModelPath);
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.bakeAxisConversion = true;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();

            var baseMap = ImportTexture("BaseColor", true);
            var normal = ImportTexture("Normal", false);
            var metalSmooth = ImportTexture("MetalSmooth", false);
            ImportTexture("Icon", true);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit fehlt.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Feldschulter_Atlas" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", metalSmooth);
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);

            var root = new GameObject("Feldschulter_Wearable");
            try
            {
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform, false);
                model.name = "Feldschulter";
                foreach (var imported in model.GetComponentsInChildren<LODGroup>(true))
                    UnityEngine.Object.DestroyImmediate(imported);
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length != 3) throw new InvalidOperationException("Drei eigene LOD-Meshes erwartet.");
                float[] transitions = { 0.14f, 0.047f, 0.002f };
                var lods = new LOD[transitions.Length];
                ulong previous = ulong.MaxValue;
                for (int i = 0; i < lods.Length; ++i)
                {
                    var renderer = renderers.Single(r => r.name == "Feldschulter_LOD" + i);
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    ulong triangles = mesh.GetIndexCount(0) / 3u;
                    if (mesh.subMeshCount != 1 || triangles == 0 || triangles >= previous)
                        throw new InvalidOperationException("LOD ohne absteigende Geometrie oder geteilten Atlas: " + renderer.name);
                    var size = renderer.bounds.size;
                    if (size.magnitude < 0.12f || size.magnitude > 0.85f)
                        throw new InvalidOperationException("Maßstab in Metern prüfen: " + renderer.name + " " + size);
                    previous = triangles;
                    renderer.enabled = true;
                    renderer.sharedMaterial = material;
                    lods[i] = new LOD(transitions[i], new Renderer[] { renderer });
                    Debug.Log(renderer.name + ": " + triangles + " Dreiecke; " + size.ToString("F4") + " m");
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var saved);
                if (!saved) throw new InvalidOperationException("Feldschulter-Prefab konnte nicht gespeichert werden.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            FieldPauldronPrivateFitBaker.Bake();
        }

        private static Texture2D ImportTexture(string kind, bool srgb)
        {
            string path = Folder + "Feldschulter_" + kind + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Textur fehlt: " + path);
            importer.textureType = kind == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = kind == "Icon";
            importer.mipmapEnabled = true;
            importer.anisoLevel = kind == "Icon" ? 1 : 4;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
