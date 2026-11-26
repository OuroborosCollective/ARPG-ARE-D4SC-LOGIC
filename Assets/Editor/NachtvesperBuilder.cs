using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AreArpg.Core;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Owner request 2026-11-26: the Nachtvesper harness from the owner's archive becomes rare loot, fitted to the hero. This
    /// builds its private pictures (never in the repository) from the tablet version of the modular model
    /// (Tools/Art/aschenwacht/convert.py, about 28,000 triangles from 69,412) and the archive's seven tileable surfaces:
    ///
    /// - nine URP materials: seven textured (base colour, normal map, metal in red and smoothness in alpha of the
    ///   UnityMetalSmooth map), the faceted garnet and the matte visor insert with the archive's own colours; all two-sided
    ///   like the archive's;
    /// - the eighteen meshes grouped into the eight regional pieces and fitted to the hero like Aschenwacht's armour
    ///   (<see cref="AschenwachtGearBuilder.FitToHero"/>): one arsenal prefab per piece.
    /// </summary>
    public static class NachtvesperBuilder
    {
        public const string Root = "Assets/PrivateArt/Nachtvesper/";
        public const string Model = Root + "Models/Nachtvesper.fbx";
        private const string Textures = Root + "Textures/";
        private const string Materials = Root + "Materials/";
        private const string MeshFolder = Root + "Armor/";

        private static readonly string[] Textured = { "Eisen", "Geflecht", "Leder", "Messing", "Samt", "Schwarzstahl", "Silber" };
        /// <summary>The two plain surfaces: linear colour, metal and roughness as the archive's model gives them.</summary>
        private static readonly (string name, Color linear, float metal, float roughness)[] Plain =
        {
            ("Granat", new Color(.19f, .009f, .019f), .35f, .18f),
            ("Schatten", new Color(.008f, .006f, .009f), .02f, .85f)
        };

        /// <summary>The archive's eighteen meshes, by the eight pieces the loot gives.</summary>
        public static readonly (RegionalRelic id, string[] meshes)[] Pieces =
        {
            (RegionalRelic.N_Schaller, new[] { "04_Schaller" }),
            (RegionalRelic.N_Kuerass, new[] { "01_Hauberk", "02_Kuerass", "03_Ringkragen" }),
            (RegionalRelic.N_Schulterstuecke, new[] { "08_Schulter.L", "08_Schulter.R" }),
            (RegionalRelic.N_Armschienen, new[] { "07_Armschutz.L", "07_Armschutz.R" }),
            (RegionalRelic.N_Handschuhe, new[] { "09_Handschuh.L", "09_Handschuh.R" }),
            (RegionalRelic.N_Guertung, new[] { "05_Guertung", "06_Wappenbahnen", "11_Hueftschurz.L", "11_Hueftschurz.R" }),
            (RegionalRelic.N_Beinzeug, new[] { "10_Beinzeug.L", "10_Beinzeug.R" }),
            (RegionalRelic.N_Panzerschuhe, new[] { "12_Panzerschuh.L", "12_Panzerschuh.R" })
        };

        public static bool TryBuild()
        {
            AssetDatabase.Refresh();
            if (AssetImporter.GetAtPath(Model) == null) return false;
            var materials = MaterialSet();
            PrepareModel(materials);
            var skins = AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .ToDictionary(s => s.name, StringComparer.Ordinal);
            var all = Pieces.SelectMany(p => p.meshes).ToList();
            if (all.Count != skins.Count || all.Distinct().Count() != all.Count || all.Any(m => !skins.ContainsKey(m)))
                throw new InvalidOperationException("Nachtvesper meshes do not match: model has " + string.Join(",", skins.Keys.OrderBy(k => k, StringComparer.Ordinal)));
            var pieces = Pieces.Select(p => (p.id, p.meshes.Select(m => skins[m]).ToArray())).ToList();
            if (!AschenwachtGearBuilder.FitToHero(pieces, MeshFolder)) return false;
            Debug.Log("ARE_NACHTVESPER_BUILT pieces=" + Pieces.Length + " meshes=" + all.Count);
            return true;
        }

        private static Dictionary<string, Material> MaterialSet()
        {
            Directory.CreateDirectory(Materials);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("URP/Lit missing");
            var set = new Dictionary<string, Material>(StringComparer.Ordinal);
            Material Make(string name)
            {
                string path = Materials + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(lit); AssetDatabase.CreateAsset(material, path); }
                material.shader = lit;
                material.SetFloat("_Cull", 0f); // two-sided like the archive's thin plates and cloth
                material.doubleSidedGI = true;
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.enableInstancing = true;
                set.Add(name, material);
                return material;
            }
            foreach (string surface in Textured)
            {
                Texture2D colour = AschenwachtCourtBuilder.Texture(Textures + surface + "_BaseColor.png", TextureImporterType.Default, true);
                Texture2D normal = AschenwachtCourtBuilder.Texture(Textures + surface + "_Normal.png", TextureImporterType.NormalMap, false);
                Texture2D mask = AschenwachtCourtBuilder.Texture(Textures + surface + "_UnityMetalSmooth.png", TextureImporterType.Default, false);
                if (colour == null || normal == null || mask == null) throw new InvalidOperationException("Nachtvesper surface incomplete: " + surface);
                var material = Make(surface);
                material.SetTexture("_BaseMap", colour);
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", mask);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f); // smoothness from the metal map's alpha
                EditorUtility.SetDirty(material);
            }
            foreach (var (name, linear, metal, roughness) in Plain)
            {
                var material = Make(name);
                material.SetTexture("_BaseMap", null);
                material.SetColor("_BaseColor", linear.gamma);
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", null);
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Metallic", metal);
                material.SetFloat("_Smoothness", 1f - roughness);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary>The model is read as it is (readable for fitting, no animation) and wears the set's materials by name.</summary>
        private static void PrepareModel(Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            bool changed = false;
            void Set<T>(T now, T want, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(now, want)) { apply(want); changed = true; } }
            Set(importer.bakeAxisConversion, true, v => importer.bakeAxisConversion = v);
            Set(importer.isReadable, true, v => importer.isReadable = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            Set(importer.importAnimation, false, v => importer.importAnimation = v);
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.ImportViaMaterialDescription, v => importer.materialImportMode = v);
            var map = importer.GetExternalObjectMap();
            foreach (var pair in materials)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                if (!map.TryGetValue(id, out UnityEngine.Object current) || current != pair.Value) { importer.AddRemap(id, pair.Value); changed = true; }
            }
            if (changed) importer.SaveAndReimport();
        }
    }
}
