using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// The figure pool as the game sees it (owner direction 2026-11-22). Resources/ARE/Visuals/FolkIndex lists every
    /// baked pool figure with its kind, group, motion set and skin tone. A person without a figure of their own, such
    /// as a resident who moved into the village, is shown with one of them, chosen from their number: the first
    /// people of a group each get a figure of their own, kind by kind, every further round the same bodies with the
    /// head of another figure of the same motion set and skin (FigureComposer). Presentation only: Core and save know
    /// no figures.
    /// </summary>
    public static class FolkFigures
    {
        public const string VisualRoot = "ARE/Visuals/";
        public const string IndexResource = VisualRoot + "FolkIndex";
        /// <summary>Village workers: farmers, farmhands, woodcutters, foresters, hunters, miners, the old.</summary>
        public const string Workers = "arbeit";

        public struct Entry { public string Key, Kind, Group, Motion, Skin; }

        public struct Look
        {
            public string Body, Head;
            public bool OwnHead { get { return Body == Head; } }
        }

        static List<Entry> _installed;

        /// <summary>The pool figures baked in this build (none where no art is installed); read once.</summary>
        public static IReadOnlyList<Entry> Installed
        {
            get
            {
                if (_installed == null)
                {
                    var index = Resources.Load<TextAsset>(IndexResource);
                    _installed = index != null ? Parse(index.text) : new List<Entry>();
                    if (index != null) Resources.UnloadAsset(index);
                }
                return _installed;
            }
        }

        /// <summary>Lines of key, kind, group, motion set and skin, tab separated. Anything malformed is skipped.</summary>
        public static List<Entry> Parse(string text)
        {
            var result = new List<Entry>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split('\t');
                if (f.Length != 5 || !IsKey(f[0]) || !IsWord(f[1]) || !IsWord(f[2]) || (f[3] != "m" && f[3] != "f") || !IsWord(f[4])) continue;
                if (keys.Add(f[0])) result.Add(new Entry { Key = f[0], Kind = f[1], Group = f[2], Motion = f[3], Skin = f[4] });
            }
            return result;
        }

        /// <summary>A pool visual: "Folk" and letters or digits only, so it can never name a path outside the visuals.</summary>
        public static bool IsKey(string key)
        {
            if (key == null || key.Length < 6 || key.Length > 41 || !key.StartsWith("Folk", StringComparison.Ordinal)) return false;
            foreach (char c in key) if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9')) return false;
            return true;
        }

        static bool IsWord(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length > 20) return false;
            foreach (char c in word) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_')) return false;
            return true;
        }

        /// <summary>
        /// The look of person number <paramref name="ordinal"/> of a group. The group's figures are dealt out in
        /// <see cref="DealingOrder"/>; the first round gives each person a figure of their own, every further round the
        /// same bodies with the head of the next figure of the same motion set and skin. False when the group has no
        /// figure installed.
        /// </summary>
        public static bool TryLook(IReadOnlyList<Entry> entries, string group, int ordinal, out Look look)
        {
            look = default(Look);
            List<Entry> bodies = DealingOrder(entries, group);
            if (bodies.Count == 0) return false;
            long n = Math.Abs((long)ordinal);
            Entry body = bodies[(int)(n % bodies.Count)];
            int round = (int)(n / bodies.Count);
            List<Entry> heads = bodies.FindAll(entry => entry.Motion == body.Motion && entry.Skin == body.Skin);
            int own = heads.FindIndex(entry => entry.Key == body.Key);
            look = new Look { Body = body.Key, Head = heads[(own + round) % heads.Count].Key };
            return true;
        }

        /// <summary>
        /// The order in which a group's figures are dealt out: one figure of every kind per round, the kinds in a fixed
        /// mixed order and each kind starting at another of its looks. The first people of a village differ in kind,
        /// skin and colours, and every look of a kind comes once before any comes again.
        /// </summary>
        public static List<Entry> DealingOrder(IReadOnlyList<Entry> entries, string group)
        {
            var byKind = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
            int total = 0;
            foreach (Entry entry in entries ?? new Entry[0])
            {
                if (entry.Group != group) continue;
                List<Entry> looks;
                if (!byKind.TryGetValue(entry.Kind, out looks)) byKind.Add(entry.Kind, looks = new List<Entry>());
                looks.Add(entry);
                total++;
            }
            var kinds = new List<string>(byKind.Keys);
            kinds.Sort((a, b) =>
            {
                int order = Mix(a).CompareTo(Mix(b));
                return order != 0 ? order : string.CompareOrdinal(a, b);
            });
            foreach (List<Entry> looks in byKind.Values) looks.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var dealt = new List<Entry>(total);
            for (int round = 0; dealt.Count < total; round++)
                for (int i = 0; i < kinds.Count; i++)
                {
                    List<Entry> looks = byKind[kinds[i]];
                    if (round < looks.Count) dealt.Add(looks[(round + i) % looks.Count]);
                }
            return dealt;
        }

        /// <summary>
        /// FNV-1a with a final avalanche (MurmurHash3's fmix32): the same on every device and in every session, and
        /// names that differ in one letter land far apart. FNV-1a alone kept them side by side.
        /// </summary>
        static uint Mix(string text)
        {
            uint hash = 2166136261u;
            foreach (char c in text) { hash ^= c; hash *= 16777619u; }
            hash ^= hash >> 16; hash *= 0x85ebca6bu;
            hash ^= hash >> 13; hash *= 0xc2b2ae35u;
            hash ^= hash >> 16;
            return hash;
        }

        /// <summary>Put the look's head on the figure just bound (nothing to do when it keeps its own head).</summary>
        public static bool PutHead(Transform art, Look look)
        {
            if (art == null || look.OwnHead || !IsKey(look.Head)) return false;
            GameObject donor = Resources.Load<GameObject>(VisualRoot + look.Head);
            return donor != null && FigureComposer.TryPutHead(art, donor);
        }
    }
}
