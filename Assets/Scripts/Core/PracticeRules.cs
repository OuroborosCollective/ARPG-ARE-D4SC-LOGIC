using System;
using System.Numerics;

namespace AreArpg.Core
{
    /// <summary>Pure versioned learning rules; CharacterProgression retains all practice XP and levels.</summary>
    public static class PracticeRules
    {
        public const int Revision = 1;

        /// <summary>Bind stable discipline identities to the existing four persisted practice slots.</summary>
        public static string DisciplineId(int slot)
        {
            switch (slot)
            {
                case 0: return "practice.blade";
                case 1: return "practice.force";
                case 2: return "practice.guard";
                case 3: return "practice.ruin";
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        /// <summary>Only effective damage or healing earns practice: one per ten HP, minimum one for positive effects.</summary>
        public static BigInteger ExperienceFromEffectiveAmount(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return amount == 0 ? BigInteger.Zero : new BigInteger(Math.Max(1, amount / 10));
        }

        /// <summary>Preserve the existing finite damage bonus while allowing arbitrary BigInteger practice levels.</summary>
        public static int DamageBonusBp(BigInteger level)
        {
            if (level < BigInteger.One) throw new ArgumentOutOfRangeException(nameof(level));
            return (int)BigInteger.Min(level - BigInteger.One, 50) * 100;
        }

        /// <summary>Preserve the existing finite Guard bonus independently from the unbounded progression curve.</summary>
        public static int HealingBonus(BigInteger level)
        {
            if (level < BigInteger.One) throw new ArgumentOutOfRangeException(nameof(level));
            return (int)BigInteger.Min(level - BigInteger.One, 100) * 2;
        }
    }
}
