using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>
    /// AC22 container: one header, one versioned section per system and a checksum over everything.
    /// The existing AC1..AC21 chain stays the checked "base" section, so every old format keeps loading
    /// unchanged and is rewritten into this container on the next save. A save without sections stays
    /// byte-identical to the legacy chain: SaveString only writes AC22 once a system has called SetSection,
    /// so a legacy AC5/AC8 checkpoint that is loaded and re-saved without a section keeps its old format.
    /// </summary>
    public sealed partial class CharacterProgression
    {
        private const string SectionContainerVersion = "1";
        /// <summary>One upper bound for the whole serialized container, used by both the writer and the loader.</summary>
        internal const int MaxSaveLength = 64 * 1024 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly SortedDictionary<string, string> _sections = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>True once at least one system contributes a section; only then is the AC22 header written.</summary>
        public bool HasSections { get { return _sections.Count != 0; } }

        /// <summary>Add or replace one system's section without touching the base or any other section. The value must be
        /// valid UTF-16 (no lone surrogate) and must keep the serialized container within the shared size bound.</summary>
        public void SetSection(string name, string value)
        {
            if (!IsValidSectionName(name)) throw new ArgumentException("Invalid save section name.", nameof(name));
            value = value ?? string.Empty;
            byte[] encoded;
            try { encoded = StrictUtf8.GetBytes(value); }
            catch (EncoderFallbackException) { throw new ArgumentException("Save section value is not valid UTF-16.", nameof(value)); }
            if (Convert.ToBase64String(encoded).Length > MaxSaveLength)
                throw new ArgumentException("Save section value exceeds the checkpoint size bound.", nameof(value));
            _sections[name] = value;
        }

        public bool TryGetSection(string name, out string value) { return _sections.TryGetValue(name, out value); }

        private static bool IsValidSectionName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 64) return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>Wrap the complete legacy checkpoint only when a section exists; otherwise return it unchanged.
        /// The writer refuses to emit a container the loader would reject for its size, so the previous save stays
        /// intact instead of being replaced by an unreadable one.</summary>
        private string SectionContainer(string legacy)
        {
            if (_sections.Count == 0) return legacy;
            var b = new StringBuilder();
            b.Append("AC22|").Append(SectionContainerVersion).Append('|')
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(legacy)))
                .Append('|').Append(_sections.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var pair in _sections)
                b.Append('|').Append(pair.Key).Append('=').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value)));
            string body = b.ToString();
            string container = body + "|" + HashText(body).ToString("X16", CultureInfo.InvariantCulture);
            if (container.Length > MaxSaveLength)
                throw new InvalidOperationException("Checkpoint exceeds the size bound; the previous save is kept.");
            return container;
        }

        /// <summary>Read the checked base through the existing chain, then attach the validated sections.</summary>
        private static bool TryLoadSectionContainer(string saved, JourneyMapCatalog maps, out CharacterProgression progress)
        {
            progress = null;
            if (saved.Length > MaxSaveLength) return false;
            string[] f = saved.Split('|');
            if (f.Length < 5 || f[0] != "AC22" || f[1] != SectionContainerVersion) return false;
            int count;
            if (!int.TryParse(f[3], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 0) return false;
            if (f.Length != 5 + count) return false;
            if (f[f.Length - 1] != HashText(string.Join("|", f, 0, f.Length - 1)).ToString("X16", CultureInfo.InvariantCulture)) return false;
            string inner;
            try
            {
                byte[] bytes = Convert.FromBase64String(f[2]);
                if (Convert.ToBase64String(bytes) != f[2]) return false;
                inner = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (FormatException) { return false; }
            catch (DecoderFallbackException) { return false; }
            if (inner.StartsWith("AC22|", StringComparison.Ordinal)) return false;
            var sections = new SortedDictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string entry = f[4 + i];
                int eq = entry.IndexOf('=');
                if (eq <= 0) return false;
                string name = entry.Substring(0, eq);
                if (!IsValidSectionName(name) || sections.ContainsKey(name)) return false;
                string encoded = entry.Substring(eq + 1);
                try
                {
                    byte[] valueBytes = Convert.FromBase64String(encoded);
                    if (Convert.ToBase64String(valueBytes) != encoded) return false;
                    sections[name] = new UTF8Encoding(false, true).GetString(valueBytes);
                }
                catch (FormatException) { return false; }
                catch (DecoderFallbackException) { return false; }
            }
            CharacterProgression p;
            bool ok = maps == null ? TryLoadRestoring(inner, null, out p) : TryLoadJourney(inner, maps, null, out p);
            if (!ok || p == null) return false;
            foreach (var pair in sections) p._sections[pair.Key] = pair.Value;
            p.RestoreRegionSection();
            p.RestoreContentSection();
            p.RestoreCraftSection();
            p.RestoreCompanionRecoverySection();
            p.RestoreVillageSection();
            if (p.SaveString() != saved) return false;
            progress = p;
            return true;
        }
    }
}
