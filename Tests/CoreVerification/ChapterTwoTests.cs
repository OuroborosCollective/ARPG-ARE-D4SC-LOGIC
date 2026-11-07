using System;
using System.Numerics;
using AreArpg.Core;

/// <summary>Contracts for the original chapter transition, not Unity-rendering evidence.</summary>
internal static class ChapterTwoTests
{
    public static void Run(Action<bool, string> require)
    {
        var first = new ArenaSimulation(0xA11CEUL);
        require(first.ChapterId == 1 && first.ChapterName == "Ashen Crypt",
            "chapter-one-title-and-identity");

        var second = new ArenaSimulation(0xA11CEUL, first.Progression, new BigInteger(2));
        require(second.ChapterId == 2 && second.ChapterName == "Ember Rift",
            "chapter-two-is-new-region");
        require(second.Monsters.Count == 3 && second.Monsters[2].Kind == 3,
            "chapter-two-has-unique-ember-warden-boss");
        require(second.Monsters[0].Xmm != first.Monsters[0].Xmm ||
            second.Monsters[0].Zmm != first.Monsters[0].Zmm,
            "chapter-two-has-different-enemy-layout");

        require(!second.CryptCleared && second.TryAdvanceCrypt() == null,
            "chapter-two-exit-locked-until-boss-dies");
        require(second.Monsters[2].BossWindupTicks == 0,
            "chapter-two-boss-starts-without-telegraph");

        // The new boss attack is a real deterministic tick-driven windup.
        var warning = new ArenaSimulation(0xB055UL, null, new BigInteger(2));
        foreach (var m in warning.Monsters)
            if (m.Kind != 3) { m.Xmm = 25000; m.Zmm = 25000; }
        warning.Monsters[2].Xmm = 8000;
        warning.Monsters[2].Zmm = 0;
        for (int i = 0; i < 80 && warning.Monsters[2].BossWindupTicks == 0; i++) warning.Step();
        require(warning.Monsters[2].BossWindupTicks > 0,
            "ember-warden-telegraphs-on-authoritative-ticks");

        var replay = new ArenaSimulation(0xB055UL, null, new BigInteger(2));
        foreach (var m in replay.Monsters)
            if (m.Kind != 3) { m.Xmm = 25000; m.Zmm = 25000; }
        replay.Monsters[2].Xmm = 8000;
        replay.Monsters[2].Zmm = 0;
        for (int i = 0; i < (int)warning.CurrentTick; i++) replay.Step();
        require(warning.ComputeWorldHash() == replay.ComputeWorldHash(),
            "chapter-two-boss-windup-replay-deterministic");

        // The channel is dodgeable: moving outside the impact radius avoids the new pulse.
        var evade = new ArenaSimulation(0xBABEUL, null, new BigInteger(2));
        foreach (var m in evade.Monsters)
            if (m.Kind != 3) { m.Xmm = 25000; m.Zmm = 25000; }
        evade.Monsters[2].Xmm = 8000;
        evade.Monsters[2].Zmm = 0;
        for (int i = 0; i < 80 && evade.Monsters[2].BossWindupTicks == 0; i++) evade.Step();
        require(evade.Monsters[2].BossWindupTicks > 0, "boss-dodge-setup-has-telegraph");
        long healthBefore = evade.PlayerHealth;
        evade.SetMoveInput(-10000, 0);
        for (int i = 0; i < 14; i++) evade.Step();
        require(evade.PlayerHealth == healthBefore, "ember-pulse-can-be-dodged");

        var repeated = new ArenaSimulation(0xA11CEUL, first.Progression, new BigInteger(10000));
        require(repeated.ChapterId == 2 && repeated.Monsters[2].Kind == 3,
            "higher-depths-reuse-chapter-two-rules-without-reset");
        require(repeated.Monsters[2].Health > 0 && repeated.Monsters[2].Health < int.MaxValue,
            "chapter-two-depth-scaling-bounded");

        foreach (MonsterState m in first.Monsters) { m.Xmm = 1000; m.Zmm = 0; m.Health = 1; }
        require(first.ActivateSkill(3).Kills == 3 && first.CryptCleared,
            "transition-requires-actual-first-boss-defeat");
        require(first.TryAdvanceCrypt() == null, "transition-waits-for-ground-loot");
        for (int i = 0; i < 3; i++) require(first.TryCollectLoot(), "chapter-one-loot-" + i);
        var entered = first.TryAdvanceCrypt();
        require(entered != null && entered.ChapterId == 2 && entered.Monsters[2].Kind == 3,
            "transition-loads-unique-ember-rift-boss");
        require(!object.ReferenceEquals(entered.Progression, first.Progression) &&
            entered.Progression.TotalExperience == first.Progression.TotalExperience,
            "chapter-transition-preserves-uncapped-progression");
    }
}
