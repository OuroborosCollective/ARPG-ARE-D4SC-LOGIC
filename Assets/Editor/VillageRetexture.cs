using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Lays the village's derived dark pictures (Tools/Art/village/retexture.py) over its built visuals. For every
    /// bound source material a private copy carries the derived picture and takes the source's place in the listed
    /// built prefabs; a material the game's own builders made is changed where it is. Licensed materials and
    /// textures are only read. Bindings, pictures and copies are private and never part of the repository.
    /// </summary>
    public static class VillageRetexture
    {
        public const string DefaultFolder = "Assets/PrivateArt/VillageDark/";
        public const string BindingsFile = "bindings.json";
        private const string BuiltRoot = "Assets/Resources/ARE/";
        private static readonly Regex PictureId = new Regex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$");

        [Serializable] private sealed class Binding
        {
            public string material;         // the source material the built prefabs use
            public string albedo;           // derived picture: <albedo>.png next to the bindings
            public string normal;           // optional derived normal map
            public float normalScale = 1f;
            public float smoothness = -1f;  // below zero keeps the source's value
            public bool tintWhite;          // the derived picture already carries the colour
            public bool inPlace;            // made by the game's own builders: change it where it is
        }
        [Serializable] private sealed class Swap { public string prefab, child, material; }
        [Serializable] private sealed class Bindings
        {
            public Binding[] bindings;
            public string[] prefabs;
            public Swap[] swaps;
            public int maxSize = 1024;
        }

        /// <summary>True where the private pictures and their bindings are installed.</summary>
        public static bool Installed => File.Exists(DefaultFolder + BindingsFile);

        [MenuItem("ARE ARPG/Art/Apply Village Dark Textures")]
        public static void ApplyDefault()
        {
            if (!Installed) { Debug.LogWarning("ARE ARPG: no private village pictures installed; the village keeps its source look."); return; }
            Apply(DefaultFolder);
        }

        /// <summary>Applies the bindings of one private folder. Returns how many renderer slots and materials changed.</summary>
        public static int Apply(string folder)
        {
            folder = Folder(folder);
            Bindings plan = JsonUtility.FromJson<Bindings>(File.ReadAllText(folder + BindingsFile));
            if (plan == null || plan.bindings == null || plan.bindings.Length == 0)
                throw new InvalidOperationException("Village bindings name no material");
            int size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(plan.maxSize), 64, 2048);
            var copies = new Dictionary<Material, Material>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int changed = 0;
            foreach (Binding binding in plan.bindings)
            {
                if (binding == null || !seen.Add(binding.material ?? "")) throw new InvalidOperationException("Empty or repeated village binding");
                Material source = AssetDatabase.LoadAssetAtPath<Material>(Readable(binding.material));
                if (source == null) throw new InvalidOperationException("Village binding names no material: " + binding.material);
                Texture2D picture = Picture(folder, binding.albedo, false, size);
                Texture2D normal = string.IsNullOrEmpty(binding.normal) ? null : Picture(folder, binding.normal, true, size);
                Material target;
                if (binding.inPlace)
                {
                    Writable(binding.material, folder);
                    target = source;
                }
                else
                {
                    string path = folder + "Materials/" + source.name + ".mat";
                    Directory.CreateDirectory(folder + "Materials");
                    target = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (target == null) { target = new Material(source); AssetDatabase.CreateAsset(target, path); }
                    target.shader = source.shader;
                    target.CopyPropertiesFromMaterial(source);
                    copies.Add(source, target);
                }
                if (Dress(target, binding, picture, normal)) changed++;
                EditorUtility.SetDirty(target);
            }
            AssetDatabase.SaveAssets();
            foreach (string prefab in plan.prefabs ?? new string[0])
                changed += Rebind(Writable(prefab, folder), renderer => true, slot => copies.TryGetValue(slot, out Material copy) ? copy : slot);
            foreach (Swap swap in plan.swaps ?? new Swap[0])
            {
                if (swap == null || string.IsNullOrEmpty(swap.child)) throw new InvalidOperationException("Village swap names no part");
                Material material = AssetDatabase.LoadAssetAtPath<Material>(Readable(swap.material));
                if (material == null) throw new InvalidOperationException("Village swap names no material: " + swap.material);
                changed += Rebind(Writable(swap.prefab, folder), renderer => Under(renderer.transform, swap.child), slot => material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ARE_VILLAGE_RETEXTURE bindings=" + plan.bindings.Length + " changed=" + changed);
            return changed;
        }

        /// <summary>Source materials that a listed built prefab still wears, for a check after any village build.</summary>
        public static List<string> Unapplied(string folder)
        {
            folder = Folder(folder);
            Bindings plan = JsonUtility.FromJson<Bindings>(File.ReadAllText(folder + BindingsFile));
            var sources = new HashSet<Material>();
            foreach (Binding binding in plan.bindings)
                if (!binding.inPlace && AssetDatabase.LoadAssetAtPath<Material>(Readable(binding.material)) is Material material) sources.Add(material);
            var left = new List<string>();
            foreach (string prefab in plan.prefabs ?? new string[0])
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(Writable(prefab, folder));
                if (root == null) { left.Add(prefab + ": missing"); continue; }
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material slot in renderer.sharedMaterials)
                        if (slot != null && sources.Contains(slot)) left.Add(prefab + ": " + renderer.name + " wears " + slot.name);
            }
            return left;
        }

        private static bool Dress(Material target, Binding binding, Texture2D picture, Texture2D normal)
        {
            string main = MainProperty(target);
            if (main == null) throw new InvalidOperationException("Village material shows no picture: " + target.name);
            bool changed = target.GetTexture(main) != picture;
            target.SetTexture(main, picture);
            foreach (string twin in new[] { "_BaseMap", "_MainTex" })
                if (twin != main && target.HasProperty(twin)) target.SetTexture(twin, picture);
            if (normal != null)
            {
                if (!target.HasProperty("_BumpMap")) throw new InvalidOperationException("Village material takes no normal map: " + target.name);
                changed |= target.GetTexture("_BumpMap") != normal;
                target.SetTexture("_BumpMap", normal);
                if (target.HasProperty("_BumpScale")) target.SetFloat("_BumpScale", binding.normalScale);
                target.EnableKeyword("_NORMALMAP");
            }
            if (binding.smoothness >= 0f && target.HasProperty("_Smoothness")) target.SetFloat("_Smoothness", binding.smoothness);
            if (binding.tintWhite)
                foreach (string tint in new[] { "_BaseColor", "_Color" })
                    if (target.HasProperty(tint)) target.SetColor(tint, Color.white);
            return changed;
        }

        /// <summary>The property that holds the picture a material shows: URP's, the built-in one, or a pack shader's first colour map.</summary>
        private static string MainProperty(Material material)
        {
            foreach (string name in new[] { "_BaseMap", "_MainTex" })
                if (material.HasProperty(name) && material.GetTexture(name) != null) return name;
            foreach (string name in material.GetTexturePropertyNames())
            {
                string lower = name.ToLowerInvariant();
                if (material.GetTexture(name) != null && !lower.Contains("norm") && !lower.Contains("bump")) return name;
            }
            foreach (string name in new[] { "_BaseMap", "_MainTex" })
                if (material.HasProperty(name)) return name;
            return null;
        }

        private static int Rebind(string prefab, Func<Renderer, bool> wanted, Func<Material, Material> worn)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null) throw new InvalidOperationException("Village prefab missing: " + prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(prefab);
            int changed = 0;
            try
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!wanted(renderer)) continue;
                    Material[] slots = renderer.sharedMaterials;
                    int before = changed;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] == null) continue;
                        Material next = worn(slots[i]);
                        if (next != slots[i]) { slots[i] = next; changed++; }
                    }
                    if (changed != before) renderer.sharedMaterials = slots;
                }
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return changed;
        }

        private static bool Under(Transform transform, string name)
        {
            for (Transform current = transform; current != null; current = current.parent)
                if (current.name == name) return true;
            return false;
        }

        private static Texture2D Picture(string folder, string id, bool normal, int size)
        {
            if (string.IsNullOrEmpty(id) || !PictureId.IsMatch(id) || id.Contains("..")) throw new InvalidOperationException("Invalid village picture name: " + id);
            string path = folder + id + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Village picture missing: " + path);
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.sRGBTexture == normal || !importer.mipmapEnabled || importer.isReadable ||
                importer.wrapMode != TextureWrapMode.Repeat || importer.maxTextureSize != size || importer.anisoLevel != 4 ||
                !android.overridden || android.maxTextureSize != size || android.format != TextureImporterFormat.ASTC_6x6)
            {
                importer.textureType = type;
                importer.sRGBTexture = !normal;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = size;
                importer.anisoLevel = 4;
                android.overridden = true; android.maxTextureSize = size; android.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The private folder of pictures and bindings: below Assets/PrivateArt, never a path that climbs out.</summary>
        private static string Folder(string folder)
        {
            folder = (folder ?? "").Replace('\\', '/');
            if (!folder.EndsWith("/", StringComparison.Ordinal)) folder += "/";
            if (!folder.StartsWith("Assets/PrivateArt/", StringComparison.Ordinal) || folder.Contains("..") || folder.Contains("//"))
                throw new InvalidOperationException("Village pictures live below Assets/PrivateArt: " + folder);
            if (!File.Exists(folder + BindingsFile)) throw new FileNotFoundException("Village bindings missing", folder + BindingsFile);
            return folder;
        }

        private static string Readable(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || path.Contains("\\") || path.Contains("//"))
                throw new InvalidOperationException("Village binding path leaves the project's assets: " + path);
            return path;
        }

        /// <summary>Only built visuals of the game and the private folder itself are ever written.</summary>
        private static string Writable(string path, string folder)
        {
            Readable(path);
            if (!path.StartsWith(BuiltRoot, StringComparison.Ordinal) && !path.StartsWith(folder, StringComparison.Ordinal))
                throw new InvalidOperationException("Village retexture writes only built visuals: " + path);
            return path;
        }
    }
}
