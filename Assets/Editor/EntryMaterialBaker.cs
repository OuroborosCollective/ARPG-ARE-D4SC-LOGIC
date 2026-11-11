using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Convert privately configured palette masks into ordinary URP materials without vendor runtime shaders.</summary>
    public static class EntryMaterialBaker
    {
        [Serializable] private sealed class Layer
        {
            public string mask; public int channel = -1; public Color match; public Color color;
            public bool matchChannel; public float channelValue; public float channelTolerance = .058f;
        }
        [Serializable] private sealed class Definition { public string id; public string texture; public Layer[] layers; }
        [Serializable] private sealed class Catalog { public Definition[] materials; }
        private const string Output = "Assets/Resources/ARE/Visuals/EntryMaterials/";

        /// <summary>Read private mappings and build persistent presentation materials; never alter source assets.</summary>
        public static Dictionary<string, Material> Build()
        {
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/PrivateArt/entry-materials.json"));
            if (catalog == null || catalog.materials == null) throw new InvalidOperationException("Missing private material definitions");
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var result = new Dictionary<string, Material>();
            foreach (var definition in catalog.materials)
            {
                if (definition == null || string.IsNullOrEmpty(definition.id) || definition.id.IndexOfAny(new[] { '/', '\\', '.' }) >= 0)
                    throw new InvalidOperationException("Invalid material key");
                Texture2D texture;
                if (definition.layers != null && definition.layers.Length > 0) texture = Bake(definition);
                else texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PrivatePath(definition.texture));
                if (texture == null) throw new InvalidOperationException("Missing texture for " + definition.id);
                string path = Output + definition.id + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
                material.mainTexture = texture; material.color = Color.white;
                material.SetFloat("_Smoothness", .2f);
                EditorUtility.SetDirty(material);
                result.Add(definition.id, material);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Only ignored local asset roots may supply authoring inputs.</summary>
        private static string PrivatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("..") ||
                !(path.StartsWith("Assets/PrivateArt/", StringComparison.Ordinal) ||
                  path.StartsWith("Assets/PrivateVendor/", StringComparison.Ordinal) ||
                  path.StartsWith("Assets/Resources/ARE/Visuals/", StringComparison.Ordinal)))
                throw new InvalidOperationException("Material input is outside the private asset roots");
            return path;
        }

        /// <summary>Load source pixels separately so importer readability settings and original textures stay intact.</summary>
        private static Texture2D ReadPixels(string path)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(image, File.ReadAllBytes(PrivatePath(path))))
            { UnityEngine.Object.DestroyImmediate(image); throw new InvalidOperationException("Unsupported palette image"); }
            return image;
        }

        /// <summary>Apply explicit colour-key or channel masks to the supplied base atlas, retaining original UV layout.</summary>
        private static Texture2D Bake(Definition definition)
        {
            var basis = ReadPixels(definition.texture);
            try
            {
                Color[] pixels = basis.GetPixels();
                var colors = new Color[pixels.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                foreach (var layer in definition.layers)
                {
                    var mask = ReadPixels(layer.mask);
                    try
                    {
                        if (mask.width != basis.width || mask.height != basis.height) throw new InvalidOperationException("Palette dimensions disagree");
                        Color[] weights = mask.GetPixels();
                        for (int i = 0; i < weights.Length; i++)
                        {
                            Color v = weights[i];
                            float weight = layer.channel >= 0 && layer.channel <= 3 ?
                                (layer.matchChannel ? (Mathf.Abs(v[layer.channel] - layer.channelValue) <= layer.channelTolerance ? 1f : 0f) : v[layer.channel]) :
                                (new Vector3(v.r - layer.match.r, v.g - layer.match.g, v.b - layer.match.b).sqrMagnitude <= .01f ? 1f : 0f);
                            colors[i] = Color.Lerp(colors[i], layer.color.gamma, weight);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(mask); }
                }
                for (int i = 0; i < pixels.Length; i++) { pixels[i] *= colors[i]; pixels[i].a = 1f; }
                basis.SetPixels(pixels); basis.Apply();
                string path = Output + definition.id + ".png";
                File.WriteAllBytes(path, basis.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            finally { UnityEngine.Object.DestroyImmediate(basis); }
        }
    }
}
