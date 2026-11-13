using System.Linq;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    public enum MasteryPath { Might = 0, Vitality = 1, Resolve = 2 }

    /// <summary>
    /// Permanent progress without a level cap. AC5 adds local NPC memory; AC1/AC2/AC3/AC4 remain readable.
    /// The checksum detects accidental damage, not malicious tampering or cloud ownership.
    /// </summary>
    public sealed partial class CharacterProgression
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private readonly BigInteger[] _practice = new BigInteger[4];
        private readonly List<LootItem> _items = new List<LootItem>();
        private ReadOnlyCollection<LootItem> _itemsView;
        /// <summary>Collected upgrade receipts; only the existing progression owner can mutate them.</summary>
        public ReadOnlyCollection<LootItem> Items { get { return _itemsView ?? (_itemsView = _items.AsReadOnly()); } }
        /// <summary>Accept each confirmed kill ordinal once, retain stable order and apply the existing upgrade path.</summary>
        internal bool CollectItem(LootItem item)
        {
            if (item == null || item.KillOrdinal > LootOrdinal) return false;
            foreach (var existing in _items) if (existing.KillOrdinal == item.KillOrdinal) return false;
            int index = 0;
            while (index < _items.Count && _items[index].KillOrdinal < item.KillOrdinal) index++;
            _items.Insert(index, item);
            AddWeaponUpgrade(item.AffixValue);
            return true;
        }
        /// <summary>Join acquired receipts in canonical kill order for checkpoint persistence.</summary>
        private string ItemToken()
        {
            var b = new StringBuilder();
            foreach (var item in _items) { if (b.Length != 0) b.Append(';'); b.Append(item.Token()); }
            return b.ToString();
        }
        public SocialLedger Social { get; private set; } = new SocialLedger();
        /// <summary>Zero retains legacy chapter semantics; entry revision 1 has Crypt=1 and Village=2.</summary>
        public int EntryAct { get; private set; }
        /// <summary>Create a separate explicit entry profile; never reinterpret an existing save.</summary>
        public static CharacterProgression CreateEntryProfile() { return new CharacterProgression { EntryAct = 1 }; }
        /// <summary>Only the canonical cleared-crypt transition may advance this checkpoint once.</summary>
        internal bool EnterVillage()
        {
            if (EntryAct != 1) return false;
            EntryAct = 2;
            return true;
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= 4) throw new ArgumentOutOfRangeException(nameof(slot));
        }

        public BigInteger PracticeExperience(int slot) { ValidateSlot(slot); return _practice[slot]; }
        public BigInteger PracticeLevel(int slot) { return LevelForExperience(PracticeExperience(slot)); }
        public int PracticeDamageBonusBp(int slot)
        {
            return PracticeRules.DamageBonusBp(PracticeLevel(slot));
        }
        public int PracticeHealingBonus { get { return PracticeRules.HealingBonus(PracticeLevel(2)); } }

        // Only simulation-confirmed effective damage/healing calls this; UI cannot award training.
        internal void RecordPractice(int slot, BigInteger amount)
        {
            ValidateSlot(slot);
            if (amount < BigInteger.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
            _practice[slot] += amount;
        }
        public BigInteger TotalExperience { get; private set; }
        // The legacy save field remains the monotonic defeat/loot ordinal. AC18v3 separately
        // attributes new companion defeats, without rewriting older historical rewards.
        internal BigInteger LootOrdinal { get; private set; }
        public BigInteger LifetimeKills => LootOrdinal - Companion.LifetimeKills;
        public BigInteger LootCollected { get; private set; }
        public int WeaponBonus { get; private set; }
        public BigInteger MightRank { get; private set; }
        public BigInteger VitalityRank { get; private set; }
        public BigInteger ResolveRank { get; private set; }
        public BigInteger UnlockedCryptDepth { get; private set; } = BigInteger.One;
        public BigInteger Level { get { return LevelForExperience(TotalExperience); } }
        public BigInteger SpentMasteryPoints { get { return MightRank + VitalityRank + ResolveRank; } }
        public BigInteger UnspentMasteryPoints { get { return Level - BigInteger.One - SpentMasteryPoints; } }
        public BigInteger ExperienceToNextLevel { get { return RequiredExperienceForLevel(Level + BigInteger.One) - TotalExperience; } }

        public static BigInteger RequiredExperienceForLevel(BigInteger level)
        {
            if (level < BigInteger.One) throw new ArgumentOutOfRangeException(nameof(level));
            return new BigInteger(75) * (level - BigInteger.One) * level;
        }

        public static BigInteger LevelForExperience(BigInteger experience)
        {
            if (experience < BigInteger.Zero) throw new ArgumentOutOfRangeException(nameof(experience));
            BigInteger low = BigInteger.One, high = new BigInteger(2);
            while (RequiredExperienceForLevel(high) <= experience) high <<= 1;
            while (low + BigInteger.One < high)
            {
                BigInteger mid = (low + high) >> 1;
                if (RequiredExperienceForLevel(mid) <= experience) low = mid;
                else high = mid;
            }
            return low;
        }

        public void EarnExperience(BigInteger amount)
        {
            if (amount < BigInteger.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
            TotalExperience += amount;
        }

        public bool TrySpendMastery(MasteryPath path)
        {
            if (UnspentMasteryPoints <= BigInteger.Zero) return false;
            switch (path)
            {
                case MasteryPath.Might: MightRank++; return true;
                case MasteryPath.Vitality: VitalityRank++; return true;
                case MasteryPath.Resolve: ResolveRank++; return true;
                default: return false;
            }
        }

        // Effects use a bounded gameplay scaling even when character ranks/XP are arbitrarily large.
        // This avoids integer overflow and preserves finite deterministic combat arithmetic.
        public int MightDamageBonusBp { get { return (int)BigInteger.Min(MightRank, 100) * 100; } }
        public int VitalityHealthBonus { get { return (int)BigInteger.Min(VitalityRank, 1000) * 20; } }
        public int ResolveHealBonus { get { return (int)BigInteger.Min(ResolveRank, 1000) * 10; } }

        /// <summary>Record only the consecutively cleared depth, never an arbitrary skip.</summary>
        internal bool TryUnlockNextCryptDepth(BigInteger clearedDepth)
        {
            if (clearedDepth != UnlockedCryptDepth) return false;
            UnlockedCryptDepth += BigInteger.One;
            return true;
        }

        public void RecordKill() { LootOrdinal += BigInteger.One; }
        internal void RecordCompanionKill() { LootOrdinal += BigInteger.One; Companion.LifetimeKills += BigInteger.One; }
        public void AddWeaponUpgrade(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            WeaponBonus = amount > int.MaxValue - WeaponBonus ? int.MaxValue : WeaponBonus + amount;
            LootCollected += BigInteger.One;
            Social.ProvisionFromLoot();
        }

        /// <summary>Keep legacy AC5/AC7 bytes; explicit entry profiles bind revision/act and receipts in AC8.</summary>
        private string BasePayload()
        {
            return (Woodland.Active ? "AC15|" : Dungeon.Active ? "AC14|" : Rattler.Active ? "AC12|" : Investigation.Active ? "AC11|" : Road.Started ? "AC10|" : HungerStage > 0 ? "AC9|" : EntryAct > 0 ? "AC8|" : (_items.Count == 0 ? "AC5|" : "AC7|")) + TotalExperience.ToString(CultureInfo.InvariantCulture) +
                "|" + WeaponBonus.ToString(CultureInfo.InvariantCulture) +
                "|" + LootCollected.ToString(CultureInfo.InvariantCulture) +
                "|" + LootOrdinal.ToString(CultureInfo.InvariantCulture) +
                "|" + MightRank.ToString(CultureInfo.InvariantCulture) +
                "|" + VitalityRank.ToString(CultureInfo.InvariantCulture) +
                "|" + ResolveRank.ToString(CultureInfo.InvariantCulture) +
                "|" + UnlockedCryptDepth.ToString(CultureInfo.InvariantCulture) +
                "|" + _practice[0].ToString(CultureInfo.InvariantCulture) +
                "|" + _practice[1].ToString(CultureInfo.InvariantCulture) +
                "|" + _practice[2].ToString(CultureInfo.InvariantCulture) +
                "|" + _practice[3].ToString(CultureInfo.InvariantCulture) +
                "|" + Social.Token() + (EntryAct > 0 ? "|" + ItemToken() + "|" + (HungerStage > 0 ? HungerToken() : "1," + EntryAct.ToString(CultureInfo.InvariantCulture)) :
                    (_items.Count == 0 ? "" : "|" + ItemToken())) +
                (Road.Started || Investigation.Active ? "|" + Road.Token() + "|" + Social.RoadSocialToken() : "") +
                (Investigation.Active ? "|" + Investigation.Token() : "") + (Rattler.Active ? "|" + Rattler.Token() : Dungeon.Active ? "|-" : "") + (Dungeon.Active ? "|" + Dungeon.Token() : Woodland.Active ? "|-" : "") + (Woodland.Active ? "|" + Woodland.Token() : "");
        }

        private static ulong HashText(string payload)
        {
            ulong hash = Offset;
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            unchecked { foreach (byte value in bytes) { hash ^= value; hash *= Prime; } }
            return hash;
        }

        public ulong StableHash { get { return HashText(Payload()); } }
        public string SaveString() { return Payload() + "|" + StableHash.ToString("X16", CultureInfo.InvariantCulture); }

        private static bool CanonicalNonNegative(string text, out BigInteger value)
        {
            value = BigInteger.Zero;
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++) if (text[i] < '0' || text[i] > '9') return false;
            if (text.Length > 1 && text[0] == '0') return false;
            return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= BigInteger.Zero;
        }

        /// <summary>Validate checkpoint version, checksum, entry revision and item invariants; AC6 remains reserved.</summary>
        public static bool TryLoad(string saved, out CharacterProgression progress)
        {
            progress = null;
            if (string.IsNullOrEmpty(saved)) return false;
            if(saved.StartsWith("AC19|",StringComparison.Ordinal))return TryLoadFreight(saved,out progress);
            if(saved.StartsWith("AC18|",StringComparison.Ordinal))return TryLoadCompanion(saved,out progress);
            if(saved.StartsWith("AC17|",StringComparison.Ordinal))return TryLoadForge(saved,out progress);
            if(saved.StartsWith("AC16|",StringComparison.Ordinal))return TryLoadEquipment(saved,out progress);
            string[] parts = saved.Split('|');
            bool legacy = parts[0] == "AC1" && parts.Length == 6;
            bool ac2 = parts[0] == "AC2" && parts.Length == 9;
            bool ac3 = parts[0] == "AC3" && parts.Length == 10;
            bool ac4 = parts[0] == "AC4" && parts.Length == 14;
            bool ac5 = parts[0] == "AC5" && parts.Length == 15;
            bool ac7 = parts[0] == "AC7" && parts.Length == 16;
            bool ac8 = parts[0] == "AC8" && parts.Length == 17;
            bool ac15 = parts[0] == "AC15" && parts.Length == 23;
            bool ac14 = (parts[0] == "AC14" && parts.Length == 22) || (ac15 && parts[20] != "-");
            bool ac12 = (parts[0] == "AC12" && parts.Length == 21) || ((ac14 || ac15) && parts[19] != "-");
            bool ac11 = (parts[0] == "AC11" && parts.Length == 20) || ac12 || ac14 || ac15;
            bool ac10 = (parts[0] == "AC10" && parts.Length == 19) || ac11;
            bool ac9 = (parts[0] == "AC9" && parts.Length == 17) || ac10;
            bool current = ac2 || ac3 || ac4 || ac5 || ac7 || ac8 || ac9;
            if (!legacy && !current) return false;
            int last = parts.Length - 1;
            if (parts[last].Length != 16) return false;
            BigInteger xp, weapon, loot, kills;
            if (!CanonicalNonNegative(parts[1], out xp) ||
                !CanonicalNonNegative(parts[2], out weapon) || weapon > int.MaxValue ||
                !CanonicalNonNegative(parts[3], out loot) ||
                !CanonicalNonNegative(parts[4], out kills)) return false;
            BigInteger might = BigInteger.Zero, vitality = BigInteger.Zero, resolve = BigInteger.Zero;
            if (current && (!CanonicalNonNegative(parts[5], out might) ||
                !CanonicalNonNegative(parts[6], out vitality) ||
                !CanonicalNonNegative(parts[7], out resolve))) return false;
            BigInteger unlockedDepth = BigInteger.One;
            if ((ac3 || ac4 || ac5 || ac7 || ac8 || ac9) && (!CanonicalNonNegative(parts[8], out unlockedDepth) ||
                unlockedDepth < BigInteger.One)) return false;
            var practice = new BigInteger[4];
            if (ac4 || ac5 || ac7 || ac8 || ac9)
                for (int slot = 0; slot < practice.Length; slot++)
                    if (!CanonicalNonNegative(parts[9 + slot], out practice[slot])) return false;
            SocialLedger social = new SocialLedger();
            if ((ac5 || ac7 || ac8 || ac9) && !SocialLedger.TryRead(parts[13], out social)) return false;
            int entryAct = 0, hungerStage = 0;
            bool entryFromNorth = false;
            HungerChoice supplyChoice = HungerChoice.Undecided;
            if (ac9 && (!unlockedDepth.IsOne || !ReadHungerToken(parts[15], out entryAct, out entryFromNorth, out hungerStage, out supplyChoice, ac11, ac12 || ac14, ac15))) return false;
            if (ac8)
            {
                if (!unlockedDepth.IsOne || (parts[15] != "1,1" && parts[15] != "1,2")) return false;
                entryAct = parts[15] == "1,1" ? 1 : 2;
            }
            HunterEncounter road = new HunterEncounter();
            if (ac10 && (!HunterEncounter.TryRead(parts[16], out road) || (!road.Started && !ac11) || !social.ReadRoadSocial(parts[17]) ||
                (road.ResumePlayer && entryAct != 3))) return false;
            var dungeon = new CryptDungeonState();
            if (ac14 && !CryptDungeonState.TryRead(parts[20], entryAct, out dungeon)) return false;
            CryptInvestigation investigation = new CryptInvestigation();
            if (ac11 && (hungerStage != 4 || !CryptInvestigation.TryRead(parts[18], entryAct, out investigation, ac12, ac14 && (entryAct == 6 || entryAct == 7) ? dungeon.Current.Layout : null))) return false;
            var rattler = new RattlerEncounter();
            if (ac12 && (!investigation.TraceFound || !RattlerEncounter.TryRead(parts[19], out rattler, ac14 ? dungeon.Floor(3).Layout : null))) return false;
            if (ac14 && (investigation.Approach == SealApproach.Undecided ||
                ((dungeon.UnlockedDepth == 3 || entryAct == 7) && !rattler.Active) || (entryAct != 6 && entryAct != 7 && dungeon.Depth != 1) ||
                (dungeon.Floor(3).Loot.Any(l=>l.Id==42) && !rattler.Creature.IsDead))) return false;
            // A valid checksum is necessary but does not make an invalid progression valid.
            ulong checksum;
            if (!ulong.TryParse(parts[last], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out checksum) ||
                checksum != HashText(string.Join("|", parts, 0, last)) ||
                checksum.ToString("X16", CultureInfo.InvariantCulture) != parts[last]) return false;
            if (might + vitality + resolve >= LevelForExperience(xp)) return false;
            var items = new List<LootItem>();
            if (ac7 || ((ac8 || ac9) && parts[14].Length != 0))
            {
                BigInteger previousOrdinal = BigInteger.Zero;
                BigInteger receiptPoints = BigInteger.Zero;
                foreach (string token in parts[14].Split(';'))
                {
                    if (!LootItem.TryRead(token, out var item) || item.KillOrdinal <= previousOrdinal || item.KillOrdinal > kills) return false;
                    items.Add(item); previousOrdinal = item.KillOrdinal;
                    receiptPoints += item.AffixValue;
                }
                if (items.Count > loot || BigInteger.Min(receiptPoints, int.MaxValue) > weapon) return false;
            }
            if (ac14)
            {
                var ordinals = new HashSet<BigInteger>(items.Select(i => i.KillOrdinal));
                for (int d = 1; d <= 3; d++)
                    foreach (var drop in dungeon.Floor(d).Loot)
                        if (drop.Item != null && (drop.Item.KillOrdinal > kills || !ordinals.Add(drop.Item.KillOrdinal))) return false;
            }
            var woodland = new WoodlandState();
            if (ac15 && (!rattler.Reported || !WoodlandState.TryRead(parts[21], entryAct, kills, items,
                ac14 ? Enumerable.Range(1,3).SelectMany(d=>dungeon.Floor(d).Loot) : Enumerable.Empty<LootState>(), out woodland))) return false;
            progress = new CharacterProgression {
                TotalExperience = xp, WeaponBonus = (int)weapon,
                LootCollected = loot, LootOrdinal = kills,
                MightRank = might, VitalityRank = vitality, ResolveRank = resolve,
                UnlockedCryptDepth = unlockedDepth, Social = social, EntryAct = entryAct,
                HungerStage = hungerStage, SupplyChoice = supplyChoice, EntryFromNorth = entryFromNorth, Road = road, Investigation = investigation, Rattler = rattler, Dungeon = dungeon, Woodland = woodland
            };
            if (ac15 && woodland.SavedHealth > CombatRules.MaximumHealth(progress)) { progress=null; return false; }
            if (ac11 && investigation.SavedHealth > CombatRules.MaximumHealth(progress)) { progress = null; return false; }
            if (ac10 && (road.SavedHealth > CombatRules.MaximumHealth(progress) ||
                (social.RoadEventId(RoadFact.Rescue) > 0) != road.Rescued ||
                (social.RoadEventId(RoadFact.Death) > 0) != (road.HunterHealth == 0) ||
                (social.RoadEventId(RoadFact.RefusedHelp) > 0 && (road.HunterHealth > 0 || road.WitnessOpportunity < 30)) ||
                (social.RoadEventId(RoadFact.CorpseTheft) > 0) != (road.HunterHealth == 0 && road.Purse == 0)))
            { progress = null; return false; }
            for (int slot = 0; slot < practice.Length; slot++) progress._practice[slot] = practice[slot];
            progress._items.AddRange(items);
            return true;
        }
    }
}
