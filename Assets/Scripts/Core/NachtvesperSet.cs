using System;

namespace AreArpg.Core
{
    /// <summary>
    /// Owner request 2026-11-26: the Nachtvesper, a plate harness of the Glockenwacht from the owner's archive, is rare random
    /// loot. Any picked-up loot may carry a piece the wanderer does not own yet (2 percent); loot a knight dropped does so far
    /// more often (25 percent). Its eight pieces are regional pieces (<see cref="RegionalRelic"/>) that never lie anywhere by
    /// themselves, and the chance is decided by the loot's own receipt, so the same loot always gives the same find.
    ///
    /// Set bonus: every worn piece adds one percent to the hero's maximum health; all eight together add a quarter to his
    /// stamina. The helm, cuirass, gauntlets, greaves and sabatons protect like plate in the five weighted armour slots of
    /// <see cref="ArmorRules"/> (and cost healing like plate); the shoulder pieces, arm guards and girdle carry only the set.
    /// </summary>
    public static class NachtvesperSet
    {
        public static readonly RegionalRelic[] Pieces =
        {
            RegionalRelic.N_Schaller, RegionalRelic.N_Kuerass, RegionalRelic.N_Schulterstuecke, RegionalRelic.N_Armschienen,
            RegionalRelic.N_Handschuhe, RegionalRelic.N_Guertung, RegionalRelic.N_Beinzeug, RegionalRelic.N_Panzerschuhe
        };

        /// <summary>Chance of a piece under loot from anything but a knight, and under a knight's loot, in basis points.</summary>
        public const int LootChanceBp = 200, KnightChanceBp = 2500;
        /// <summary>Maximum health added by every worn piece, and stamina added by the whole set, in basis points.</summary>
        public const int HealthPerPieceBp = 100, FullSetStaminaBp = 2500;
        /// <summary>The kind of enemy the game calls a knight (MonsterState.Kind).</summary>
        public const int KnightKind = 1;
        private const ulong Salt = 0x3C9E5B71D2A84F06UL;

        public static int ChanceBpFor(LootItem item) { return item != null && item.EnemyKind == KnightKind ? KnightChanceBp : LootChanceBp; }

        /// <summary>The piece a picked-up loot carries, or None: by its chance, among the pieces not owned yet.</summary>
        public static RegionalRelic Roll(LootItem item, WeaponEquipment gear)
        {
            return LootSetRoll.Roll(item, gear, Pieces, Salt, ChanceBpFor(item));
        }

        public static bool IsPiece(RegionalRelic id) { return Array.IndexOf(Pieces, id) >= 0; }

        /// <summary>How many pieces of the set are worn.</summary>
        public static int Worn(WeaponEquipment gear)
        {
            int worn = 0;
            if (gear != null) foreach (var id in Pieces) if (gear.IsRegionalRelicEquipped(id)) worn++;
            return worn;
        }

        /// <summary>Maximum health with <paramref name="worn"/> pieces on, from the health without the set.</summary>
        public static int WithHealthBonus(int health, int worn) { return health + (int)((long)health * worn * HealthPerPieceBp / 10000L); }

        internal static readonly ArmorPiece Helm = new ArmorPiece("nachtvesper.plate.schaller", "Schaller der Nachtvesper", ArmorSlot.Head, ArmorMaterial.Plate);
        internal static readonly ArmorPiece Cuirass = new ArmorPiece("nachtvesper.plate.kuerass", "Kürass der Nachtvesper", ArmorSlot.Chest, ArmorMaterial.Plate);
        internal static readonly ArmorPiece Gauntlets = new ArmorPiece("nachtvesper.plate.handschuhe", "Panzerhandschuhe der Nachtvesper", ArmorSlot.Hands, ArmorMaterial.Plate);
        internal static readonly ArmorPiece Greaves = new ArmorPiece("nachtvesper.plate.beinzeug", "Beinzeug der Nachtvesper", ArmorSlot.Legs, ArmorMaterial.Plate);
        internal static readonly ArmorPiece Sabatons = new ArmorPiece("nachtvesper.plate.panzerschuhe", "Panzerschuhe der Nachtvesper", ArmorSlot.Feet, ArmorMaterial.Plate);
    }

    public sealed partial class CharacterProgression
    {
        /// <summary>How many pieces of the Nachtvesper are worn (each adds one percent to maximum health).</summary>
        public int NachtvesperPiecesWorn { get { return NachtvesperSet.Worn(Equipment); } }
        /// <summary>All eight pieces worn: a quarter more stamina.</summary>
        public bool NachtvesperComplete { get { return NachtvesperPiecesWorn == NachtvesperSet.Pieces.Length; } }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The stamina pool: the combat rules' pool, a quarter larger with the whole Nachtvesper on.</summary>
        public int MaxStamina
        {
            get
            {
                int pool = CombatV2Rules.StaminaMax;
                return Progression.NachtvesperComplete ? pool + pool * NachtvesperSet.FullSetStaminaBp / 10000 : pool;
            }
        }

        /// <summary>Only for a character whose equipment is switched on, like Aschenwacht's gear.</summary>
        private void FindNachtvesper(LootItem item)
        {
            if (!Progression.Equipment.Active) return;
            var id = NachtvesperSet.Roll(item, Progression.Equipment);
            if (id != RegionalRelic.None && Progression.Equipment.ClaimRegionalRelic(Progression, id)) LastGearFind = id;
        }

        /// <summary>
        /// Taking a piece off can shrink the health and stamina pools; what the hero has left is kept within them.
        /// </summary>
        private void KeepWithinPools()
        {
            if (PlayerHealth > MaxPlayerHealth) PlayerHealth = MaxPlayerHealth;
            if (Stamina > MaxStamina) Stamina = MaxStamina;
        }
    }
}
