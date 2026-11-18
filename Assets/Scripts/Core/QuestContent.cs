using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>One authored quest as the Core reads it from a versioned content table (never a second authority).</summary>
    public sealed class QuestContent
    {
        public string Id;
        public string Title;
        public string GiverNpcId;
        public List<string> Prerequisites = new List<string>();
        public int ObjectiveCount;
        public int RewardMin;
        public int RewardMax;
    }

    /// <summary>Versioned content table of quests with a checksum, read by the Core.</summary>
    public sealed class QuestContentTable
    {
        public const int CurrentVersion = 1;
        public int Version { get; private set; } = CurrentVersion;
        public readonly List<QuestContent> Quests = new List<QuestContent>();
        public string Checksum { get; private set; }

        public void Add(QuestContent quest) { if (quest != null) Quests.Add(quest); }
        public QuestContent Find(string id) { return Quests.FirstOrDefault(q => q != null && q.Id == id); }

        /// <summary>Canonical, deterministic signature over version and every quest; doubles as the checksum source.
        /// Every string field is length-prefixed and Title is included, so different content cannot collide.</summary>
        public string Signature()
        {
            var b = new StringBuilder();
            b.Append("1|").Append(Version.ToString(CultureInfo.InvariantCulture)).Append('|');
            foreach (var q in Quests.OrderBy(q => q.Id, StringComparer.Ordinal))
            {
                Field(b, q.Id); Field(b, q.Title); Field(b, q.GiverNpcId);
                b.Append(q.ObjectiveCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(q.RewardMin.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(q.RewardMax.ToString(CultureInfo.InvariantCulture)).Append(',');
                var prerequisites = q.Prerequisites.OrderBy(x => x, StringComparer.Ordinal).ToList();
                b.Append(prerequisites.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
                foreach (var p in prerequisites) Field(b, p);
                b.Append(';');
            }
            return b.ToString();
        }

        private static void Field(StringBuilder b, string value)
        {
            value = value ?? "";
            b.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }

        public void Seal() { Checksum = Hash(Signature()); }

        internal static string Hash(string payload)
        {
            ulong hash = 14695981039346656037UL;
            unchecked { foreach (byte value in Encoding.UTF8.GetBytes(payload)) { hash ^= value; hash *= 1099511628211UL; } }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        /// <summary>The pilot quest bound to the existing code path; the table only mirrors its contract.</summary>
        public static QuestContentTable Pilot()
        {
            var table = new QuestContentTable();
            table.Add(new QuestContent
            {
                Id = AuthoredHungerBinding.QuestId,
                Title = "Der Hunger hinter den Mauern",
                GiverNpcId = "ilyan",
                ObjectiveCount = 4,
                RewardMin = 0,
                RewardMax = 0
            });
            table.Seal();
            return table;
        }

        /// <summary>Pilot quest state read from the table equals the existing code path's status.</summary>
        public static string PilotStatus(CharacterProgression progress) { return AuthoredHungerBinding.Status(progress); }
    }

    /// <summary>Offline checker: every quest completable, no dead ends, rewards within bounds, ids unique.</summary>
    public static class QuestContentValidator
    {
        public const int MaxReward = 100000;

        public static List<string> Validate(QuestContentTable table)
        {
            var issues = new List<string>();
            if (table == null) { issues.Add("table is null"); return issues; }
            if (table.Version != QuestContentTable.CurrentVersion) issues.Add("unknown version " + table.Version);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in table.Quests)
            {
                if (q == null) { issues.Add("null quest"); continue; }
                if (string.IsNullOrEmpty(q.Id)) { issues.Add("quest without id"); continue; }
                if (!ids.Add(q.Id)) issues.Add("duplicate id " + q.Id);
                if (q.ObjectiveCount < 1) issues.Add("quest " + q.Id + " has no objective and cannot be completed");
                if (q.RewardMin < 0 || q.RewardMax < 0) issues.Add("quest " + q.Id + " reward must not be negative");
                if (q.RewardMin > q.RewardMax) issues.Add("quest " + q.Id + " reward min > max");
                if (q.RewardMax > MaxReward) issues.Add("quest " + q.Id + " reward exceeds the bound");
            }

            // Reachability: every prerequisite exists; a prerequisite cycle makes its members unreachable.
            foreach (var q in table.Quests)
            {
                if (q == null || string.IsNullOrEmpty(q.Id)) continue;
                foreach (var p in q.Prerequisites)
                    if (!ids.Contains(p)) issues.Add("quest " + q.Id + " unreachable prerequisite " + p);
            }
            var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0 unvisited, 1 visiting, 2 done
            foreach (var q in table.Quests)
            {
                if (q != null && !string.IsNullOrEmpty(q.Id)) DetectCycle(q.Id, table, state, issues);
            }
            return issues;
        }

        private static void DetectCycle(string id, QuestContentTable table, Dictionary<string, int> state, List<string> issues)
        {
            int s;
            state.TryGetValue(id, out s);
            if (s == 2) return;
            if (s == 1) { issues.Add("quest cycle at " + id); return; }
            state[id] = 1;
            var q = table.Find(id);
            if (q != null)
                foreach (var p in q.Prerequisites)
                    if (table.Find(p) != null) DetectCycle(p, table, state, issues);
            state[id] = 2;
        }
    }

    public sealed partial class CharacterProgression
    {
        /// <summary>Content-table version and checksum carried in the save; 0 means "no table stored".</summary>
        public int ContentTableVersion { get; private set; }
        public string ContentTableChecksum { get; private set; } = "";

        private const string ContentSectionName = "content";

        /// <summary>Bind a validated content table's version and checksum to this character. The table must be sealed
        /// and unchanged since sealing, so the stored checksum always identifies the content the Core read.</summary>
        public bool SetContentTable(QuestContentTable table)
        {
            if (table == null || table.Checksum == null) return false;
            if (table.Checksum != QuestContentTable.Hash(table.Signature())) return false;
            if (QuestContentValidator.Validate(table).Count != 0) return false;
            ContentTableVersion = table.Version;
            ContentTableChecksum = table.Checksum;
            SetSection(ContentSectionName, table.Version.ToString(CultureInfo.InvariantCulture) + "|" + table.Checksum);
            return true;
        }

        private void SyncContentSection()
        {
            // Mirror the regions section: a character without a table removes any stale/corrupt section so the
            // canonical re-save check in TryLoad rejects a non-canonical token.
            if (ContentTableVersion == 0) { _sections.Remove(ContentSectionName); return; }
            SetSection(ContentSectionName, ContentTableVersion.ToString(CultureInfo.InvariantCulture) + "|" + ContentTableChecksum);
        }

        private void RestoreContentSection()
        {
            string token;
            if (!_sections.TryGetValue(ContentSectionName, out token)) return;
            string[] f = token.Split('|');
            int version;
            if (f.Length != 2 || !int.TryParse(f[0], NumberStyles.None, CultureInfo.InvariantCulture, out version) ||
                version.ToString(CultureInfo.InvariantCulture) != f[0] || version < 1 || !IsHex16(f[1]))
            {
                _sections.Remove(ContentSectionName);
                return;
            }
            ContentTableVersion = version;
            ContentTableChecksum = f[1];
        }

        private static bool IsHex16(string text)
        {
            if (text == null || text.Length != 16) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }
    }
}
