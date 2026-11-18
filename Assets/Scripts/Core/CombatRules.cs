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
        /// <summary>Critical chance of the carried weapon type (default 19 % unarmed).</summary>
        public int CritChanceBp { get; }
        /// <summary>The unconditional additive damage share every character starts with (+20 %).</summary>
        public const int BaseAdditiveBp = 2000;
        /// <summary>Skill slot of Ruin, the spell that a staff (magic focus) strengthens.</summary>
        public const int RuinSkillSlot = 3;

        /// <summary>Derive finite values from the current progression, widening weapon addition before arithmetic.</summary>
        public CombatStats(CharacterProgression progression, int skillSlot)
        {
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            var weapon = progression.Equipment.Weapon;
            var profile = WeaponProfiles.For(weapon?.Definition);
            // The weapon type weights the weapon's own power; upgrade points from loot stay unweighted.
            WeaponPoints = (weapon != null ? weapon.Definition.BasePower * (long)profile.DamageBp / 10000L : 95L) + progression.WeaponBonus;
            CritChanceBp = weapon != null ? profile.CritChanceBp : CombatRules.CriticalChanceBp;
            AdditiveDamageBp = BaseAdditiveBp + progression.Equipment.Bonus(WeaponAffix.Power) + progression.MightDamageBonusBp +
                progression.PracticeDamageBonusBp(skillSlot) + progression.WeaponMasteryDamageBonusBp +
                progression.Crafting.AttackBonusBp +
                (skillSlot == RuinSkillSlot ? profile.RuinFocusBp : 0);
            MaximumHealth = CombatRules.MaximumHealth(progression);
            HealingBonus = progression.ResolveHealBonus + progression.PracticeHealingBonus;
        }

        /// <summary>Explicit values, for previews and rule tests only.</summary>
        internal CombatStats(long weaponPoints, int additiveDamageBp, int critChanceBp)
        {
            WeaponPoints = weaponPoints; AdditiveDamageBp = additiveDamageBp; CritChanceBp = critChanceBp;
            MaximumHealth = 0; HealingBonus = 0;
        }
    }

    /// <summary>Pure revision-one combat rules over existing integer units, with no health or RNG ownership.</summary>
    public static class CombatRules
    {
        public const int Revision = 1;
        public const int CriticalChanceBp = 1900;
        public const int CriticalMultiplierBp = 15000;
        public const int MainStat = 190;

        /// <summary>Damage reduction of each enemy kind in basis points, as used by every hit.</summary>
        public static int ArmorBp(int enemyKind) { return enemyKind >= 2 ? 4000 : (enemyKind == 1 ? 2200 : 1000); }

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
            if (powerBp < 0 || enemyKind < 0 || enemyKind > 5) throw new ArgumentOutOfRangeException();
            return FixedMath.Damage(stats.WeaponPoints, powerBp, MainStat, stats.AdditiveDamageBp,
                ArmorBp(enemyKind), stats.CritChanceBp, rng, out critical);
        }

        /// <summary>Same order with an explicit armour value, so a monster role can supply its own reduction.</summary>
        public static long DamageWithArmor(CombatStats stats, int powerBp, int armorBp, DeterministicRng rng, out bool critical)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (powerBp < 0 || armorBp < 0) throw new ArgumentOutOfRangeException();
            return FixedMath.Damage(stats.WeaponPoints, powerBp, MainStat, stats.AdditiveDamageBp,
                armorBp, stats.CritChanceBp, rng, out critical);
        }

        /// <summary>
        /// The exact result of Damage for a known critical outcome, without touching any PRNG. Presentation uses it
        /// to show what a stat or skill changes; it never decides a hit.
        /// </summary>
        public static long PreviewDamage(CombatStats stats, int powerBp, int enemyKind, bool critical)
        {
            if (powerBp < 0 || enemyKind < 0 || enemyKind > 5) throw new ArgumentOutOfRangeException();
            const long Bp = 10000;
            long damage = checked(stats.WeaponPoints * powerBp) / Bp;
            damage = checked(damage * (Bp + MainStat * 10)) / Bp;
            damage = checked(damage * (Bp + stats.AdditiveDamageBp)) / Bp;
            if (critical) damage = checked(damage * CriticalMultiplierBp) / Bp;
            damage = checked(damage * Bp) / (Bp + Math.Max(0, ArmorBp(enemyKind)));
            return Math.Max(1, damage);
        }

        /// <summary>Keep the existing int UI result contract while preventing per-hit and area-sum wraparound.</summary>
        public static int AddReportedDamage(int previous, long damage)
        {
            if (previous < 0 || damage < 0) throw new ArgumentOutOfRangeException();
            return damage >= int.MaxValue - previous ? int.MaxValue : previous + (int)damage;
        }
    }
}
