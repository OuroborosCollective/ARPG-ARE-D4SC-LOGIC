using System;
using System.IO;
using System.Linq;
using System.Text;
using AreArpg.Core;
using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// The workplaces of the village economy as passive visuals.
    ///
    /// The models are made for this game with Tools/Art/blender/village_work_props.py and written into the private
    /// art folder below. This tool gives them one shared matt material (a small palette picture, plus a second
    /// picture for what glows) and saves each as a mesh-only visual under the key the game binds. Parts named
    /// Stock_&lt;Good&gt;_&lt;n&gt; stay separate objects: the game shows as many of them as the village owns.
    /// </summary>
    public static class VillageWorkArt
    {
        /// <summary>Output folder of the Blender script; private art, never part of the public repository.</summary>
        public const string Source = "Assets/PrivateArt/VillageWork";
        const string Visuals = "Assets/Resources/ARE/Visuals/";
        public const string Palette = Source + "/VillageWorkPalette.png", Glow = Source + "/VillageWorkGlow.png";
        public const string MaterialPath = Source + "/Materials/VillageWork.mat";
        /// <summary>How strongly the fire shines; the glow picture is black everywhere else.</summary>
        public const float FireGlow = 2.6f;

        public static string[] Keys() { return ArenaRuntime.VillageWorkplaces.Select(w => w.Key).ToArray(); }

        public static bool Installed
        {
            get { return File.Exists(Palette) && File.Exists(Glow) && Keys().All(key => File.Exists(Source + "/" + key + ".fbx")); }
        }

        [MenuItem("ARE ARPG/Art/Village Work/Build Workplaces")]
        public static void Build()
        {
            if (!Installed)
                throw new InvalidOperationException("Werkplatz-Modelle fehlen in " + Source + ". Zuerst Tools/Art/blender/village_work_props.py dorthin ausführen.");
            if (!AssetDatabase.IsValidFolder(Source + "/Materials")) AssetDatabase.CreateFolder(Source, "Materials");
            Material material = WorkMaterial();
            var report = new StringBuilder();
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces) report.AppendLine(BuildVisual(workplace, material));
            AssetDatabase.SaveAssets();
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Library/VillageWork/report.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report.ToString());
            Debug.Log("ARE_VILLAGE_WORK: " + ArenaRuntime.VillageWorkplaces.Length + " workplaces built; report " + path);
        }

        /// <summary>Flat colours need exact pixels: no smoothing, no smaller copies, no compression.</summary>
        static Texture2D Picture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Bild fehlt: " + path);
            if (importer.textureType != TextureImporterType.Default || !importer.sRGBTexture || importer.mipmapEnabled || importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 64 ||
                importer.npotScale != TextureImporterNPOTScale.None || importer.alphaSource != TextureImporterAlphaSource.None)
            {
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 64;
                importer.npotScale = TextureImporterNPOTScale.None; importer.alphaSource = TextureImporterAlphaSource.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material WorkMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit fehlt.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            else { material.shader = shader; material.shaderKeywords = new string[0]; }
            material.SetTexture("_BaseMap", Picture(Palette));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .08f); material.SetFloat("_Metallic", 0f);
            // Wood, hide and stone do not shine: no highlights, no reflections (cheaper on phones too).
            material.SetFloat("_SpecularHighlights", 0f); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetTexture("_EmissionMap", Picture(Glow)); material.SetColor("_EmissionColor", Color.white * FireGlow); material.EnableKeyword("_EMISSION");
            // Not "emissive is black": the pipeline's material check would switch the glow off again.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static string BuildVisual(ArenaRuntime.VillageWorkplace workplace, Material material)
        {
            string model = Source + "/" + workplace.Key + ".fbx";
            var importer = AssetImporter.GetAtPath(model) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Modell fehlt: " + model);
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation || importer.importCameras || importer.importLights ||
                importer.isReadable || importer.addCollider || importer.animationType != ModelImporterAnimationType.None || importer.importBlendShapes)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.importAnimation = false; importer.importCameras = false;
                importer.importLights = false; importer.isReadable = false; importer.addCollider = false;
                importer.animationType = ModelImporterAnimationType.None; importer.importBlendShapes = false;
                importer.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(model);
            if (source == null) throw new InvalidOperationException("Modell lässt sich nicht laden: " + model);
            GameObject visual = Object.Instantiate(source);
            visual.name = workplace.Key;
            visual.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            int triangles = 0;
            foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterial = material;
                bool fire = renderer.name == ArenaRuntime.FireName;
                renderer.shadowCastingMode = fire ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = !fire;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                triangles += renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
            }
            // What the game expects to find, good by good.
            var counts = new StringBuilder();
            for (int g = 0; g < workplace.Goods.Length; g++)
            {
                string prefix = ArenaRuntime.StockPrefix + workplace.Goods[g] + "_";
                int found = visual.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith(prefix, StringComparison.Ordinal));
                if (found != workplace.Units[g])
                {
                    Object.DestroyImmediate(visual);
                    throw new InvalidOperationException(workplace.Key + ": " + found + " Teile für " + workplace.Goods[g] + ", erwartet " + workplace.Units[g]);
                }
                counts.Append(' ').Append(workplace.Goods[g]).Append('=').Append(found);
            }
            if (workplace.Fire && visual.GetComponentsInChildren<Transform>(true).All(t => t.name != ArenaRuntime.FireName))
            {
                Object.DestroyImmediate(visual);
                throw new InvalidOperationException(workplace.Key + ": das Feuer fehlt.");
            }
            Bounds bounds = visual.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            string prefab = Visuals + workplace.Key + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(visual, prefab);
            Object.DestroyImmediate(visual);
            return workplace.Key + "\t" + prefab + "\ttriangles=" + triangles + "\tmin=" + bounds.min.ToString("F2") + "\tmax=" + bounds.max.ToString("F2") + "\tstock:" + counts;
        }
    }
}
