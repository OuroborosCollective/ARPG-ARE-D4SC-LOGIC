using System;
using System.Numerics;
using AreArpg.Core;

internal static class ProgressionTests
{
    public static void Run(Action<bool, string> require)
    {
        var progress = new CharacterProgression();
        require(progress.Level == BigInteger.One && progress.TotalExperience == BigInteger.Zero,
            "progression-begins-at-level-one");
        require(progress.ExperienceToNextLevel == new BigInteger(150), "level-two-needs-150-xp");
        progress.EarnExperience(new BigInteger(160));
        require(progress.Level == new BigInteger(2), "experience-levels-up");
        require(progress.ExperienceToNextLevel == new BigInteger(290), "level-three-requires-450-cumulative-xp");
        require(progress.UnspentMasteryPoints == progress.Level - BigInteger.One,
            "every-level-earns-mastery-point");

        BigInteger hugeAward = BigInteger.Pow(new BigInteger(10), 50);
        progress.EarnExperience(hugeAward);
        require(progress.Level > new BigInteger(ulong.MaxValue), "levels-exceed-64-bit-counter");
        require(progress.TotalExperience > new BigInteger(long.MaxValue), "xp-exceeds-64-bit-counter");
        require(CharacterProgression.RequiredExperienceForLevel(progress.Level) <= progress.TotalExperience,
            "uncapped-level-lower-bound");
        require(CharacterProgression.RequiredExperienceForLevel(progress.Level + BigInteger.One) > progress.TotalExperience,
            "uncapped-level-upper-bound");
        require(progress.ExperienceToNextLevel > BigInteger.Zero,
            "unlimited-xp-curve-always-has-next-level");
        BigInteger previous = progress.TotalExperience;
        bool rejected = false;
        try { progress.EarnExperience(new BigInteger(-1)); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        require(rejected && progress.TotalExperience == previous, "negative-xp-rejected-without-mutation");

        progress.AddWeaponUpgrade(17);
        progress.RecordKill();
        string serialized = progress.SaveString();
        CharacterProgression loaded;
        require(CharacterProgression.TryLoad(serialized, out loaded) && loaded != null,
            "progression-save-roundtrip-loads");
        require(loaded.Level == progress.Level && loaded.TotalExperience == progress.TotalExperience,
            "progression-save-roundtrip-preserves-huge-xp");
        require(loaded.WeaponBonus == 17 && loaded.LootCollected == 1 &&
            loaded.LifetimeKills == BigInteger.One,
            "progression-save-preserves-gear-and-lifetime-kills");
        require(loaded.StableHash == progress.StableHash, "progression-save-stable-hash");
        CharacterProgression invalid;
        require(!CharacterProgression.TryLoad("AC1|-100|17|1|1", out invalid),
            "negative-save-xp-rejected");
        require(!CharacterProgression.TryLoad("AC1|010|17|1|1", out invalid),
            "noncanonical-save-xp-rejected");
        require(!CharacterProgression.TryLoad("AC1|0|garbage|1|1", out invalid),
            "corrupt-save-rejected");

        var arena = new ArenaSimulation(0xFEEDUL);
        var firstEnemy = arena.Monsters[0];
        firstEnemy.Xmm = 1000;
        firstEnemy.Zmm = 0;
        firstEnemy.Health = 1;
        require(arena.Progression.TotalExperience == BigInteger.Zero, "arena-begins-no-experience");
        require(arena.ActivateSkill(0).Kills == 1, "arena-real-kill-for-xp");
        require(arena.Progression.TotalExperience == new BigInteger(160), "arena-kill-awards-160-xp");
        require(arena.Progression.LifetimeKills == BigInteger.One, "arena-first-kill-counted");
        arena.ActivateSkill(0);
        require(arena.Progression.TotalExperience == new BigInteger(160) &&
            arena.Progression.LifetimeKills == BigInteger.One, "no-double-xp-on-dead-monster");
        require(arena.TryCollectLoot(), "arena-first-gear-pickup");
        var nextRun = new ArenaSimulation(0xFEEDUL, arena.Progression);
        require(nextRun.Progression.Level == new BigInteger(2) &&
            nextRun.WeaponBonus == 8 && nextRun.LootCollected == 1,
            "death-and-new-run-preserve-level-and-gear");
        require(nextRun.PlayerHealth == nextRun.MaxPlayerHealth && nextRun.Kills == 0,
            "run-health-resets-but-account-progress-does-not");
        var reloaded = new ArenaSimulation(0xFEEDUL, loaded);
        require(reloaded.Progression.Level == progress.Level && reloaded.WeaponBonus == 17,
            "saved-character-works-in-fresh-arena");
        var unchanged = new ArenaSimulation(0xFEEDUL);
        require(reloaded.ComputeWorldHash() != unchanged.ComputeWorldHash(),
            "replay-hash-tracks-progression-state");
    }
}
