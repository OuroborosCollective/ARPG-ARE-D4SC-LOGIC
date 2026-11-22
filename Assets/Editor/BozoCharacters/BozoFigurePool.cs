using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// The figure pool (owner direction 2026-11-22): about two hundred ready-made people for residents and roles, from
    /// the village worker to the mountain guard in full plate. pool.json describes each kind of person once (preset,
    /// parts, body and face) and a few looks of it (skin, hair, eyes, a palette for the clothes); this class turns
    /// them into ordinary figure recipes. The first look of a kind is its model, every other look shares the model's
    /// mesh and only brings its own colours. All pool figures are built in two parts, so heads can be swapped between
    /// figures of the same preset and skin (FigureComposer) when more people are needed than the pool holds.
    /// Plain data handling: compiles without the pack.
    /// </summary>
    public static class BozoFigurePool
    {
        public const string Schema = "ashen.bozo-pool.v1";
        public const string PoolPath = "Assets/Editor/BozoCharacters/pool.json";
        /// <summary>Written next to the game visuals after a bake: one line per baked pool figure, read by the game.</summary>
        public const string IndexPath = "Assets/Resources/ARE/Visuals/FolkIndex.txt";
        public const string IdPrefix = "folk_";
        public const string KeyPrefix = "Folk";

        [Serializable] public sealed class Base { public string id = ""; public string motion = ""; public BozoFigureRecipes.Shape[] face = new BozoFigureRecipes.Shape[0]; public string[] remove = new string[0]; }
        [Serializable] public sealed class Named { public string id = ""; public string[] hex = new string[0]; }
        [Serializable] public sealed class Tone { public string id = ""; public string[] head = new string[0]; public string[] body = new string[0]; }
        [Serializable] public sealed class PartRoles { public string slot = ""; public string id = ""; public string[] roles = new string[0]; }
        [Serializable] public sealed class Look { public string tone = ""; public string hair = ""; public string palette = ""; public string eyes = ""; }
        [Serializable]
        public sealed class Kind
        {
            public string id = "";
            /// <summary>What the game uses the kind for: "arbeit" (village workers), "handwerk", "wache", "krieger".</summary>
            public string group = "";
            public string role = "";
            public string @base = "";
            public float height = 1.9f;
            public string head = "";
            public string bodyPart = "";
            /// <summary>Every worn part apart from head, body, eyes and brows: clothes, hat, hair, beard.</summary>
            public string[] wear = new string[0];
            public BozoFigureRecipes.Shape[] body = new BozoFigureRecipes.Shape[0];
            public BozoFigureRecipes.Shape[] face = new BozoFigureRecipes.Shape[0];
            public Look[] looks = new Look[0];
        }
        [Serializable]
        public sealed class Pool
        {
            public string schema = "";
            public int texture = 512;
            public string eyesPart = "";
            public string browsPart = "";
            /// <summary>Slots a figure loses when it wears nothing there, so no part of the preset stays behind.</summary>
            public string[] wearable = new string[0];
            public string[] groups = new string[0];
            public Base[] bases = new Base[0];
            /// <summary>Colour roles of the clothes; every palette gives one colour per role, in this order.</summary>
            public string[] roles = new string[0];
            public Named[] palettes = new Named[0];
            public Tone[] tones = new Tone[0];
            /// <summary>Base, light and dark shade of a hair colour (roles hair, hair_light, hair_dark).</summary>
            public Named[] hair = new Named[0];
            public Named[] eyes = new Named[0];
            public PartRoles[] parts = new PartRoles[0];
            public Kind[] kinds = new Kind[0];
        }

        static readonly string[] HairRoles = { "hair", "hair_light", "hair_dark" };
        static readonly Regex KindId = new Regex("^[a-z]{2,20}$");
        static readonly Regex Name = new Regex("^[a-z][a-z0-9_]{0,19}$"); // at most 20 characters, as FolkFigures reads them
        static readonly Regex Part = new Regex("^[A-Za-z][A-Za-z0-9_]{1,60}$");
        static readonly Regex Hex = new Regex("^#[0-9a-fA-F]{6}$");
        const int MaxLooks = 9;

        public static string FigureId(string kind, int look) { return IdPrefix + kind + "_" + look.ToString(CultureInfo.InvariantCulture); }
        public static string Key(string kind, int look)
        {
            return KeyPrefix + char.ToUpperInvariant(kind[0]) + kind.Substring(1) + look.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The pool's figures, or none when this workspace has no pool file.</summary>
        public static BozoFigureRecipes.Figure[] Load()
        {
            return File.Exists(PoolPath) ? Expand(File.ReadAllText(PoolPath)) : new BozoFigureRecipes.Figure[0];
        }

        public static Pool Read(string json)
        {
            Pool pool;
            try { pool = JsonUtility.FromJson<Pool>(json); }
            catch (ArgumentException e) { throw new FormatException("pool.json is not valid JSON: " + e.Message); }
            if (pool == null || pool.schema != Schema) throw new FormatException("Unknown figure pool schema.");
            string error = Validate(pool);
            if (error != null) throw new FormatException("pool.json: " + error);
            return pool;
        }

        /// <summary>Parse, check and turn the pool into figure recipes; throws FormatException with the first defect.</summary>
        public static BozoFigureRecipes.Figure[] Expand(string json)
        {
            Pool pool = Read(json);
            var figures = new List<BozoFigureRecipes.Figure>();
            foreach (Kind kind in pool.kinds)
            {
                Base preset = pool.bases.First(b => b.id == kind.@base);
                var ownFace = kind.face ?? new BozoFigureRecipes.Shape[0];
                var worn = (kind.wear ?? new string[0]).Select(id => pool.parts.First(p => p.id == id)).ToArray();
                var remove = pool.wearable.Where(slot => worn.All(p => p.slot != slot)).Concat(preset.remove ?? new string[0]).ToArray();
                var face = ownFace.Concat((preset.face ?? new BozoFigureRecipes.Shape[0]).Where(extra => ownFace.All(own => own.id != extra.id))).ToArray();
                for (int n = 0; n < kind.looks.Length; n++)
                {
                    Look look = kind.looks[n];
                    Tone tone = pool.tones.First(t => t.id == look.tone);
                    string[] hair = pool.hair.First(h => h.id == look.hair).hex;
                    Named palette = pool.palettes.First(p => p.id == look.palette);
                    var outfits = new List<BozoFigureRecipes.Outfit>
                    {
                        Outfit("Head", kind.head, tone.head),
                        Outfit("Body", kind.bodyPart, tone.body),
                        Outfit("Eyes", pool.eyesPart, pool.eyes.First(e => e.id == look.eyes).hex),
                        Outfit("EyeBrows", pool.browsPart, new[] { hair[0] }),
                    };
                    foreach (PartRoles part in worn)
                        outfits.Add(Outfit(part.slot, part.id, part.roles.Select(role => Colour(pool, role, palette, hair)).ToArray()));
                    figures.Add(new BozoFigureRecipes.Figure
                    {
                        id = FigureId(kind.id, n),
                        role = kind.role + " (Aussehen " + n + ": Haut " + look.tone + ", Haar " + look.hair + ", Farben " + look.palette + ", Augen " + look.eyes + ")",
                        @base = kind.@base,
                        height = kind.height,
                        texture = pool.texture,
                        motion = preset.motion,
                        keys = new[] { Key(kind.id, n) },
                        sources = new string[0],
                        body = (kind.body ?? new BozoFigureRecipes.Shape[0]).ToArray(),
                        face = face,
                        remove = remove,
                        outfits = outfits.ToArray(),
                        skin = new string[0],
                        sameShapeAs = n == 0 ? "" : FigureId(kind.id, 0),
                        parts = true,
                    });
                }
            }
            return figures.ToArray();
        }

        static BozoFigureRecipes.Outfit Outfit(string slot, string id, string[] colours)
        {
            return new BozoFigureRecipes.Outfit { slot = slot, id = id, colors = colours.ToArray() };
        }

        static string Colour(Pool pool, string role, Named palette, string[] hair)
        {
            int shade = Array.IndexOf(HairRoles, role);
            if (shade >= 0) return hair[shade];
            if (Hex.IsMatch(role)) return role;
            return palette.hex[Array.IndexOf(pool.roles, role)];
        }

        /// <summary>Null when the pool is consistent, else the first defect.</summary>
        public static string Validate(Pool pool)
        {
            if (pool.texture != 512 && pool.texture != 1024) return "texture must be 512 or 1024.";
            if (!Part.IsMatch(pool.eyesPart ?? "") || !Part.IsMatch(pool.browsPart ?? "")) return "eyesPart and browsPart must name parts.";
            string error = Names("wearable slot", pool.wearable, Part) ?? Names("group", pool.groups, Name) ?? Names("role", pool.roles, Name);
            if (error != null) return error;
            foreach (string slot in pool.wearable)
                if (BozoFigureRecipes.IsHeadSlot(slot) == null) return "wearable slot " + slot + " is no outfit type of the pack.";
            foreach (string role in pool.roles)
                if (Array.IndexOf(HairRoles, role) >= 0) return "role " + role + " is reserved for the hair colour.";
            if (pool.bases == null || pool.bases.Length == 0) return "no bases.";
            if (pool.bases.Select(b => b?.id).Distinct().Count() != pool.bases.Length) return "a base is listed twice.";
            foreach (Base b in pool.bases)
            {
                if (b == null || string.IsNullOrEmpty(b.id)) return "base without id.";
                if (b.motion != "m" && b.motion != "f") return "base " + b.id + ": motion must be m or f.";
                error = Shapes("base " + b.id, b.face) ?? Names("base " + b.id + " slot", b.remove, Part, true);
                if (error != null) return error;
            }
            error = Colours("palette", pool.palettes, pool.roles.Length, pool.roles.Length) ?? Colours("hair", pool.hair, 3, 3) ?? Colours("eyes", pool.eyes, 1, 9);
            if (error != null) return error;
            if (pool.tones == null || pool.tones.Length == 0) return "no skin tones.";
            if (pool.tones.Select(t => t?.id).Distinct().Count() != pool.tones.Length) return "a skin tone is listed twice.";
            foreach (Tone t in pool.tones)
            {
                if (t == null || !Name.IsMatch(t.id ?? "")) return "invalid skin tone id.";
                if (t.head == null || t.head.Length == 0 || t.head.Length > 9 || t.body == null || t.body.Length == 0 || t.body.Length > 9)
                    return "skin tone " + t.id + ": one to nine colours for head and body.";
                foreach (string c in t.head.Concat(t.body)) if (!Hex.IsMatch(c ?? "")) return "skin tone " + t.id + ": '" + c + "' is not #rrggbb.";
            }
            if (pool.parts == null || pool.parts.Length == 0) return "no parts.";
            var partIds = new HashSet<string>();
            foreach (PartRoles p in pool.parts)
            {
                if (p == null || !Part.IsMatch(p.id ?? "") || !Part.IsMatch(p.slot ?? "")) return "invalid part entry.";
                if (!partIds.Add(p.id)) return "part " + p.id + " is listed twice.";
                if (Array.IndexOf(pool.wearable, p.slot) < 0) return "part " + p.id + ": slot " + p.slot + " is not wearable.";
                if (p.roles == null || p.roles.Length == 0 || p.roles.Length > 9) return "part " + p.id + ": one to nine colour roles.";
                foreach (string role in p.roles)
                    if (Array.IndexOf(HairRoles, role) < 0 && Array.IndexOf(pool.roles, role) < 0 && !Hex.IsMatch(role ?? ""))
                        return "part " + p.id + ": unknown colour role '" + role + "'.";
            }
            if (pool.kinds == null || pool.kinds.Length == 0) return "no kinds of people.";
            var kindIds = new HashSet<string>();
            foreach (Kind k in pool.kinds)
            {
                if (k == null || !KindId.IsMatch(k.id ?? "")) return "kind ids are 2–20 lowercase letters.";
                if (!kindIds.Add(k.id)) return "kind " + k.id + " is listed twice.";
                if (Array.IndexOf(pool.groups, k.group) < 0) return k.id + ": unknown group '" + k.group + "'.";
                if (string.IsNullOrWhiteSpace(k.role)) return k.id + ": role missing.";
                if (pool.bases.All(b => b.id != k.@base)) return k.id + ": unknown base '" + k.@base + "'.";
                if (!Part.IsMatch(k.head ?? "") || !Part.IsMatch(k.bodyPart ?? "")) return k.id + ": head and bodyPart must name parts.";
                var slots = new HashSet<string>();
                foreach (string id in k.wear ?? new string[0])
                {
                    PartRoles part = pool.parts.FirstOrDefault(p => p.id == id);
                    if (part == null) return k.id + ": part " + id + " has no colour roles in the pool.";
                    if (!slots.Add(part.slot)) return k.id + ": slot " + part.slot + " is worn twice.";
                }
                foreach (string slot in new[] { "Top", "Bottom", "Feet" })
                    if (!slots.Contains(slot)) return k.id + ": nobody walks around without " + slot + ".";
                error = Shapes(k.id, k.body) ?? Shapes(k.id, k.face);
                if (error != null) return error;
                if (k.looks == null || k.looks.Length == 0 || k.looks.Length > MaxLooks) return k.id + ": one to " + MaxLooks + " looks.";
                var seen = new HashSet<string>();
                var faces = new HashSet<string>();
                foreach (Look look in k.looks)
                {
                    if (look == null) return k.id + ": empty look.";
                    if (pool.tones.All(t => t.id != look.tone)) return k.id + ": unknown skin tone '" + look.tone + "'.";
                    if (pool.hair.All(h => h.id != look.hair)) return k.id + ": unknown hair colour '" + look.hair + "'.";
                    if (pool.palettes.All(p => p.id != look.palette)) return k.id + ": unknown palette '" + look.palette + "'.";
                    if (pool.eyes.All(e => e.id != look.eyes)) return k.id + ": unknown eye colour '" + look.eyes + "'.";
                    if (!seen.Add(look.tone + "/" + look.hair + "/" + look.palette + "/" + look.eyes)) return k.id + ": two looks are the same.";
                    // Heads are swapped between figures: two looks with one skin, hair and eye colour would give two keys
                    // for the same head.
                    if (!faces.Add(look.tone + "/" + look.hair + "/" + look.eyes)) return k.id + ": two looks share skin, hair and eyes, so their heads look alike.";
                }
            }
            return null;
        }

        static string Names(string what, string[] names, Regex pattern, bool mayBeEmpty = false)
        {
            if (names == null || (!mayBeEmpty && names.Length == 0)) return mayBeEmpty ? null : "no " + what + "s.";
            var seen = new HashSet<string>();
            foreach (string name in names)
            {
                if (!pattern.IsMatch(name ?? "")) return "invalid " + what + " '" + name + "'.";
                if (!seen.Add(name)) return what + " " + name + " is listed twice.";
            }
            return null;
        }

        static string Shapes(string owner, BozoFigureRecipes.Shape[] shapes)
        {
            var seen = new HashSet<string>();
            foreach (BozoFigureRecipes.Shape s in shapes ?? new BozoFigureRecipes.Shape[0])
            {
                if (s == null || !Part.IsMatch(s.id ?? "") || float.IsNaN(s.value) || s.value < 0f || s.value > 100f) return owner + ": shape values must be 0–100.";
                if (!seen.Add(s.id)) return owner + ": shape " + s.id + " is set twice.";
            }
            return null;
        }

        static string Colours(string what, Named[] entries, int min, int max)
        {
            if (entries == null || entries.Length == 0) return "no " + what + " colours.";
            var seen = new HashSet<string>();
            foreach (Named entry in entries)
            {
                if (entry == null || !Name.IsMatch(entry.id ?? "")) return "invalid " + what + " id.";
                if (!seen.Add(entry.id)) return what + " " + entry.id + " is listed twice.";
                if (entry.hex == null || entry.hex.Length < min || entry.hex.Length > max)
                    return what + " " + entry.id + ": " + (min == max ? min.ToString(CultureInfo.InvariantCulture) : min + "–" + max) + " colours.";
                foreach (string c in entry.hex) if (!Hex.IsMatch(c ?? "")) return what + " " + entry.id + ": '" + c + "' is not #rrggbb.";
            }
            return null;
        }

        const string IndexHeader = "# Ashen Crypt figure pool: key<TAB>kind<TAB>group<TAB>motion<TAB>skin (written by the figure baker)\n";

        /// <summary>
        /// The index after a bake: one line per pool figure whose game visual exists (key, kind, group, motion set, skin
        /// tone). The game picks residents from it and only puts a head on a body of the same motion set and skin, so
        /// a line must describe the visual as it was built: it comes from the current pool only for the figures rebuilt
        /// successfully now, or when the index has no line for a visual yet. Every other line stays as it was, lines
        /// of visuals that are gone are dropped.
        /// </summary>
        public static string UpdateIndex(string existing, IEnumerable<string> rebuiltIds, Func<string, bool> baked)
        {
            var lines = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in (existing ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                string key = line.Split('\t')[0];
                if (!lines.ContainsKey(key)) lines.Add(key, line);
            }
            var rebuilt = new HashSet<string>(rebuiltIds ?? new string[0], StringComparer.Ordinal);
            var order = new List<string>();
            if (File.Exists(PoolPath))
            {
                Pool pool = Read(File.ReadAllText(PoolPath));
                foreach (Kind kind in pool.kinds)
                {
                    string motion = pool.bases.First(b => b.id == kind.@base).motion;
                    for (int n = 0; n < kind.looks.Length; n++)
                    {
                        string key = Key(kind.id, n);
                        order.Add(key);
                        if (rebuilt.Contains(FigureId(kind.id, n)) || !lines.ContainsKey(key))
                            lines[key] = key + "\t" + kind.id + "\t" + kind.group + "\t" + motion + "\t" + kind.looks[n].tone;
                    }
                }
            }
            var known = new HashSet<string>(order, StringComparer.Ordinal);
            var text = new StringBuilder(IndexHeader);
            foreach (string key in order.Concat(lines.Keys.Where(k => !known.Contains(k)).OrderBy(k => k, StringComparer.Ordinal)))
                if (lines.ContainsKey(key) && baked(key)) text.Append(lines[key]).Append('\n');
            return text.ToString();
        }
    }
}
