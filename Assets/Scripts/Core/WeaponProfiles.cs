using System;

namespace AreArpg.Core
{
    /// <summary>
    /// How a weapon type fights and how it carries (owner decisions 2026-11-17). Attack speed and reach come from the
    /// weapon definitions; this profile adds the damage weight, critical chance, accuracy and movement speed.
    /// Integer basis points only; revision 2 adds the staff's magic focus.
    /// </summary>
    public readonly struct WeaponProfile
    {
        public string Name { get; }
        /// <summary>Multiplier on the weapon's base power (10000 = unchanged).</summary>
        public int DamageBp { get; }
        public int CritChanceBp { get; }
        /// <summary>Player movement speed while carried (10000 = full speed).</summary>
        public int MoveSpeedBp { get; }
        /// <summary>Extra additive damage share for Ruin (the spell) while carried; only the staff, the magic focus, has one.</summary>
        public int RuinFocusBp { get; }
        private readonly int _baseAccuracyBp, _accuracyPerLevelBp;

        internal WeaponProfile(string name, int damageBp, int critChanceBp, int moveSpeedBp, int baseAccuracyBp = 10000,
            int accuracyPerLevelBp = 0, int ruinFocusBp = 0)
        {
            Name = name; DamageBp = damageBp; CritChanceBp = critChanceBp; MoveSpeedBp = moveSpeedBp;
            _baseAccuracyBp = baseAccuracyBp; _accuracyPerLevelBp = accuracyPerLevelBp; RuinFocusBp = ruinFocusBp;
        }

        /// <summary>Chance to hit at the given skill level; melee weapons never miss.</summary>
        public int Accuracy(int skillLevel)
        {
            if (skillLevel < 1 || skillLevel > WeaponMasteryRules.MaxLevel) throw new ArgumentOutOfRangeException(nameof(skillLevel));
            return Math.Min(10000, _baseAccuracyBp + _accuracyPerLevelBp * skillLevel);
        }

        public bool CanMiss => _baseAccuracyBp < 10000;
    }

    public static class WeaponProfiles
    {
        /// <summary>Revision 2 (owner decision 2026-11-18): the staff is the magic focus.</summary>
        public const int Revision = 2;
        /// <summary>The staff strengthens Ruin, the spell: +25 % additive damage share while it is carried.</summary>
        public const int StaffRuinFocusBp = 2500;
        /// <summary>A shield makes the bearer noticeably slower: minus 12 percentage points of movement speed.</summary>
        public const int ShieldMovePenaltyBp = 1200;
        /// <summary>Each shield-skill level adds 0.25 % chance to block an attack completely.</summary>
        public const int BlockChancePerLevelBp = 25;

        public static readonly WeaponProfile Unarmed = new WeaponProfile("Ohne Waffe", 10000, CombatRules.CriticalChanceBp, 10000);
        private static readonly WeaponProfile Dagger = new WeaponProfile("Dolch", 6800, 3500, 10000);
        private static readonly WeaponProfile Sword = new WeaponProfile("Einhandschwert", 10000, 1900, 9800);
        private static readonly WeaponProfile GreatSword = new WeaponProfile("Zweihandschwert", 10600, 1600, 9400);
        private static readonly WeaponProfile Axe = new WeaponProfile("Einhandaxt", 10600, 1500, 9600);
        private static readonly WeaponProfile GreatAxe = new WeaponProfile("Zweihandaxt", 13300, 1200, 9000);
        private static readonly WeaponProfile Bow = new WeaponProfile("Bogen", 10500, 2200, 9800, 6000, 125);
        private static readonly WeaponProfile Staff = new WeaponProfile("Stab", 11000, 1500, 9700, ruinFocusBp: StaffRuinFocusBp);
        private static readonly WeaponProfile Shield = new WeaponProfile("Schild", 0, 0, 10000);

        private static readonly WeaponProfile Haft = new WeaponProfile("Gratkeule", 11750, 450, 9400);
        private static readonly WeaponProfile Fork = new WeaponProfile("Ofengabel", 10500, 700, 9700);

        public static WeaponProfile For(WeaponDefinition definition)
        {
            if (definition == null) return Unarmed;
            switch (definition.Family)
            {
                case WeaponFamily.Dagger: return Dagger;
                case WeaponFamily.Sword: return definition.TwoHanded ? GreatSword : Sword;
                case WeaponFamily.Axe: return definition.TwoHanded ? GreatAxe : Axe;
                case WeaponFamily.Bow: return Bow;
                case WeaponFamily.Staff: return Staff;
                case WeaponFamily.Shield: return Shield;
                case WeaponFamily.Mace: return Haft;
                case WeaponFamily.Spear: return Fork;
                default: return Sword;
            }
        }

        public static int BlockChanceBp(int shieldLevel)
        {
            if (shieldLevel < 1 || shieldLevel > WeaponMasteryRules.MaxLevel) throw new ArgumentOutOfRangeException(nameof(shieldLevel));
            return BlockChancePerLevelBp * shieldLevel;
        }
    }

    public sealed partial class CharacterProgression
    {
        public WeaponProfile CarriedProfile => WeaponProfiles.For(Equipment.Weapon?.Definition);

        /// <summary>Accuracy of attacks made with the carried weapon at its current skill level.</summary>
        public int WeaponAccuracyBp
        {
            get
            {
                var weapon = Equipment.Weapon;
                if (weapon == null) return 10000;
                int accuracy = WeaponProfiles.For(weapon.Definition).Accuracy(WeaponMastery.Level(weapon.Definition.TrainingFamily));
                return CombatVersion >= 2 ? Math.Min(10000, accuracy + Equipment.SteadyHandTier * 200) : accuracy;
            }
        }

        /// <summary>Movement speed from the carried weapon, minus the shield penalty.</summary>
        public int MoveSpeedBp => Math.Max(1000, CarriedProfile.MoveSpeedBp - (Equipment.Shield != null ? WeaponProfiles.ShieldMovePenaltyBp : 0));

        public int BlockChanceBp => Equipment.Shield == null ? 0 : WeaponProfiles.BlockChanceBp(WeaponMastery.Level(WeaponFamily.Shield));

        /// <summary>Every attack received while a shield is carried trains the shield skill, blocked or not.</summary>
        internal int RecordShieldPractice(int attackDamage)
        {
            if (Equipment.Shield == null || attackDamage <= 0) return 0;
            return WeaponMastery.Record(WeaponFamily.Shield, (long)PracticeRules.ExperienceFromEffectiveAmount(attackDamage));
        }
    }
}
