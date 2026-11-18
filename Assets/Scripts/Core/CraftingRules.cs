using System;
using System.Numerics;

namespace AreArpg.Core
{
    /// <summary>Gathering and crafting skills, each level 1-99 like the weapon skills.</summary>
    public enum CraftSkill { Herbalism = 0, Mining = 1, Woodcutting = 2, Alchemy = 3, FirstAid = 4, Carpentry = 5, Smithing = 6 }

    /// <summary>Gathered and consumed materials. Order is persisted; never reorder without a migration.</summary>
    public enum MaterialKind
    {
        Ashenflieder = 0, Glutklee = 1, Grauwurz = 2, Dornbeere = 3, Nachtbluete = 4,
        Ascheisen = 5, Schlackenkupfer = 6, Grausilber = 7, Glutstein = 8, Runenerz = 9, Holz = 10,
        Honig = 11
    }

    /// <summary>One potion item per type; the tier is the level-based affix, not a separate item.</summary>
    public enum PotionType { Heal = 0, Ward = 1, Might = 2, Regen = 3 }
    public enum PotionTier { Small = 0, Medium = 1, Large = 2 }
    public enum BandageTier { Small = 0, Medium = 1, Mighty = 2 }

    /// <summary>
    /// Pure crafting rules. Experience is the classic level table shared with the weapon skills; gathering yields are
    /// reproducible and never fall with a higher level; the potion tier is a deterministic level-based roll.
    /// </summary>
    public static class CraftingRules
    {
        public const int Revision = 1;
        public const int SkillCount = 7;
        public const int MaterialCount = 12;
        public const int PotionTypeCount = 4;
        public const int TierCount = 3;

        /// <summary>Classic level table (shared with the weapon skills): level 1 at 0, 99 at 13,034,431.</summary>
        public static int LevelForExperience(long experience) { return WeaponMasteryRules.LevelForExperience(experience); }
        public static long ExperienceForLevel(int level) { return WeaponMasteryRules.ExperienceForLevel(level); }

        /// <summary>Only effective actions earn experience: one per ten effect, minimum one.</summary>
        public static BigInteger ExperienceFromEffectiveAmount(int amount) { return PracticeRules.ExperienceFromEffectiveAmount(amount); }

        private struct Spot { public int Material, Threshold, Div; public bool Primary; public Spot(int material, int threshold, int div, bool primary) { Material = material; Threshold = threshold; Div = div; Primary = primary; } }

        private static readonly Spot[][] Table =
        {
            new[] { new Spot((int)MaterialKind.Ashenflieder, 0, 10, true), new Spot((int)MaterialKind.Glutklee, 15, 25, false),
                    new Spot((int)MaterialKind.Grauwurz, 15, 25, false), new Spot((int)MaterialKind.Dornbeere, 40, 40, false),
                    new Spot((int)MaterialKind.Nachtbluete, 65, 50, false) },
            new[] { new Spot((int)MaterialKind.Ascheisen, 0, 8, true), new Spot((int)MaterialKind.Schlackenkupfer, 0, 12, false),
                    new Spot((int)MaterialKind.Grausilber, 25, 20, false), new Spot((int)MaterialKind.Glutstein, 45, 30, false),
                    new Spot((int)MaterialKind.Runenerz, 60, 40, false) },
            new[] { new Spot((int)MaterialKind.Holz, 0, 8, true) },
            Array.Empty<Spot>(), Array.Empty<Spot>(), Array.Empty<Spot>(), Array.Empty<Spot>()
        };

        /// <summary>Deterministic gather yields per material. The seed bonus is level-independent, so a higher level
        /// never yields less of any material for the same seed.</summary>
        public static int[] Gather(CraftSkill skill, int level, ulong seed)
        {
            if (level < 1) level = 1; if (level > WeaponMasteryRules.MaxLevel) level = WeaponMasteryRules.MaxLevel;
            var yields = new int[MaterialCount];
            var rng = new DeterministicRng(seed ^ ((ulong)skill + 1UL) * 0x9E3779B97F4A7C15UL);
            int bonus = (int)(rng.Next64() % 3UL);
            foreach (var spot in Table[(int)skill])
            {
                if (level < spot.Threshold) continue;
                int q = 1 + (level - spot.Threshold) / spot.Div + (spot.Primary ? bonus : 0);
                yields[spot.Material] += q;
            }
            return yields;
        }

        /// <summary>Level-based potion tier: a low skill never yields a large potion, a high skill yields any tier.</summary>
        public static PotionTier RollTier(int level, ulong seed)
        {
            if (level < 1) level = 1; if (level > WeaponMasteryRules.MaxLevel) level = WeaponMasteryRules.MaxLevel;
            int largeBp = level <= 40 ? 0 : Math.Min(4000, (level - 40) * 60);
            int mediumBp = Math.Min(5000, 2000 + level * 30);
            int roll = (int)(new DeterministicRng(seed ^ 0x504F54494F4EUL).Next64() % 10000UL);
            if (roll < largeBp) return PotionTier.Large;
            if (roll < largeBp + mediumBp) return PotionTier.Medium;
            return PotionTier.Small;
        }

        /// <summary>Buff duration in 100-ms ticks: small 3 min, medium 6 min, large 9 min.</summary>
        public static int DurationTicks(PotionTier tier)
        {
            switch (tier) { case PotionTier.Medium: return 3600; case PotionTier.Large: return 5400; default: return 1800; }
        }

        /// <summary>Immediate healing of a heal potion by tier.</summary>
        public static int HealAmount(PotionTier tier)
        {
            switch (tier) { case PotionTier.Medium: return 400; case PotionTier.Large: return 700; default: return 200; }
        }

        /// <summary>Health restored each tick by a regeneration potion of the given tier.</summary>
        public static int RegenPerTick(PotionTier tier)
        {
            switch (tier) { case PotionTier.Medium: return 2; case PotionTier.Large: return 4; default: return 1; }
        }

        public const int BuffBonusBp = 2500; // +25 %

        /// <summary>Success chance of the holy revive cloth in basis points: only from First-Aid 85 upward, and even
        /// at 99 it stays "annähernd" (close to but never certain). Deterministic per seed.</summary>
        public static bool HolyReviveSucceeds(int firstAidLevel, ulong seed)
        {
            if (firstAidLevel < 85) return false;
            int chance = 5000 + Math.Min(14, firstAidLevel - 85) * 300; // 50 % at 85, up to 92 % at 99
            int roll = (int)(new DeterministicRng(seed ^ 0x484F4C59524556UL).Next64() % 10000UL);
            return roll < chance;
        }
    }
}
