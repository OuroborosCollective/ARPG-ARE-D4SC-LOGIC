using System;
using System.Linq;
using AreArpg.Core;

/// <summary>Owner decisions 2026-11-17: every weapon type has its own character; shields block and slow the bearer.</summary>
internal static class WeaponProfileTests
{
    private static WeaponDefinition Def(string id) =>
        Enumerable.Range(0, WeaponCatalog.Count).Select(WeaponCatalog.Get).First(d => d.Id == id);

    private static LootItem GiveId(CharacterProgression p, string id)
    {
        for (ulong seed = 0; seed < 20000; seed++)
        {
            var item = LootCatalog.Version1.Generate(seed, 1, 1, 1, 0, p.LifetimeKills + 1); var gear = new WeaponItem(item);
            if (gear.Definition.Id != id) continue;
            p.RecordKill(); if (!p.CollectItem(item)) throw new Exception("Profile fixture receipt rejected"); return item;
        }
        throw new Exception("Unreachable weapon " + id);
    }

    /// <summary>Expected damage per 100-ms tick (x1000) against a ghoul, from the real Core damage rule.</summary>
    private static long PerTick(WeaponDefinition d, int skillLevel)
    {
        var profile = WeaponProfiles.For(d);
        var stats = new CombatStats(d.BasePower * profile.DamageBp / 10000L, CombatStats.BaseAdditiveBp, profile.CritChanceBp);
        long normal = CombatRules.PreviewDamage(stats, 10000, 0, false), crit = CombatRules.PreviewDamage(stats, 10000, 0, true);
        long expected = normal * (10000 - profile.CritChanceBp) + crit * profile.CritChanceBp; // x10000
        return expected * profile.Accuracy(skillLevel) / 10000L * 1000L / 10000L / d.StrikeCooldown;
    }

    private static void Heal(ArenaSimulation w) =>
        typeof(ArenaSimulation).GetProperty("PlayerHealth").GetSetMethod(true).Invoke(w, new object[] { w.MaxPlayerHealth });

    private static ArenaSimulation Arena(CharacterProgression p, out MonsterState target)
    {
        var w = new ArenaSimulation(8, p, 1, LootCatalog.Version1);
        target = w.Monsters[0]; target.Xmm = 1000; target.Zmm = 0; target.Health = target.MaxHealth = 100000000;
        foreach (var other in w.Monsters) if (other != target) { other.Health = 0; other.LootGranted = true; other.Xmm = 40000; }
        return w;
    }

