using System;
using System.Numerics;
using AreArpg.Core;

internal static class NpcSocialTests
{
    private static string Signed(string payload)
    {
        ulong h = 14695981039346656037UL;
        unchecked { foreach (byte b in System.Text.Encoding.UTF8.GetBytes(payload)) { h ^= b; h *= 1099511628211UL; } }
        return payload + "|" + h.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static void Run(Action<bool, string> require)
    {
        var world = new ArenaSimulation(121);
        require(world.Npcs.Count == 3, "npc-first-region-has-three-distinct-people");
        var npc = world.Npcs[0]; npc.Xmm = 1000; npc.Zmm = 0;
        var other = world.Npcs[1];
        require(world.Progression.Social.Provisions == 3, "npc-new-character-has-three-real-provisions");
        require(world.InteractNpc(0, NpcAction.Greet).Accepted, "npc-player-can-greet-nearby-person");
        BigInteger meetings = npc.Memory.Meetings;
        require(!world.InteractNpc(0, NpcAction.Greet).Accepted && npc.Memory.Meetings == meetings, "npc-interaction-cooldown-does-not-invent-events");
        for (int i = 0; i < 10; i++) world.Step();
        npc.Xmm = 1000; npc.Zmm = 0;
        require(world.InteractNpc(0, NpcAction.Help).Accepted, "npc-real-need-can-be-helped");
        require(world.Progression.Social.Provisions == 2 && npc.Memory.PositivePoints == 8, "npc-help-consumes-provision-and-records-positive-account");
        require(npc.Memory.Disposition(npc.Personality) == NpcDisposition.Friendly, "npc-helped-person-becomes-friendly");
        require(CharacterProgression.TryLoad(world.Progression.SaveString(), out var grateful) &&
            new ArenaSimulation(121, grateful).Npcs[0].Greeting().Contains("Beistand"), "npc-gratitude-and-recognition-survive-reload");
        require(other.Memory.PositivePoints.IsZero, "npc-personal-memory-is-not-global-reputation");
        for (int i = 0; i < 10; i++) world.Step();
        npc.Xmm = 1000; npc.Zmm = 0;
        require(world.InteractNpc(0, NpcAction.Attack).Accepted && npc.Memory.Health < NpcMemory.MaxHealth, "npc-assault-applies-real-damage");
        require(npc.Memory.NegativePoints > 0 && npc.Memory.PositivePoints == 8, "npc-positive-and-negative-accounts-remain-separate");
        for (int i = 0; i < 10; i++) world.Step();
        npc.Xmm = 1000; npc.Zmm = 0;
        int purse = npc.Memory.Purse;
        require(world.InteractNpc(0, NpcAction.Rob).Accepted, "npc-robbery-is-a-real-interaction");
        require(npc.Memory.Purse == 0 && world.Progression.Social.Coins == purse, "npc-robbery-transfers-existing-coins-only");
        BigInteger coins = world.Progression.Social.Coins;
        require(npc.Memory.Disposition(npc.Personality) == NpcDisposition.Hostile, "npc-repeated-harm-escalates-hostility-despite-old-help");
        for (int i = 0; i < 10; i++) world.Step();
        npc.Xmm = 1000; npc.Zmm = 0;
        require(!world.InteractNpc(0, NpcAction.Rob).Accepted && world.Progression.Social.Coins == coins, "npc-empty-purse-cannot-duplicate-rewards");
        string save = world.Progression.SaveString();
        CharacterProgression loaded;
        require(save.StartsWith("AC5|") && CharacterProgression.TryLoad(save, out loaded), "npc-ac5-roundtrip-loads");
        require(CharacterProgression.TryLoad(save, out loaded) && loaded.Social.GetMemory(0).PositivePoints == 8 &&
            loaded.Social.GetMemory(0).NegativePoints == npc.Memory.NegativePoints &&
            loaded.Social.Coins == coins && loaded.StableHash == world.Progression.StableHash, "npc-reload-remembers-help-harm-and-coins");
        var resumed = new ArenaSimulation(121, loaded);
        var familiar = resumed.Npcs[0]; familiar.Xmm = 1000; familiar.Zmm = 0;
        require(resumed.InteractNpc(0, NpcAction.Greet).Text.Contains("Bleib"), "npc-hostile-greeting-recognizes-player-after-reload");
        var guardWorld = new ArenaSimulation(122);
        var guard = guardWorld.Npcs[2]; guard.Xmm = 1000; guard.Zmm = 0;
        require(guardWorld.InteractNpc(2, NpcAction.Attack).Accepted, "npc-brave-guard-can-be-antagonized");
        int hp = guardWorld.PlayerHealth;
        foreach (var m in guardWorld.Monsters) { m.Xmm = 25000; m.Zmm = 25000; }
        guardWorld.Step();
        require(guard.Behavior == NpcBehavior.Attack && guardWorld.PlayerHealth < hp, "npc-brave-hostile-person-retaliates-through-real-core");
        var knockdown = new ArenaSimulation(131);
        var victim = knockdown.Npcs[2]; victim.Xmm = 1000; victim.Zmm = 0; victim.Memory.Health = 1;
        require(knockdown.InteractNpc(2, NpcAction.Attack).Accepted && victim.Memory.Health == 0 &&
            victim.Memory.RecoveryTicks == 300, "npc-knockdown-has-deterministic-recovery-not-unapproved-permadeath");
        for (int i = 0; i < 10; i++) knockdown.Step();
        require(knockdown.InteractNpc(2, NpcAction.Rob).Accepted, "npc-downed-person-can-actually-be-robbed");
        require(CharacterProgression.TryLoad(knockdown.Progression.SaveString(), out loaded) &&
            loaded.Social.GetMemory(2).RecoveryTicks == 290, "npc-recovery-and-robbery-survive-save");
        var recovered = new ArenaSimulation(131, loaded);
        foreach (var m in recovered.Monsters) { m.Xmm = 25000; m.Zmm = 25000; }
        for (int i = 0; i < 290; i++) recovered.Step();
        var survivor = recovered.Npcs[2];
        require(survivor.Memory.Health > 0 && survivor.Memory.Purse == 0 && survivor.Memory.NegativePoints > 0,
            "npc-recovers-without-erasing-harm-or-regenerating-stolen-coins");
        var fleeWorld = new ArenaSimulation(123);
        var coward = fleeWorld.Npcs[0]; coward.Xmm = 1000; coward.Zmm = 0;
        coward.Memory.Record(NpcAction.Attack, 3);
        fleeWorld.Step();
        require(coward.Behavior == NpcBehavior.Flee, "npc-cautious-person-flees-the-same-danger");
        var needs = new ArenaSimulation(124);
        var hungry = needs.Npcs[1]; hungry.Memory.Hunger = 900; hungry.Xmm = ArenaSimulation.CampXmm; hungry.Zmm = ArenaSimulation.CampZmm;
        int food = needs.Progression.Social.CampProvisions;
        needs.Step();
        require(hungry.Behavior == NpcBehavior.Eat && hungry.Memory.Hunger < 900 && needs.Progression.Social.CampProvisions == food - 1, "npc-need-drives-real-food-consumption");
        hungry.Memory.Fatigue = 900; hungry.Memory.Hunger = 0;
        needs.Step();
        require(hungry.Behavior == NpcBehavior.Rest && hungry.Memory.Fatigue < 900, "npc-fatigue-drives-rest");
        while (needs.Progression.Social.CampProvisions > 0)
        { hungry.Memory.Hunger = 900; needs.Progression.Social.EatAtCamp(hungry.Memory); }
        hungry.Memory.Hunger = 900;
        require(!needs.Progression.Social.EatAtCamp(hungry.Memory) && hungry.Memory.Hunger == 900,
            "npc-empty-food-cache-does-not-invent-meals");
        var explorer = new ArenaSimulation(125);
        long x = explorer.Npcs[0].Xmm, z = explorer.Npcs[0].Zmm;
        explorer.Step();
        require(explorer.Npcs[0].Xmm != x || explorer.Npcs[0].Zmm != z, "npc-adventurer-moves-with-own-purpose");
        for (int i = 0; i < 100; i++) npc.Memory.Record(NpcAction.Greet, 0);
        require(npc.Memory.RecentEvents.Length == 6 && npc.Memory.NegativePoints > 0, "npc-bounded-local-history-keeps-lifetime-action-accounts");
        var empty = new ArenaSimulation(126); var satisfied = empty.Npcs[1]; satisfied.Xmm = 1000; satisfied.Zmm = 0;
        satisfied.Memory.Health = NpcMemory.MaxHealth; satisfied.Memory.Hunger = 0; satisfied.Memory.Fatigue = 0;
        require(!empty.InteractNpc(1, NpcAction.Help).Accepted && empty.Progression.Social.Provisions == 3, "npc-unneeded-help-cannot-farm-friendship");
        require(!empty.InteractNpc(99, NpcAction.Help).Accepted && !empty.InteractNpc(1, (NpcAction)99).Accepted, "npc-invalid-target-and-action-rejected");
        var distant = new ArenaSimulation(127);
        require(!distant.InteractNpc(1, NpcAction.Help).Accepted, "npc-interaction-requires-real-proximity");
        string old = Signed("AC4|160|8|1|1|1|0|0|2|150|0|0|0");
        require(CharacterProgression.TryLoad(old, out loaded) && loaded.PracticeExperience(0) == 150 &&
            loaded.UnlockedCryptDepth == 2 && loaded.Social.GetMemory(0).Meetings.IsZero, "npc-ac4-migration-keeps-training-without-invented-relationships");
        string[] fields = save.Split('|'); string[] chunks = fields[13].Split(';');
        string[] header = chunks[0].Split(','); header[0] = "-1"; chunks[0] = string.Join(",", header);
        fields[13] = string.Join(";", chunks);
        require(!CharacterProgression.TryLoad(Signed(string.Join("|", fields, 0, 14)), out loaded), "npc-negative-provisions-rejected-even-with-valid-checksum");
        var a = new ArenaSimulation(128); var b = new ArenaSimulation(128);
        for (int i = 0; i < 150; i++) { a.Step(); b.Step(); require(a.ComputeWorldHash() == b.ComputeWorldHash(), "npc-replay-tick-" + i); }
        b.Progression.Social.GetMemory(0).Record(NpcAction.Attack, 2);
        require(a.ComputeWorldHash() != b.ComputeWorldHash(), "npc-replay-hash-binds-personal-memory");
    }
}

