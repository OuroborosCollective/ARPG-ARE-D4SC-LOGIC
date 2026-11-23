using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AreArpg.Core
{
    public enum ArmorSlot { Head = 0, Chest = 1, Hands = 2, Legs = 3, Feet = 4 }
    public enum ArmorMaterial { Cloth = 0, Leather = 1, Chain = 2, Plate = 3 }

    /// <summary>One wearable armor piece. Pieces come from crafting and rewards, never from random loot (#127).</summary>
    public sealed class ArmorPiece
    {
        public string Id { get; }
        public string Name { get; }
        public ArmorSlot Slot { get; }
        public ArmorMaterial Material { get; }
        internal ArmorPiece(string id, string name, ArmorSlot slot, ArmorMaterial material)
        {
            if (!LootCatalog.ValidId(id) || string.IsNullOrEmpty(name)) throw new ArgumentException("Invalid armor piece");
            Id = id; Name = name; Slot = slot; Material = material;
        }
    }

    /// <summary>
    /// Armor values (owner decisions 2026-11-23, issue #127 with the healing course): physical protection cloth 0 %,
    /// leather 15 %, chain 30 %, plate 45 %; outgoing healing 100/75/50/25 % (owner's Heilkunst draft); each slot weighs
    /// chest 40, legs 25, head 15, hands 10, feet 10. An empty slot neither protects nor hinders. Integer basis points,
    /// one rounding at the end. First test values: tune after playtests.
    /// </summary>
    public static class ArmorRules
    {
        public const int Revision = 1;
        public const int SlotCount = 5;
        private static readonly int[] SlotWeightBp = { 1500, 4000, 1000, 2500, 1000 };
        private static readonly int[] ProtectionBp = { 0, 1500, 3000, 4500 };
        private static readonly int[] HealingBp = { 10000, 7500, 5000, 2500 };

        public static int SlotWeight(ArmorSlot slot) { return SlotWeightBp[(int)slot]; }
        public static int MaterialProtection(ArmorMaterial material) { return ProtectionBp[(int)material]; }
        public static int MaterialHealing(ArmorMaterial material) { return HealingBp[(int)material]; }

        /// <summary>Physical protection of what is worn, in basis points (full plate = 4500).</summary>
        public static int Protection(IReadOnlyList<ArmorPiece> worn)
        {
            long sum = 0;
            for (int s = 0; s < SlotCount; s++)
                if (worn[s] != null) sum += (long)SlotWeightBp[s] * ProtectionBp[(int)worn[s].Material];
            return (int)(sum / 10000);
        }

        /// <summary>Outgoing healing of what is worn, in basis points (nothing or cloth = 10000, full plate = 2500).</summary>
        public static int HealingEfficiency(IReadOnlyList<ArmorPiece> worn)
        {
            long sum = 0;
            for (int s = 0; s < SlotCount; s++)
                sum += (long)SlotWeightBp[s] * (worn[s] == null ? 10000 : HealingBp[(int)worn[s].Material]);
            return (int)(sum / 10000);
        }
    }

    /// <summary>The fixed armor pieces of revision 1. New pieces get new ids; existing ids are never changed.</summary>
    public static class ArmorCatalog
    {
        public const int Revision = 1;
        private static readonly ArmorPiece[] Pieces =
        {
            new ArmorPiece("armor.cloth.robe.healer", "Robe der Feldheilerin", ArmorSlot.Chest, ArmorMaterial.Cloth),
            new ArmorPiece("armor.cloth.hood", "Stoffkapuze", ArmorSlot.Head, ArmorMaterial.Cloth),
            new ArmorPiece("armor.cloth.wraps", "Stoffwickel", ArmorSlot.Hands, ArmorMaterial.Cloth),
            new ArmorPiece("armor.cloth.trousers", "Stoffhose", ArmorSlot.Legs, ArmorMaterial.Cloth),
            new ArmorPiece("armor.cloth.shoes", "Stoffschuhe", ArmorSlot.Feet, ArmorMaterial.Cloth),
            new ArmorPiece("armor.leather.cap", "Lederkappe", ArmorSlot.Head, ArmorMaterial.Leather),
            new ArmorPiece("armor.leather.jerkin", "Lederwams", ArmorSlot.Chest, ArmorMaterial.Leather),
            new ArmorPiece("armor.leather.gloves", "Lederhandschuhe", ArmorSlot.Hands, ArmorMaterial.Leather),
            new ArmorPiece("armor.leather.trousers", "Lederhose", ArmorSlot.Legs, ArmorMaterial.Leather),
            new ArmorPiece("armor.leather.boots", "Lederstiefel", ArmorSlot.Feet, ArmorMaterial.Leather),
            new ArmorPiece("armor.chain.coif", "Kettenhaube", ArmorSlot.Head, ArmorMaterial.Chain),
            new ArmorPiece("armor.chain.shirt", "Kettenhemd", ArmorSlot.Chest, ArmorMaterial.Chain),
            new ArmorPiece("armor.chain.gloves", "Kettenhandschuhe", ArmorSlot.Hands, ArmorMaterial.Chain),
            new ArmorPiece("armor.chain.leggings", "Kettenbeinlinge", ArmorSlot.Legs, ArmorMaterial.Chain),
            new ArmorPiece("armor.chain.boots", "Kettenschuhe", ArmorSlot.Feet, ArmorMaterial.Chain),
            new ArmorPiece("armor.plate.helm", "Plattenhelm", ArmorSlot.Head, ArmorMaterial.Plate),
            new ArmorPiece("armor.plate.cuirass", "Plattenharnisch", ArmorSlot.Chest, ArmorMaterial.Plate),
            new ArmorPiece("armor.plate.gauntlets", "Plattenhandschuhe", ArmorSlot.Hands, ArmorMaterial.Plate),
            new ArmorPiece("armor.plate.greaves", "Plattenbeinschienen", ArmorSlot.Legs, ArmorMaterial.Plate),
            new ArmorPiece("armor.plate.sabatons", "Plattenschuhe", ArmorSlot.Feet, ArmorMaterial.Plate)
        };
        public static int Count { get { return Pieces.Length; } }
        public static ArmorPiece Get(int index) { return Pieces[index]; }
        public static ArmorPiece Find(string id)
        {
            foreach (var piece in Pieces) if (piece.Id == id) return piece;
            return null;
        }
    }

    /// <summary>The armor a character owns and wears. Own save section "armor"; absent while nothing is owned.</summary>
    public sealed class ArmorState
    {
        private readonly List<ArmorPiece> _owned = new List<ArmorPiece>();
        private readonly ArmorPiece[] _worn = new ArmorPiece[ArmorRules.SlotCount];

        public IReadOnlyList<ArmorPiece> Owned { get { return _owned; } }
        public IReadOnlyList<ArmorPiece> Worn { get { return _worn; } }
        public bool HasAny { get { return _owned.Count != 0; } }
        public int ProtectionBp { get { return ArmorRules.Protection(_worn); } }
        public int HealingEfficiencyBp { get { return ArmorRules.HealingEfficiency(_worn); } }

        /// <summary>Receive one piece from crafting or a reward; a piece is owned at most once.</summary>
        public bool Grant(ArmorPiece piece)
        {
            if (piece == null || ArmorCatalog.Find(piece.Id) != piece || _owned.Contains(piece)) return false;
            _owned.Add(piece);
            return true;
        }

        /// <summary>Wear an owned piece in its slot, replacing what was there.</summary>
        public bool Wear(ArmorPiece piece)
        {
            if (piece == null || !_owned.Contains(piece)) return false;
            _worn[(int)piece.Slot] = piece;
            return true;
        }

        public bool TakeOff(ArmorSlot slot)
        {
            if (_worn[(int)slot] == null) return false;
            _worn[(int)slot] = null;
            return true;
        }

        internal ulong StableHash
        {
            get { return LootCatalog.HashText(Token()); }
        }

        /// <summary>"1;owned ids in order;worn id per slot (or -)".</summary>
        internal string Token()
        {
            var b = new StringBuilder("1;");
            for (int i = 0; i < _owned.Count; i++) { if (i > 0) b.Append(','); b.Append(_owned[i].Id); }
            b.Append(';');
            for (int s = 0; s < ArmorRules.SlotCount; s++) { if (s > 0) b.Append(','); b.Append(_worn[s] == null ? "-" : _worn[s].Id); }
            return b.ToString();
        }

        internal static bool TryRead(string token, out ArmorState state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] f = token.Split(';');
            if (f.Length != 3 || f[0] != "1" || f[1].Length == 0) return false;
            var s = new ArmorState();
            foreach (string id in f[1].Split(','))
                if (!s.Grant(ArmorCatalog.Find(id))) return false;
            string[] worn = f[2].Split(',');
            if (worn.Length != ArmorRules.SlotCount) return false;
            for (int slot = 0; slot < ArmorRules.SlotCount; slot++)
            {
                if (worn[slot] == "-") continue;
                var piece = ArmorCatalog.Find(worn[slot]);
                if (piece == null || (int)piece.Slot != slot || !s.Wear(piece)) return false;
            }
            state = s;
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        private ArmorState _armor = new ArmorState();
        public ArmorState Armor { get { return _armor; } }
        private const string ArmorSectionName = "armor";

        private void SyncArmorSection()
        {
            if (_armor.HasAny) SetSection(ArmorSectionName, _armor.Token());
            else _sections.Remove(ArmorSectionName);
        }

        /// <summary>A malformed armor section rejects the whole save instead of silently dropping owned pieces.</summary>
        private bool RestoreArmorSection()
        {
            if (!_sections.TryGetValue(ArmorSectionName, out string token)) return true;
            if (!ArmorState.TryRead(token, out var state)) return false;
            _armor = state;
            return true;
        }
    }
}
