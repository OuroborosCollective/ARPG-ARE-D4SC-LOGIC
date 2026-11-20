using System;
using System.Collections.Generic;
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
    /// The ground maps come from Tools/Art/generate_ashen_ground.py, the ember mushrooms are built here with maps from
    /// Tools/Art/generate_ember_flora.py, and grass, moss, shrub and fire roses come from the "Cinderbloom" plant set
    /// made for this game. All of it is private art in this workspace. Everything built here is passive: materials
    /// for named ground surfaces and mesh-only plant visuals under the keys the game binds.
    /// </summary>
    public static class AshenWorldArt
    {
        /// <summary>Private source folder of the Cinderbloom plant models.</summary>
        public const string SourceRoot = "Assets/PrivateArt/Cinderbloom";
        /// <summary>Private folder for everything built or painted for the ash world. Never committed.</summary>
        public const string WorkFolder = "Assets/PrivateArt/AshenWorld";
        /// <summary>The maps painted by the tools under Tools/Art.</summary>
        public const string Generated = WorkFolder + "/Generated/";
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

        // Layout of the ember cap maps, shared with Tools/Art/generate_ember_flora.py.
        static readonly Vector2 CapCentre = new Vector2(.25f, .75f), GillCentre = new Vector2(.75f, .75f);
        const float AtlasDisc = .235f, StemLow = .02f, StemHigh = .48f;
        const int CapSides = 16, CapRings = 6, StemSides = 8, StemRings = 4;
        /// <summary>Glow of the ember caps and of the palette plants on top of their maps.</summary>
        const float CapGlow = 2.2f, PaletteGlow = 3f;
        /// <summary>The light a cluster throws on the ash lies this far above the plant's foot.</summary>
        const float PoolHeight = .03f;
        /// <summary>A plant smaller than this share of the screen height is no longer drawn.</summary>
        const float CullHeight = .02f;
        const int CapPart = 0, PlantPart = 1, GlowPart = 2;
        const float CapGrowth = 1.25f;

        sealed class Plant { public string Model; public Vector3 At; public float Scale = 1f, Yaw; }
        sealed class Cap { public Vector3 At; public float Radius, Stem, Lean, Yaw; }
        sealed class Clump
        {
            public string Key; public Plant[] Plants = new Plant[0]; public Cap[] Caps = new Cap[0];
            /// <summary>Radius of the light the clump throws on the ash; 0 for plants that do not glow.</summary>
            public float Glow;
        }
        sealed class Paint { public Color Colour, Glow = Color.black; public float Strength; }

        static Paint Dye(float r, float g, float b) { return new Paint { Colour = new Color(r, g, b) }; }

        /// <summary>Glow is a linear colour: red needs very little green, the screen lifts mid values towards yellow.</summary>
        static Paint Ember(float r, float g, float b, float glowR, float glowG, float glowB, float strength)
        {
            return new Paint { Colour = new Color(r, g, b), Glow = new Color(glowR, glowG, glowB), Strength = strength };
        }

        /// <summary>
        /// Colours of the Cinderbloom plant parts, by the material name in the model. The models carry only a flat
        /// wash as texture, so one small palette serves all of them: every plant clump is drawn in a single pass.
        /// Cold blue-grey leaves, so the red of petals and embers has something to burn against.
        /// </summary>
        static readonly SortedDictionary<string, Paint> Palette = new SortedDictionary<string, Paint>(StringComparer.Ordinal)
        {
            { "AshGrass_Deep", Dye(.26f, .36f, .36f) }, { "AshGrass_Cool", Dye(.42f, .52f, .58f) },
            { "Moss_Deep", Dye(.24f, .36f, .32f) }, { "Moss_Blue", Dye(.36f, .50f, .58f) },
            { "Shrub_Deep", Dye(.22f, .33f, .32f) }, { "Shrub_Cool", Dye(.34f, .45f, .55f) },
            { "Shrub_Red", Ember(.55f, .16f, .14f, 1f, .07f, .01f, .9f) },
            { "Rose_Stem", Dye(.20f, .30f, .28f) }, { "Rose_Leaf", Dye(.26f, .40f, .40f) }, { "Rose_LeafCool", Dye(.40f, .50f, .60f) },
            { "Rose_PetalShadow", Ember(.40f, .04f, .08f, .8f, .02f, .008f, .5f) },
            { "Rose_PetalRuby", Ember(.72f, .05f, .10f, 1f, .04f, .008f, 1.1f) },
            { "Rose_PetalEdge", Ember(.95f, .20f, .12f, 1f, .10f, .012f, 1.8f) },
            { "Rose_EmberHeart", Ember(1f, .45f, .12f, 1f, .13f, .014f, 2.2f) },
        };
        const int PaletteCells = 8, PaletteCellPixels = 8;

        static Plant P(string model, float x, float z, float scale = 1f, float yaw = 0f)
        {
            return new Plant { Model = model, At = new Vector3(x, 0f, z), Scale = scale, Yaw = yaw };
        }

        /// <summary>
        /// An ember mushroom, drawn small and grown by CapGrowth: the old one of a cluster reaches the hero's thigh,
        /// so the clusters read as landmarks in the field. It leans away from the middle of its cluster.
        /// </summary>
        static Cap M(float x, float z, float radius, float stem, float lean)
        {
            float yaw = x == 0f && z == 0f ? 35f : Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            return new Cap { At = new Vector3(x, 0f, z) * CapGrowth, Radius = radius * CapGrowth, Stem = stem * CapGrowth, Lean = lean, Yaw = yaw };
        }

        /// <summary>An old mushroom with its young around it, knee-high.</summary>
        static Cap[] Elders()
        {
            return new[]
            {
                M(0f, 0f, .30f, .46f, 5f), M(.40f, .10f, .19f, .30f, 13f), M(-.31f, .22f, .16f, .25f, 17f),
                M(-.12f, -.36f, .13f, .19f, 21f), M(.27f, -.27f, .09f, .12f, 25f), M(-.45f, -.09f, .07f, .09f, 28f),
            };
        }

        static Cap[] Young()
        {
            return new[] { M(0f, 0f, .15f, .22f, 8f), M(.21f, .09f, .10f, .14f, 19f), M(-.14f, .17f, .07f, .10f, 26f) };
        }

        /// <summary>
        /// The two roadside keys the game already binds become ash plants; the single plants have their own keys for
        /// the scatter in the open field (AshFloraScatter) and for later places.
        /// </summary>
        static Clump[] Clumps()
        {
            return new[]
            {
                new Clump { Key = "RoadsideShrub0", Plants = new[]
                    { P("FireSilk_Ash_Shrub", 0, 0, 1.4f), P("AshGrass_Tuft", .8f, .2f, 1f, 40f), P("AshGrass_Tuft", -.7f, -.25f, .8f, 200f) } },
                new Clump { Key = "RoadsideShrub1", Caps = Elders(), Glow = 1.25f, Plants = new[]
                    { P("Cinderbloom_Fire_Rose_Clump", .95f, -.15f, .8f, 120f), P("AshGrass_Tuft", -.95f, .1f, .9f, 300f) } },
                new Clump { Key = "AshFloraGrass", Plants = new[] { P("AshGrass_Tuft", 0, 0), P("AshGrass_Tuft", .45f, .2f, .7f, 140f) } },
                new Clump { Key = "AshFloraMoss", Plants = new[] { P("BlueAsh_Moss_Cushion", 0, 0) } },
                new Clump { Key = "AshFloraShrub", Plants = new[] { P("FireSilk_Ash_Shrub", 0, 0) } },
                new Clump { Key = "AshFloraMushrooms", Caps = Elders(), Glow = 1.3f },
                new Clump { Key = "AshFloraMushroomsSmall", Caps = Young(), Glow = .8f },
                new Clump { Key = "AshFloraRoses", Plants = new[] { P("Cinderbloom_Fire_Rose_Clump", 0, 0) }, Glow = .75f },
            };
        }

        public static string[] Keys() { return Clumps().Select(c => c.Key).ToArray(); }

        public static bool Installed
        {
            get
            {
                return File.Exists(Generated + "AshGround_Albedo.png") && File.Exists(Generated + "EmberCap_Albedo.png") &&
                    AssetDatabase.IsValidFolder(SourceRoot + "/Flora");
            }
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
            if (!Installed) throw new InvalidOperationException("Aschenwelt-Quellen fehlen: " + Generated + " und " + SourceRoot);
            // Materials are rebuilt from nothing; every visual that uses them is rebuilt below.
            AssetDatabase.DeleteAsset(WorkFolder + "/Materials");
            EnsureFolder(WorkFolder + "/Materials"); EnsureFolder(WorkFolder + "/Meshes"); EnsureFolder(WorkFolder + "/Textures");
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

            Material caps = CapMaterial(), plants = PlantMaterial(), glow = GlowMaterial();
            foreach (Clump clump in Clumps()) report.AppendLine(BuildClump(clump, caps, plants, glow));
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
            Texture2D colour = Texture("AshGround_Albedo.png", TextureImporterType.Default, true, GroundTextureSize, TextureWrapMode.Repeat);
            Texture2D normal = Texture("AshGround_Normal.png", TextureImporterType.NormalMap, false, GroundTextureSize, TextureWrapMode.Repeat);
            Texture2D veins = Texture("AshGround_Emission.png", TextureImporterType.Default, true, GlowTextureSize, TextureWrapMode.Repeat);
            Texture2D grain = Texture("AshGround_Detail.png", TextureImporterType.Default, false, DetailTextureSize, TextureWrapMode.Repeat);
            Material material = Lit(Surfaces + surface + ".mat");
            material.SetTexture("_BaseMap", colour); material.SetTextureScale("_BaseMap", tiling); material.SetTextureOffset("_BaseMap", offset);
            material.SetColor("_BaseColor", Color.white);
            // The large map covers 16 m, so one of its pixels is nearly a centimetre: the grain keeps the ash crisp
            // under the hero's feet. It multiplies the colour around mid grey.
            material.SetTexture("_DetailAlbedoMap", grain); material.SetTextureScale("_DetailAlbedoMap", Vector2.one * DetailRepeats);
            material.SetFloat("_DetailAlbedoMapScale", 1f); material.EnableKeyword("_DETAIL_MULX2");
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1f); material.EnableKeyword("_NORMALMAP");
            Glowing(material, veins, GroundGlow);
            material.SetFloat("_Smoothness", .06f); material.SetFloat("_Metallic", 0f);
            Matt(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Glowing(Material material, Texture map, float strength)
        {
            material.SetTexture("_EmissionMap", map); material.SetColor("_EmissionColor", Color.white * strength); material.EnableKeyword("_EMISSION");
            // Not "emissive is black": the pipeline's material check would switch the glow off again.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        static Texture2D Texture(string file, TextureImporterType type, bool colourSpace, int size, TextureWrapMode wrap, bool transparency = false)
        {
            string path = Generated + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Textur fehlt: " + path);
            if (importer.textureType != type || importer.sRGBTexture != colourSpace || importer.maxTextureSize != size ||
                importer.wrapMode != wrap || !importer.mipmapEnabled || importer.anisoLevel != 4 || importer.alphaIsTransparency != transparency)
            {
                importer.textureType = type; importer.sRGBTexture = colourSpace; importer.maxTextureSize = size;
                importer.wrapMode = wrap; importer.mipmapEnabled = true; importer.anisoLevel = 4; importer.alphaIsTransparency = transparency;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- plants

        /// <summary>The ember caps: crust, glowing cracks, gills and stem from one small pair of maps.</summary>
        static Material CapMaterial()
        {
            Material material = Lit(WorkFolder + "/Materials/EmberCap.mat");
            material.SetTexture("_BaseMap", Texture("EmberCap_Albedo.png", TextureImporterType.Default, true, 512, TextureWrapMode.Clamp));
            material.SetColor("_BaseColor", Color.white);
            Glowing(material, Texture("EmberCap_Emission.png", TextureImporterType.Default, true, 512, TextureWrapMode.Clamp), CapGlow);
            material.SetFloat("_Smoothness", .12f); material.SetFloat("_Metallic", 0f);
            Matt(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Grass, moss, shrub and roses: flat colours from the palette, drawn from both sides.</summary>
        static Material PlantMaterial()
        {
            int size = PaletteCells * PaletteCellPixels;
            var colours = new Color[size * size]; var glows = new Color[size * size];
            for (int i = 0; i < colours.Length; i++) { colours[i] = new Color(.3f, .3f, .3f, 1f); glows[i] = Color.black; }
            int index = 0;
            foreach (Paint paint in Palette.Values)
            {
                Color stored = (paint.Glow * (paint.Strength / PaletteGlow)).gamma;   // the picture holds screen values
                for (int y = 0; y < PaletteCellPixels; y++)
                    for (int x = 0; x < PaletteCellPixels; x++)
                    {
                        int at = (index / PaletteCells * PaletteCellPixels + y) * size + index % PaletteCells * PaletteCellPixels + x;
                        colours[at] = new Color(paint.Colour.r, paint.Colour.g, paint.Colour.b, 1f);
                        glows[at] = new Color(stored.r, stored.g, stored.b, 1f);
                    }
                index++;
            }
            Material material = Lit(WorkFolder + "/Materials/AshFlora.mat");
            material.SetTexture("_BaseMap", PaletteMap("AshFloraPalette_Albedo.png", colours, size)); material.SetColor("_BaseColor", Color.white);
            Glowing(material, PaletteMap("AshFloraPalette_Emission.png", glows, size), PaletteGlow);
            material.SetFloat("_Smoothness", .1f); material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off); material.doubleSidedGI = true;
            Matt(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D PaletteMap(string file, Color[] pixels, int size)
        {
            string path = WorkFolder + "/Textures/" + file;
            var picture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try { picture.SetPixels(pixels); picture.Apply(); File.WriteAllBytes(path, picture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(picture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // Flat cells: no smoothing, no smaller copies, no compression, or neighbouring colours would bleed.
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Vector2 PaletteCell(string part)
        {
            int index = 0;
            foreach (string name in Palette.Keys)
            {
                if (name == part) return new Vector2((index % PaletteCells + .5f) / PaletteCells, (index / PaletteCells + .5f) / PaletteCells);
                index++;
            }
            throw new InvalidOperationException("Pflanzenteil ohne Farbe in der Palette: " + part);
        }

        /// <summary>The light a glowing cluster throws on the ash around it: a soft round glow added to the ground.</summary>
        static Material GlowMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP/Unlit fehlt.");
            string path = WorkFolder + "/Materials/EmberGlow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            material.SetTexture("_BaseMap", Texture("EmberGlow.png", TextureImporterType.Default, true, 128, TextureWrapMode.Clamp, true));
            material.SetColor("_BaseColor", new Color(1f, .42f, .14f, .45f));
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 2f); // transparent, additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f); material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Corners, normals and triangles of one clump, in up to three parts: ember caps, palette plants, light.</summary>
        sealed class Builder
        {
            public readonly List<Vector3> Points = new List<Vector3>(), Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int>[] Parts = { new List<int>(), new List<int>(), new List<int>() };

            public int Add(Vector3 point, Vector3 normal, Vector2 uv)
            {
                Points.Add(point); Normals.Add(normal.normalized); Uvs.Add(uv);
                return Points.Count - 1;
            }

            /// <summary>Unity draws a triangle from the side its corners run clockwise; turn it so that side is the outside.</summary>
            public void Face(int part, int a, int b, int c)
            {
                Vector3 outside = Normals[a] + Normals[b] + Normals[c];
                if (Vector3.Dot(Vector3.Cross(Points[b] - Points[a], Points[c] - Points[a]), outside) < 0f) { int swap = b; b = c; c = swap; }
                Parts[part].Add(a); Parts[part].Add(b); Parts[part].Add(c);
            }

            /// <summary>a and b on one ring, c and d above them on the next.</summary>
            public void Quad(int part, int a, int b, int c, int d) { Face(part, a, b, c); Face(part, b, d, c); }
        }

        /// <summary>
        /// One ember mushroom: a bent stem, a domed cap whose rim curls under, and gills from the rim to the stem.
        /// 272 triangles. The cap's map is laid out from the apex (centre) to the rim (edge), so the ash lies on top.
        /// </summary>
        static void Mushroom(Builder mesh, Cap cap, int number)
        {
            float radius = cap.Radius, dome = radius * .62f, stem = cap.Stem, thick = Mathf.Max(.022f, radius * .24f);
            float sway = radius * .28f;                                                   // the stem bends towards its lean
            Quaternion turn = Quaternion.Euler(0f, cap.Yaw, 0f) * Quaternion.Euler(cap.Lean, 0f, 0f);
            Func<Vector3, Vector3> place = point => cap.At + turn * point;

            var ring = new int[StemRings + 1, StemSides + 1];                             // one extra corner closes the map
            for (int j = 0; j <= StemRings; j++)
            {
                float t = j / (float)StemRings;
                var centre = new Vector3(0f, stem * t, sway * t * t);
                float girth = thick * Mathf.Lerp(1.35f, .85f, Mathf.Sqrt(t));
                for (int i = 0; i <= StemSides; i++)
                {
                    float angle = i * Mathf.PI * 2f / StemSides;
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    ring[j, i] = mesh.Add(place(centre + radial * girth), turn * (radial + Vector3.up * .25f),
                        new Vector2(Mathf.Lerp(StemLow, StemHigh, i / (float)StemSides), Mathf.Lerp(StemLow, StemHigh, t)));
                }
            }
            for (int j = 0; j < StemRings; j++)
                for (int i = 0; i < StemSides; i++)
                    mesh.Quad(CapPart, ring[j, i], ring[j, i + 1], ring[j + 1, i], ring[j + 1, i + 1]);

            var top = new Vector3(0f, stem, sway);
            float phase = number * 1.7f;
            int apex = mesh.Add(place(top + Vector3.up * dome), turn * Vector3.up, CapCentre);
            var caps = new int[CapRings, CapSides];
            var rim = new Vector3[CapSides];
            for (int k = 1; k <= CapRings; k++)
            {
                float share = k / (float)CapRings, bend = share * 1.15f * Mathf.PI * .5f;  // past a quarter turn the rim curls under
                for (int i = 0; i < CapSides; i++)
                {
                    float angle = i * Mathf.PI * 2f / CapSides;
                    // No cap is a perfect circle.
                    float wobble = 1f + share * (.06f * Mathf.Sin(3f * angle + phase) + .04f * Mathf.Sin(5f * angle + phase * 2.3f));
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 point = top + radial * (radius * Mathf.Sin(bend) * wobble) + Vector3.up * (dome * Mathf.Cos(bend));
                    Vector3 normal = radial * (Mathf.Sin(bend) / radius) + Vector3.up * (Mathf.Cos(bend) / dome);
                    caps[k - 1, i] = mesh.Add(place(point), turn * normal,
                        CapCentre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (AtlasDisc * share));
                    if (k == CapRings) rim[i] = point;
                }
            }
            for (int i = 0; i < CapSides; i++)
            {
                int next = (i + 1) % CapSides;
                mesh.Face(CapPart, apex, caps[0, i], caps[0, next]);
                for (int k = 0; k < CapRings - 1; k++) mesh.Quad(CapPart, caps[k, i], caps[k, next], caps[k + 1, i], caps[k + 1, next]);
            }

            var outer = new int[CapSides]; var inner = new int[CapSides];
            for (int i = 0; i < CapSides; i++)
            {
                float angle = i * Mathf.PI * 2f / CapSides;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var around = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector3 down = turn * (Vector3.down + radial * .35f);
                outer[i] = mesh.Add(place(rim[i]), down, GillCentre + around * (AtlasDisc * .97f));
                inner[i] = mesh.Add(place(top + radial * (thick * .85f) + Vector3.down * .01f), down,
                    GillCentre + around * (AtlasDisc * thick * .85f / radius));
            }
            for (int i = 0; i < CapSides; i++)
            {
                int next = (i + 1) % CapSides;
                mesh.Quad(CapPart, outer[i], outer[next], inner[i], inner[next]);
            }
        }

        /// <summary>A plant of the Cinderbloom set: its parts take their colour from the palette, by material name.</summary>
        static void Model(Builder mesh, Plant plant)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + plant.Model + ".obj");
            if (model == null) throw new InvalidOperationException("Pflanze fehlt: " + plant.Model);
            MeshFilter[] sources = model.GetComponentsInChildren<MeshFilter>(true);
            float bottom = float.MaxValue;
            foreach (MeshFilter source in sources)
                if (source.sharedMesh != null) bottom = Mathf.Min(bottom, source.sharedMesh.bounds.min.y);
            if (bottom == float.MaxValue) throw new InvalidOperationException("Pflanze ohne Netz: " + plant.Model);
            Quaternion turn = Quaternion.Euler(0f, plant.Yaw, 0f);
            // Every plant stands on the ground, sunk in by a finger's width so no gap shows under it.
            Vector3 foot = plant.At - Vector3.up * (bottom * plant.Scale + Sink);
            foreach (MeshFilter source in sources)
            {
                var renderer = source.GetComponent<MeshRenderer>();
                Mesh shape = source.sharedMesh;
                if (renderer == null || shape == null) continue;
                Vector3[] points = shape.vertices, normals = shape.normals;
                if (normals.Length != points.Length) throw new InvalidOperationException("Pflanze ohne Normalen: " + plant.Model);
                Material[] materials = renderer.sharedMaterials;
                for (int part = 0; part < shape.subMeshCount; part++)
                {
                    Material material = materials[Mathf.Min(part, materials.Length - 1)];
                    if (material == null) throw new InvalidOperationException("Pflanzenteil ohne Material: " + plant.Model);
                    Vector2 cell = PaletteCell(material.name);
                    var moved = new Dictionary<int, int>();
                    foreach (int corner in shape.GetTriangles(part))
                    {
                        int to;
                        if (!moved.TryGetValue(corner, out to))
                        {
                            to = mesh.Add(foot + turn * (points[corner] * plant.Scale), turn * normals[corner], cell);
                            moved.Add(corner, to);
                        }
                        mesh.Parts[PlantPart].Add(to);
                    }
                }
            }
        }

        static void Pool(Builder mesh, float radius)
        {
            int a = mesh.Add(new Vector3(-radius, PoolHeight, -radius), Vector3.up, new Vector2(0f, 0f));
            int b = mesh.Add(new Vector3(radius, PoolHeight, -radius), Vector3.up, new Vector2(1f, 0f));
            int c = mesh.Add(new Vector3(-radius, PoolHeight, radius), Vector3.up, new Vector2(0f, 1f));
            int d = mesh.Add(new Vector3(radius, PoolHeight, radius), Vector3.up, new Vector2(1f, 1f));
            mesh.Quad(GlowPart, a, b, c, d);
        }

        /// <summary>One clump is one mesh with at most three materials, so it costs at most three draws.</summary>
        static string BuildClump(Clump clump, Material caps, Material plants, Material glow)
        {
            var mesh = new Builder();
            foreach (Plant plant in clump.Plants) Model(mesh, plant);
            for (int i = 0; i < clump.Caps.Length; i++) Mushroom(mesh, clump.Caps[i], i);
            if (clump.Glow > 0f) Pool(mesh, clump.Glow);
            if (mesh.Points.Count == 0) throw new InvalidOperationException("Leeres Visual: " + clump.Key);
            if (mesh.Points.Count > 65000) throw new InvalidOperationException("Zu viele Ecken: " + clump.Key);

            var shape = new Mesh { name = clump.Key };
            shape.SetVertices(mesh.Points); shape.SetNormals(mesh.Normals); shape.SetUVs(0, mesh.Uvs);
            var materials = new List<Material>();
            Material[] byPart = { caps, plants, glow };
            long triangles = 0;
            shape.subMeshCount = mesh.Parts.Count(part => part.Count > 0);
            for (int part = 0; part < mesh.Parts.Length; part++)
            {
                if (mesh.Parts[part].Count == 0) continue;
                shape.SetTriangles(mesh.Parts[part], materials.Count);
                materials.Add(byPart[part]); triangles += mesh.Parts[part].Count / 3;
            }
            shape.RecalculateBounds();
            Bounds size = shape.bounds;
            Mesh saved = SaveMesh(shape, clump.Key);

            var root = new GameObject(clump.Key);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = saved;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials.ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.Off; // thin blades and petals: shadows cost more than they show
                // Far away a knee-high plant is a few pixels: stop drawing it there.
                var distance = root.AddComponent<LODGroup>();
                distance.SetLODs(new[] { new LOD(CullHeight, new Renderer[] { renderer }) });
                distance.RecalculateBounds();
                if (PrefabUtility.SaveAsPrefabAsset(root, Visuals + clump.Key + ".prefab") == null)
                    throw new IOException("Konnte nicht speichern: " + clump.Key);
            }
            finally { Object.DestroyImmediate(root); }
            return clump.Key + "\tplants=" + clump.Plants.Length + "\tcaps=" + clump.Caps.Length + "\ttris=" + triangles +
                "\tdraws=" + materials.Count + "\tsize=" + size.size.ToString("0.00");
        }

        static Mesh SaveMesh(Mesh mesh, string key)
        {
            string path = WorkFolder + "/Meshes/" + key + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); } // keep the asset identity
            // The game never reads the corners: do not ship a second copy for scripts.
            var serialized = new SerializedObject(saved);
            serialized.FindProperty("m_IsReadable").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(saved);
            return saved;
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
