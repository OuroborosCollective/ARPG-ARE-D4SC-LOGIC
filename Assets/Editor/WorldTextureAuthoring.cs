using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Apply an explicit private world-material recipe without editing scenes, UI or simulation.</summary>
    public static class WorldTextureAuthoring
    {
        private const string Root = "Assets/Resources/ARE/Visuals/WorldSurfaces/";
        private const string RecipePath = "Assets/PrivateArt/world-textures.json";
        [Serializable] private sealed class Surface
        {
            public string id, albedo, normal, occlusion;
            public Color tint = Color.white;
            public Vector2 tiling = Vector2.one;
            public float smoothness = .16f, normalScale = .65f, metallic, emission;
        }
        [Serializable] private sealed class MaterialEdit { public string path, surface; }
        [Serializable] private sealed class PrefabEdit
        {
            public string path, rendererContains, surface;
            public bool planarUv;
            public float metersPerTile = 2f;
        }
        [Serializable] private sealed class Recipe
        {
            public Surface[] surfaces;
            public MaterialEdit[] materials;
            public PrefabEdit[] prefabs;
            public string[] worldPrefabs;
        }

        /// <summary>Build imported materials, update exact world bindings and cap their referenced texture imports.</summary>
        [MenuItem("ARE ARPG/Art/Apply Private World Textures")]
        public static void Apply()
        {
            Recipe recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText(RecipePath));
            if (recipe == null || recipe.surfaces == null || recipe.surfaces.Length == 0)
                throw new InvalidOperationException("World texture recipe has no surfaces");
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Pinned URP/Lit shader is missing");
            var surfaces = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (Surface s in recipe.surfaces)
            {
                if (string.IsNullOrEmpty(s.id) || s.id.IndexOfAny(new[] { '/', '\\', '.' }) >= 0 || surfaces.ContainsKey(s.id))
                    throw new InvalidOperationException("Invalid or duplicate surface identity");
                Texture2D albedo = Texture(s.albedo, false);
                if (albedo == null) throw new InvalidOperationException("Missing albedo for " + s.id);
                Texture2D normal = Texture(s.normal, true);
                Texture2D occlusion = Texture(s.occlusion, false, true);
                string path = Root + s.id + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.SetTexture("_BaseMap", albedo);
                material.SetColor("_BaseColor", s.tint);
                material.SetTextureScale("_BaseMap", s.tiling);
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", s.normalScale);
                if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
                material.SetTexture("_OcclusionMap", occlusion);
                material.SetFloat("_OcclusionStrength", .65f);
                if (occlusion != null) material.EnableKeyword("_OCCLUSIONMAP"); else material.DisableKeyword("_OCCLUSIONMAP");
                material.SetFloat("_Smoothness", s.smoothness);
                material.SetFloat("_Metallic", s.metallic);
                if (s.emission > 0)
                {
                    material.SetTexture("_EmissionMap", albedo);
                    material.SetColor("_EmissionColor", Color.white * s.emission);
                    material.EnableKeyword("_EMISSION");
                }
                else { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black); }
                EditorUtility.SetDirty(material);
                surfaces.Add(s.id, material);
            }
            int edits = 0;
            foreach (MaterialEdit edit in recipe.materials ?? new MaterialEdit[0])
            {
                ValidatePrivatePath(edit.path);
                Material target = AssetDatabase.LoadAssetAtPath<Material>(edit.path);
                if (target == null) throw new InvalidOperationException("Missing world material " + edit.path);
                string name = target.name;
                target.shader = shader;
                target.CopyPropertiesFromMaterial(surfaces[edit.surface]);
                target.name = name;
                EditorUtility.SetDirty(target); edits++;
            }
            foreach (PrefabEdit edit in recipe.prefabs ?? new PrefabEdit[0])
            {
                ValidatePrivatePath(edit.path);
                GameObject root = PrefabUtility.LoadPrefabContents(edit.path);
                int count = 0;
                try
                {
                    foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (!string.IsNullOrEmpty(edit.rendererContains) &&
                            renderer.name.IndexOf(edit.rendererContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (edit.planarUv)
                        {
                            MeshFilter filter = renderer.GetComponent<MeshFilter>();
                            if (filter == null || filter.sharedMesh == null)
                                throw new InvalidOperationException("World surface has no static mesh");
                            filter.sharedMesh = PlanarMesh(filter.sharedMesh, edit.metersPerTile);
                        }
                        Material[] slots = renderer.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++) slots[i] = surfaces[edit.surface];
                        renderer.sharedMaterials = slots; count++;
                    }
                    if (count == 0) throw new InvalidOperationException("World binding matched no renderer: " + edit.path);
                    PrefabUtility.SaveAsPrefabAsset(root, edit.path); edits += count;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            var textures = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string path in BoundAssets(recipe))
                if (AssetImporter.GetAtPath(path) is TextureImporter) textures.Add(path);
            foreach (string path in textures)
            {
                ValidatePrivatePath(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.Sprite)
                    throw new InvalidOperationException("Sprite/UI unexpectedly referenced by world recipe: " + path);
                Optimize(importer, false);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ARE_WORLD_TEXTURE_PASS surfaces=" + surfaces.Count + " bindings=" + edits + " existingWorldTextures=" + textures.Count);
            Audit();
        }

        /// <summary>Read back the imported world dependencies and preserve a private, native-editor receipt.</summary>
        public static void Audit()
        {
            Recipe recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText(RecipePath));
            var paths = BoundAssets(recipe);
            var lines = new List<string> { "path\tkind\tshader-or-format\twidth\theight\tandroid-max\tmipmaps\treadable" };
            int textures = 0, materials = 0, meshes = 0;
            foreach (string path in paths)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                        throw new InvalidOperationException("Invalid world shader: " + path);
                    lines.Add(path + "\tmaterial\t" + material.shader.name); materials++;
                }
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    var android = importer.GetPlatformTextureSettings("Android");
                    if (!android.overridden || android.maxTextureSize > 1024 || android.format != TextureImporterFormat.ASTC_6x6 || importer.isReadable || !importer.mipmapEnabled)
                        throw new InvalidOperationException("World texture budget mismatch: " + path);
                    lines.Add(path + "\ttexture\t" + android.format + "\t" + texture.width + "\t" + texture.height + "\t" + android.maxTextureSize + "\t" + importer.mipmapEnabled + "\t" + importer.isReadable); textures++;
                }
                if (path.StartsWith(Root + "Meshes/", StringComparison.Ordinal)) meshes++;
            }
            File.WriteAllLines("Assets/PrivateArt/world-textures-audit.tsv", lines);
            AssetDatabase.ImportAsset("Assets/PrivateArt/world-textures-audit.tsv", ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("ARE_WORLD_TEXTURE_AUDIT textures=" + textures + " materials=" + materials + " generatedMeshes=" + meshes);
        }

        /// <summary>Inspect actual renderer bindings, excluding unused FBX default materials and their textures.</summary>
        private static SortedSet<string> BoundAssets(Recipe recipe)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            var materials = new HashSet<Material>();
            foreach (Surface surface in recipe.surfaces)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(Root + surface.id + ".mat");
                if (material == null) throw new InvalidOperationException("Missing authored world surface: " + surface.id);
                materials.Add(material);
            }
            foreach (string path in recipe.worldPrefabs)
            {
                ValidatePrivatePath(path);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Missing world prefab: " + path);
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null) throw new InvalidOperationException("Missing world material: " + path + "/" + renderer.name);
                        materials.Add(material);
                    }
                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) paths.Add(AssetDatabase.GetAssetPath(filter.sharedMesh));
            }
            foreach (Material material in materials)
            {
                paths.Add(AssetDatabase.GetAssetPath(material));
                foreach (string property in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
                        paths.Add(AssetDatabase.GetAssetPath(texture));
                }
            }
            return paths;
        }

        /// <summary>Give palette-mapped static masonry usable tiling UVs without changing its geometry.</summary>
        private static Mesh PlanarMesh(Mesh source, float metersPerTile)
        {
            if (!(metersPerTile > 0f)) throw new InvalidOperationException("Invalid texture scale");
            string sourcePath = AssetDatabase.GetAssetPath(source);
            ValidatePrivatePath(sourcePath);
            if (sourcePath.StartsWith(Root + "Meshes/", StringComparison.Ordinal)) return source;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
            string key = Hash128.Compute(guid + "/" + localId + "/" + metersPerTile.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).ToString();
            string path = Root + "Meshes/" + key + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Root + "Meshes");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var colors = new List<Color>();
            Vector3[] sv = source.vertices, sn = source.normals;
            Vector2[] su2 = source.uv2; Color[] sc = source.colors;
            var submeshes = new List<int[]>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                if (source.GetTopology(sub) != MeshTopology.Triangles)
                    throw new InvalidOperationException("World UV authoring requires triangles");
                int[] indices = source.GetTriangles(sub), output = new int[indices.Length];
                for (int t = 0; t < indices.Length; t += 3)
                {
                    Vector3 face = Vector3.Cross(sv[indices[t + 1]] - sv[indices[t]], sv[indices[t + 2]] - sv[indices[t]]).normalized;
                    int axis = Mathf.Abs(face.y) >= Mathf.Abs(face.x) && Mathf.Abs(face.y) >= Mathf.Abs(face.z) ? 1 :
                        (Mathf.Abs(face.x) >= Mathf.Abs(face.z) ? 0 : 2);
                    for (int j = 0; j < 3; j++)
                    {
                        int i = indices[t + j]; Vector3 v = sv[i]; output[t + j] = vertices.Count;
                        vertices.Add(v); normals.Add(sn.Length == sv.Length ? sn[i] : face);
                        uv.Add((axis == 1 ? new Vector2(v.x, v.z) : axis == 0 ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y)) / metersPerTile);
                        if (su2.Length == sv.Length) uv2.Add(su2[i]);
                        if (sc.Length == sv.Length) colors.Add(sc[i]);
                    }
                }
                submeshes.Add(output);
            }
            var mesh = new Mesh { name = source.name + "_WorldSurface", indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            if (uv2.Count == vertices.Count) mesh.SetUVs(1, uv2);
            if (colors.Count == vertices.Count) mesh.SetColors(colors);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Require original private texture inputs and their authored normal/AO maps.</summary>
        private static Texture2D Texture(string path, bool normal, bool linear = false)
        {
            if (string.IsNullOrEmpty(path)) return null;
            ValidatePrivatePath(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture not imported: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !linear;
            importer.wrapMode = TextureWrapMode.Repeat;
            Optimize(importer, true);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Bound Android memory while preserving alpha channels and authored normal-map semantics.</summary>
        private static void Optimize(TextureImporter importer, bool surface)
        {
            importer.isReadable = false;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = Math.Min(importer.maxTextureSize, surface ? 2048 : 1024);
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = surface ? 1024 : Math.Min(importer.maxTextureSize, 1024);
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 80;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        /// <summary>Art authoring is confined to the existing ignored world overlay, never the private UI root.</summary>
        private static void ValidatePrivatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("..") ||
                !(path.StartsWith("Assets/PrivateArt/", StringComparison.Ordinal) ||
                  path.StartsWith("Assets/PrivateVendor/", StringComparison.Ordinal) ||
                  path.StartsWith("Assets/Resources/ARE/Visuals/", StringComparison.Ordinal)))
                throw new InvalidOperationException("World texture path leaves private world roots");
        }
    }
}
