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
        public int CatalogRevision { get; }
        /// <summary>Revision 2 only: what the kill path knew beyond the enemy's kind (guaranteed drop, boss).</summary>
        internal int Flags { get; }
        public int DefinitionRevision { get { return 1; } }
        public int AffixRevision { get { return 1; } }
        public string DefinitionId { get; }
        public ItemRarity Rarity { get; }
        public string AffixId { get { return "weapon.upgrade"; } }
        public int AffixValue { get; }
        public BigInteger KillOrdinal { get; }
        internal int SourceEnemyId => int.Parse(_context.Split(',')[4], CultureInfo.InvariantCulture);
        /// <summary>The kind of enemy whose kill made this receipt (0 ghoul, 1 knight, 2 boss, 3 ember warden).</summary>
        public int EnemyKind { get { return int.Parse(_context.Split(',')[5], CultureInfo.InvariantCulture); } }
        internal LootItem(string context, string definitionId, ItemRarity rarity, int value, BigInteger killOrdinal, int revision = 1, int flags = 0)
        { _context = context; DefinitionId = definitionId; Rarity = rarity; AffixValue = value; KillOrdinal = killOrdinal; CatalogRevision = revision; Flags = flags; }
        // Only used when restoring an explicit equipment award. An old iron shard never grants it on load.
        internal bool IsFieldPauldronSource(CharacterProgression character)
        {
            if (character.EntryAct < 1) return false;
            var context = _context.Split(',');
            if (context[2] != "1" || context[5] != "1") return false;
            if (context[4] == "2") return true; // original entry crypt knight
            if (!character.Woodland.Active) return false;
            int enemyId = int.Parse(context[4], CultureInfo.InvariantCulture);
            if (enemyId < WoodlandLayout.FirstMonsterId || enemyId >= WoodlandLayout.FirstMonsterId + WoodlandLayout.MonstersPerRun)
                return false;
            ulong seed = ulong.Parse(context[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var layout = character.Woodland.Seed == seed ? character.Woodland.Layout : new WoodlandLayout(seed);
            return layout.Monster(enemyId - WoodlandLayout.FirstMonsterId).Kind == 1;
        }
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
                AffixValue.ToString(CultureInfo.InvariantCulture) + (CatalogRevision >= 2 ? "," + Flags.ToString(CultureInfo.InvariantCulture) : "");
        }
        public ulong StableHash { get { return LootCatalog.HashText(Token()); } }
        /// <summary>
        /// Regenerate the receipt with the catalog of its revision and reject unknown or noncanonical persisted receipts.
        /// A revision-2 receipt has one more field, the flags; they must fit the kill (<see cref="LootCatalog.WeaponFlagsFit"/>),
        /// and a kill that dropped nothing has no receipt.
        /// </summary>
        internal static bool TryRead(string token, out LootItem item)
        {
            item = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] p = token.Split(',');
            bool second = p.Length == 12 && (p[0] == "2" || p[0] == "3" || p[0] == "4");
            if ((!second && (p.Length != 11 || p[0] != "1")) ||
                !ulong.TryParse(p[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var seed) ||
                !BigInteger.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out var depth) || depth < 1 ||
                !ulong.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out var tick) ||
                !int.TryParse(p[4], NumberStyles.None, CultureInfo.InvariantCulture, out var enemyId) || enemyId <= 0 ||
                !int.TryParse(p[5], NumberStyles.None, CultureInfo.InvariantCulture, out var kind) || kind < 0 || kind > 3 ||
                !BigInteger.TryParse(p[6], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) || ordinal < 1) return false;
            int flags = 0;
            if (second && (!int.TryParse(p[11], NumberStyles.None, CultureInfo.InvariantCulture, out flags) ||
                !(p[0] == "4" ? LootCatalog.ValidFinalCryptKill(seed, depth, enemyId, kind, flags) : LootCatalog.WeaponFlagsFit(flags, kind, enemyId, depth)))) return false;
            var generated = second ? (p[0] == "4" ? LootCatalog.Version4 : p[0] == "3" ? LootCatalog.Version3 : LootCatalog.Version2).GenerateFlagged(seed, depth, tick, enemyId, kind, ordinal, flags)
                : LootCatalog.Version1.Generate(seed, depth, tick, enemyId, kind, ordinal);
            // Replay the exact shipped definition: rejects unknown IDs, values, rarity and noncanonical numbers.
            if (generated == null || generated.Token() != token) return false;
            item = generated;
            return true;
        }
    }
}
