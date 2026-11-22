using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AreArpg.EditorTools
{
    public static class AshWorksBaker
    {
        private const string Folder = "Assets/Resources/Equipment/AshWorks/";
        private const string Display = "Assets/Resources/ARE/Visuals/Werkarsenal/";

        [MenuItem("Ashen Crypt/Ausrüstung/Regionale Arbeitsmonturen backen")]
        public static void Bake()
        {
            Directory.CreateDirectory(Display);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var guard = Model("Schlackengarde");
            var bell = Model("Glockentraegerin");
            var brand = Model("Brandreif");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP/Lit fehlt.");
            var materialPath = Folder + "Werkstoff.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!mat)
            {
                mat = new Material(shader) {name = "Werkstoff"};
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", ImportTexture("Werkstoff_BaseColor", true, false));
            mat.SetTexture("_MetallicGlossMap", ImportTexture("Werkstoff_MetalSmooth", false, false));
            mat.SetTexture("_BumpMap", ImportTexture("Werkstoff_Normal", false, true));
            mat.SetFloat("_WorkflowMode", 1f);
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_BumpScale", .60f);
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(mat);
            foreach (string name in new[] {"Schlackengarde", "Glockentraegerin", "Brandreif"})
                ImportTexture(name + "_Icon", true, false);
            Single(guard, "Garde_Ofenhaube", Folder+"Schlackengarde_Fund.prefab", mat);
            Single(bell, "Glocke_Handglocke", Folder+"Glockentraegerin_Fund.prefab", mat);
            Single(brand, "Brandreif", Folder+"Brandreif_Fund.prefab", mat);
            Single(guard, "Garde_Schlackenschild", Display+"G_Ziegelschild.prefab", mat);
            Single(guard, "Garde_Schuerhaken", Display+"G_Schuerhaken.prefab", mat);
            Single(bell, "Glocke_Handglocke", Display+"M_Handglocke.prefab", mat);
            AssetDatabase.SaveAssets();
            AreArpg.Editor.FieldPauldronPrivateFitBaker.BakeRegionalFits();
            Debug.Log("AshWorks: zwei Monturen, Brandreif, drei Fundmodelle, drei Handobjekte und URP-Atlas gebacken.");
        }

        private static Texture2D ImportTexture(string name, bool srgb, bool normal)
        {
            string path = Folder + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = name.EndsWith("_Icon", StringComparison.Ordinal);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = !importer.alphaIsTransparency;
            importer.anisoLevel = importer.alphaIsTransparency ? 1 : 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject Model(string name)
        {
            var path = Folder+name+".fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) throw new FileNotFoundException(path);
            importer.importLights = false; importer.importCameras = false;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.bakeAxisConversion = true; importer.addCollider = false; importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var parts = source.GetComponentsInChildren<MeshFilter>(true);
            var expected=AreArpg.UnityClient.RegionalWearableRig.RequiredModelNodes(name).OrderBy(n=>n,StringComparer.Ordinal);
            var imported=parts.Select(p=>p.name).OrderBy(n=>n,StringComparer.Ordinal);
            if (!imported.SequenceEqual(expected))
                throw new InvalidOperationException(name+" benötigt die im Spiel verankerten Teile: "+string.Join(", ",expected));
            foreach (var part in parts)
            {
                if (!part.sharedMesh || part.sharedMesh.subMeshCount != 1 || part.sharedMesh.vertexCount < 3)
                    throw new InvalidOperationException(part.name + ": fehlendes Atlasmesh");
                float metre = part.GetComponent<MeshRenderer>().bounds.size.magnitude;
                if (metre < .025f || metre > 1.8f)
                    throw new InvalidOperationException(part.name + ": unerwartete Größe " + metre + " m");
            }
            return source;
        }

        private static void Single(GameObject source, string partName, string path, Material material)
        {
            // A single-node FBX may elide its intermediate object and keep only the model root.
            var part = source.GetComponentsInChildren<MeshFilter>(true).Single(p=>p.name==partName);
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            try
            {
                var child = new GameObject(partName).transform;
                child.SetParent(root.transform, false);
                var m = source.transform.worldToLocalMatrix * part.transform.localToWorldMatrix;
                child.SetLocalPositionAndRotation(m.MultiplyPoint3x4(Vector3.zero),m.rotation);
                child.localScale = m.lossyScale;
                child.gameObject.AddComponent<MeshFilter>().sharedMesh = part.sharedMesh;
                child.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(root,path,out var saved);
                if(!saved) throw new InvalidOperationException("Prefab konnte nicht gespeichert werden: " + path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
