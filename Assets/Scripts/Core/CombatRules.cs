using System;

namespace AreArpg.Core
{
    /// <summary>Read-only derived combat values; CharacterProgression remains the sole persisted stat owner.</summary>
    public readonly struct CombatStats
    {
        public long WeaponPoints { get; }
        public int AdditiveDamageBp { get; }
        public int MaximumHealth { get; }
        public int HealingBonus { get; }

        /// <summary>Derive finite values from the current progression, widening weapon addition before arithmetic.</summary>
        public CombatStats(CharacterProgression progression, int skillSlot)
        {
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            WeaponPoints = 95L + progression.WeaponBonus;
            AdditiveDamageBp = 2000 + progression.MightDamageBonusBp + progression.PracticeDamageBonusBp(skillSlot);
            MaximumHealth = CombatRules.MaximumHealth(progression);
            HealingBonus = progression.ResolveHealBonus + progression.PracticeHealingBonus;
        }
    }

    /// <summary>Pure revision-one combat rules over existing integer units, with no health or RNG ownership.</summary>
    public static class CombatRules
    {
        public const int Revision = 1;

        /// <summary>Preserve the existing bounded Vitality scaling without storing a second health value.</summary>
        public static int MaximumHealth(CharacterProgression progression)
        {
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            return 1200 + progression.VitalityHealthBonus;
        }

        /// <summary>Use the existing FixedMath order and caller-owned PRNG, preserving ordinary replay outcomes.</summary>
        public static long Damage(CombatStats stats, int powerBp, int enemyKind, DeterministicRng rng, out bool critical)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (powerBp < 0 || enemyKind < 0 || enemyKind > 4) throw new ArgumentOutOfRangeException();
            return FixedMath.Damage(stats.WeaponPoints, powerBp, 190, stats.AdditiveDamageBp,
                enemyKind >= 2 ? 4000 : (enemyKind == 1 ? 2200 : 1000), 1900, rng, out critical);
        }

        /// <summary>Keep the existing int UI result contract while preventing per-hit and area-sum wraparound.</summary>
        public static int AddReportedDamage(int previous, long damage)
        {
            if (previous < 0 || damage < 0) throw new ArgumentOutOfRangeException();
            return damage >= int.MaxValue - previous ? int.MaxValue : previous + (int)damage;
        }
    }
}
