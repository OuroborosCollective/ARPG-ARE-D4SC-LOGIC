using System;
using System.Collections.Generic;

namespace AreArpg.Core
{
    /// <summary>Five ordered armour slots per set, rolled only on a remaining canonical kill receipt in its own region.</summary>
    public sealed class ThreeOrderSet
    {
        public static readonly ThreeOrderSet Rostkeil = new ThreeOrderSet("Rostkeil", "Rostkeil", RegionalRelic.Rostkeil_Kopf,
            ArmorMaterial.Plate, 2000, 0x395AE188EA203B59UL, "+2 / +4 Prozentpunkte Schutz");
        public static readonly ThreeOrderSet Moorvogt = new ThreeOrderSet("Moorvogt", "Moorvogt", RegionalRelic.Moorvogt_Kopf,
            ArmorMaterial.Leather, 2500, 0x01358743B512202FUL, "−1 / −2 Ausdauer je Ausweichen");
        public static readonly ThreeOrderSet Konvent = new ThreeOrderSet("Konvent der Roten Glut", "Konvent_der_Roten_Glut", RegionalRelic.Konvent_Kopf,
            ArmorMaterial.Cloth, 1667, 0xEB78B10C0AA50347UL, "+5 / +10 Wundschluss-Basisheilung");
        public static readonly IReadOnlyList<ThreeOrderSet> All = Array.AsReadOnly(new[] { Rostkeil, Moorvogt, Konvent });

        public readonly string Name, AssetName, BonusText;
        public readonly int ChanceBp;
        public readonly IReadOnlyList<RegionalRelic> Pieces;
        private readonly RegionalRelic[] _ids = new RegionalRelic[ArmorRules.SlotCount];
        private readonly ArmorPiece[] _pieces = new ArmorPiece[ArmorRules.SlotCount];
        private readonly ulong _salt;

        private ThreeOrderSet(string name, string assetName, RegionalRelic first, ArmorMaterial material, int chance, ulong salt, string bonus)
        {
            Name = name; AssetName = assetName; ChanceBp = chance; _salt = salt; BonusText = bonus;
            for (int i = 0; i < _ids.Length; i++)
            {
                var id = (RegionalRelic)((int)first + i);
                _ids[i] = id;
                _pieces[i] = new ArmorPiece("orders." + assetName.Replace("_", "").ToLowerInvariant() + "." + ((ArmorSlot)i).ToString().ToLowerInvariant(),
                    RegionalRelics.Name(id), (ArmorSlot)i, material);
            }
            Pieces = Array.AsReadOnly(_ids);
        }

        public static ThreeOrderSet For(RegionalRelic id)
        {
            foreach (var set in All)
                if (id >= set._ids[0] && id <= set._ids[set._ids.Length - 1]) return set;
            return null;
        }

        public ArmorPiece Armor(RegionalRelic id) => _pieces[(int)id - (int)_ids[0]];
        public int Worn(WeaponEquipment gear)
        {
            int count = 0;
            foreach (var id in _ids) if (gear.IsRegionalRelicEquipped(id)) count++;
            return count;
        }
        public int Tier(WeaponEquipment gear)
        {
            int count = Worn(gear);
            return count == 5 ? 2 : count >= 3 ? 1 : 0;
        }
        internal RegionalRelic Roll(LootItem receipt, WeaponEquipment gear) => LootSetRoll.Roll(receipt, gear, _ids, _salt, ChanceBp);
    }

    public sealed partial class ArenaSimulation
    {
        public ThreeOrderSet LocalOrderSet
        {
            get
            {
                if (InDungeon && Progression.Dungeon.Depth == 1) return ThreeOrderSet.Rostkeil;
                if (InDungeon && Progression.Dungeon.Depth == 3) return ThreeOrderSet.Konvent;
                return InWoodlandRegion && InForest ? ThreeOrderSet.Moorvogt : null;
            }
        }

        private void FindThreeOrderPiece(LootItem item)
        {
            var gear = Progression.Equipment;
            if (!gear.Active || item.KillOrdinal < gear.FirstOrdinal) return;
            var id = LocalOrderSet?.Roll(item, gear) ?? RegionalRelic.None;
            if (id != RegionalRelic.None && gear.ClaimRegionalRelic(Progression, id)) LastGearFind = id;
        }
    }
}
