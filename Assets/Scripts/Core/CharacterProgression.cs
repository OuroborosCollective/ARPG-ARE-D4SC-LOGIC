using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    public enum MasteryPath { Might = 0, Vitality = 1, Resolve = 2 }

    /// <summary>
    /// Permanent progress, no season clock, no level cap. AC1/AC2 are read-only legacy; AC3 includes unlocked chapter depth.
    /// The checksum detects accidental damage, not malicious tampering or cloud ownership.
    /// </summary>
    public sealed class CharacterProgression
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
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
        }

        private string Payload()
        {
            return "AC3|" + TotalExperience.ToString(CultureInfo.InvariantCulture) +
                "|" + WeaponBonus.ToString(CultureInfo.InvariantCulture) +
                "|" + LootCollected.ToString(CultureInfo.InvariantCulture) +
                "|" + LifetimeKills.ToString(CultureInfo.InvariantCulture) +
                "|" + MightRank.ToString(CultureInfo.InvariantCulture) +
                "|" + VitalityRank.ToString(CultureInfo.InvariantCulture) +
                "|" + ResolveRank.ToString(CultureInfo.InvariantCulture) +
                "|" + UnlockedCryptDepth.ToString(CultureInfo.InvariantCulture);
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
            bool current = ac2 || ac3;
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
            if (ac3 && (!CanonicalNonNegative(parts[8], out unlockedDepth) ||
                unlockedDepth < BigInteger.One)) return false;
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
                UnlockedCryptDepth = unlockedDepth
            };
            return true;
        }
    }
}
