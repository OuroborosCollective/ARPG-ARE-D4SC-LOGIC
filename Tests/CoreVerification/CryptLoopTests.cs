using System;
using System.Numerics;
using AreArpg.Core;

internal static class CryptLoopTests
{
    public static void Run(Action<bool, string> require)
    {
        var world = new ArenaSimulation(0xFACEUL);
        require(world.CryptDepth == BigInteger.One && !world.CryptCleared,
            "crypt-new-arena-not-cleared");
        require(world.TryAdvanceCrypt() == null,
            "crypt-cannot-advance-before-boss-and-enemies-die");

        // Exercise real damage/loot/XP transitions, with bounded test-fixture HP.
        foreach (MonsterState monster in world.Monsters)
        {
            monster.Xmm = 1000;
            monster.Zmm = 0;
            monster.Health = 1;
        }
        SkillResult clear = world.ActivateSkill(3);
        require(clear.Activated && clear.Kills == 3 && world.CryptCleared,
            "crypt-real-combat-can-clear-all-three");
        require(world.Progression.TotalExperience == new BigInteger(1770),
            "crypt-first-clear-awards-once-only-xp");
        require(world.GroundLoot.Count == 3 && world.TryAdvanceCrypt() == null,
            "crypt-cannot-advance-and-abandon-ground-loot");

        for (int i = 0; i < 3; i++)
            require(world.TryCollectLoot(), "crypt-collect-all-ground-loot-" + i);
        require(world.GroundLoot.Count == 0 && world.LootCollected == new BigInteger(3),
            "crypt-loot-fully-collected");
        ulong oldHash = world.ComputeWorldHash();
        ArenaSimulation next = world.TryAdvanceCrypt();
        require(next != null && next.CryptDepth == new BigInteger(2),
            "crypt-next-depth-started");
        require(world.CryptDepth == BigInteger.One && world.ComputeWorldHash() == oldHash,
            "crypt-advance-does-not-rewrite-prior-world-state");
        require(object.ReferenceEquals(next.Progression, world.Progression) &&
            next.Progression.TotalExperience == new BigInteger(1770) &&
            next.LootCollected == new BigInteger(3),
            "crypt-next-depth-keeps-permanent-character");
        require(next.Monsters.Count == 3 && next.Monsters.TrueForAll(m => !m.IsDead) &&
            next.PlayerHealth == next.MaxPlayerHealth && next.Kills == 0,
            "crypt-next-depth-spawns-fresh-enemies-and-player");
        require(next.Monsters[2].MaxHealth > world.Monsters[2].MaxHealth,
            "crypt-next-depth-scales-boss-health");
        require(next.TryAdvanceCrypt() == null, "crypt-cannot-double-advance");

        ArenaSimulation sameDepth = new ArenaSimulation(0xFACEUL, world.Progression, new BigInteger(2));
        ArenaSimulation depthOne = new ArenaSimulation(0xFACEUL, world.Progression);
        require(sameDepth.ComputeWorldHash() != depthOne.ComputeWorldHash(),
            "crypt-depth-influences-replay-hash");

        BigInteger hugeDepth = BigInteger.Pow(new BigInteger(10), 60);
        ArenaSimulation abyss = new ArenaSimulation(19UL, world.Progression, hugeDepth);
        require(abyss.CryptDepth == hugeDepth && abyss.Monsters[2].MaxHealth > 0 &&
            abyss.Monsters[2].MaxHealth < int.MaxValue,
            "crypt-depth-beyond-64bit-has-bounded-combat-stats");

        bool rejected = false;
        try { new ArenaSimulation(19UL, null, BigInteger.Zero); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        require(rejected, "crypt-zero-depth-rejected");

        // A cleared crypt must remain a cleared crypt until the player explicitly advances.
        for (int t = 0; t < 5; t++) world.Step();
        require(world.CryptCleared && world.TryAdvanceCrypt() != null,
            "crypt-cleared-state-waits-for-explicit-next-round");
    }
}
