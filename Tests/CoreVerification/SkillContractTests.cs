using System;
using AreArpg.Core;

internal static class SkillContractTests
{
    /// <summary>Verify immutable definitions and the existing skill lifecycle through the real Core owner.</summary>
    public static void Run(Action<bool,string> require)
    {
        string[] ids = { "skill.strike", "skill.heavy.strike", "skill.guard", "skill.ruin.storm" };
        int[] cooldowns = { 2, 4, 50, 100 };
        long[] ranges = { 3100, 4300, 0, 6500 };
        int[] powers = { 10000, 16000, 0, 30000 };
        require(SkillCatalog.Revision == 1 && SkillCatalog.Count == 4, "skill-catalog-version-count");
        for (int slot = 0; slot < SkillCatalog.Count; slot++)
        {
            var definition = SkillCatalog.Get(slot);
            require(definition.Id == ids[slot] && definition.Slot == slot && definition.Revision == 1, "skill-stable-id-" + slot);
            require(definition.CooldownTicks == cooldowns[slot] && definition.RangeMm == ranges[slot] && definition.PowerBp == powers[slot], "skill-legacy-units-" + slot);
            var world = new ArenaSimulation(123);
            world.Monsters[0].Xmm = 1000; world.Monsters[0].Zmm = 0; world.Monsters[0].Health = 200000;
            int focus = world.Resource;
            var result = world.ActivateSkill(slot);
            require(result.Activated && world.Cooldowns[slot] == definition.CooldownTicks, "skill-core-consumes-catalog-cooldown-" + slot);
            require(world.Resource == Math.Min(100, focus - definition.FocusCost + definition.FocusGain), "skill-core-consumes-focus-contract-" + slot);
            require(!world.ActivateSkill(slot).Activated, "skill-no-double-activation-" + slot);
            world.Step();
            require(world.Cooldowns[slot] == cooldowns[slot] - 1, "skill-existing-tick-owner-" + slot);
        }
        require(SkillCatalog.Get(0).FocusGain == 12 && SkillCatalog.Get(1).FocusCost == 25 && SkillCatalog.Get(2).BaseHealing == 170, "skill-resource-healing-balance");
        require(SkillCatalog.Get(2).Target == SkillTarget.Self && SkillCatalog.Get(3).Target == SkillTarget.Area, "skill-target-contracts");
        bool rejected = false;
        try { SkillCatalog.Get(4); } catch (ArgumentOutOfRangeException) { rejected = true; }
        require(rejected, "skill-unknown-slot-rejected");
        ulong[] goldens = { 13266565184721069864UL, 12514476509666749084UL, 16147038201894234482UL };
        int goldenIndex = 0;
        foreach (ulong seed in new[] { 0UL, 123UL, ulong.MaxValue })
        {
            var world = new ArenaSimulation(seed);
            for (int tick = 0; tick < 100; tick++)
            {
                world.SetMoveInput(tick < 40 ? 10000 : 0, 0);
                world.ActivateSkill(tick % 4);
                world.TryCollectLoot();
                world.Step();
            }
            require(world.ComputeWorldHash() == goldens[goldenIndex++], "skill-legacy-replay-golden-" + seed);
        }
    }
}
