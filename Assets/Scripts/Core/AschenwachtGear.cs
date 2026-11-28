using System;

namespace AreArpg.Core
{
    /// <summary>
    /// Owner decision 2026-11-26 (answer A): the Glockenwacht's armour from the first Aschenwacht archive (visored helm,
    /// cuirass, shoulder plates, limb armour) and the Aschbeter's two claw staffs from the second are rare loot, fitted to the
    /// hero. They are regional pieces (<see cref="RegionalRelic"/>) that never lie anywhere by themselves: once the wanderer
    /// has been in Aschenwacht, every piece of ordinary loot he picks up may carry one he does not own yet. The chance follows
    /// the loot's rarity (1, 2, 4 and 10 percent) and is decided by the loot's own receipt, so the same loot always gives the
    /// same find. The random weapon catalog stays as it is; this is an exception to "armor never from random loot" (#127)
    /// that the owner asked for.
    ///
    /// The armour protects like plate in the five weighted armour slots of <see cref="ArmorRules"/>: the helm as the head,
    /// the cuirass as the chest, the limb armour as the legs, the shoulder plates in the place of the hands (10 percent).
    /// Worn, a piece takes that slot's place over a crafted piece; it costs healing like plate.
    /// </summary>
    public static class AschenwachtGear
    {
        public static readonly RegionalRelic[] Pieces =
        {
            RegionalRelic.A_Visierhelm, RegionalRelic.A_Kuerass, RegionalRelic.A_Schulterplatten, RegionalRelic.A_Gliedpanzer,
            RegionalRelic.A_KlauenstabEinhand, RegionalRelic.A_KlauenstabZweihand
        };

        private static readonly int[] ChanceBp = { 100, 200, 400, 1000 };
        public static int ChanceBpFor(ItemRarity rarity) { return ChanceBp[(int)rarity]; }
        private const ulong Salt = 0xA5C4B37E9D1F2A61UL;

        internal static readonly ArmorPiece Helm = new ArmorPiece("aschenwacht.plate.visierhelm", "Visierhelm der Glockenwacht", ArmorSlot.Head, ArmorMaterial.Plate);
        internal static readonly ArmorPiece Cuirass = new ArmorPiece("aschenwacht.plate.kuerass", "Kürass der Glockenwacht", ArmorSlot.Chest, ArmorMaterial.Plate);
        internal static readonly ArmorPiece Shoulders = new ArmorPiece("aschenwacht.plate.schulterplatten", "Schulterplatten der Glockenwacht", ArmorSlot.Hands, ArmorMaterial.Plate);
        internal static readonly ArmorPiece Limbs = new ArmorPiece("aschenwacht.plate.gliedpanzer", "Gliedpanzer der Glockenwacht", ArmorSlot.Legs, ArmorMaterial.Plate);

        /// <summary>Whether the wanderer has been in Aschenwacht: from then on its gear can turn up under loot.</summary>
        public static bool Open(CharacterProgression p) { return p.EntryAct >= 2 || p.InCourt; }

        /// <summary>The piece a picked-up loot carries, or None: by its rarity's chance, among the pieces not owned yet.</summary>
        public static RegionalRelic Roll(LootItem item, WeaponEquipment gear)
        {
            if (item == null || gear == null) return RegionalRelic.None;
            ulong h = item.StableHash ^ Salt;
            unchecked { h ^= h >> 33; h *= 0xFF51AFD7ED558CCDUL; h ^= h >> 33; h *= 0xC4CEB9FE1A85EC53UL; h ^= h >> 33; }
            if ((int)(h % 10000UL) >= ChanceBpFor(item.Rarity)) return RegionalRelic.None;
            int missing = 0;
            foreach (var id in Pieces) if (!gear.OwnsRegionalRelic(id)) missing++;
            if (missing == 0) return RegionalRelic.None;
            int pick = (int)((h >> 20) % (ulong)missing);
            foreach (var id in Pieces)
            {
                if (gear.OwnsRegionalRelic(id)) continue;
                if (pick-- == 0) return id;
            }
            return RegionalRelic.None;
        }
    }

