using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AreArpg.Editor
{
    public static class WerkarsenalBaker
    {
        const string Source = "Assets/Resources/Equipment/Werkarsenal";
        const string Targets = "Assets/Resources/ARE/Visuals/Werkarsenal";
        static readonly string[] PartNames = {
            "R_Gratkeule", "R_Splitterzahn", "R_Scharrbeil", "R_Rippenschild", "R_Grabschild",
            "G_Ofengabel", "G_Zollmesser", "G_Hammerbeil", "G_Zollbuckler", "G_Zunderpfanne",
            "M_Wehrsaebel", "M_Lotklinge", "M_Jochbeil", "M_Wehrschild", "M_Glockenring",
            "R_Wangenmaske", "R_Gratharnisch", "R_Scharrstulpe_L", "R_Scharrstulpe_R",
            "R_Gratschiene_L", "R_Gratschiene_R"
        };

        [MenuItem("Ashen Crypt/Ausrüstung/Werkarsenal backen")]
        public static void Bake()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string fbx = Source + "/Werkarsenal.fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Werkarsenal-FBX fehlt.");
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.importBlendShapes = false;
            importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) throw new InvalidOperationException("Werkarsenal-Modell fehlt nach dem Import.");
            var filters = model.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name, StringComparer.Ordinal);
            if (filters.Count != PartNames.Length || PartNames.Any(n => !filters.ContainsKey(n)))
                throw new InvalidOperationException("Die 21 benannten Werkarsenal-Meshes sind im Modell unvollständig.");
            var material = Atlas();
            if (!AssetDatabase.IsValidFolder(Targets))
                AssetDatabase.CreateFolder("Assets/Resources/ARE/Visuals", "Werkarsenal");
            foreach (var name in PartNames)
            {
                ConfigureIcon(name);
                SavePiece(name, filters[name], model.transform, material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Werkarsenal: 21 passive native Prefabs, alle mit vollständigem UV-Atlas.");
        }

        static void ConfigureIcon(string piece)
        {
            string path = Source + "/Icons/" + piece + ".png";
            var texture = AssetImporter.GetAtPath(path) as TextureImporter;
            if (texture == null) throw new InvalidOperationException("Gerendertes Objektbild fehlt: " + piece);
            texture.textureType = TextureImporterType.Default;
            texture.sRGBTexture = true;
            texture.alphaSource = TextureImporterAlphaSource.FromInput;
            texture.alphaIsTransparency = true;
            texture.mipmapEnabled = true;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.maxTextureSize = 256;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
            texture.SaveAndReimport();
        }

        static Texture2D LoadMap(string kind)
        {
            string path = Source + "/Werkarsenal_" + kind + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) throw new InvalidOperationException("Textur fehlt: " + path);
            ti.textureType = kind == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = kind == "BaseColor";
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 2;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Atlas()
        {
            var baseMap = LoadMap("BaseColor");
            var normal = LoadMap("Normal");
            var metalSmooth = LoadMap("MetalSmooth");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Das URP-Lit-Material ist im gewählten Projekt nicht vorhanden.");
            string path = Source + "/Werkarsenal_Atlas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Werkarsenal_Atlas" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", metalSmooth);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_BumpScale", .62f);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);
            return material;
        }

        static void SavePiece(string name, MeshFilter source, Transform modelRoot, Material material)
        {
            var mesh = source.sharedMesh;
            if (mesh == null || mesh.subMeshCount != 1 || mesh.GetIndexCount(0) < 30 ||
                !mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
                throw new InvalidOperationException("Ungültiges Originalmesh oder UV-Atlas: " + name);
            var root = new GameObject(name);
            try
            {
                var child = new GameObject(name + "_Art");
                child.transform.SetParent(root.transform, false);
                // Preserve the importer's genuine basis and metric conversion inside a neutral grip.
                Matrix4x4 local = modelRoot.worldToLocalMatrix * source.transform.localToWorldMatrix;
                child.transform.localPosition = local.GetColumn(3);
                child.transform.localRotation = local.rotation;
                child.transform.localScale = local.lossyScale;
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var size = renderer.bounds.size;
                if (size.magnitude < .14f || size.magnitude > 2.2f)
                    throw new InvalidOperationException("Unplausibler nativer Maßstab: " + name + " " + size);
                string path = Targets + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok) throw new InvalidOperationException("Speichern fehlgeschlagen: " + path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var loaded = prefab.GetComponentInChildren<MeshFilter>();
                if (loaded == null || loaded.sharedMesh != mesh || prefab.GetComponentInChildren<Renderer>().sharedMaterial != material)
                    throw new InvalidOperationException("Prefab hat beim Wiederöffnen Mesh oder Material verloren: " + name);
                Debug.Log("Werkarsenal " + name + " " + mesh.GetIndexCount(0)/3u + " Dreiecke " + size.ToString("F3") + " m");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
