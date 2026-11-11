using System;
using System.Globalization;
using System.Numerics;

namespace AreArpg.Core
{
    /// <summary>Immutable consumable upgrade receipt with one valid v1 affix. No Unity object references.</summary>
    public sealed class LootItem
    {
        private readonly string _context;
        public string InstanceId { get { return "loot." + _context; } }
        public int CatalogRevision { get { return 1; } }
        public int DefinitionRevision { get { return 1; } }
        public int AffixRevision { get { return 1; } }
        public string DefinitionId { get; }
        public ItemRarity Rarity { get; }
        public string AffixId { get { return "weapon.upgrade"; } }
        public int AffixValue { get; }
        public BigInteger KillOrdinal { get; }
        internal LootItem(string context, string definitionId, ItemRarity rarity, int value, BigInteger killOrdinal)
        { _context = context; DefinitionId = definitionId; Rarity = rarity; AffixValue = value; KillOrdinal = killOrdinal; }
        /// <summary>Encode the complete canonical kill identity without culture-sensitive formatting.</summary>
        internal static string Context(int revision, ulong seed, BigInteger depth, ulong tick, int enemyId, int kind, BigInteger ordinal)
        {
            return revision.ToString(CultureInfo.InvariantCulture) + "," + seed.ToString("X16", CultureInfo.InvariantCulture) + "," +
                depth.ToString(CultureInfo.InvariantCulture) + "," + tick.ToString(CultureInfo.InvariantCulture) + "," +
                enemyId.ToString(CultureInfo.InvariantCulture) + "," + kind.ToString(CultureInfo.InvariantCulture) + "," + ordinal.ToString(CultureInfo.InvariantCulture);
        }
        /// <summary>Serialize the kill context and immutable selected item and affix for replay validation.</summary>
        internal string Token()
        {
            return _context + "," + DefinitionId + "," + ((int)Rarity).ToString(CultureInfo.InvariantCulture) + "," + AffixId + "," +
                AffixValue.ToString(CultureInfo.InvariantCulture);
        }
        public ulong StableHash { get { return LootCatalog.HashText(Token()); } }
        /// <summary>Regenerate revision-one loot and reject unknown or noncanonical persisted receipts.</summary>
        internal static bool TryRead(string token, out LootItem item)
        {
            item = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] p = token.Split(',');
            if (p.Length != 11 || p[0] != "1" ||
                !ulong.TryParse(p[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var seed) ||
                !BigInteger.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out var depth) || depth < 1 ||
                !ulong.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out var tick) ||
                !int.TryParse(p[4], NumberStyles.None, CultureInfo.InvariantCulture, out var enemyId) || enemyId <= 0 ||
                !int.TryParse(p[5], NumberStyles.None, CultureInfo.InvariantCulture, out var kind) || kind < 0 || kind > 3 ||
                !BigInteger.TryParse(p[6], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) || ordinal < 1) return false;
            var generated = LootCatalog.Version1.Generate(seed, depth, tick, enemyId, kind, ordinal);
            // Replay the exact shipped definition: rejects unknown IDs, values, rarity and noncanonical numbers.
            if (generated.Token() != token) return false;
            item = generated;
            return true;
        }
    }
}
