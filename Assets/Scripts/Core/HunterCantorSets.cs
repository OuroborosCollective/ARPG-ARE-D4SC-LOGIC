namespace AreArpg.Core
{
    /// <summary>
    /// The roll every archive set uses under picked-up loot: the loot's own receipt decides, mixed with the set's salt, so
    /// the same loot always gives the same find; a hit picks one of the pieces not owned yet.
    /// </summary>
    internal static class LootSetRoll
    {
        internal static RegionalRelic Roll(LootItem item, WeaponEquipment gear, RegionalRelic[] pieces, ulong salt, int chanceBp)
        {
            if (item == null || gear == null) return RegionalRelic.None;
            ulong h = item.StableHash ^ salt;
            unchecked { h ^= h >> 33; h *= 0xFF51AFD7ED558CCDUL; h ^= h >> 33; h *= 0xC4CEB9FE1A85EC53UL; h ^= h >> 33; }
            if ((int)(h % 10000UL) >= chanceBp) return RegionalRelic.None;
            int missing = 0;
            foreach (var id in pieces) if (!gear.OwnsRegionalRelic(id)) missing++;
            if (missing == 0) return RegionalRelic.None;
            int pick = (int)((h >> 20) % (ulong)missing);
            foreach (var id in pieces)
            {
                if (gear.OwnsRegionalRelic(id)) continue;
                if (pick-- == 0) return id;
            }
            return RegionalRelic.None;
        }
    }

    /// <summary>
    /// Owner request 2026-11-26: Rotfährte, the hunter's leather set from the owner's archive, with its slate longbow. Rare
    /// random loot (2 percent); the game has no hunter enemies, so the hunter's grounds stand in for them: loot picked up
    /// in the forests (the forest towards Aschenzoll, the bear cave, Aschenzoll, the forest road beyond it and its side
    /// cave) carries a piece far more often (25 percent). Its armour is leather in ArmorRules' weighted slots.
    /// </summary>
    public static class RotfaehrteSet
    {
        public static readonly RegionalRelic[] Pieces =
        {
            RegionalRelic.F_Gugel, RegionalRelic.F_Wams, RegionalRelic.F_Wetterkragen, RegionalRelic.F_Aermel,
            RegionalRelic.F_Handschuhe, RegionalRelic.F_Koppel, RegionalRelic.F_Beinkleid, RegionalRelic.F_Stiefel, RegionalRelic.F_Langbogen
        };
        public const int LootChanceBp = 200, ForestChanceBp = 2500;
        private const ulong Salt = 0x6D2B79F5A1C3E487UL;

        public static int ChanceBpFor(bool inHuntingGrounds) { return inHuntingGrounds ? ForestChanceBp : LootChanceBp; }
        public static RegionalRelic Roll(LootItem item, WeaponEquipment gear, bool inHuntingGrounds)
        { return LootSetRoll.Roll(item, gear, Pieces, Salt, ChanceBpFor(inHuntingGrounds)); }
        public static bool IsPiece(RegionalRelic id) { return System.Array.IndexOf(Pieces, id) >= 0; }

        internal static readonly ArmorPiece Hood = new ArmorPiece("rotfaehrte.leather.gugel", "Fährtengugel der Rotfährte", ArmorSlot.Head, ArmorMaterial.Leather);
        internal static readonly ArmorPiece Jerkin = new ArmorPiece("rotfaehrte.leather.wams", "Lederwams der Rotfährte", ArmorSlot.Chest, ArmorMaterial.Leather);
        internal static readonly ArmorPiece Gloves = new ArmorPiece("rotfaehrte.leather.handschuhe", "Jagdhandschuhe der Rotfährte", ArmorSlot.Hands, ArmorMaterial.Leather);
        internal static readonly ArmorPiece Breeches = new ArmorPiece("rotfaehrte.leather.beinkleid", "Beinkleid der Rotfährte", ArmorSlot.Legs, ArmorMaterial.Leather);
        internal static readonly ArmorPiece Boots = new ArmorPiece("rotfaehrte.leather.stiefel", "Jagdstiefel der Rotfährte", ArmorSlot.Feet, ArmorMaterial.Leather);
    }

    /// <summary>
    /// Owner request 2026-11-26: Rußkantor, the sorcerer's linen set from the owner's archive, with a short staff (one hand)
    /// and a long staff (two hands). Rare random loot (2 percent); the game has no sorcerer enemies, so the ghouls stand in
    /// for them (the necromancer's army, the one enemy that casts): their loot carries a piece far more often (25 percent).
    /// Its clothes are cloth in ArmorRules' weighted slots: no protection, full healing.
    /// </summary>
    public static class RusskantorSet
    {
        public static readonly RegionalRelic[] Pieces =
        {
            RegionalRelic.K_Kapuze, RegionalRelic.K_Kittel, RegionalRelic.K_Pelerine, RegionalRelic.K_Aermel, RegionalRelic.K_Handschuhe,
            RegionalRelic.K_Gurt, RegionalRelic.K_Beinkleid, RegionalRelic.K_Stiefel, RegionalRelic.K_Kurzstab, RegionalRelic.K_Langstab
        };
        public const int LootChanceBp = 200, GhoulChanceBp = 2500;
        /// <summary>The kind of enemy the game calls a ghoul (MonsterState.Kind).</summary>
        public const int GhoulKind = 0;
        private const ulong Salt = 0xB5297A4D3F81C6E9UL;

        public static int ChanceBpFor(LootItem item) { return item != null && item.EnemyKind == GhoulKind ? GhoulChanceBp : LootChanceBp; }
        // The two-argument overload is the legacy kind table. The pickup path also knows the source monster.
        public static RegionalRelic Roll(LootItem item, WeaponEquipment gear) => Roll(item, gear, true);
        public static RegionalRelic Roll(LootItem item, WeaponEquipment gear, bool ghoulSource) =>
            LootSetRoll.Roll(item, gear, Pieces, Salt, ghoulSource ? ChanceBpFor(item) : LootChanceBp);
        public static bool IsPiece(RegionalRelic id) { return System.Array.IndexOf(Pieces, id) >= 0; }

        internal static readonly ArmorPiece Hood = new ArmorPiece("russkantor.cloth.kapuze", "Kantorenkapuze des Rußkantors", ArmorSlot.Head, ArmorMaterial.Cloth);
        internal static readonly ArmorPiece Smock = new ArmorPiece("russkantor.cloth.kittel", "Leinenkittel des Rußkantors", ArmorSlot.Chest, ArmorMaterial.Cloth);
        internal static readonly ArmorPiece Gloves = new ArmorPiece("russkantor.cloth.handschuhe", "Leinenhandschuhe des Rußkantors", ArmorSlot.Hands, ArmorMaterial.Cloth);
        internal static readonly ArmorPiece Breeches = new ArmorPiece("russkantor.cloth.beinkleid", "Leinenbeinkleid des Rußkantors", ArmorSlot.Legs, ArmorMaterial.Cloth);
        internal static readonly ArmorPiece Boots = new ArmorPiece("russkantor.cloth.stiefel", "Kantorenstiefel des Rußkantors", ArmorSlot.Feet, ArmorMaterial.Cloth);
    }

    public sealed partial class WeaponEquipment
    {
        // Only armour advances a tier. Each signature weapon enables its own set while in hand.
        private int ArmourBonusTier(RegionalRelic[] pieces)
        {
            var progress = ArmourSetProgress(pieces);
            return progress.Worn == progress.Total ? 2 : progress.Worn >= 4 ? 1 : 0;
        }

        internal int SteadyHandTier => IsRegionalRelicEquipped(RegionalRelic.F_Langbogen)
            ? ArmourBonusTier(RotfaehrteSet.Pieces) : 0;

        internal int FieldSutureTier => IsRegionalRelicEquipped(RegionalRelic.K_Kurzstab) || IsRegionalRelicEquipped(RegionalRelic.K_Langstab)
            ? ArmourBonusTier(RusskantorSet.Pieces) : 0;

        public (int Worn, int Total) ArmourSetProgress(RegionalRelic[] pieces)
        {
            int worn = 0, total = 0;
            foreach (var piece in pieces)
            {
                if (RegionalRelics.HandDefinition(piece) != null) continue;
                total++;
                if (IsRegionalRelicEquipped(piece)) worn++;
            }
            return (worn, total);
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The forests, where the hunter's set turns up more often: the forest region and the road beyond Aschenzoll.</summary>
        public bool InHuntingGrounds { get { return InWoodlandRegion || InJourney; } }

        /// <summary>After Aschenwacht's gear and the Nachtvesper: the hunter's set, then the sorcerer's. One find per loot.</summary>
        private void FindHunterAndCantorSets(LootItem item)
        {
            if (!Progression.Equipment.Active) return;
            var gear = Progression.Equipment;
            var id = RotfaehrteSet.Roll(item, gear, InHuntingGrounds);
            if (id == RegionalRelic.None)
            {
                // Receipts already persist the source ID. Generated areas restore those same monsters with the ground loot.
                // The fixed second chapter uses an ash hunter for its kind-zero slot.
                bool legacyGhouls = ChapterId == 1 || InDungeon || InWoodlandRegion || InJourney;
                int sourceId = item.SourceEnemyId;
                bool ghoul = legacyGhouls && item.EnemyKind == RusskantorSet.GhoulKind &&
                    Monsters.Exists(m => m.Id == sourceId && m.IsDead && m.Family == MonsterFamily.Legacy && m.Kind == RusskantorSet.GhoulKind);
                id = RusskantorSet.Roll(item, gear, ghoul);
            }
            if (id != RegionalRelic.None && gear.ClaimRegionalRelic(Progression, id)) LastGearFind = id;
        }
    }
}
