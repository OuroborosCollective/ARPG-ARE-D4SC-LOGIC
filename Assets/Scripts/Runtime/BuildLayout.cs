using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Bau-Modus: how a catalog entry is realised in the world.</summary>
    public enum BuildKind { Prefab = 0, Surface = 1 }

    /// <summary>
    /// One placeable building block of the Bau-Modus. Presentation only: placed pieces are passive scenery,
    /// never enter Core rules, saves, collision or walkability.
    /// </summary>
    public sealed class BuildCatalogEntry
    {
        public readonly string Key, Label, Category;
        private readonly string resourceKey;
        public readonly BuildKind Kind;
        /// <summary>Fallback blockout size in metres when the private art is not installed.</summary>
        public readonly float Width, Depth, Height;
        public readonly Color Fallback;
        public readonly PrimitiveType Shape;

        public BuildCatalogEntry(string key, string label, string category, BuildKind kind, float width, float depth, float height,
            Color fallback, PrimitiveType shape = PrimitiveType.Cube, string resourceKey = null)
        {
            Key = key; Label = label; Category = category; Kind = kind;
            this.resourceKey = resourceKey ?? key;
            Width = width; Depth = depth; Height = height; Fallback = fallback; Shape = shape;
        }

        /// <summary>Resource path below Resources/ (prefab or surface material).</summary>
        public string ResourcePath
        {
            get { return Kind == BuildKind.Surface ? BuildCatalog.SurfaceRoot + resourceKey : BuildCatalog.PrefabRoot + resourceKey; }
        }
    }

    /// <summary>Category of the Bau-Modus menu.</summary>
    public sealed class BuildCategory
    {
        public readonly string Key, Label, Icon;
        public BuildCategory(string key, string label, string icon) { Key = key; Label = label; Icon = icon; }
    }

    /// <summary>
    /// Reviewed building blocks. Story-owned figures (hero, Mara, Ilyan, Dorn, patients, hunter, bear, enemies),
    /// loot and world shells are deliberately not placeable: they belong to the game rules.
    /// </summary>
    public static class BuildCatalog
    {
        public const string PrefabRoot = "ARE/Visuals/";
        public const string SurfaceRoot = "ARE/Visuals/WorldSurfaces/";
        public const string Houses = "haeuser", Walls = "mauern", Props = "props", Residents = "bewohner", Nature = "natur", Grounds = "untergruende";

        private static readonly Color Wood = new Color(0.45f, 0.32f, 0.20f), Stone = new Color(0.42f, 0.42f, 0.44f),
            Leaf = new Color(0.22f, 0.40f, 0.20f), Cloth = new Color(0.36f, 0.30f, 0.46f);

        private static readonly BuildCategory[] CategoryList =
        {
            new BuildCategory(Houses, "Häuser", "🏠"),
            new BuildCategory(Walls, "Mauern & Wege", "🧱"),
            new BuildCategory(Props, "Props", "📦"),
            new BuildCategory(Residents, "Bewohner", "🧑"),
            new BuildCategory(Nature, "Natur & Wald", "🌲"),
            new BuildCategory(Grounds, "Untergründe", "🟫")
        };

        private static readonly BuildCatalogEntry[] Entries =
        {
            new BuildCatalogEntry("VillageHome0", "Wohnhaus 1", Houses, BuildKind.Prefab, 6.4f, 6.4f, 5.5f, Wood),
            new BuildCatalogEntry("VillageHome1", "Wohnhaus 2", Houses, BuildKind.Prefab, 6.4f, 6.4f, 5.5f, Wood),
            new BuildCatalogEntry("VillageHome2", "Wohnhaus 3", Houses, BuildKind.Prefab, 6.4f, 6.4f, 5.5f, Wood),
            new BuildCatalogEntry("VillageHome3", "Wohnhaus 4", Houses, BuildKind.Prefab, 6.4f, 6.4f, 5.5f, Wood),
            new BuildCatalogEntry("VillageHome4", "Wohnhaus 5", Houses, BuildKind.Prefab, 6.4f, 6.4f, 5.5f, Wood),
            new BuildCatalogEntry("VillageHome5", "Wohnhaus 6", Houses, BuildKind.Prefab, 6.4f, 6.4f, 5.5f, Wood),
            new BuildCatalogEntry("VillageSupplyHall", "Vorratshalle", Houses, BuildKind.Prefab, 5.4f, 5f, 5.5f, Wood),
            new BuildCatalogEntry("VillageWatchtower", "Wachturm", Houses, BuildKind.Prefab, 4.1f, 4.1f, 9f, Wood),
            new BuildCatalogEntry("VillageMarketStall", "Marktstand", Houses, BuildKind.Prefab, 3.2f, 2f, 2.8f, Cloth),
            new BuildCatalogEntry("AshRuin", "Ruine", Houses, BuildKind.Prefab, 4f, 4f, 3f, Stone),
            new BuildCatalogEntry("AshMonument", "Denkmal", Houses, BuildKind.Prefab, 2f, 2f, 3.5f, Stone),

            new BuildCatalogEntry("AshStoneWall", "Steinmauer", Walls, BuildKind.Prefab, 4f, 0.8f, 2.4f, Stone),
            new BuildCatalogEntry("AshBrokenWall", "Zerfallene Mauer", Walls, BuildKind.Prefab, 4f, 0.8f, 1.8f, Stone),
            new BuildCatalogEntry("DungeonWall", "Gewölbewand", Walls, BuildKind.Prefab, 4f, 1f, 3f, Stone),
            new BuildCatalogEntry("DungeonArch", "Torbogen", Walls, BuildKind.Prefab, 4f, 1f, 3.5f, Stone),
            new BuildCatalogEntry("DungeonPillar", "Säule", Walls, BuildKind.Prefab, 1f, 1f, 3.5f, Stone, PrimitiveType.Cylinder),
            new BuildCatalogEntry("AshSteps", "Steinstufen", Walls, BuildKind.Prefab, 3f, 2f, 0.8f, Stone),

            new BuildCatalogEntry("AshCrate", "Kiste", Props, BuildKind.Prefab, 0.9f, 0.9f, 0.9f, Wood),
            new BuildCatalogEntry("AshLantern", "Laterne", Props, BuildKind.Prefab, 0.5f, 0.5f, 2.2f, new Color(0.85f, 0.62f, 0.25f), PrimitiveType.Cylinder),

            new BuildCatalogEntry("Npc0", "Dorfbewohner A", Residents, BuildKind.Prefab, 0.7f, 0.7f, 1.8f, new Color(0.62f, 0.46f, 0.32f), PrimitiveType.Capsule, "Villager0"),
            new BuildCatalogEntry("Npc1", "Dorfbewohner B", Residents, BuildKind.Prefab, 0.7f, 0.7f, 1.8f, new Color(0.50f, 0.36f, 0.30f), PrimitiveType.Capsule, "Villager1"),
            new BuildCatalogEntry("Npc2", "Dorfbewohner C", Residents, BuildKind.Prefab, 0.7f, 0.7f, 1.8f, new Color(0.40f, 0.44f, 0.34f), PrimitiveType.Capsule, "Villager2"),
            new BuildCatalogEntry("Merchant0", "Händler", Residents, BuildKind.Prefab, 0.8f, 0.7f, 1.84f, Cloth, PrimitiveType.Capsule),

            new BuildCatalogEntry("WoodlandTreeA", "Baum", Nature, BuildKind.Prefab, 3f, 3f, 7f, Leaf, PrimitiveType.Cylinder),
            new BuildCatalogEntry("WoodlandTreeB", "Baum (hoch)", Nature, BuildKind.Prefab, 3f, 3f, 8f, Leaf, PrimitiveType.Cylinder),
            new BuildCatalogEntry("WoodlandShrub", "Busch", Nature, BuildKind.Prefab, 1.6f, 1.6f, 1.4f, Leaf, PrimitiveType.Sphere),
            new BuildCatalogEntry("RoadsideShrub0", "Wegbusch", Nature, BuildKind.Prefab, 1.4f, 1.4f, 1.2f, Leaf, PrimitiveType.Sphere),
            new BuildCatalogEntry("RoadsideShrub1", "Wegbusch (dicht)", Nature, BuildKind.Prefab, 1.4f, 1.4f, 1.2f, Leaf, PrimitiveType.Sphere),
            new BuildCatalogEntry("WoodlandFern", "Farn", Nature, BuildKind.Prefab, 1f, 1f, 0.7f, Leaf, PrimitiveType.Sphere),
            new BuildCatalogEntry("AshGrass", "Grasbüschel", Nature, BuildKind.Prefab, 1f, 1f, 0.5f, new Color(0.40f, 0.46f, 0.26f), PrimitiveType.Sphere),
            new BuildCatalogEntry("WoodlandMushroomRed", "Pilz (rot)", Nature, BuildKind.Prefab, 0.5f, 0.5f, 0.5f, new Color(0.70f, 0.20f, 0.16f), PrimitiveType.Sphere),
            new BuildCatalogEntry("WoodlandMushroomMoon", "Mondpilz", Nature, BuildKind.Prefab, 0.5f, 0.5f, 0.5f, new Color(0.55f, 0.62f, 0.85f), PrimitiveType.Sphere),
            new BuildCatalogEntry("WoodlandRock", "Fels", Nature, BuildKind.Prefab, 1.6f, 1.6f, 1.1f, Stone, PrimitiveType.Sphere),
            new BuildCatalogEntry("AshRocks", "Steinhaufen", Nature, BuildKind.Prefab, 2f, 2f, 0.9f, Stone, PrimitiveType.Sphere),

            new BuildCatalogEntry("GrassVerge", "Rasen", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.30f, 0.42f, 0.22f)),
            new BuildCatalogEntry("VillageSoil", "Dorferde", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.36f, 0.29f, 0.21f)),
            new BuildCatalogEntry("OutdoorGround", "Waldboden", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.28f, 0.27f, 0.20f)),
            new BuildCatalogEntry("RoadStone", "Pflasterweg", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.45f, 0.44f, 0.42f)),
            new BuildCatalogEntry("CryptPaving", "Steinplatten", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.38f, 0.38f, 0.40f)),
            new BuildCatalogEntry("StoneMasonry", "Mauerwerk", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.40f, 0.37f, 0.34f)),
            new BuildCatalogEntry("MillBoard", "Holzbohlen", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.47f, 0.35f, 0.22f)),
            new BuildCatalogEntry("MillWater", "Wasser", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.18f, 0.30f, 0.38f)),
            new BuildCatalogEntry("BasaltGround", "Basalt", Grounds, BuildKind.Surface, 4f, 4f, 0.04f, new Color(0.16f, 0.15f, 0.16f)),
            new BuildCatalogEntry("AshStoneFloor", "Steinboden-Platte", Grounds, BuildKind.Prefab, 4f, 4f, 0.3f, Stone),
            new BuildCatalogEntry("DungeonFloor", "Gewölbeboden-Platte", Grounds, BuildKind.Prefab, 4f, 4f, 0.3f, Stone)
        };

        /// <summary>Keys the forest brush plants, by weight order: trees first, then undergrowth.</summary>
        public static readonly string[] ForestKeys = { "WoodlandTreeA", "WoodlandTreeB", "WoodlandTreeA", "WoodlandShrub", "WoodlandFern", "WoodlandRock" };

        public static IList<BuildCatalogEntry> All { get { return Array.AsReadOnly(Entries); } }
        public static IList<BuildCategory> Categories { get { return Array.AsReadOnly(CategoryList); } }

        public static BuildCatalogEntry Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (BuildCatalogEntry entry in Entries)
                if (string.Equals(entry.Key, key, StringComparison.Ordinal)) return entry;
            return null;
        }
    }

    /// <summary>A building site of the Bau-Modus: one fixed game area with its own layout file.</summary>
    public sealed class BuildArea
    {
        public readonly string Key, Label;
        /// <summary>Private world shell shown in the editor preview, or null when the area is assembled by code.</summary>
        public readonly string PreviewWorld;
        /// <summary>Half size of the buildable square around the area origin, in metres.</summary>
        public readonly float Extent;
        public BuildArea(string key, string label, string previewWorld, float extent)
        { Key = key; Label = label; PreviewWorld = previewWorld; Extent = extent; }
    }

    public static class BuildAreas
    {
        private static readonly BuildArea[] List =
        {
            new BuildArea("aschengrenze", "Die Aschengrenze (Dorf)", "EntryVillageWorld", 42f),
            new BuildArea("krypta", "Ashen Crypt (Krypta)", "EntryCryptWorld", 30f),
            new BuildArea("aschestrasse", "Die Aschestraße", null, 40f),
            new BuildArea("muehle", "Die alte Mühle", null, 40f),
            new BuildArea("speicher", "Der alte Speicher", null, 40f),
            new BuildArea("verborgene-krypta", "Die verborgene Krypta", null, 40f),
            new BuildArea("rattlernest", "Das Rattlernest", null, 40f),
            new BuildArea("aschenzoll", "Aschenzoll – Frachtplatz", null, 45f),
            new BuildArea("wald", "Der Wald nach Aschenzoll", null, 60f),
            new BuildArea("baerenhoehle", "Die Bärenhöhle", null, 40f)
        };

        public static IList<BuildArea> All { get { return Array.AsReadOnly(List); } }

        public static BuildArea Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (BuildArea area in List)
                if (string.Equals(area.Key, key, StringComparison.Ordinal)) return area;
            return null;
        }
    }

    /// <summary>One placed piece. Rotation is the Unity yaw in degrees (clockwise seen from above).</summary>
    [Serializable]
    public sealed class BuildPiece
    {
        public string id = "";
        public string key = "";
        public float x, z, rot;
        public float scale = 1f;
        /// <summary>Width/depth in metres; surfaces only (0 for prefabs).</summary>
        public float w, d;

        public BuildPiece Clone()
        {
            return new BuildPiece { id = id, key = key, x = x, z = z, rot = rot, scale = scale, w = w, d = d };
        }
    }

    /// <summary>Saved layout of one area: Assets/Resources/ARE/BuildLayouts/&lt;area&gt;.json.</summary>
    [Serializable]
    public sealed class BuildLayoutData
    {
        public int schema = BuildLayoutRules.Schema;
        public string area = "";
        public int revision;
        public List<BuildPiece> pieces = new List<BuildPiece>();

        public BuildLayoutData Clone()
        {
            var copy = new BuildLayoutData { schema = schema, area = area, revision = revision };
            foreach (BuildPiece piece in pieces) copy.pieces.Add(piece.Clone());
            return copy;
        }

        public BuildPiece Find(string id)
        {
            foreach (BuildPiece piece in pieces)
                if (piece != null && string.Equals(piece.id, id, StringComparison.Ordinal)) return piece;
            return null;
        }
    }

    /// <summary>
    /// Pure layout rules shared by the editor bridge, the runtime loader and the tests. Deterministic: no
    /// UnityEngine.Random, no clock; the forest brush uses its own integer generator.
    /// </summary>
    public static class BuildLayoutRules
    {
        public const int Schema = 1;
        public const int MaxPieces = 1500;
        public const float MinScale = 0.3f, MaxScale = 3f, MinSurface = 0.5f, MaxSurface = 80f;
        public const int MaxForestCount = 60;
        public const float MaxForestRadius = 25f;

        public static BuildLayoutData Empty(string area)
        {
            return new BuildLayoutData { area = area, revision = 0 };
        }

        /// <summary>Parse and validate; throws FormatException with a German message on any defect.</summary>
        public static BuildLayoutData Parse(string json, string expectedArea)
        {
            if (string.IsNullOrWhiteSpace(json)) return Empty(expectedArea);
            BuildLayoutData data;
            try { data = JsonUtility.FromJson<BuildLayoutData>(json); }
            catch (ArgumentException e) { throw new FormatException("Bauplan ist kein gültiges JSON: " + e.Message); }
            if (data == null) throw new FormatException("Bauplan ist leer.");
            if (data.pieces == null) data.pieces = new List<BuildPiece>();
            string error = Validate(data, expectedArea);
            if (error != null) throw new FormatException(error);
            return data;
        }

        public static string ToJson(BuildLayoutData data)
        {
            return JsonUtility.ToJson(data, true) + "\n";
        }

        /// <summary>Null when the layout is valid, else a German explanation.</summary>
        public static string Validate(BuildLayoutData data, string expectedArea)
        {
            if (data == null) return "Bauplan fehlt.";
            if (data.schema != Schema) return "Unbekannte Bauplan-Version " + data.schema + ".";
            BuildArea area = BuildAreas.Find(data.area);
            if (area == null) return "Unbekanntes Baugebiet '" + data.area + "'.";
            if (expectedArea != null && data.area != expectedArea) return "Bauplan gehört zu '" + data.area + "', nicht zu '" + expectedArea + "'.";
            if (data.revision < 0) return "Ungültige Revision.";
            if (data.pieces == null) return "Teileliste fehlt.";
            if (data.pieces.Count > MaxPieces) return "Zu viele Teile (höchstens " + MaxPieces + ").";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuildPiece piece in data.pieces)
            {
                string pieceError = ValidatePiece(piece, area);
                if (pieceError != null) return pieceError;
                if (!ids.Add(piece.id)) return "Doppelte Teil-ID '" + piece.id + "'.";
            }
            return null;
        }

        public static string ValidatePiece(BuildPiece piece, BuildArea area)
        {
            if (piece == null) return "Leeres Teil im Bauplan.";
            if (!IsId(piece.id)) return "Ungültige Teil-ID '" + piece.id + "'.";
            BuildCatalogEntry entry = BuildCatalog.Find(piece.key);
            if (entry == null) return "Unbekannter Baustein '" + piece.key + "'.";
            if (!Finite(piece.x) || !Finite(piece.z) || !Finite(piece.rot) || !Finite(piece.scale) || !Finite(piece.w) || !Finite(piece.d))
                return "Ungültige Zahl bei '" + piece.id + "'.";
            if (Mathf.Abs(piece.x) > area.Extent || Mathf.Abs(piece.z) > area.Extent)
                return "'" + entry.Label + "' liegt außerhalb des Baugebiets (±" + area.Extent.ToString("0", CultureInfo.InvariantCulture) + " m).";
            if (piece.rot < 0f || piece.rot >= 360f) return "Drehung muss zwischen 0 und 360 Grad liegen.";
            if (piece.scale < MinScale || piece.scale > MaxScale) return "Größe muss zwischen 30 % und 300 % liegen.";
            if (entry.Kind == BuildKind.Surface)
            {
                if (piece.w < MinSurface || piece.w > MaxSurface || piece.d < MinSurface || piece.d > MaxSurface)
                    return "Fläche muss zwischen 0,5 und 80 m groß sein.";
            }
            else if (piece.w != 0f || piece.d != 0f) return "Nur Untergründe haben eine Flächengröße.";
            return null;
        }

        public static bool IsId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 12 || id[0] != 'b') return false;
            for (int i = 1; i < id.Length; i++) if (id[i] < '0' || id[i] > '9') return false;
            return id.Length > 1;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        /// <summary>Wrap any yaw into [0, 360) and round to a tenth of a degree.</summary>
        public static float NormalizeRotation(float degrees)
        {
            if (!Finite(degrees)) return 0f;
            float r = Mathf.Round(degrees * 10f) / 10f % 360f;
            if (r < 0f) r += 360f;
            return r >= 360f ? 0f : r;
        }

        /// <summary>Round positions to centimetres so saved plans stay readable and diff-friendly.</summary>
        public static float RoundCm(float value) { return Mathf.Round(value * 100f) / 100f; }

        public static string NextId(BuildLayoutData data)
        {
            int max = 0;
            foreach (BuildPiece piece in data.pieces)
            {
                int n;
                if (piece != null && IsId(piece.id) && int.TryParse(piece.id.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out n) && n > max)
                    max = n;
            }
            return "b" + (max + 1).ToString("0000", CultureInfo.InvariantCulture);
        }

        /// <summary>Normalise a requested piece (rounding, defaults for surfaces) and validate it against the area.</summary>
        public static BuildPiece Prepare(BuildArea area, string id, string key, float x, float z, float rot, float scale, float w, float d, out string error)
        {
            BuildCatalogEntry entry = BuildCatalog.Find(key);
            error = null;
            if (entry == null) { error = "Unbekannter Baustein '" + key + "'."; return null; }
            var piece = new BuildPiece
            {
                id = id, key = key, x = RoundCm(x), z = RoundCm(z), rot = NormalizeRotation(rot),
                scale = Mathf.Approximately(scale, 0f) ? 1f : Mathf.Round(scale * 100f) / 100f
            };
            if (entry.Kind == BuildKind.Surface)
            {
                piece.w = RoundCm(w <= 0f ? entry.Width : w);
                piece.d = RoundCm(d <= 0f ? entry.Depth : d);
            }
            error = ValidatePiece(piece, area);
            return error == null ? piece : null;
        }

        /// <summary>
        /// Forest brush: deterministic positions inside a circle (golden-angle spiral with seeded jitter),
        /// identical in the browser preview and in Unity.
        /// </summary>
        public static List<BuildPiece> Forest(float cx, float cz, float radius, int count, uint seed)
        {
            var result = new List<BuildPiece>();
            count = Mathf.Clamp(count, 1, MaxForestCount);
            radius = Mathf.Clamp(radius, 1f, MaxForestRadius);
            uint state = seed == 0 ? 0x9E3779B9u : seed;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                float r = radius * Mathf.Sqrt(t);
                float angle = i * 2.39996323f + Unit(ref state) * 0.6f;
                float jitter = 1f + (Unit(ref state) - 0.5f) * 0.3f;
                string key = BuildCatalog.ForestKeys[(int)(Next(ref state) % (uint)BuildCatalog.ForestKeys.Length)];
                float rot = Mathf.Floor(Unit(ref state) * 24f) * 15f;
                float scale = 0.85f + Mathf.Floor(Unit(ref state) * 7f) * 0.05f;
                result.Add(new BuildPiece
                {
                    key = key, x = RoundCm(cx + Mathf.Cos(angle) * r * jitter), z = RoundCm(cz + Mathf.Sin(angle) * r * jitter),
                    rot = rot, scale = scale
                });
            }
            return result;
        }

        /// <summary>xorshift32; the browser preview implements the same sequence.</summary>
        private static uint Next(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return state;
        }

        private static float Unit(ref uint state) { return (Next(ref state) & 0xFFFFFF) / 16777216f; }
    }
}
