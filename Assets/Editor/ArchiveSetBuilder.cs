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
    /// Builds the private pictures of a worn set from the owner's archives (never in the repository): the set's URP materials
    /// from its tileable surfaces, its model wearing them by name, the pieces fitted to the hero
    /// (<see cref="AschenwachtGearBuilder.FitToHero(IList{AschenwachtGearBuilder.FitPiece}, string)"/>) and its weapons as
    /// arsenal pieces. Every archive brings the same kinds of surfaces: textured ones (base colour, normal map, metal in red
    /// and smoothness in alpha of the UnityMetalSmooth map), plain ones with the archive's own colours, and glowing ones (base
    /// colour and emission maps). All are two-sided like the archives'.
    /// </summary>
    public static class ArchiveSetBuilder
    {
        public sealed class Spec
        {
            public string Label;
            /// <summary>The set's private folder; the model, textures and weapon models lie under it.</summary>
            public string Root;
            public string Model;
            public string[] Textured = new string[0];
            public (string name, Color linear, float metal, float roughness)[] Plain = new (string, Color, float, float)[0];
            public (string name, float strength, float roughness)[] Glowing = new (string, float, float)[0];
            public (string name, string[] meshes, AschenwachtGearBuilder.HeadFit head)[] Pieces = new (string, string[], AschenwachtGearBuilder.HeadFit)[0];
            /// <summary>Meshes of the model that are not worn (weapons the archive also shows in the hands).</summary>
            public string[] Unused = new string[0];
            /// <summary>Weapons from their own static models in the set's Models folder.</summary>
            public (RegionalRelic id, string model)[] Weapons = new (RegionalRelic, string)[0];
        }

        private static string Models(Spec spec) => spec.Root + "Models/";

        public static bool Build(Spec spec)
        {
            AssetDatabase.Refresh();
            string model = Models(spec) + spec.Model;
            if (AssetImporter.GetAtPath(model) == null) return false;
            var materials = MaterialSet(spec);
            PrepareModel(model, materials, readable: true);
            foreach (var (_, weapon) in spec.Weapons) PrepareModel(Models(spec) + weapon, materials, readable: false);

            var skins = AssetDatabase.LoadAssetAtPath<GameObject>(model).GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .ToDictionary(s => s.name, StringComparer.Ordinal);
            var worn = spec.Pieces.SelectMany(p => p.meshes).ToList();
            var expected = worn.Concat(spec.Unused).OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (worn.Distinct().Count() != worn.Count || !expected.SequenceEqual(skins.Keys.OrderBy(k => k, StringComparer.Ordinal)))
                throw new InvalidOperationException(spec.Label + " meshes do not match: model has " + string.Join(",", skins.Keys.OrderBy(k => k, StringComparer.Ordinal)));
            var pieces = spec.Pieces.Select(p => new AschenwachtGearBuilder.FitPiece(p.name, p.meshes.Select(m => skins[m]).ToArray(), p.head)).ToList();
            if (!AschenwachtGearBuilder.FitToHero(pieces, spec.Root + "Armor/")) return false;
            foreach (var (id, weapon) in spec.Weapons) AschenwachtGearBuilder.BuildHandPiece(Models(spec) + weapon, id);
            AssetDatabase.SaveAssets();
            Debug.Log("ARE_ARCHIVE_SET_BUILT " + spec.Label + " pieces=" + spec.Pieces.Length + " weapons=" + spec.Weapons.Length);
            return true;
        }

        private static Dictionary<string, Material> MaterialSet(Spec spec)
        {
            string folder = spec.Root + "Materials/", textures = spec.Root + "Textures/";
            Directory.CreateDirectory(folder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("URP/Lit missing");
            var set = new Dictionary<string, Material>(StringComparer.Ordinal);
            Material Make(string name)
            {
                string path = folder + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(lit); AssetDatabase.CreateAsset(material, path); }
                material.shader = lit;
                material.SetFloat("_Cull", 0f);
                material.doubleSidedGI = true;
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                material.SetTexture("_BumpMap", null); material.DisableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.SetTexture("_EmissionMap", null);
                material.enableInstancing = true;
                set.Add(name, material);
                return material;
            }
            Texture2D Map(string name, TextureImporterType type, bool colour)
            {
                var map = AschenwachtCourtBuilder.Texture(textures + name, type, colour);
                if (map == null) throw new InvalidOperationException(spec.Label + " texture missing: " + name);
                return map;
            }
            foreach (string surface in spec.Textured)
            {
                var material = Make(surface);
                material.SetTexture("_BaseMap", Map(surface + "_BaseColor.png", TextureImporterType.Default, true));
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", Map(surface + "_Normal.png", TextureImporterType.NormalMap, false));
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", Map(surface + "_UnityMetalSmooth.png", TextureImporterType.Default, false));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                EditorUtility.SetDirty(material);
            }
            foreach (var (name, linear, metal, roughness) in spec.Plain)
            {
                var material = Make(name);
                material.SetTexture("_BaseMap", null);
                material.SetColor("_BaseColor", linear.gamma);
                material.SetFloat("_Metallic", metal);
                material.SetFloat("_Smoothness", 1f - roughness);
                EditorUtility.SetDirty(material);
            }
            foreach (var (name, strength, roughness) in spec.Glowing)
            {
                var material = Make(name);
                material.SetTexture("_BaseMap", Map(name + "_BaseColor.png", TextureImporterType.Default, true));
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 1f - roughness);
                material.SetTexture("_EmissionMap", Map(name + "_Emission.png", TextureImporterType.Default, true));
                material.SetColor("_EmissionColor", Color.white * strength);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary>A model is read as it is (readable when it is fitted, no animation) and wears the set's materials by name.</summary>
        private static void PrepareModel(string path, Dictionary<string, Material> materials, bool readable)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Model missing: " + path);
            bool changed = false;
            void Set<T>(T now, T want, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(now, want)) { apply(want); changed = true; } }
            Set(importer.bakeAxisConversion, true, v => importer.bakeAxisConversion = v);
            Set(importer.isReadable, readable, v => importer.isReadable = v);
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
