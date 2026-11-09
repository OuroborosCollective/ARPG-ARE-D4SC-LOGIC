using System;
using System.Numerics;
using AreArpg.Core;

internal static class PracticeWorldTests
{
    private static string Signed(string payload)
    {
        ulong h = 14695981039346656037UL;
        unchecked { foreach (byte b in System.Text.Encoding.UTF8.GetBytes(payload)) { h ^= b; h *= 1099511628211UL; } }
        return payload + "|" + h.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static void Run(Action<bool, string> require)
    {
        var p = new CharacterProgression();
        for (int slot = 0; slot < 4; slot++)
            require(p.PracticeExperience(slot).IsZero && p.PracticeLevel(slot).IsOne, "practice-new-discipline-" + slot);
        var world = new ArenaSimulation(112);
        require(!world.ActivateSkill(0).Activated && world.Progression.PracticeExperience(0).IsZero, "practice-miss-earns-nothing");
        require(world.ActivateSkill(2).Activated && world.Progression.PracticeExperience(2).IsZero, "practice-full-health-guard-earns-nothing");
        world.Monsters[0].Xmm = 1000; world.Monsters[0].Zmm = 0; world.Monsters[0].Health = 10000;
        var hit = world.ActivateSkill(0);
        require(hit.Hits == 1 && world.Progression.PracticeExperience(0) > 0, "practice-real-strike-trains-blade");
        require(world.Progression.PracticeExperience(1).IsZero && world.Progression.PracticeExperience(3).IsZero, "practice-only-used-discipline-grows");
        BigInteger earned = world.Progression.PracticeExperience(0);
        world.ActivateSkill(0);
        require(world.Progression.PracticeExperience(0) == earned, "practice-rejected-cooldown-earns-nothing");
        var wounded = new ArenaSimulation(113);
        wounded.Monsters[0].Xmm = 1000; wounded.Monsters[0].Zmm = 0;
        wounded.Step();
        require(wounded.PlayerHealth < wounded.MaxPlayerHealth, "practice-real-core-enemy-wounds-player");
        wounded.ActivateSkill(2);
        require(wounded.Progression.PracticeExperience(2) > 0 && wounded.Progression.PracticeExperience(0).IsZero, "practice-effective-guard-trains-warding");
        var tiny = new ArenaSimulation(114);
        tiny.Monsters[0].Xmm = 1000; tiny.Monsters[0].Zmm = 0; tiny.Monsters[0].Health = 1;
        tiny.ActivateSkill(1);
        require(tiny.Progression.PracticeExperience(1) == BigInteger.One, "practice-overkill-does-not-inflate-training");
        var storm = new ArenaSimulation(115);
        storm.ActivateSkill(3);
        require(storm.Progression.PracticeExperience(3) > 0, "practice-ruin-trains-on-real-hits");
        var huge = BigInteger.Pow(10, 100);
        p.RecordPractice(0, huge);
        require(p.PracticeLevel(0) > new BigInteger(ulong.MaxValue), "practice-ranks-have-no-machine-counter-cap");
        require(p.PracticeDamageBonusBp(0) <= 5000, "practice-finite-combat-bonus-with-unbounded-rank");
        bool rejected = false;
        try { p.RecordPractice(0, -1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        require(rejected && p.PracticeExperience(0) == huge, "practice-negative-award-rejected-without-mutation");
        string save = p.SaveString();
        CharacterProgression loaded;
        require(save.StartsWith("AC4|") && CharacterProgression.TryLoad(save, out loaded), "practice-ac4-roundtrip-loads");
        require(CharacterProgression.TryLoad(save, out loaded) && loaded.PracticeExperience(0) == huge && loaded.StableHash == p.StableHash, "practice-save-preserves-unbounded-training-and-hash");
        require(!CharacterProgression.TryLoad(save.Replace(huge.ToString(), "01"), out loaded), "practice-corrupt-counter-rejected");
        string ac3 = Signed("AC3|160|8|1|1|1|0|0|2");
        require(CharacterProgression.TryLoad(ac3, out loaded) && loaded.UnlockedCryptDepth == 2 && loaded.MightRank == 1 && loaded.PracticeExperience(0).IsZero, "practice-ac3-migration-keeps-progress-with-zero-new-training");
        require(!CharacterProgression.TryLoad(Signed("AC4|0|0|0|0|0|0|0|1|-1|0|0|0"), out loaded), "practice-negative-save-rejected-even-with-valid-checksum");
        var trained = new CharacterProgression(); trained.RecordPractice(0, 100000);
        var noviceFight = new ArenaSimulation(118); var trainedFight = new ArenaSimulation(118, trained);
        foreach (var fight in new[] { noviceFight, trainedFight })
        { fight.Monsters[0].Xmm = 1000; fight.Monsters[0].Zmm = 0; fight.Monsters[0].Health = 10000; }
        require(trainedFight.ActivateSkill(0).TotalDamage > noviceFight.ActivateSkill(0).TotalDamage, "practice-rank-increases-real-matching-skill-damage");
        var a = new CharacterProgression(); var b = new CharacterProgression();
        b.RecordPractice(1, 1);
        require(a.StableHash != b.StableHash, "practice-hash-binds-discipline-state");

        var region = new ArenaSimulation(116);
        require(region.TryTraversePassage() == null, "passage-cannot-skip-live-encounter");
        foreach (var enemy in region.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
        region.Step(); // Receive real damage; crossing must not grant a free heal.
        region.ActivateSkill(3);
        require(region.TryTraversePassage() == null, "passage-uncollected-loot-is-not-discarded");
        for (int i = 0; i < 3; i++) require(region.TryCollectLoot(), "passage-retrieve-loot-" + i);
        require(region.TryTraversePassage() == null, "passage-clear-is-not-a-remote-next-level-button");
        region.SetMoveInput(0, 10000);
        for (int i = 0; i < 38; i++) region.Step();
        require(region.AtWorldPassage, "passage-reachable-with-real-core-movement");
        ulong sealedHash = region.ComputeWorldHash();
        int health = region.PlayerHealth, resource = region.Resource;
        BigInteger practice = region.Progression.PracticeExperience(3);
        ArenaSimulation next = region.TryTraversePassage();
        require(next != null && next.CryptDepth == 2 && next.ChapterId == 2, "passage-real-crossing-enters-next-region");
        require(next.PlayerHealth == health && next.Resource == resource, "passage-preserves-health-and-focus");
        require(next.PlayerZmm == next.PrevPlayerZmm && next.PlayerZmm < 0 && !next.AtWorldPassage, "passage-enters-from-opposite-world-edge");
        require(next.Progression.PracticeExperience(3) == practice && next.Progression.UnlockedCryptDepth == 2, "passage-checkpoint-retains-learned-disciplines");
        require(region.ComputeWorldHash() == sealedHash, "passage-leaves-prior-world-receipt-unchanged");
        require(next.TryTraversePassage() == null, "passage-cannot-loop-without-new-encounter");
    }
}

