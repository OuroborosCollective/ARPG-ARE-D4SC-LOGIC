using System;
using AreArpg.Core;

internal static class CombatStatsTests
{
    /// <summary>Exercise high persisted weapon bonuses through real combat before testing extracted rules.</summary>
    public static void Run(Action<bool,string> require)
    {
        var progress = new CharacterProgression();
        progress.AddWeaponUpgrade(int.MaxValue);
        var stats = new CombatStats(progress, 0);
        require(stats.WeaponPoints == 2147483742L && stats.MaximumHealth == 1200, "stats-widen-before-add-no-second-owner");
        require(CombatRules.AddReportedDamage(10, 12) == 22 && CombatRules.AddReportedDamage(10, long.MaxValue) == int.MaxValue, "combat-report-boundaries");
        var world = new ArenaSimulation(123, progress);
        world.Monsters[0].Xmm = 1000; world.Monsters[0].Zmm = 0; world.Monsters[0].Health = 200000;
        var result = world.ActivateSkill(0);
        require(result.Kills == 1 && result.TotalDamage > 0, "combat-max-weapon-must-not-wrap-to-one-damage");
        require(progress.PracticeExperience(0) == 20000, "combat-overkill-practice-uses-effective-health");
        world = new ArenaSimulation(123, progress);
        foreach (var enemy in world.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 200000; }
        result = world.ActivateSkill(3);
        require(result.Kills == world.Monsters.Count && result.TotalDamage == int.MaxValue, "combat-area-damage-report-saturates-without-negative-wrap");
        require(CharacterProgression.TryLoad(progress.SaveString(), out var loaded) && loaded.WeaponBonus == int.MaxValue, "combat-max-weapon-save-roundtrip");
    }
}