    public static void Run(Action<bool, string> require)
    {
        WeaponDefinition dagger = Def("dagger_1h_newbie.1"), sword = Def("sword_1h_newbie.1"), sword2 = Def("sword_2h_newbie.1"),
            axe1 = Def("axe_1h_newbie.1"), axe2 = Def("axe_2hl_newbie.1"), bow = Def("bow_newbie.1"), staff = Def("staff_newbie.1");
        WeaponProfile pd = WeaponProfiles.For(dagger), ps = WeaponProfiles.For(sword), pa = WeaponProfiles.For(axe2), pb = WeaponProfiles.For(bow);

        // Character of each weapon type (owner decisions).
        require(pd.CritChanceBp > ps.CritChanceBp && ps.CritChanceBp > pa.CritChanceBp, "wp-dagger-crits-most-two-handed-axe-least");
        require(dagger.RangeMm < sword.RangeMm && sword.RangeMm < axe2.RangeMm && bow.RangeMm > axe2.RangeMm, "wp-reach-dagger-sword-axe-bow");
        require(dagger.StrikeCooldown < sword.StrikeCooldown && sword.StrikeCooldown < axe2.StrikeCooldown, "wp-dagger-fastest-two-handed-axe-slowest-strike");
        require(bow.TwoHanded && axe2.TwoHanded && !sword.TwoHanded && !dagger.TwoHanded, "wp-bow-and-great-axe-are-two-handed");
        require(pd.MoveSpeedBp > ps.MoveSpeedBp && ps.MoveSpeedBp > pa.MoveSpeedBp, "wp-heavier-weapons-move-slower");
        require(pb.Accuracy(1) == 6125 && pb.Accuracy(2) - pb.Accuracy(1) == 125 && pb.Accuracy(32) == 10000 && pb.Accuracy(99) == 10000 &&
                ps.Accuracy(1) == 10000 && pd.Accuracy(1) == 10000 && pa.Accuracy(1) == 10000, "wp-bow-gains-1.25-percent-accuracy-per-level-melee-never-misses");
        long eDagger = PerTick(dagger, 1), eSword = PerTick(sword, 1), eAxe2 = PerTick(axe2, 1), eBowLow = PerTick(bow, 1), eBowHigh = PerTick(bow, 32);
        long[] all = { eDagger, eSword, PerTick(sword2, 1), PerTick(axe1, 1), eAxe2, eBowHigh, PerTick(staff, 1) };
        require(eDagger < eSword && eSword < eAxe2 && eAxe2 == all.Max(), "wp-damage-over-time-dagger-sword-great-axe");
        require(eDagger < eBowHigh && eBowHigh < eSword, "wp-accurate-bow-between-dagger-and-sword");
        require(eBowLow < eBowHigh, "wp-low-level-bow-loses-damage-to-misses");
        require(eSword < PerTick(axe1, 1) && PerTick(axe1, 1) < PerTick(sword2, 1) && PerTick(sword2, 1) < eAxe2,
                "wp-heavier-melee-weapons-deal-more-over-time");

        // The dagger's higher crit chance reaches the real combat roll.
        var p = new CharacterProgression(); p.Equipment.Enable(p);
        var daggerItem = GiveId(p, dagger.Id); var swordItem = GiveId(p, sword.Id); var axeItem = GiveId(p, axe2.Id);
        var bowItem = GiveId(p, bow.Id); var shieldItem = GiveId(p, "shield_newbie.1");
        var w = Arena(p, out var target);
        require(w.TryEquipWeapon(daggerItem.KillOrdinal) && new CombatStats(p, 0).CritChanceBp == pd.CritChanceBp &&
                w.TryEquipWeapon(swordItem.KillOrdinal) && new CombatStats(p, 0).CritChanceBp == ps.CritChanceBp, "wp-equipped-weapon-sets-the-crit-chance");
        require(w.TryUnequipWeapon() && new CombatStats(p, 0).CritChanceBp == CombatRules.CriticalChanceBp, "wp-unarmed-keeps-the-default-crit");

        // Bow: real misses at low level, none once accurate; misses never train.
        require(w.TryEquipWeapon(bowItem.KillOrdinal), "wp-fixture-equips-bow");
        int shots = 0, misses = 0; long xpFromHits = 0;
        for (int i = 0; i < 160; i++)
        {
            for (int t = 0; t < 30 && w.Cooldowns[0] > 0; t++) w.Step();
            Heal(w); target.Xmm = 6000; target.Zmm = 0; target.AttackCooldownTicks = 50;
            int before = target.Health; long xp = p.WeaponMastery.Experience(WeaponFamily.Bow);
            var r = w.ActivateSkill(0); if (!r.Activated) continue;
            shots++;
            if (target.Health == before) { misses++; require(r.Misses == 1 && p.WeaponMastery.Experience(WeaponFamily.Bow) == xp, "wp-a-miss-deals-and-trains-nothing"); }
            else xpFromHits += p.WeaponMastery.Experience(WeaponFamily.Bow) - xp;
        }
        require(shots >= 40 && misses > 0 && misses < shots && p.WeaponMastery.Level(WeaponFamily.Bow) < 32, "wp-low-level-bow-really-misses-sometimes");
        p.WeaponMastery.Record(WeaponFamily.Bow, WeaponMasteryRules.ExperienceForLevel(32) - p.WeaponMastery.Experience(WeaponFamily.Bow));
        int lateMisses = 0;
        for (int i = 0; i < 60; i++)
        {
            for (int t = 0; t < 30 && w.Cooldowns[0] > 0; t++) w.Step();
            Heal(w); target.Xmm = 6000; target.Zmm = 0; target.AttackCooldownTicks = 50;
            var r = w.ActivateSkill(0); if (r.Activated && r.Misses > 0) lateMisses++;
        }
        require(lateMisses == 0, "wp-bow-level-32-never-misses");

        // Shield: two-handed weapons are refused while it is on; it blocks and trains on every attack received.
        require(w.TryEquipWeapon(swordItem.KillOrdinal) && w.TryEquipWeapon(shieldItem.KillOrdinal), "wp-one-handed-weapon-plus-shield");
        require(!w.TryEquipWeapon(axeItem.KillOrdinal) && !w.TryEquipWeapon(bowItem.KillOrdinal) &&
                p.Equipment.Shield != null && p.Equipment.Weapon.Definition.Id == sword.Id, "wp-shield-refuses-two-handed-weapons");
        int attacks0 = w.IncomingAttacks, blocks0 = w.BlockedAttacks;
        for (int i = 0; i < 600 && w.IncomingAttacks - attacks0 < 30; i++)
        {
            Heal(w); target.Xmm = 1000; target.Zmm = 0; w.Step();
        }
        int received = w.IncomingAttacks - attacks0;
        require(received == 30 && p.WeaponMastery.Experience(WeaponFamily.Shield) == received * (long)PracticeRules.ExperienceFromEffectiveAmount(40),
                "wp-shield-trains-on-every-attack-received");
        p.WeaponMastery.Record(WeaponFamily.Shield, WeaponMasteryRules.ExperienceForLevel(99) - p.WeaponMastery.Experience(WeaponFamily.Shield));
        require(WeaponProfiles.BlockChanceBp(p.WeaponMastery.Level(WeaponFamily.Shield)) == 2475 && WeaponProfiles.BlockChanceBp(1) == 25,
                "wp-block-chance-quarter-percent-per-shield-level");
        int a1 = w.IncomingAttacks, b1 = w.BlockedAttacks, lostOnHits = 0, hitsTaken = 0; bool blockedTookDamage = false;
        for (int i = 0; i < 20000 && w.IncomingAttacks - a1 < 400; i++)
        {
            Heal(w); target.Xmm = 1000; target.Zmm = 0;
            int hp = w.PlayerHealth, attacks = w.IncomingAttacks, blocks = w.BlockedAttacks;
            w.Step();
            if (w.BlockedAttacks > blocks) blockedTookDamage |= w.PlayerHealth != hp;
            else if (w.IncomingAttacks > attacks) { hitsTaken++; lostOnHits += hp - w.PlayerHealth; }
        }
        int blocked = w.BlockedAttacks - b1;
        require(w.IncomingAttacks - a1 == 400 && blocked > 60 && blocked < 140, "wp-level-99-shield-blocks-about-a-quarter");
        require(!blockedTookDamage && hitsTaken > 0 && lostOnHits > 0, "wp-a-block-takes-zero-damage");
        require(w.TryUnequipWeapon(true) && w.TryEquipWeapon(axeItem.KillOrdinal) && !w.TryEquipWeapon(shieldItem.KillOrdinal),
                "wp-two-handed-weapon-after-removing-the-shield");
        int unshieldedBlocks = w.BlockedAttacks; long shieldXp = p.WeaponMastery.Experience(WeaponFamily.Shield);
        for (int i = 0; i < 200; i++) { Heal(w); target.Xmm = 1000; target.Zmm = 0; w.Step(); }
        require(w.BlockedAttacks == unshieldedBlocks && p.WeaponMastery.Experience(WeaponFamily.Shield) == shieldXp, "wp-no-shield-no-block-no-training");

        // Movement: shields and heavy weapons slow the bearer.
        var q = new CharacterProgression(); q.Equipment.Enable(q);
        var qDagger = GiveId(q, dagger.Id); var qAxe = GiveId(q, axe2.Id); var qSword = GiveId(q, sword.Id); var qShield = GiveId(q, "shield_newbie.1");
        long Moved(Func<ArenaSimulation, bool> gear)
        {
            var m = Arena(q, out var dummy); dummy.Health = 0; dummy.LootGranted = true; dummy.Xmm = 40000;
            if (!gear(m)) throw new Exception("Movement fixture gear rejected");
            long z = m.PlayerZmm; m.SetMoveInput(0, 10000); for (int t = 0; t < 4; t++) m.Step(); m.SetMoveInput(0, 0);
            return m.PlayerZmm - z;
        }
        long unarmed = Moved(m => m.TryUnequipWeapon() || true);
        long withDagger = Moved(m => m.TryEquipWeapon(qDagger.KillOrdinal));
        long withAxe = Moved(m => m.TryUnequipWeapon(true) | true && m.TryEquipWeapon(qAxe.KillOrdinal));
        long withShield = Moved(m => m.TryEquipWeapon(qSword.KillOrdinal) && m.TryEquipWeapon(qShield.KillOrdinal));
        require(unarmed == 4 * ArenaSimulation.PlayerSpeedMmPerTick && withDagger == unarmed, "wp-dagger-moves-at-full-speed");
        require(withAxe == 4 * (ArenaSimulation.PlayerSpeedMmPerTick * pa.MoveSpeedBp / 10000) && withAxe < withDagger, "wp-great-axe-moves-slower");
        require(withShield == 4 * (ArenaSimulation.PlayerSpeedMmPerTick * (ps.MoveSpeedBp - WeaponProfiles.ShieldMovePenaltyBp) / 10000) &&
                withShield < withAxe, "wp-shield-bearer-moves-noticeably-slower");

        // The shield skill persists like the weapon skills.
        string saved = p.SaveString();
        require(saved.StartsWith("AC21|", StringComparison.Ordinal) && CharacterProgression.TryLoad(saved, out var loaded) &&
                loaded.WeaponMastery.Level(WeaponFamily.Shield) == 99 && loaded.SaveString() == saved, "wp-shield-skill-survives-save-and-load");
    }
}
