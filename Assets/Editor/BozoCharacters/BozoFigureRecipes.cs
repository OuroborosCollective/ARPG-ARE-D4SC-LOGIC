using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Recipes for every human figure of the game. Owner rule 2026-11-19: all NPCs, merchants and human figures are
    /// built from the purchased pack "BoZo: Stylized Modular Characters - Fantasy Pack" (one unified character system).
    /// This file is plain data handling and compiles without the pack; the baker needs the pack (ARE_BOZO_CHARACTERS).
    /// </summary>
    public static class BozoFigureRecipes
    {
        public const string Schema = "ashen.bozo-figures.v1";
        public const string RecipePath = "Assets/Editor/BozoCharacters/figures.json";
        /// <summary>Game visuals that must always come from a recipe (Resources/ARE/Visuals/&lt;key&gt;.prefab).</summary>
        public static readonly string[] RequiredKeys = { "EntryNpc0", "EntryNpc1", "EntryNpc2", "Npc0", "Npc1", "Npc2", "RoadHunter", "Hero" };
        /// <summary>
        /// Private source figures for the existing authoring tools (hero builder, resting mill patients). They live
        /// outside Resources so they never enter a build on their own.
        /// </summary>
        public static readonly string[] RequiredSources = { "HeroSource", "PatientSource0", "PatientSource1", "PatientSource2" };
        /// <summary>
        /// Game visuals other tools build from a source figure: the hero builder adds weapon socket and combat
        /// controller, the mill patients are laid down and covered. A recipe key must never replace them.
        /// </summary>
        public static readonly string[] BuiltByOtherTools = { "EntryCombatHero", "EntryHero", "MillPatient0", "MillPatient1", "MillPatient2" };

        [Serializable] public sealed class Shape { public string id = ""; public float value; }
        [Serializable] public sealed class Outfit { public string slot = ""; public string id = ""; public string[] colors = new string[0]; }
        [Serializable]
        public sealed class Figure
        {
            public string id = "";
            public string role = "";
            /// <summary>Vendor preset the figure starts from (CustomCharacters/Resources/&lt;base&gt;.asset).</summary>
            public string @base = "";
            /// <summary>Total visible height in metres of the finished prefab.</summary>
            public float height = 1.9f;
            /// <summary>Edge length of the merged texture atlas: 512, 1024, 2048.</summary>
            public int texture = 1024;
            /// <summary>"m" or "f": which idle/walk set the figure uses.</summary>
            public string motion = "m";
            /// <summary>Game visuals written to Resources/ARE/Visuals.</summary>
            public string[] keys = new string[0];
            /// <summary>Private source prefabs for other authoring tools (not in Resources).</summary>
            public string[] sources = new string[0];
            public Shape[] body = new Shape[0];
            public Shape[] face = new Shape[0];
            public string[] remove = new string[0];
            public Outfit[] outfits = new Outfit[0];
            public string[] skin = new string[0];
            /// <summary>
            /// Id of another figure with the same preset, the same parts and the same body and face shapes. This
            /// figure differs from it in colours only and uses its mesh: a second colour of a figure costs one small
            /// texture and no second model.
            /// </summary>
            public string sameShapeAs = "";
            /// <summary>
            /// Built as two parts, body and head (see FigureComposer), so that the head of another figure can be put
            /// on this body and this head on other bodies. The visual then has two renderers on one skeleton.
            /// </summary>
            public bool parts;
        }

        // The pack's outfit types (Assets/BoZo_StylizedModularCharacters/OutfitTypes): what belongs to the head when a
        // figure is built in two parts. A type in neither list is refused, so that a new type of the pack is sorted
        // deliberately instead of landing on the wrong side of the neck.
        static readonly string[] HeadSlots = { "Ears", "EyeBrows", "EyeLashes", "EyeShine", "Eyes", "FaceDetails", "FaceTexture", "HairBack", "HairFront",
            "Hat", "Head", "HeadAcc", "HeadDetail", "HeadType", "Iris", "LowerFace", "MakeUpCheeks", "MakeUpEyes", "MakeUpLips", "Pupil", "Teeth", "UpperFace" };
        static readonly string[] BodySlots = { "Body", "BodyType", "Bottom", "Feet", "Gloves", "Leggings", "Neck", "Overall", "Shoulders", "Socks", "Top",
            "UnderLower", "UnderUpper", "Waist" };

        /// <summary>True for an outfit type that belongs to the head, false for the body, null for a type nobody sorted.</summary>
        public static bool? IsHeadSlot(string type)
        {
            if (Array.IndexOf(HeadSlots, type) >= 0) return true;
            if (Array.IndexOf(BodySlots, type) >= 0) return false;
            return null;
        }
        [Serializable] sealed class File { public string schema = ""; public string pack = ""; public float light; public Figure[] figures = new Figure[0]; }

        static readonly Regex Id = new Regex("^[a-z][a-z0-9_]{1,30}$");
        static readonly Regex Key = new Regex("^[A-Z][A-Za-z0-9]{2,40}$");
        static readonly Regex Part = new Regex("^[A-Za-z][A-Za-z0-9_]{1,60}$");
        static readonly Regex Preset = new Regex("^[A-Za-z][A-Za-z0-9 _/]{1,60}$");
        static readonly Regex Hex = new Regex("^#[0-9a-fA-F]{6}$");

        /// <summary>Private folder of the baked figures: &lt;BakeFolder&gt;/&lt;id&gt;/&lt;id&gt;.prefab. Never committed.</summary>
        public const string BakeFolder = "Assets/PrivateArt/BozoCharacters";
        public static string BakedPath(string id) { return BakeFolder + "/" + id + "/" + id + ".prefab"; }

        /// <summary>The figures of figures.json and of the figure pool (BozoFigurePool), checked together.</summary>
        public static Figure[] Load()
        {
            Figure[] figures = Parse(System.IO.File.ReadAllText(RecipePath));
            Figure[] pool = BozoFigurePool.Load();
            if (pool.Length == 0) return figures;
            Figure[] all = figures.Concat(pool).ToArray();
            string error = Validate(all);
            if (error != null) throw new FormatException(error);
            return all;
        }

        /// <summary>
        /// Brightening of the figures' colour atlas for the game ("light" at the top of the recipe file): 1 keeps the
        /// baked colours, smaller values lift dark tones more than light ones (brightness to the power of this value).
        /// The pack multiplies every colour with its painted shading, so baked figures come out darker than the
        /// world around them.
        /// </summary>
        public static float LoadLight()
        {
            File file = JsonUtility.FromJson<File>(System.IO.File.ReadAllText(RecipePath));
            return file == null ? 1f : LightOf(file);
        }

        static float LightOf(File file) { return file.light <= 0f ? 1f : file.light; }

        /// <summary>
        /// True when this game visual belongs to a recipe whose figure is baked in this workspace. Older art builders
        /// then leave the key alone, so a unified figure is never replaced by a previous look.
        /// </summary>
        public static bool Owns(string key)
        {
            if (!System.IO.File.Exists(RecipePath)) return false;
            foreach (Figure figure in Load())
                if (Array.IndexOf(figure.keys, key) >= 0) return System.IO.File.Exists(BakedPath(figure.id));
            return false;
        }

        /// <summary>Parse and validate; throws FormatException with the first defect.</summary>
        public static Figure[] Parse(string json)
        {
            File file;
            try { file = JsonUtility.FromJson<File>(json); }
            catch (ArgumentException e) { throw new FormatException("figures.json is not valid JSON: " + e.Message); }
            if (file == null || file.schema != Schema) throw new FormatException("Unknown figure recipe schema.");
            // Written so that "not a number" fails too; only a missing entry (0) means "keep the baked colours".
            if (!(file.light >= 0f) || !(LightOf(file) >= .4f && LightOf(file) <= 1f))
                throw new FormatException("light must be 0.4–1 (1 keeps the baked colours).");
            string error = Validate(file.figures);
            if (error != null) throw new FormatException(error);
            return file.figures;
        }

        /// <summary>What two recipes differ in apart from colours, role, height and names; null when they share a shape.</summary>
        public static string ShapeDifference(Figure a, Figure b)
        {
            if (a.@base != b.@base) return "base";
            Func<Shape[], string> shapes = list => string.Join(",", (list ?? new Shape[0]).Where(s => s != null)
                .OrderBy(s => s.id, StringComparer.Ordinal).Select(s => s.id + "=" + s.value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            if (shapes(a.body) != shapes(b.body)) return "body";
            if (shapes(a.face) != shapes(b.face)) return "face";
            Func<string[], string> slots = list => string.Join(",", (list ?? new string[0]).OrderBy(s => s, StringComparer.Ordinal));
            if (slots(a.remove) != slots(b.remove)) return "remove";
            if (a.parts != b.parts) return "parts";
            Func<Outfit[], string> parts = list => string.Join(",", (list ?? new Outfit[0]).Where(o => o != null)
                .OrderBy(o => o.slot, StringComparer.Ordinal).Select(o => o.slot + "=" + o.id));
            if (parts(a.outfits) != parts(b.outfits)) return "outfits";
            return null;
        }

        /// <summary>Null when valid, else the first defect.</summary>
        public static string Validate(Figure[] figures)
        {
            if (figures == null || figures.Length == 0) return "No figures.";
            var ids = new HashSet<string>();
            var keys = new HashSet<string>();
            foreach (Figure f in figures)
            {
                if (f == null || !Id.IsMatch(f.id ?? "")) return "Invalid figure id.";
                if (!ids.Add(f.id)) return "Duplicate figure " + f.id + ".";
                // Each figure owns the folder <BakeFolder>/<id>; these two names are shared folders there.
                if (f.id == "sources" || f.id == "animation") return "Figure id " + f.id + " is reserved.";
                if (string.IsNullOrWhiteSpace(f.role)) return f.id + ": role missing (figures are designed for their role).";
                if (!Preset.IsMatch(f.@base ?? "") || f.@base.Contains("..")) return f.id + ": invalid base preset.";
                if (!(f.height >= 1.2f && f.height <= 2.3f)) return f.id + ": height must be 1.2–2.3 m."; // also rejects "not a number"
                if (f.texture != 512 && f.texture != 1024 && f.texture != 2048) return f.id + ": texture must be 512, 1024 or 2048.";
                if (f.motion != "m" && f.motion != "f") return f.id + ": motion must be m or f.";
                if ((f.keys == null || f.keys.Length == 0) && (f.sources == null || f.sources.Length == 0)) return f.id + ": no visual key.";
                foreach (string key in f.keys ?? new string[0])
                    if (Array.IndexOf(BuiltByOtherTools, key) >= 0)
                        return f.id + ": visual " + key + " is built by another tool from a source figure; list the figure under sources instead.";
                foreach (string key in Both(f.keys, f.sources))
                {
                    if (!Key.IsMatch(key ?? "")) return f.id + ": invalid visual key '" + key + "'.";
                    if (!keys.Add(key)) return "Visual key " + key + " is produced twice.";
                }
                foreach (Shape s in Concat(f.body, f.face))
                    if (s == null || !Part.IsMatch(s.id ?? "") || float.IsNaN(s.value) || s.value < 0f || s.value > 100f)
                        return f.id + ": shape values must be 0–100.";
                foreach (string slot in f.remove ?? new string[0])
                    if (!Part.IsMatch(slot ?? "")) return f.id + ": invalid slot to remove.";
                var slots = new HashSet<string>();
                foreach (Outfit o in f.outfits ?? new Outfit[0])
                {
                    if (o == null || !Part.IsMatch(o.slot ?? "") || !Part.IsMatch(o.id ?? "")) return f.id + ": invalid outfit entry.";
                    if (!slots.Add(o.slot)) return f.id + ": slot " + o.slot + " is set twice.";
                    if (o.colors != null && o.colors.Length > 9) return f.id + ": at most nine colours per outfit.";
                    bool ended = false;
                    foreach (string c in o.colors ?? new string[0])
                    {
                        if (c != "" && !Hex.IsMatch(c ?? "")) return f.id + ": colour '" + c + "' is not #rrggbb.";
                        if (c == "") ended = true;
                        else if (ended) return f.id + ": colour after an empty entry.";
                    }
                }
                foreach (string c in f.skin ?? new string[0])
                    if (c != "" && !Hex.IsMatch(c ?? "")) return f.id + ": skin colour '" + c + "' is not #rrggbb.";
            }
            foreach (Figure f in figures)
            {
                if (string.IsNullOrEmpty(f.sameShapeAs)) continue;
                Figure model = Array.Find(figures, other => other.id == f.sameShapeAs);
                if (model == null || model == f) return f.id + ": sameShapeAs names no other figure.";
                if (!string.IsNullOrEmpty(model.sameShapeAs)) return f.id + ": sameShapeAs must name a figure with a mesh of its own.";
                string difference = ShapeDifference(f, model);
                if (difference != null) return f.id + ": differs from " + model.id + " in more than colours (" + difference + ").";
            }
            var produced = new HashSet<string>();
            var sources = new HashSet<string>();
            foreach (Figure f in figures)
            {
                foreach (string key in f.keys ?? new string[0]) produced.Add(key);
                foreach (string key in f.sources ?? new string[0]) sources.Add(key);
            }
            foreach (string required in RequiredKeys)
                if (!produced.Contains(required)) return "No recipe produces the required figure " + required + ".";
            foreach (string required in RequiredSources)
                if (!sources.Contains(required)) return "No recipe produces the required source figure " + required + ".";
            return null;
        }

        static IEnumerable<string> Both(string[] a, string[] b)
        {
            foreach (string s in a ?? new string[0]) yield return s;
            foreach (string s in b ?? new string[0]) yield return s;
        }

        static IEnumerable<Shape> Concat(Shape[] a, Shape[] b)
        {
            foreach (Shape s in a ?? new Shape[0]) yield return s;
            foreach (Shape s in b ?? new Shape[0]) yield return s;
        }

        /// <summary>"#rrggbb" to colour; an empty entry returns false; the baker stops overrides there and keeps all remaining part colours.</summary>
        public static bool TryColor(string hex, out Color color)
        {
            color = Color.white;
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out color);
        }
    }
}
