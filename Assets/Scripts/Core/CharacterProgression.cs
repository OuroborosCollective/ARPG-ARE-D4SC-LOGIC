using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>
    /// Permanent character progression. No season clock, rollover or maximum level.
    /// Levels and lifetime statistics are arbitrary-precision integers, separate from finite combat values.
    /// Save format AC1 has a stable non-cryptographic corruption checksum, not an anti-cheat signature.
    /// </summary>
    public sealed class CharacterProgression
    {
        private const string Format = "AC1";
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public BigInteger TotalExperience { get; private set; }
        public BigInteger LifetimeKills { get; private set; }
        public BigInteger LootCollected { get; private set; }
        public int WeaponBonus { get; private set; }
        public BigInteger Level { get { return LevelForExperience(TotalExperience); } }
        public BigInteger UnspentMasteryPoints { get { return Level - BigInteger.One; } }
        public BigInteger ExperienceToNextLevel
        {
            get { return RequiredExperienceForLevel(Level + BigInteger.One) - TotalExperience; }
        }

        public static BigInteger RequiredExperienceForLevel(BigInteger level)
        {
            if (level < BigInteger.One) throw new ArgumentOutOfRangeException(nameof(level));
            // A quadratic XP curve guarantees a meaningful next level for *every* finite XP sum.
            return new BigInteger(75) * (level - BigInteger.One) * level;
        }

        public static BigInteger LevelForExperience(BigInteger experience)
        {
            if (experience < BigInteger.Zero) throw new ArgumentOutOfRangeException(nameof(experience));
            BigInteger low = BigInteger.One;
            BigInteger high = new BigInteger(2);
            while (RequiredExperienceForLevel(high) <= experience) high <<= 1;
            while (low + BigInteger.One < high)
            {
                BigInteger middle = (low + high) >> 1;
                if (RequiredExperienceForLevel(middle) <= experience) low = middle;
                else high = middle;
            }
            return low;
        }

        public void EarnExperience(BigInteger amount)
        {
            if (amount < BigInteger.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
            TotalExperience += amount;
        }

        public void RecordKill() { LifetimeKills += BigInteger.One; }

        public void AddWeaponUpgrade(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            // The prototype's *combat* stats are intentionally finite; character level is not.
            WeaponBonus = amount > int.MaxValue - WeaponBonus ? int.MaxValue : WeaponBonus + amount;
            LootCollected += BigInteger.One;
        }

        private string Payload()
        {
            return Format + "|" + TotalExperience.ToString(CultureInfo.InvariantCulture) +
                "|" + WeaponBonus.ToString(CultureInfo.InvariantCulture) +
                "|" + LootCollected.ToString(CultureInfo.InvariantCulture) +
                "|" + LifetimeKills.ToString(CultureInfo.InvariantCulture);
        }

        private static ulong HashText(string payload)
        {
            ulong hash = Offset;
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            unchecked
            {
                foreach (byte value in bytes)
                {
                    hash ^= value;
                    hash *= Prime;
                }
            }
            return hash;
        }

        public ulong StableHash { get { return HashText(Payload()); } }

        public string SaveString()
        {
            return Payload() + "|" + StableHash.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static bool CanonicalNonNegative(string text, out BigInteger value)
        {
            value = BigInteger.Zero;
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
                if (text[i] < '0' || text[i] > '9') return false;
            if (text.Length > 1 && text[0] == '0') return false;
            return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
                value >= BigInteger.Zero;
        }

        public static bool TryLoad(string saved, out CharacterProgression progress)
        {
            progress = null;
            if (string.IsNullOrEmpty(saved)) return false;
            string[] parts = saved.Split('|');
            if (parts.Length != 6 || parts[0] != Format || parts[5].Length != 16) return false;
            BigInteger xp, loot, kills, gear;
            if (!CanonicalNonNegative(parts[1], out xp) ||
                !CanonicalNonNegative(parts[2], out gear) || gear > int.MaxValue ||
                !CanonicalNonNegative(parts[3], out loot) ||
                !CanonicalNonNegative(parts[4], out kills)) return false;
            string payload = string.Join("|", parts, 0, 5);
            ulong checksum;
            if (!ulong.TryParse(parts[5], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out checksum) ||
                checksum != HashText(payload) ||
                checksum.ToString("X16", CultureInfo.InvariantCulture) != parts[5]) return false;
            progress = new CharacterProgression {
                TotalExperience = xp,
                WeaponBonus = (int)gear,
                LootCollected = loot,
                LifetimeKills = kills
            };
            return true;
        }
    }
}