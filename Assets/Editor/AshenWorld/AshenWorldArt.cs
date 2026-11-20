using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// The ash world of the overworld (owner direction 2026-11-20): ash snow on the ground, red glowing threads in
    /// the ash and plants that glow like embers, instead of a world hidden in haze.
    ///
    /// Ground textures come from Tools/Art/generate_ashen_ground.py; the plants come from the "Cinderbloom" set made
    /// for this game. Both are private art in this workspace. Everything built here is passive: materials for named
    /// ground surfaces and mesh-only plant visuals under the keys the game already binds.
    /// </summary>
    public static class AshenWorldArt
    {
        /// <summary>Private source folder: the plant set and, below Generated, the generated ground textures.</summary>
        public const string SourceRoot = "Assets/PrivateArt/Cinderbloom";
        public const string Generated = SourceRoot + "/Generated/";
        /// <summary>Private folder for the materials built here. Never committed.</summary>
        public const string WorkFolder = "Assets/PrivateArt/AshenWorld";
        const string Surfaces = "Assets/Resources/ARE/Visuals/WorldSurfaces/";
        const string Visuals = "Assets/Resources/ARE/Visuals/";
        const string Models = SourceRoot + "/Flora/Models/";
        /// <summary>
        /// One repeat of the ground texture covers this many metres. Large on purpose: the scorched islands must
        /// not line up in a visible grid, and 16 m lets both verge strips continue the ground's pattern.
        /// </summary>
        public const float GroundMetresPerTile = 16f;
        /// <summary>The fine grain repeats this often inside one repeat of the ground (every 1.6 m).</summary>
        public const float DetailRepeats = 10f;
        /// <summary>Memory budget on a tablet: colour, normal and glow map at most 2048, the grain 512.</summary>
        public const int GroundTextureSize = 2048, GlowTextureSize = 2048, DetailTextureSize = 512;
        /// <summary>Glow of the ember veins on top of the map; above 1 so they stay red in full daylight.</summary>
        public const float GroundGlow = 1.4f;
        /// <summary>Sizes the game builds: the outdoor ground square and the two verge strips beside the ash road.</summary>
        public const float GroundSize = 57f, VergeWidth = 4f, VergeLength = 36f, VergeCentre = 8f;
        const float Sink = .02f;

        sealed class Plant { public string Model; public Vector3 At; public float Scale = 1f, Yaw; }
        sealed class Clump { public string Key; public Plant[] Plants; }

        /// <summary>Glow of a plant part by the material name of the model; parts not listed do not glow.</summary>
        static readonly Dictionary<string, Color> Glow = new Dictionary<string, Color>
        {
            { "Mushroom_Red", new Color(1f, .16f, .04f) * 1.5f }, { "Mushroom_Crimson", new Color(1f, .10f, .05f) * 1.1f },
            { "Mushroom_Gills", new Color(1f, .45f, .12f) * 2.4f }, { "Mushroom_Spots", new Color(1f, .72f, .35f) * 3f },
            { "Rose_PetalRuby", new Color(1f, .10f, .04f) * 1.2f }, { "Rose_PetalEdge", new Color(1f, .30f, .08f) * 2f },
            { "Rose_EmberHeart", new Color(1f, .65f, .25f) * 3.2f }, { "Rose_PetalShadow", new Color(1f, .08f, .04f) * .4f },
            { "Shrub_Red", new Color(1f, .18f, .05f) * 1f },
        };

        static Plant P(string model, float x, float z, float scale = 1f, float yaw = 0f)
        {
            return new Plant { Model = model, At = new Vector3(x, 0f, z), Scale = scale, Yaw = yaw };
        }

        /// <summary>
        /// The two roadside keys the game already binds become ash plants; the single plants get their own keys for
        /// later use (builder menu, new places).
        /// </summary>
        static Clump[] Clumps()
        {
            return new[]
            {
                new Clump { Key = "RoadsideShrub0", Plants = new[]
                {
                    P("FireSilk_Ash_Shrub", 0, 0, 1.4f), P("AshGrass_Tuft", .8f, .2f, 1f, 40f), P("AshGrass_Tuft", -.7f, -.25f, .8f, 200f),
                    P("BlueAsh_Moss_Cushion", .15f, .7f, 1.3f, 70f),
                } },
                new Clump { Key = "RoadsideShrub1", Plants = new[]
                {
                    P("Embercap_Mushroom_Cluster", 0, 0, 1.5f), P("Cinderbloom_Fire_Rose_Clump", .75f, .15f, .9f, 120f),
                    P("AshGrass_Tuft", -.75f, .1f, .9f, 300f),
                } },
                new Clump { Key = "AshFloraGrass", Plants = new[] { P("AshGrass_Tuft", 0, 0) } },
                new Clump { Key = "AshFloraMoss", Plants = new[] { P("BlueAsh_Moss_Cushion", 0, 0) } },
                new Clump { Key = "AshFloraShrub", Plants = new[] { P("FireSilk_Ash_Shrub", 0, 0) } },
                new Clump { Key = "AshFloraMushrooms", Plants = new[] { P("Embercap_Mushroom_Cluster", 0, 0) } },
                new Clump { Key = "AshFloraRoses", Plants = new[] { P("Cinderbloom_Fire_Rose_Clump", 0, 0) } },
            };
        }

        public static string[] Keys() { return Clumps().Select(c => c.Key).ToArray(); }

        public static bool Installed
        {
            get { return File.Exists(Generated + "AshGround_Albedo.png") && AssetDatabase.IsValidFolder(SourceRoot + "/Flora"); }
        }

        /// <summary>True when this plant visual was built here; the older shrub extraction then leaves the key alone.</summary>
        public static bool Owns(string key)
        {
            if (!Keys().Contains(key)) return false;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + key + ".prefab");
            if (prefab == null) return false;
            return prefab.GetComponentsInChildren<MeshRenderer>(true).Any(r =>
                r.sharedMaterial != null && AssetDatabase.GetAssetPath(r.sharedMaterial).StartsWith(WorkFolder + "/", StringComparison.Ordinal));
        }

        [MenuItem("ARE ARPG/Art/Ashen World/Build Ground And Plants")]
        public static void Build()
        {
            if (!Installed) throw new InvalidOperationException("Aschenwelt-Quellen fehlen: " + SourceRoot);
            EnsureFolder(WorkFolder + "/Materials");
            var report = new StringBuilder();
            // The built-in cube maps its top face as u = 0.5 - x and v = 0.5 - z (in units of its own size). The
            // verge strips lie on the ground at x = +-8: with this shift their ash continues the ground's pattern,
            // so no edge shows. One material fits both strips because the 16 m between them are a whole repeat.
            float repeats = GroundSize / GroundMetresPerTile;
            Material ground = Ground("OutdoorGround", new Vector2(repeats, repeats), Vector2.zero);
            report.AppendLine("OutdoorGround\t" + AssetDatabase.GetAssetPath(ground));
            var strip = new Vector2(VergeWidth / GroundMetresPerTile, VergeLength / GroundMetresPerTile);
            var shift = new Vector2(Mathf.Repeat(repeats * .5f - strip.x * (.5f + VergeCentre / VergeWidth), 1f),
                Mathf.Repeat(repeats * .5f - strip.y * .5f, 1f));
            Material verge = Ground("GrassVerge", strip, shift);
            report.AppendLine("GrassVerge\t" + AssetDatabase.GetAssetPath(verge));
            foreach (Clump clump in Clumps()) report.AppendLine(BuildClump(clump));
            AssetDatabase.SaveAssets();
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Library/AshenWorld/report.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report.ToString());
            Debug.Log("ARE_ASHEN_WORLD: ground and " + Clumps().Length + " plant visuals built; report " + path);
        }

        /// <summary>
        /// A ground surface: ash colour map, normal map, the glow map of the ember veins and a fine grain for close
        /// views. The glow is part of the material, so the veins shine by day and stand out at night without any script.
        /// </summary>
        static Material Ground(string surface, Vector2 tiling, Vector2 offset)
        {
            Texture2D colour = Texture("AshGround_Albedo.png", TextureImporterType.Default, true, GroundTextureSize);
            Texture2D normal = Texture("AshGround_Normal.png", TextureImporterType.NormalMap, false, GroundTextureSize);
            Texture2D veins = Texture("AshGround_Emission.png", TextureImporterType.Default, true, GlowTextureSize);
            Texture2D grain = Texture("AshGround_Detail.png", TextureImporterType.Default, false, DetailTextureSize);
            Material material = Lit(Surfaces + surface + ".mat");
            material.SetTexture("_BaseMap", colour); material.SetTextureScale("_BaseMap", tiling); material.SetTextureOffset("_BaseMap", offset);
            material.SetColor("_BaseColor", Color.white);
            // The large map covers 16 m, so one of its pixels is nearly a centimetre: the grain keeps the ash crisp
            // under the hero's feet. It multiplies the colour around mid grey.
            material.SetTexture("_DetailAlbedoMap", grain); material.SetTextureScale("_DetailAlbedoMap", Vector2.one * DetailRepeats);
            material.SetFloat("_DetailAlbedoMapScale", 1f); material.EnableKeyword("_DETAIL_MULX2");
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1f); material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_EmissionMap", veins); material.SetColor("_EmissionColor", Color.white * GroundGlow); material.EnableKeyword("_EMISSION");
            // Not "emissive is black": the pipeline's material check would switch the glow off again.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.SetFloat("_Smoothness", .06f); material.SetFloat("_Metallic", 0f);
            Matt(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D Texture(string file, TextureImporterType type, bool colourSpace, int size)
        {
            string path = Generated + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Textur fehlt: " + path);
            if (importer.textureType != type || importer.sRGBTexture != colourSpace || importer.maxTextureSize != size ||
                importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled || importer.anisoLevel != 4)
            {
                importer.textureType = type; importer.sRGBTexture = colourSpace; importer.maxTextureSize = size;
                importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static string BuildClump(Clump clump)
        {
            var root = new GameObject(clump.Key);
            try
            {
                long triangles = 0; int glowing = 0;
                foreach (Plant plant in clump.Plants)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + plant.Model + ".obj");
                    if (model == null) throw new InvalidOperationException("Pflanze fehlt: " + plant.Model);
                    Dictionary<string, Color> colours = ModelColours(Models + plant.Model + ".mtl");
                    MeshFilter[] sources = model.GetComponentsInChildren<MeshFilter>(true);
                    float bottom = float.MaxValue;
                    foreach (MeshFilter source in sources)
                        if (source.sharedMesh != null) bottom = Mathf.Min(bottom, source.sharedMesh.bounds.min.y);
                    if (bottom == float.MaxValue) throw new InvalidOperationException("Pflanze ohne Netz: " + plant.Model);
                    foreach (MeshFilter source in sources)
                    {
                        var renderer = source.GetComponent<MeshRenderer>();
                        if (renderer == null || source.sharedMesh == null) continue;
                        var part = new GameObject(plant.Model);
                        part.transform.SetParent(root.transform, false);
                        // Every plant stands on the ground, sunk in by a finger's width so no gap shows under it.
                        part.transform.localPosition = plant.At - Vector3.up * (bottom * plant.Scale + Sink);
                        part.transform.localRotation = Quaternion.Euler(0f, plant.Yaw, 0f);
                        part.transform.localScale = Vector3.one * plant.Scale;
                        part.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                        var materials = new List<Material>();
                        foreach (Material imported in renderer.sharedMaterials)
                        {
                            Material material = PlantMaterial(imported, colours);
                            if (material.IsKeywordEnabled("_EMISSION")) glowing++;
                            materials.Add(material);
                        }
                        var target = part.AddComponent<MeshRenderer>();
                        target.sharedMaterials = materials.ToArray();
                        target.shadowCastingMode = ShadowCastingMode.Off; // thin blades and petals: shadows cost more than they show
                        for (int sub = 0; sub < source.sharedMesh.subMeshCount; sub++) triangles += source.sharedMesh.GetIndexCount(sub) / 3;
                    }
                }
                if (root.transform.childCount == 0) throw new InvalidOperationException("Leeres Visual: " + clump.Key);
                if (PrefabUtility.SaveAsPrefabAsset(root, Visuals + clump.Key + ".prefab") == null)
                    throw new IOException("Konnte nicht speichern: " + clump.Key);
                return clump.Key + "\tplants=" + clump.Plants.Length + "\ttris=" + triangles + "\tglowingParts=" + glowing;
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// One material per plant part. The model file names its colours in the .mtl next to it; the importer keeps
        /// only the texture, so the colour is read from there. Blades and petals are drawn from both sides.
        /// </summary>
        static Material PlantMaterial(Material imported, Dictionary<string, Color> colours)
        {
            if (imported == null) throw new InvalidOperationException("Pflanzenteil ohne Material.");
            Material material = Lit(WorkFolder + "/Materials/" + imported.name + ".mat");
            Texture texture = imported.HasProperty("_BaseMap") ? imported.GetTexture("_BaseMap") : imported.mainTexture;
            Color colour;
            if (!colours.TryGetValue(imported.name, out colour)) colour = Color.white;
            // The painted map is a mid-tone wash; lifted so that the named colour of the part comes through.
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.Lerp(colour, Color.white, .25f) * 1.35f);
            material.SetFloat("_Smoothness", .1f); material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off); material.doubleSidedGI = true;
            Color glow;
            if (Glow.TryGetValue(imported.name, out glow))
            {
                material.SetColor("_EmissionColor", glow); material.SetTexture("_EmissionMap", null); material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else
            {
                material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            Matt(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Diffuse colours of a Wavefront .mtl file: "newmtl Name" followed by "Kd r g b".</summary>
        static Dictionary<string, Color> ModelColours(string path)
        {
            var colours = new Dictionary<string, Color>();
            if (!File.Exists(path)) return colours;
            string current = null;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 2 && words[0] == "newmtl") current = words[1];
                float r, g, b;
                if (current != null && words.Length == 4 && words[0] == "Kd" &&
                    float.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out r) &&
                    float.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out g) &&
                    float.TryParse(words[3], NumberStyles.Float, CultureInfo.InvariantCulture, out b))
                    colours[current] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b));
            }
            return colours;
        }

        static Material Lit(string path)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit fehlt.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else
            {
                // An older material under this name may carry maps and switches of its own; start clean.
                material.shader = shader;
                material.shaderKeywords = new string[0];
                foreach (string slot in new[] { "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_ParallaxMap", "_DetailMask", "_DetailAlbedoMap", "_DetailNormalMap" })
                    if (material.HasProperty(slot)) material.SetTexture(slot, null);
            }
            return material;
        }

        /// <summary>Ash and leaves do not shine: no highlights, no reflections (cheaper on phones too).</summary>
        static void Matt(Material material)
        {
            material.SetFloat("_SpecularHighlights", 0f); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
