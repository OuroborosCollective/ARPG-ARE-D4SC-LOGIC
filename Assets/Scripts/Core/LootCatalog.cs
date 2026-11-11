using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>Stable serialized rarity values; version 1 grants one upgrade affix at either rarity.</summary>
    public enum ItemRarity { Magic = 1, Rare = 2 }

    /// <summary>An immutable weighted definition. Values are integer weapon points, never fixed-point raw units.</summary>
    public sealed class LootDropDefinition
    {
        public string Id { get; }
        public int MonsterKind { get; }
        public string ItemId { get; }
        public ItemRarity Rarity { get; }
        public int Weight { get; }
        public int WeaponPoints { get; }
        /// <summary>Validate stable IDs, supported rarity and positive integer weights and rewards.</summary>
        public LootDropDefinition(string id, int monsterKind, string itemId, ItemRarity rarity, int weight, int weaponPoints)
        {
            if (!LootCatalog.ValidId(id) || !LootCatalog.ValidId(itemId) || monsterKind < 0 || monsterKind > 3 ||
                (rarity != ItemRarity.Magic && rarity != ItemRarity.Rare) || weight <= 0 || weaponPoints <= 0)
                throw new ArgumentException("Invalid loot definition");
            Id = id; MonsterKind = monsterKind; ItemId = itemId; Rarity = rarity; Weight = weight; WeaponPoints = weaponPoints;
        }
    }

    /// <summary>Copies and sorts definitions by ordinal ID. Filtering precedes weight summation.</summary>
    public sealed class LootDropTable
    {
        private readonly LootDropDefinition[] _entries;
        private readonly uint[] _totals = new uint[4];
        /// <summary>Copy caller definitions, sort ordinal IDs and reject duplicates or overflowing per-kind weights.</summary>
        public LootDropTable(params LootDropDefinition[] entries)
        {
            if (entries == null || entries.Length == 0) throw new ArgumentException("Empty drop table");
            _entries = (LootDropDefinition[])entries.Clone();
            foreach (var entry in _entries) if (entry == null) throw new ArgumentException("Null drop definition");
            Array.Sort(_entries, (a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            for (int i = 0; i < _entries.Length; i++)
            {
                var e = _entries[i];
                if (i > 0 && _entries[i - 1].Id == e.Id) throw new ArgumentException("Duplicate drop ID");
                _totals[e.MonsterKind] = checked(_totals[e.MonsterKind] + (uint)e.Weight);
            }
        }
        /// <summary>Return only the weight eligible for this monster kind.</summary>
        public uint TotalWeight(int kind)
        {
            if (kind < 0 || kind >= _totals.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            return _totals[kind];
        }
        /// <summary>Select from a pre-bounded integer roll; missing/invalid tables fail closed.</summary>
        public LootDropDefinition Select(int kind, uint roll)
        {
            if (roll >= TotalWeight(kind)) throw new ArgumentOutOfRangeException(nameof(roll));
            foreach (var e in _entries)
            {
                if (e.MonsterKind != kind) continue;
                if (roll < e.Weight) return e;
                roll -= (uint)e.Weight;
            }
            throw new InvalidOperationException("Inconsistent drop table");
        }
        /// <summary>Serialize ordered definitions with culture-invariant numbers for the catalog hash.</summary>
        internal string Token()
        {
            var b = new StringBuilder();
            foreach (var e in _entries)
                b.Append(e.Id).Append(',').Append(e.MonsterKind.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(e.ItemId).Append(',').Append(((int)e.Rarity).ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(e.Weight.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(e.WeaponPoints.ToString(CultureInfo.InvariantCulture)).Append(';');
            return b.ToString();
        }
    }

    /// <summary>Immutable shipped catalog. Publishing changed definitions requires a new revision and loader.</summary>
    public sealed class LootCatalog
    {
        public static LootCatalog Version1 { get; } = new LootCatalog();
        public int Revision { get { return 1; } }
        public ulong StableHash { get; }
        private readonly LootDropTable _table;
        /// <summary>Freeze revision-one drop weights and the existing 8/17/34-point reward balance.</summary>
        private LootCatalog()
        {
            var entries = new LootDropDefinition[8];
            for (int kind = 0; kind < 4; kind++)
            {
                string id = kind == 0 ? "ash.shard" : kind == 1 ? "iron.shard" : "crown.shard";
                int points = kind == 0 ? 8 : kind == 1 ? 17 : 34;
                string prefix = "kind." + kind.ToString(CultureInfo.InvariantCulture);
                entries[kind * 2] = new LootDropDefinition(prefix + ".magic", kind, id, ItemRarity.Magic, 75, points);
                entries[kind * 2 + 1] = new LootDropDefinition(prefix + ".rare", kind, id, ItemRarity.Rare, 25, points);
            }
            _table = new LootDropTable(entries);
            StableHash = HashText("loot.v1;affix.weapon.upgrade.v1;" + _table.Token());
        }
        /// <summary>Accept only nonempty lowercase ASCII identifiers with digits and dots.</summary>
        internal static bool ValidId(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '.') return false;
            return true;
        }
        /// <summary>Compute stable UTF-8 FNV-1a; this is a replay hash, not an authentication signature.</summary>
        internal static ulong HashText(string value)
        {
            ulong h = 14695981039346656037UL;
            unchecked { foreach (byte b in Encoding.UTF8.GetBytes(value)) { h ^= b; h *= 1099511628211UL; } }
            return h;
        }
        /// <summary>Unbiased bounded selection from an explicit stream. Does not touch the combat PRNG.</summary>
        internal static uint Roll(DeterministicRng rng, uint bound)
        {
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound));
            ulong limit = (1UL << 32) - ((1UL << 32) % bound);
            uint value;
            do { value = (uint)(rng.Next64() >> 32); } while (value >= limit);
            return value % bound;
        }
        /// <summary>Called only by the canonical kill path; kill ordinal remains unique across checkpoint restarts.</summary>
        internal LootItem Generate(ulong seed, BigInteger depth, ulong tick, int enemyId, int kind, BigInteger killOrdinal)
        {
            if (depth < 1 || enemyId <= 0 || killOrdinal < 1) throw new ArgumentException("Invalid kill identity");
            string context = LootItem.Context(Revision, seed, depth, tick, enemyId, kind, killOrdinal);
            var rng = new DeterministicRng(HashText(context) ^ StableHash);
            var selected = _table.Select(kind, Roll(rng, _table.TotalWeight(kind)));
            return new LootItem(context, selected.ItemId, selected.Rarity, selected.WeaponPoints, killOrdinal);
        }
    }
}
