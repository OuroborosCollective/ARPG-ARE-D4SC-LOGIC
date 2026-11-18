using System;
using System.Numerics;
using AreArpg.Core;

/// <summary>Enemy roles and readable attack telegraphs (#74). Legacy monsters keep their exact behaviour and
/// world hash; only monsters with an explicit role take the new rules.</summary>
internal static class MonsterRoleTests
{
    private static ArenaSimulation RoleWorld(MonsterFamily family, MonsterRole role, long x, long z, int health = 400)
    {
        var w = new ArenaSimulation(0xC0FFEEUL);
        w.Monsters.Clear();
        w.Monsters.Add(new MonsterState
        {
            Id = 1, Kind = 0, Family = family, Role = role,
            Xmm = x, Zmm = z, PreviousXmm = x, PreviousZmm = z, Health = health, MaxHealth = health
        });
        return w;
    }

    public static void Run(Action<bool, string> require)
    {
        // Explicit role rules: speed, damage, armor, reach and telegraph are deterministic constants.
        require(MonsterRoles.SpeedMmPerTick(MonsterRole.Swarm) > MonsterRoles.SpeedMmPerTick(MonsterRole.Melee),
            "role-swarm-is-fast");
        require(MonsterRoles.AttackDamage(MonsterRole.Swarm) < MonsterRoles.AttackDamage(MonsterRole.Melee),
            "role-swarm-hits-weakly");
        require(MonsterRoles.ArmorBp(MonsterRole.Shield) > MonsterRoles.ArmorBp(MonsterRole.Melee),
            "role-shield-is-armored");
        require(MonsterRoles.AttackRangeMm(MonsterRole.Ranged) > MonsterRoles.AttackRangeMm(MonsterRole.Melee),
            "role-ranged-outreaches-melee");
        require(MonsterRoles.TelegraphTicks(MonsterRole.Elite) > 0 && MonsterRoles.TelegraphTicks(MonsterRole.Melee) == 0,
            "role-telegraph-only-where-intended");
        require(MonsterRoles.TelegraphTicks(MonsterRole.Ranged) > 0, "role-ranged-telegraphs");
        require(MonsterRoles.KeepsDistance(MonsterRole.Ranged) && !MonsterRoles.KeepsDistance(MonsterRole.Melee),
            "role-ranged-keeps-distance");

        // A ranged enemy telegraphs, then hits the player from outside melee reach.
        var ranged = RoleWorld(MonsterFamily.Ork, MonsterRole.Ranged, 6000, 0);
        int rangedHealth = ranged.PlayerHealth;
        bool rangedTelegraphed = false;
        for (int i = 0; i < 160; i++)
        {
            ranged.Step();
            if (ranged.Monsters[0].BossWindupTicks > 0) rangedTelegraphed = true;
        }
        require(rangedTelegraphed, "role-ranged-shows-a-telegraph");
        require(ranged.PlayerHealth < rangedHealth, "role-ranged-hits-from-afar");
        require(ranged.Monsters[0].Xmm > 1650, "role-ranged-keeps-its-distance");

        // An elite telegraphs a heavy, avoidable hit.
        var elite = RoleWorld(MonsterFamily.Undead, MonsterRole.Elite, 3000, 0);
        int eliteHealth = elite.PlayerHealth;
        bool eliteTelegraphed = false;
        for (int i = 0; i < 160; i++)
        {
            elite.Step();
            if (elite.Monsters[0].BossWindupTicks > 0) eliteTelegraphed = true;
        }
        require(eliteTelegraphed, "role-elite-telegraphs-heavy-hit");
        require(elite.PlayerHealth < eliteHealth, "role-elite-deals-heavy-damage");

        // A swarm closes the distance faster than a melee enemy from the same start.
        var swarm = RoleWorld(MonsterFamily.Plague, MonsterRole.Swarm, 12000, 0);
        var melee = RoleWorld(MonsterFamily.Human, MonsterRole.Melee, 12000, 0);
        for (int i = 0; i < 10; i++) { swarm.Step(); melee.Step(); }
        require(swarm.Monsters[0].Xmm < melee.Monsters[0].Xmm, "role-swarm-closes-faster-than-melee");

        // Role play stays deterministic: two identical role worlds replay to the same world hash.
        var a = RoleWorld(MonsterFamily.Ork, MonsterRole.Ranged, 6000, 0);
        var b = RoleWorld(MonsterFamily.Ork, MonsterRole.Ranged, 6000, 0);
        for (int i = 0; i < 60; i++) { a.Step(); b.Step(); }
        require(a.ComputeWorldHash() == b.ComputeWorldHash(), "role-replay-deterministic");

        // Review 2026-11-18: the telegraph is dodgeable. Stepping out of the frozen radius avoids the hit,
        // and the monster stands still while it announces.
        var dodge = RoleWorld(MonsterFamily.Undead, MonsterRole.Elite, 2400, 0);
        for (int i = 0; i < 120 && dodge.Monsters[0].BossWindupTicks == 0; i++) dodge.Step();
        require(dodge.Monsters[0].BossWindupTicks > 0, "role-dodge-setup-has-telegraph");
        long standX = dodge.Monsters[0].Xmm, standZ = dodge.Monsters[0].Zmm;
        int dodgeHealth = dodge.PlayerHealth;
        bool frozeDuringWindup = true;
        // Run straight away from the monster while it announces.
        dodge.SetMoveInput(-10000, 0);
        while (dodge.Monsters[0].BossWindupTicks > 0)
        {
            dodge.Step();
            if (dodge.Monsters[0].BossWindupTicks > 0 &&
                (dodge.Monsters[0].Xmm != standX || dodge.Monsters[0].Zmm != standZ)) frozeDuringWindup = false;
        }
        require(frozeDuringWindup, "role-telegraph-freezes-the-monster");
        require(dodge.PlayerHealth == dodgeHealth, "role-telegraph-can-be-dodged");

        // Review 2026-11-18 (CodeRabbit): a running announcement always resolves; it never resumes stale after the
        // player breaks line of sight. Moving far away during the windup ends it with no hit.
        var stale = RoleWorld(MonsterFamily.Undead, MonsterRole.Elite, 2400, 0);
        for (int i = 0; i < 120 && stale.Monsters[0].BossWindupTicks == 0; i++) stale.Step();
        require(stale.Monsters[0].BossWindupTicks > 0, "role-stale-setup-has-telegraph");
        int staleHealth = stale.PlayerHealth;
        stale.Monsters[0].Xmm = 25000; stale.Monsters[0].Zmm = 25000; // far outside the 14 m perception range
        for (int i = 0; i < 20; i++) stale.Step();
        require(stale.Monsters[0].BossWindupTicks == 0, "role-windup-resolves-without-sight");
        require(stale.PlayerHealth == staleHealth, "role-windup-without-sight-deals-no-stale-hit");

        // Review 2026-11-18: role and family take part in the world hash (a role swap is visible).
        var sameSeedRole = RoleWorld(MonsterFamily.Ork, MonsterRole.Ranged, 6000, 0);
        var otherRole = RoleWorld(MonsterFamily.Ork, MonsterRole.Elite, 6000, 0);
        require(sameSeedRole.ComputeWorldHash() != otherRole.ComputeWorldHash(), "role-is-hashed");

        // Legacy monsters keep Role None and their exact golden world hash (no behavioural drift).
        var legacy = new ArenaSimulation(123);
        require(legacy.Monsters[0].Role == MonsterRole.None && legacy.Monsters[0].Family == MonsterFamily.Legacy,
            "legacy-monsters-keep-no-role");
        require(legacy.ComputeWorldHash() == 778990259245021324UL, "legacy-golden-hash-unchanged");

        // The five planned families are named and distinct, so later content can bind to them.
        require(MonsterFamily.Human != MonsterFamily.Ork && MonsterFamily.Centaur != MonsterFamily.Undead &&
            MonsterFamily.BullRider != MonsterFamily.Animal && MonsterFamily.AshBrut != MonsterFamily.Plague,
            "families-are-distinct");
    }
}