    public sealed partial class CharacterProgression
    {
        /// <summary>What is worn in the five weighted armour slots: crafted pieces, with Aschenwacht's and the Nachtvesper's plate in front of them.</summary>
        private ArmorPiece[] EffectiveArmor()
        {
            var worn = new ArmorPiece[ArmorRules.SlotCount];
            for (int s = 0; s < worn.Length; s++) worn[s] = Armor.Worn[s];
            var gear = Equipment;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.A_Visierhelm)) worn[(int)ArmorSlot.Head] = AschenwachtGear.Helm;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.A_Kuerass)) worn[(int)ArmorSlot.Chest] = AschenwachtGear.Cuirass;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.A_Schulterplatten)) worn[(int)ArmorSlot.Hands] = AschenwachtGear.Shoulders;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.A_Gliedpanzer)) worn[(int)ArmorSlot.Legs] = AschenwachtGear.Limbs;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.N_Schaller)) worn[(int)ArmorSlot.Head] = NachtvesperSet.Helm;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.N_Kuerass)) worn[(int)ArmorSlot.Chest] = NachtvesperSet.Cuirass;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.N_Handschuhe)) worn[(int)ArmorSlot.Hands] = NachtvesperSet.Gauntlets;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.N_Beinzeug)) worn[(int)ArmorSlot.Legs] = NachtvesperSet.Greaves;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.N_Panzerschuhe)) worn[(int)ArmorSlot.Feet] = NachtvesperSet.Sabatons;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.F_Gugel)) worn[(int)ArmorSlot.Head] = RotfaehrteSet.Hood;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.F_Wams)) worn[(int)ArmorSlot.Chest] = RotfaehrteSet.Jerkin;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.F_Handschuhe)) worn[(int)ArmorSlot.Hands] = RotfaehrteSet.Gloves;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.F_Beinkleid)) worn[(int)ArmorSlot.Legs] = RotfaehrteSet.Breeches;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.F_Stiefel)) worn[(int)ArmorSlot.Feet] = RotfaehrteSet.Boots;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.K_Kapuze)) worn[(int)ArmorSlot.Head] = RusskantorSet.Hood;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.K_Kittel)) worn[(int)ArmorSlot.Chest] = RusskantorSet.Smock;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.K_Handschuhe)) worn[(int)ArmorSlot.Hands] = RusskantorSet.Gloves;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.K_Beinkleid)) worn[(int)ArmorSlot.Legs] = RusskantorSet.Breeches;
            if (gear.IsRegionalRelicEquipped(RegionalRelic.K_Stiefel)) worn[(int)ArmorSlot.Feet] = RusskantorSet.Boots;
            foreach (var order in ThreeOrderSet.All)
                foreach (var id in order.Pieces)
                    if (gear.IsRegionalRelicEquipped(id))
                    {
                        var piece = order.Armor(id);
                        worn[(int)piece.Slot] = piece;
                    }
            return worn;
        }

        /// <summary>Physical protection of everything worn, in basis points.</summary>
        public int WornProtectionBp { get { return ArmorRules.Protection(EffectiveArmor()) + 200 * ThreeOrderSet.Rostkeil.Tier(Equipment); } }
        /// <summary>Outgoing healing of everything worn, in basis points.</summary>
        public int WornHealingEfficiencyBp { get { return ArmorRules.HealingEfficiency(EffectiveArmor()); } }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The Aschenwacht piece the last pickup carried (None when it carried none); presentation only.</summary>
        public RegionalRelic LastGearFind { get; private set; }

        /// <summary>
        /// Only for a character whose equipment is switched on (every game the runtime starts does so): a find never turns
        /// an older save format into the equipment format behind the player's back.
        /// </summary>
        private void FindAschenwachtGear(LootItem item)
        {
            if (!Progression.Equipment.Active || !AschenwachtGear.Open(Progression)) return;
            var id = AschenwachtGear.Roll(item, Progression.Equipment);
            if (id != RegionalRelic.None && Progression.Equipment.ClaimRegionalRelic(Progression, id)) LastGearFind = id;
        }
    }
}
