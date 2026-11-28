using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>
    /// Stable serialized tier values. Revision 1 knows only Magic and Rare; revision 2 adds Normal (white) below and
    /// Legendary (orange) above them.
    /// </summary>
    public enum ItemRarity { Normal = 0, Magic = 1, Rare = 2, Legendary = 3 }

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
        // The tables of revision 2 stand before the catalogs: static initializers run in the order they are written.
        // Weapon chance in percent by loot kind: common, elite, champion. Bosses always drop.
        private static readonly int[] DropChance = { 25, 45, 75 };
        // White, blue and yellow in percent by tier: the surface first, one tier deeper per cave or crypt depth.
        private static readonly int[][] TierWeights = { new[] { 65, 28, 7 }, new[] { 53, 34, 13 }, new[] { 42, 39, 19 }, new[] { 30, 45, 25 } };
        // Coins in a bag by loot kind: common, elite, champion. A boss bag holds one silver.
        private static readonly int[] CoinsLow = { 3, 8, 20 }, CoinsHigh = { 8, 20, 50 };

        public static LootCatalog Version1 { get; } = new LootCatalog(1);
        /// <summary>
        /// Revision 2 (issue #48; owner decisions of 2026-11-15, -17 and -23): not every enemy drops a weapon, the tiers
        /// white, blue, yellow and orange improve with depth and bosses, and supply bags are a drop of their own.
        /// </summary>
        public static LootCatalog Version2 { get; } = new LootCatalog(2);
        public static LootCatalog Version3 { get; } = new LootCatalog(3);
        public static LootCatalog Version4 { get; } = new LootCatalog(4);
        internal const int FinalCryptFlag = 4;
        internal const int FinalCryptDepth = 3;

        internal static bool ValidFinalCryptKill(ulong seed, BigInteger depth, int enemyId, int kind, int flags)
        {
            if (depth != FinalCryptDepth) return false;
            if (flags == (FinalCryptFlag | BossFlag)) return enemyId == RattlerEnemyId && kind == 2;
            if (flags != FinalCryptFlag) return false;
            var layout = new CryptDungeonLayout(seed, FinalCryptDepth);
            for (int i = 0; i < layout.MonsterCount; i++)
            {
                var member = layout.Monster(i);
                if (member.Id == enemyId) return member.Kind == kind;
            }
            return false;
        }
        public int Revision { get; }
        public ulong StableHash { get; }
        private readonly LootDropTable _table;

        /// <summary>The drop falls whatever the chance says: the kill carries a story piece (the field pauldron).</summary>
        internal const int GuaranteedFlag = 1;
        /// <summary>The enemy counts as a boss although its loot kind is a champion's: the rattler.</summary>
        internal const int BossFlag = 2;
        public const int RattlerEnemyId = 42;
        private const int BossMagic = 35, BossRare = 50;            // the remaining 15 % are legendary
        private const int DeepTier = 3, DeepChampionLegendary = 2;  // deep champions: 2 % legendary, taken from yellow
        private const int SupplyChance = 25, AlwaysSupplyKills = 3, RationOneIn = 3, BossCoins = 100;

        /// <summary>Freeze revision-one drop weights and the existing 8/17/34-point reward balance.</summary>
        private LootCatalog(int revision)
        {
            Revision = revision;
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
            StableHash = revision == 1 ? HashText("loot.v1;affix.weapon.upgrade.v1;" + _table.Token()) : revision == 4 ? HashText("loot.v4;terminal-crypt;royal.v1;pool=21;regular=1/8;boss=1/3;" + RulesToken()) : HashText(revision == 2 ? RulesToken() : RulesToken() + ";weapon.glut.v1;pool=36+21;normal-affix=quarter");
        }

        /// <summary>Every number of revision 2 in one stable text: changing a number changes the catalog hash.</summary>
        private static string RulesToken()
        {
            var tiers = new StringBuilder();
            foreach (int[] tier in TierWeights) { if (tiers.Length != 0) tiers.Append('/'); tiers.Append(List(tier)); }
            return "loot.v2;affix.weapon.upgrade.v1;points=8,17,34;drop=" + List(DropChance) + ";tiers=" + tiers + ";boss=" + List(BossMagic, BossRare) +
                ";deep=" + List(DeepTier, DeepChampionLegendary) + ";supply=" + List(SupplyChance, AlwaysSupplyKills, RationOneIn, BossCoins) +
                ";coins=" + List(CoinsLow) + "-" + List(CoinsHigh);
        }

        private static string List(params int[] values)
        {
            var b = new StringBuilder();
            foreach (int value in values) { if (b.Length != 0) b.Append(','); b.Append(value.ToString(CultureInfo.InvariantCulture)); }
            return b.ToString();
        }

        /// <summary>The tier of a place: 1 on the surface, one more per depth, at most 4.</summary>
        public static int Tier(BigInteger depth) { return depth >= 4 ? 4 : depth < 1 ? 1 : (int)depth; }

        private static bool IsBoss(int kind, int flags) { return kind >= 3 || (flags & BossFlag) != 0; }

        /// <summary>
        /// Shared flag rules for a supply bag or an ordinary weapon drop: the guaranteed knight drop at depth one
        /// and the boss flag for the rattler.
        /// </summary>
        internal static bool FlagsFit(int flags, int kind, int enemyId, BigInteger depth)
        {
            if (flags < 0 || flags > (GuaranteedFlag | BossFlag)) return false;
            if ((flags & GuaranteedFlag) != 0 && (kind != 1 || !depth.IsOne ||
                (enemyId != 2 && (enemyId < WoodlandLayout.FirstMonsterId || enemyId >= WoodlandLayout.FirstMonsterId + WoodlandLayout.MonstersPerRun)))) return false;
            if ((flags & BossFlag) != 0 && (kind != 2 || enemyId != RattlerEnemyId)) return false;
            return true;
        }

        /// <summary>The Aschbeter guarantees a weapon in the forecourt at depth one and never drops a supply bag.</summary>
        internal static bool WeaponFlagsFit(int flags, int kind, int enemyId, BigInteger depth) =>
            (flags == GuaranteedFlag && kind == 2 && enemyId == ArenaSimulation.AschbeterLootId && depth.IsOne) ||
            FlagsFit(flags, kind, enemyId, depth);

        private static void RequireKill(BigInteger depth, int enemyId, int kind, BigInteger killOrdinal, int flags)
        {
            if (depth < 1 || enemyId <= 0 || killOrdinal < 1 || kind < 0 || kind > 3 || flags < 0 || flags > (GuaranteedFlag | BossFlag))
                throw new ArgumentException("Invalid kill identity");
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
        /// <summary>
        /// Called only by the canonical kill path; kill ordinal remains unique across checkpoint restarts. Revision 1
        /// always returns a receipt and ignores the flags. Revision 2 returns null when the kill drops no weapon.
        /// </summary>
        internal LootItem Generate(ulong seed, BigInteger depth, ulong tick, int enemyId, int kind, BigInteger killOrdinal)
        {
            return GenerateFlagged(seed, depth, tick, enemyId, kind, killOrdinal, 0);
        }

        /// <summary>As <see cref="Generate"/>, with what the kill path knows beyond the enemy's kind (revision 2).</summary>
        internal LootItem GenerateFlagged(ulong seed, BigInteger depth, ulong tick, int enemyId, int kind, BigInteger killOrdinal, int flags)
        {
            if (Revision == 1)
            {
                if (depth < 1 || enemyId <= 0 || killOrdinal < 1) throw new ArgumentException("Invalid kill identity");
                string legacy = LootItem.Context(Revision, seed, depth, tick, enemyId, kind, killOrdinal);
                var legacyRng = new DeterministicRng(HashText(legacy) ^ StableHash);
                var selected = _table.Select(kind, Roll(legacyRng, _table.TotalWeight(kind)));
                return new LootItem(legacy, selected.ItemId, selected.Rarity, selected.WeaponPoints, killOrdinal);
            }
            if (Revision == 4)
            {
                if ((flags & FinalCryptFlag) == 0) return Version3.GenerateFlagged(seed, depth, tick, enemyId, kind, killOrdinal, flags);
                if (!ValidFinalCryptKill(seed, depth, enemyId, kind, flags)) throw new ArgumentException("Invalid final crypt origin");
            }
            RequireKill(depth, enemyId, kind, killOrdinal, flags & (Revision == 4 ? ~FinalCryptFlag : -1));
            string context = LootItem.Context(Revision, seed, depth, tick, enemyId, kind, killOrdinal);
            var rng = new DeterministicRng(HashText(context + "," + flags.ToString(CultureInfo.InvariantCulture)) ^ StableHash);
            bool boss = IsBoss(kind, flags);
            if (!boss && (flags & GuaranteedFlag) == 0 && Roll(rng, 100) >= DropChance[kind]) return null;
            uint roll = Roll(rng, 100);
            ItemRarity rarity;
            if (boss) rarity = roll < BossMagic ? ItemRarity.Magic : roll < BossMagic + BossRare ? ItemRarity.Rare : ItemRarity.Legendary;
            else
            {
                int tier = Tier(depth);
                int[] weights = TierWeights[tier - 1];
                rarity = roll < weights[0] ? ItemRarity.Normal : roll < weights[0] + weights[1] ? ItemRarity.Magic : ItemRarity.Rare;
                if (kind == 2 && tier >= DeepTier && roll >= 100 - DeepChampionLegendary) rarity = ItemRarity.Legendary;
            }
            // Item and points stay those of the enemy's kind, as in revision 1: the areas' saves check them.
            var definition = _table.Select(kind, 0);
            return new LootItem(context, definition.ItemId, rarity, definition.WeaponPoints, killOrdinal, Revision, flags);
        }

        /// <summary>
        /// The supply bag of a kill, or null: revision 2 only. A bag falls from every fourth enemy, from every boss and
        /// from a character's first three kills (so the camp's rations stay within reach of a new character). It holds
        /// coins by the enemy's class. One bag in three holds a ration (a roll per bag, not a count); every boss bag and
        /// the first three always do.
        /// </summary>
        internal SupplyBag Supply(ulong seed, BigInteger depth, ulong tick, int enemyId, int kind, BigInteger killOrdinal, int flags = 0)
        {
            if (Revision < 2) return null;
            if (Revision == 4) return Version2.Supply(seed, depth, tick, enemyId, kind, killOrdinal, flags & ~FinalCryptFlag);
            RequireKill(depth, enemyId, kind, killOrdinal, flags);
            if (Revision == 3) return Version2.Supply(seed, depth, tick, enemyId, kind, killOrdinal, flags);
            string context = LootItem.Context(Revision, seed, depth, tick, enemyId, kind, killOrdinal);
            var rng = new DeterministicRng(HashText("supply;" + context + "," + flags.ToString(CultureInfo.InvariantCulture)) ^ StableHash);
            bool boss = IsBoss(kind, flags), always = boss || killOrdinal <= AlwaysSupplyKills;
            if (!always && Roll(rng, 100) >= SupplyChance) return null;
            int coins = boss ? BossCoins : CoinsLow[kind] + (int)Roll(rng, (uint)(CoinsHigh[kind] - CoinsLow[kind] + 1));
            int rations = always || Roll(rng, RationOneIn) == 0 ? 1 : 0;
            return new SupplyBag(context, flags, coins, rations, killOrdinal);
        }
    }
}
