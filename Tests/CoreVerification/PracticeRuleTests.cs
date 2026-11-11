using System;
using System.Numerics;
using AreArpg.Core;

internal static class PracticeRuleTests
{
    /// <summary>Check versioned disciplines and effective-action rules without changing XP/mastery curves.</summary>
    public static void Run(Action<bool,string> require)
    {
        require(PracticeRules.Revision == 1, "practice-rules-version");
        require(PracticeRules.ExperienceFromEffectiveAmount(0) == BigInteger.Zero, "practice-no-effective-action-no-xp");
        require(PracticeRules.ExperienceFromEffectiveAmount(1) == BigInteger.One && PracticeRules.ExperienceFromEffectiveAmount(9) == BigInteger.One, "practice-positive-action-minimum-one");
        require(PracticeRules.ExperienceFromEffectiveAmount(100) == 10 && PracticeRules.ExperienceFromEffectiveAmount(int.MaxValue) == 214748364, "practice-existing-integer-scale");
        require(PracticeRules.DamageBonusBp(BigInteger.Pow(10, 100)) == 5000 && PracticeRules.HealingBonus(BigInteger.Pow(10, 100)) == 200, "practice-unbounded-level-finite-gameplay-bonus");
        string[] ids = { "practice.blade", "practice.force", "practice.guard", "practice.ruin" };
        for (int slot = 0; slot < 4; slot++) require(PracticeRules.DisciplineId(slot) == ids[slot], "practice-stable-save-slot-" + slot);
        bool rejected = false;
        try { PracticeRules.ExperienceFromEffectiveAmount(-1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        require(rejected, "practice-negative-effect-rejected");
        var world = new ArenaSimulation(123);
        world.Monsters[0].Xmm = 1000; world.Monsters[0].Zmm = 0; world.Monsters[0].Health = 1;
        world.ActivateSkill(0);
        require(world.Progression.PracticeExperience(0) == BigInteger.One, "practice-overkill-uses-one-effective-hp");
        world.ActivateSkill(0);
        require(world.Progression.PracticeExperience(0) == BigInteger.One, "practice-cooldown-awards-nothing");
        world.ActivateSkill(2);
        require(world.Progression.PracticeExperience(2) == BigInteger.Zero, "practice-full-health-guard-awards-nothing");
    }
}
