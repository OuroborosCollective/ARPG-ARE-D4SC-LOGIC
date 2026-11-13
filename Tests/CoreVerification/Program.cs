using System;
using AreArpg.Core;

internal static class Program
{
    private static int _checks;

    private static void Require(bool expression, string name)
    {
        _checks++;
        if (!expression) throw new Exception("FAIL " + name);
        Console.WriteLine("PASS " + name);
    }

    public static int Main()
    {
        try
        {
            TestMath();
            TestReplay();
            TestCollisionAndMovement();
            TestCombatLootAndCooldown();
            TestAiAndDeath();
            TestGameOverPickupGate();
            ProgressionTests.Run(Require);
            MasteryTests.Run(Require);
            CryptLoopTests.Run(Require);
            ChapterTwoTests.Run(Require);
            CheckpointTests.Run(Require);
            PracticeWorldTests.Run(Require);
            NpcSocialTests.Run(Require);
            NpcSpeechTests.Run(Require);
            SupplyTheftTests.Run(Require);
            LootAffixTests.Run(Require);
            SkillContractTests.Run(Require);
            CombatStatsTests.Run(Require);
            PracticeRuleTests.Run(Require);
            EntryJourneyTests.Run(Require);
            HungerJourneyTests.Run(Require);
            HunterEncounterTests.Run(Require);
            InvestigationJourneyTests.Run(Require);
            RattlerEncounterTests.Run(Require);
            CryptDungeonTests.Run(Require);
            WoodlandTests.Run(Require);
            WeaponEquipmentTests.Run(Require);
            NpcForgeTests.Run(Require);
            NpcForgeIntegrationTests.Run(Require);
            CompanionForgeTests.Run(Require);
            ForgePolicyTests.Run(Require);
            Console.WriteLine("ALL PASS: " + _checks + " deterministic core assertions");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void TestMath()
    {
        Require(FixedMath.IntegerSqrt(0) == 0, "sqrt-zero");
        Require(FixedMath.IntegerSqrt(long.MaxValue) == 3037000499L, "sqrt-long-max-overflow-safe");
        Require(FixedMath.IntegerSqrt(81) == 9 && FixedMath.IntegerSqrt(80) == 8, "sqrt-floor");
        bool crit;
        long damage = FixedMath.Damage(100, 10000, 220, 2500, 0, 0, new DeterministicRng(1), out crit);
        Require(damage == 152 && !crit, "wolfram-fixed-point-reference-152");
    }

    private static void TestReplay()
    {
        var a = new ArenaSimulation(0xBEEF1234UL);
        var b = new ArenaSimulation(0xBEEF1234UL);
        for (int t = 0; t < 150; t++)
        {
            long x = t < 20 ? 10000 : (t < 40 ? -10000 : 0);
            long z = t < 35 ? 10000 : 0;
            a.SetMoveInput(x, z); b.SetMoveInput(x, z);
            if (t % 7 == 0) { a.ActivateSkill(0); b.ActivateSkill(0); }
            if (t % 17 == 0) { a.ActivateSkill(2); b.ActivateSkill(2); }
            if (t % 19 == 0) { a.TryCollectLoot(); b.TryCollectLoot(); }
            a.Step(); b.Step();
            Require(a.ComputeWorldHash() == b.ComputeWorldHash(), "replay-tick-" + t);
        }
        a.SetMoveInput(0, 0);
        b.SetMoveInput(-10000, 0);
        a.Step();
        b.Step();
        Require(a.ComputeWorldHash() != b.ComputeWorldHash(), "different-input-different-state");
    }

    private static void TestCollisionAndMovement()
    {
        var world = new ArenaSimulation(42);
        Require(!world.CanStand(7000, 7000), "pillar-blocked");
        Require(!world.CanStand(28000, 0), "arena-outside-blocked");
        Require(world.CanStand(0, 0), "spawn-walkable");
        world.SetMoveInput(10000, 0);
        world.Step();
        Require(world.PlayerXmm > 0, "player-moves");
        world.SetMoveInput(10000, 10000);
        for (int t = 0; t < 120; t++)
        {
            world.Step();
            Require(world.CanStand(world.PlayerXmm, world.PlayerZmm), "collision-tick-" + t);
        }
    }

    private static void TestCombatLootAndCooldown()
    {
        var world = new ArenaSimulation(100);
        MonsterState monster = world.Monsters[0];
        monster.Xmm = 1000; monster.Zmm = 0; monster.Health = 1;
        SkillResult attack = world.ActivateSkill(0);
        Require(attack.Activated && attack.Kills == 1 && monster.IsDead, "real-hit-kills-monster");
        Require(world.GroundLoot.Count == 1 && world.Kills == 1, "one-drop-on-kill");
        world.ActivateSkill(0);
        Require(world.GroundLoot.Count == 1 && world.Kills == 1, "no-duplicate-loot");
        Require(world.TryCollectLoot(), "loot-pickup-in-range");
        Require(world.WeaponBonus == 8 && world.LootCollected == 1 && world.GroundLoot.Count == 0,
            "pickup-upgrades-weapon-once");
        Require(!world.TryCollectLoot(), "no-second-pickup");
        SkillResult distantAttack = world.ActivateSkill(1);
        Require(!distantAttack.Activated && world.Resource == 100, "no-cost-for-missed-cast");
        int oldHealth = world.PlayerHealth;
        SkillResult guard = world.ActivateSkill(2);
        Require(guard.Activated && world.PlayerHealth >= oldHealth, "guard-available");
        Require(!world.ActivateSkill(2).Activated, "guard-cooldown-enforced");
        for (int i = 0; i < 50; i++) world.Step();
        Require(world.Cooldowns[2] == 0, "cooldown-decrements-on-ticks");
    }

    private static void TestAiAndDeath()
    {
        var world = new ArenaSimulation(65);
        world.SetMoveInput(0, 0);
        int healthBefore = world.PlayerHealth;
        long startMonsterX = world.Monsters[0].Xmm;
        for (int i = 0; i < 200; i++) world.Step();
        Require(world.Monsters[0].Xmm != startMonsterX, "monster-ai-pursues");
        Require(world.PlayerHealth < healthBefore, "monster-ai-damages-player");
        Require(world.GameOver && world.PlayerHealth == 0, "game-over-at-zero-hp");
        Require(!world.ActivateSkill(0).Activated, "cannot-attack-when-dead");
    }
    private static void TestGameOverPickupGate()
    {
        var world = new ArenaSimulation(78);
        MonsterState first = world.Monsters[0];
        first.Xmm = 1000;
        first.Zmm = 0;
        first.Health = 1;
        Require(world.ActivateSkill(0).Kills == 1 && world.GroundLoot.Count == 1,
            "dead-pickup-fixture-has-real-drop");
        world.Monsters[1].Xmm = 1200;
        world.Monsters[1].Zmm = 0;
        world.Monsters[2].Xmm = -1200;
        world.Monsters[2].Zmm = 0;
        for (int i = 0; i < 300 && !world.GameOver; i++) world.Step();
        Require(world.GameOver, "dead-pickup-fixture-reaches-real-death");
        Require(!world.TryCollectLoot(), "dead-player-cannot-collect-loot");
        Require(world.GroundLoot.Count == 1 && world.LootCollected == 0 && world.WeaponBonus == 0,
            "dead-pickup-cannot-change-loot-state");
    }

}
