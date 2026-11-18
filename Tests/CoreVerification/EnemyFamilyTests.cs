using System;
using System.Linq;
using AreArpg.Core;

/// <summary>Ork and centaur families on the role model (#74). Each member has a family, a role and readable
/// attack telegraphs; loot stays on the existing Kind contract so old receipts and catalogs are untouched.</summary>
internal static class EnemyFamilyTests
{
    public static void Run(Action<bool, string> require)
    {
        // Orks: melee, ranged shaman, elite warrior.
        var ork = MonsterCatalog.Definition("ork.grunt");
        var shaman = MonsterCatalog.Definition("ork.shaman");
        var warrior = MonsterCatalog.Definition("ork.warrior");
        require(ork != null && shaman != null && warrior != null, "ork-family-defined");
        require(ork.Family == MonsterFamily.Ork && shaman.Family == MonsterFamily.Ork && warrior.Family == MonsterFamily.Ork,
            "ork-family-members");
        require(ork.Role == MonsterRole.Melee && shaman.Role == MonsterRole.Ranged && warrior.Role == MonsterRole.Elite,
            "ork-roles");
        require(shaman.Telegraphs && !ork.Telegraphs && warrior.Telegraphs, "ork-telegraphs");

        // Centaurs: fast ranged skirmisher and an elite charger.
        var archer = MonsterCatalog.Definition("centaur.archer");
        var charger = MonsterCatalog.Definition("centaur.charger");
        require(archer != null && charger != null, "centaur-family-defined");
        require(archer.Family == MonsterFamily.Centaur && charger.Family == MonsterFamily.Centaur, "centaur-family-members");
        require(archer.Role == MonsterRole.Ranged && charger.Role == MonsterRole.Elite, "centaur-roles");
        require(MonsterCatalog.Definition("centaur.archer").MoveSpeedMmPerTick > MonsterCatalog.Definition("ork.grunt").MoveSpeedMmPerTick,
            "centaur-is-faster-than-ork");

        // Every definition is unique by id and valid.
        require(MonsterCatalog.All.Select(d => d.Id).Distinct().Count() == MonsterCatalog.All.Count, "family-ids-unique");
        require(MonsterCatalog.All.All(d => !string.IsNullOrEmpty(d.Id) && d.BaseHealth > 0), "family-definitions-valid");

        // Spawning yields a real monster with the family, role and matching health.
        var w = new ArenaSimulation(0x0A5C1EEDUL);
        var spawn = MonsterCatalog.Spawn("ork.warrior", 1, 4000, 0, 1);
        require(spawn.Family == MonsterFamily.Ork && spawn.Role == MonsterRole.Elite &&
            spawn.Health == spawn.MaxHealth && spawn.MaxHealth > 0, "spawn-carries-family-and-role");

        // A spawned elite closes and hits; a spawned ork grunt fights in melee without a telegraph.
        var elite = new ArenaSimulation(0x0A5C1EEDUL);
        elite.Monsters.Clear();
        elite.Monsters.Add(MonsterCatalog.Spawn("ork.warrior", 1, 3000, 0, 1));
        int before = elite.PlayerHealth;
        bool telegraphed = false;
        for (int i = 0; i < 160; i++) { elite.Step(); if (elite.Monsters[0].BossWindupTicks > 0) telegraphed = true; }
        require(telegraphed, "ork-warrior-telegraphs");
        require(elite.PlayerHealth < before, "ork-warrior-deals-damage");

        // Deterministic replay across two identical spawned family worlds.
        var a = new ArenaSimulation(0x0A5C1EEDUL); a.Monsters.Clear(); a.Monsters.Add(MonsterCatalog.Spawn("centaur.archer", 1, 6000, 0, 1));
        var b = new ArenaSimulation(0x0A5C1EEDUL); b.Monsters.Clear(); b.Monsters.Add(MonsterCatalog.Spawn("centaur.archer", 1, 6000, 0, 1));
        for (int i = 0; i < 80; i++) { a.Step(); b.Step(); }
        require(a.ComputeWorldHash() == b.ComputeWorldHash(), "family-spawn-replay-deterministic");
    }
}
