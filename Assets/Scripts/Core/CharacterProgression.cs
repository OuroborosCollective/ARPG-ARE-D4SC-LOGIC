using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    public enum MasteryPath { Might = 0, Vitality = 1, Resolve = 2 }

    /// <summary>
    /// Permanent progress without a level cap. AC5 adds local NPC memory; AC1/AC2/AC3/AC4 remain readable.
    /// The checksum detects accidental damage, not malicious tampering or cloud ownership.
    /// </summary>
    public sealed class CharacterProgression
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private readonly BigInteger[] _practice = new BigInteger[4];
        public SocialLedger Social { get; private set; } = new SocialLedger();

        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= 4) throw new ArgumentOutOfRangeException(nameof(slot));
        }

        public BigInteger PracticeExperience(int slot) { ValidateSlot(slot); return _practice[slot]; }
        public BigInteger PracticeLevel(int slot) { return LevelForExperience(PracticeExperience(slot)); }
        public int PracticeDamageBonusBp(int slot)
        {
            return (int)BigInteger.Min(PracticeLevel(slot) - BigInteger.One, 50) * 100;
        }
        public int PracticeHealingBonus { get { return (int)BigInteger.Min(PracticeLevel(2) - BigInteger.One, 100) * 2; } }

        // Only simulation-confirmed effective damage/healing calls this; UI cannot award training.
        internal void RecordPractice(int slot, BigInteger amount)
        {
            ValidateSlot(slot);
            if (amount < BigInteger.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
            _practice[slot] += amount;
        }
        public BigInteger TotalExperience { get; private set; }
        public BigInteger LifetimeKills { get; private set; }
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

        public void RecordKill() { LifetimeKills += BigInteger.One; }
        public void AddWeaponUpgrade(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            WeaponBonus = amount > int.MaxValue - WeaponBonus ? int.MaxValue : WeaponBonus + amount;
            LootCollected += BigInteger.One;
            Social.ProvisionFromLoot();
        }

        private string Payload()
        {
            return "AC5|" + TotalExperience.ToString(CultureInfo.InvariantCulture) +
                "|" + WeaponBonus.ToString(CultureInfo.InvariantCulture) +
                "|" + LootCollected.ToString(CultureInfo.InvariantCulture) +
                "|" + LifetimeKills.ToString(CultureInfo.InvariantCulture) +
                "|" + MightRank.ToString(CultureInfo.InvariantCulture) +
                "|" + VitalityRank.ToString(CultureInfo.InvariantCulture) +
                "|" + ResolveRank.ToString(CultureInfo.InvariantCulture) +
                "|" + UnlockedCryptDepth.ToString(CultureInfo.InvariantCulture) +
                "|" + _practice[0].ToString(CultureInfo.InvariantCulture) +
                "|" + _practice[1].ToString(CultureInfo.InvariantCulture) +
                "|" + _practice[2].ToString(CultureInfo.InvariantCulture) +
                "|" + _practice[3].ToString(CultureInfo.InvariantCulture) +
                "|" + Social.Token();
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

        public static bool TryLoad(string saved, out CharacterProgression progress)
        {
            progress = null;
            if (string.IsNullOrEmpty(saved)) return false;
            string[] parts = saved.Split('|');
            bool legacy = parts[0] == "AC1" && parts.Length == 6;
            bool ac2 = parts[0] == "AC2" && parts.Length == 9;
            bool ac3 = parts[0] == "AC3" && parts.Length == 10;
            bool ac4 = parts[0] == "AC4" && parts.Length == 14;
            bool ac5 = parts[0] == "AC5" && parts.Length == 15;
            bool current = ac2 || ac3 || ac4 || ac5;
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
            if ((ac3 || ac4 || ac5) && (!CanonicalNonNegative(parts[8], out unlockedDepth) ||
                unlockedDepth < BigInteger.One)) return false;
            var practice = new BigInteger[4];
            if (ac4 || ac5)
                for (int slot = 0; slot < practice.Length; slot++)
                    if (!CanonicalNonNegative(parts[9 + slot], out practice[slot])) return false;
            SocialLedger social = new SocialLedger();
            if (ac5 && !SocialLedger.TryRead(parts[13], out social)) return false;
            // A valid checksum is necessary but does not make an invalid progression valid.
            ulong checksum;
            if (!ulong.TryParse(parts[last], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out checksum) ||
                checksum != HashText(string.Join("|", parts, 0, last)) ||
                checksum.ToString("X16", CultureInfo.InvariantCulture) != parts[last]) return false;
            if (might + vitality + resolve >= LevelForExperience(xp)) return false;
            progress = new CharacterProgression {
                TotalExperience = xp, WeaponBonus = (int)weapon,
                LootCollected = loot, LifetimeKills = kills,
                MightRank = might, VitalityRank = vitality, ResolveRank = resolve,
                UnlockedCryptDepth = unlockedDepth, Social = social
            };
            for (int slot = 0; slot < practice.Length; slot++) progress._practice[slot] = practice[slot];
            return true;
        }
    }
}


