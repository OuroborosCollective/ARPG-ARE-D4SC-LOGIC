using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>
/// Owner request 2026-11-26: the hunter's leather set Rotfährte (with its longbow) and the sorcerer's linen set Rußkantor
/// (with a short and a long staff) are rare random loot (2 percent); with no hunter or sorcerer enemies in the game, the
/// forests stand in for the hunters and the ghouls for the sorcerers (25 percent). Leather and cloth follow ArmorRules.
/// </summary>
internal static class HunterCantorTests
{
    public static void Run(Action<bool, string> check)
    {
        Rolls(check);
        Finds(check);
        SharedLootKind(check);
        Wearing(check);
        Saves(check);
        SteadyHand(check);
    }

    private static List<LootItem> Loot(int count, int kind)
    {
        var items = new List<LootItem>();
        for (int i = 0; items.Count < count && i < count * 4; i++)
        {
            var item = LootCatalog.Version1.GenerateFlagged(131UL + (ulong)(i / 50), BigInteger.One, (ulong)(i * 13), i % 50 + 1, kind, new BigInteger(i + 1), 0);
            if (item != null) items.Add(item);
        }
        return items;
    }

    private static double Share(List<LootItem> items, Func<LootItem, RegionalRelic> roll) =>
        items.Count(i => roll(i) != RegionalRelic.None) / (double)items.Count;

    private static void Rolls(Action<bool, string> check)
    {
        var none = CharacterProgression.CreateEntryProfile().Equipment;
        var ghouls = Loot(6000, 0);
        var knights = Loot(20000, 1);
        bool Near(double share, int bp) => Math.Abs(share - bp / 10000.0) <= Math.Max(.004, bp / 10000.0 * .15);

        double forest = Share(ghouls, i => RotfaehrteSet.Roll(i, none, true)), elsewhere = Share(knights, i => RotfaehrteSet.Roll(i, none, false));
        check(Near(forest, RotfaehrteSet.ForestChanceBp), "rotfaehrte-forest-chance-" + forest.ToString("0.000"));
        check(Near(elsewhere, RotfaehrteSet.LootChanceBp), "rotfaehrte-loot-chance-" + elsewhere.ToString("0.000"));
        double ghoul = Share(ghouls, i => RusskantorSet.Roll(i, none)), knight = Share(knights, i => RusskantorSet.Roll(i, none));
        check(Near(ghoul, RusskantorSet.GhoulChanceBp), "russkantor-ghoul-chance-" + ghoul.ToString("0.000"));
        check(Near(knight, RusskantorSet.LootChanceBp), "russkantor-loot-chance-" + knight.ToString("0.000"));

        check(ghouls.All(i => RotfaehrteSet.Roll(i, none, true) == RotfaehrteSet.Roll(i, none, true) && RusskantorSet.Roll(i, none) == RusskantorSet.Roll(i, none)),
            "hunter-cantor-rolls-deterministic");
        check(RotfaehrteSet.Pieces.All(id => ghouls.Any(i => RotfaehrteSet.Roll(i, none, true) == id)), "rotfaehrte-every-piece-can-be-found");
        check(RusskantorSet.Pieces.All(id => ghouls.Any(i => RusskantorSet.Roll(i, none) == id)), "russkantor-every-piece-can-be-found");
        check(ghouls.Count(i => RotfaehrteSet.Roll(i, none, true) != RegionalRelic.None && RusskantorSet.Roll(i, none) != RegionalRelic.None) <
            ghouls.Count(i => RotfaehrteSet.Roll(i, none, true) != RegionalRelic.None) / 2, "hunter-cantor-rolls-independent");

        var p = CharacterProgression.CreateEntryProfile();
        foreach (var id in RusskantorSet.Pieces.Take(9)) p.Equipment.ClaimRegionalRelic(p, id);
        check(ghouls.Select(i => RusskantorSet.Roll(i, p.Equipment)).Where(id => id != RegionalRelic.None).All(id => id == RegionalRelic.K_Langstab),
            "russkantor-only-missing-pieces");
        p.Equipment.ClaimRegionalRelic(p, RegionalRelic.K_Langstab);
        check(ghouls.All(i => RusskantorSet.Roll(i, p.Equipment) == RegionalRelic.None), "russkantor-nothing-after-all-ten");
        var world = new ArenaSimulation(5, CharacterProgression.CreateEntryProfile(), BigInteger.One, LootCatalog.Version1);
        check(RotfaehrteSet.Pieces.Concat(RusskantorSet.Pieces).All(id => !world.TryGetRegionalRelicSource(id, out _, out _)) &&
            RotfaehrteSet.Pieces.All(id => RegionalRelics.Location(id) == "Seltene Beute · oft in den Wäldern") &&
            RusskantorSet.Pieces.All(id => RegionalRelics.Location(id) == "Seltene Beute · oft bei Ghulen"), "hunter-cantor-never-lie-in-the-world");
        check(RotfaehrteSet.Pieces.Take(8).Select(RegionalRelics.Slot).Distinct().Count() == 8 &&
            RusskantorSet.Pieces.Take(8).Select(RegionalRelics.Slot).Distinct().Count() == 8, "hunter-cantor-eight-places-each");
    }

    private static void Finds(Action<bool, string> check)
    {
        // In the crypt a ghoul's loot may carry a piece of the sorcerer's set.
        var crypt = Crypt(141);
        var ghoulLoot = Lucky(crypt, 0, false, out var cantor);
        check(Pick(crypt, ghoulLoot) && crypt.LastGearFind == cantor && RusskantorSet.IsPiece(cantor) && !crypt.InHuntingGrounds,
            "russkantor-found-under-a-ghouls-loot");

        // In the forest towards Aschenzoll the hunter's set turns up.
        var forest = WoodlandTests.Enter();
        forest.Progression.Equipment.Enable(forest.Progression);
        var forestLoot = Lucky(forest, 1, true, out var hunter);
        check(forest.InHuntingGrounds && Pick(forest, forestLoot) && forest.LastGearFind == hunter && RotfaehrteSet.IsPiece(hunter),
            "rotfaehrte-found-in-the-forest");
    }

    /// <summary>The world's next kill: a receipt from an enemy of <paramref name="kind"/> that carries a piece of exactly one of the two sets.</summary>
    private static LootItem Lucky(ArenaSimulation w, int kind, bool forest, out RegionalRelic piece)
    {
        w.Progression.RecordKill();
        var gear = w.Progression.Equipment;
        for (ulong seed = 1; seed < 40000; seed++)
        {
            var item = LootCatalog.Version1.GenerateFlagged(seed, BigInteger.One, seed, 1, kind, w.Progression.LootOrdinal, 0);
            if (item == null || AschenwachtGear.Roll(item, gear) != RegionalRelic.None || NachtvesperSet.Roll(item, gear) != RegionalRelic.None) continue;
            var hunter = RotfaehrteSet.Roll(item, gear, forest);
            piece = forest ? hunter : hunter == RegionalRelic.None ? RusskantorSet.Roll(item, gear) : RegionalRelic.None;
            if (piece != RegionalRelic.None) return item;
        }
        throw new Exception("no lucky loot found");
    }

    private static void SharedLootKind(Action<bool, string> check)
    {
        // Compare both rates against the same generated receipt. A source counts only if its rates disagree.
        foreach (var source in new[] { (Depth: 1, Ork: false), (Depth: 1, Ork: true), (Depth: 2, Ork: false) })
        {
            bool witnessed = false;
            for (ulong seed = 1; seed <= 80 && !witnessed; seed++)
            {
                var profile = source.Depth == 1 ? CharacterProgression.CreateEntryProfile() : new CharacterProgression();
                profile.Equipment.Enable(profile);
                var world = new ArenaSimulation(seed, profile, new BigInteger(source.Depth), LootCatalog.Version1);
                var enemy = source.Ork ? MonsterCatalog.Spawn("ork.grunt", 1, 1000, 0, 1) : world.Monsters[0];
                world.Monsters.Clear();
                enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1;
                world.Monsters.Add(enemy);
                world.ActivateSkill(3);
                var item = world.GroundLoot.Single().Item;
                if (!enemy.IsDead || item == null) throw new Exception("expected a kill and its receipt");
                var gear = profile.Equipment;
                if (NachtvesperSet.Roll(item, gear) != RegionalRelic.None || RotfaehrteSet.Roll(item, gear, false) != RegionalRelic.None) continue;
                var ordinary = RusskantorSet.Roll(item, gear, false);
                var ghoul = RusskantorSet.Roll(item, gear, true);
                if (ordinary == ghoul) continue;
                var expected = source.Depth == 1 && !source.Ork ? ghoul : ordinary;
                check(world.TryCollectLoot() && world.LastGearFind == expected, "russkantor-matches-actual-source-" + source);
                witnessed = true;
            }
            check(witnessed, "russkantor-found-distinguishing-receipt-" + source);
        }
    }

    private static CharacterProgression Dressed(RegionalRelic[] set)
    {
        var p = CharacterProgression.CreateEntryProfile();
        p.Equipment.Enable(p);
        foreach (var id in set) p.Equipment.ClaimRegionalRelic(p, id);
        foreach (var id in set) if (RegionalRelics.HandDefinition(id) == null) p.Equipment.SetRegionalRelicEquipped(id, true);
        return p;
    }

    private static void Wearing(Action<bool, string> check)
    {
        var hunter = Dressed(RotfaehrteSet.Pieces);
        check(hunter.WornProtectionBp == 1500 && hunter.WornHealingEfficiencyBp == 7500, "rotfaehrte-is-leather");
        var cantor = Dressed(RusskantorSet.Pieces);
        check(hunter.Equipment.ArmourSetProgress(RotfaehrteSet.Pieces) == (8, 8) &&
            cantor.Equipment.ArmourSetProgress(RusskantorSet.Pieces) == (8, 8), "armour-progress-excludes-bow-and-alternative-staffs");
        check(cantor.WornProtectionBp == 0 && cantor.WornHealingEfficiencyBp == 10000, "russkantor-is-cloth");
        check(CombatRules.MaximumHealth(hunter) == CombatRules.MaximumHealth(CharacterProgression.CreateEntryProfile()) &&
            hunter.NachtvesperPiecesWorn == 0, "hunter-cantor-no-set-bonus-yet");

        var bow = RegionalRelics.HandDefinition(RegionalRelic.F_Langbogen);
        var shortStaff = RegionalRelics.HandDefinition(RegionalRelic.K_Kurzstab);
        var longStaff = RegionalRelics.HandDefinition(RegionalRelic.K_Langstab);
        check(bow != null && bow.Family == WeaponFamily.Bow && bow.TwoHanded && bow.RangeMm == 12000, "rotfaehrte-slate-longbow");
        check(shortStaff != null && shortStaff.Family == WeaponFamily.Staff && !shortStaff.TwoHanded &&
            longStaff != null && longStaff.Family == WeaponFamily.Staff && longStaff.TwoHanded && longStaff.BasePower > shortStaff.BasePower,
            "russkantor-short-and-long-staff");

        var w = new ArenaSimulation(142, Dressed(RusskantorSet.Pieces.Concat(RotfaehrteSet.Pieces).ToArray()), BigInteger.One, LootCatalog.Version1);
        var gear = w.Progression.Equipment;
        check(w.TrySetRegionalRelicEquipped(RegionalRelic.F_Langbogen, true) && gear.Weapon.Definition.Name == "Schwarzschiefer-Langbogen", "rotfaehrte-bow-wielded");
        check(w.TrySetRegionalRelicEquipped(RegionalRelic.K_Kurzstab, true) && gear.Weapon.Definition.Name == "Schwarzschieferholz-Kurzstab" &&
            !gear.IsRegionalRelicEquipped(RegionalRelic.F_Langbogen), "russkantor-one-weapon-at-a-time");
        check(w.TrySetRegionalRelicEquipped(RegionalRelic.K_Kapuze, true) && w.TrySetRegionalRelicEquipped(RegionalRelic.F_Gugel, true) &&
            !gear.IsRegionalRelicEquipped(RegionalRelic.K_Kapuze) &&
            gear.WornRegionalRelic(RegionalRelicSlot.Head) == RegionalRelic.F_Gugel, "hunter-cantor-one-hood-at-a-time");
    }

    private static (int Hits, int Misses, ulong WorldHash) BowShot(ulong seed, int slot, int worn)
    {
        var p = Dressed(RotfaehrteSet.Pieces);
        p.UseCombatV2();
        var w = new ArenaSimulation(seed, p, BigInteger.One, LootCatalog.Version1);
        w.TrySetRegionalRelicEquipped(RegionalRelic.F_Langbogen, true);
        for (int i = worn; i < 8; i++) w.TrySetRegionalRelicEquipped(RotfaehrteSet.Pieces[i], false);
        var target = w.Monsters[0];
        w.Monsters.Clear();
        target.Xmm = w.PlayerXmm + 4000; target.Zmm = w.PlayerZmm;
        target.Health = target.MaxHealth = 5000; target.AttackCooldownTicks = 1000;
        w.Monsters.Add(target);
        if (!w.ActivateSkill(slot).Activated) throw new Exception("bow shot did not start");
        for (int tick = 0; tick < 12 && w.Impacts.Count == 0; tick++) w.Step();
        var impact = w.Impacts.Single();
        return (impact.Hits, impact.Misses, w.ComputeWorldHash());
    }

    private static void SteadyHand(Action<bool, string> check)
    {
        for (int slot = 0; slot <= 1; slot++)
        {
            // The first accuracy roll on this seed is between the ordinary and full-set thresholds.
            var ordinary = BowShot(102, slot, 3);
            var steady = BowShot(102, slot, 8);
            check(ordinary.Misses == 1 && steady.Hits == 1 && steady == BowShot(102, slot, 8),
                "steady-hand-changes-hit-and-replays-" + slot);
        }
        var profile = Dressed(RotfaehrteSet.Pieces);
        var gear = profile.Equipment;
        gear.SetRegionalRelicEquipped(RegionalRelic.F_Langbogen, true);
        check(profile.WeaponAccuracyBp == 6125, "legacy-hunter-accuracy");
        profile.UseCombatV2();
        for (int worn = 8; worn >= 0; worn--)
        {
            int expected = worn == 8 ? 6525 : worn >= 4 ? 6325 : 6125;
            check(profile.WeaponAccuracyBp == expected, "steady-hand-accuracy-" + worn);
            if (worn > 0) gear.SetRegionalRelicEquipped(RotfaehrteSet.Pieces[worn - 1], false);
        }

        foreach (var id in RotfaehrteSet.Pieces.Take(8)) gear.SetRegionalRelicEquipped(id, true);
        var world = new ArenaSimulation(171, profile, BigInteger.One, LootCatalog.Version1);
        world.Monsters.Clear();
        check(world.StartDodge(0, 0) && world.Stamina == 77, "steady-hand-dodge-debit");
        while (world.Stamina < 100) world.Step();
        world.SetGuard(true);
        world.Step();
        world.Step();
        world.ReceiveBlowFrom(1, world.PlayerXmm + world.FacingXbp, world.PlayerZmm + world.FacingZbp);
        for (int i = 0; i < 40 && world.Stamina > 23; i++) world.Step();
        world.SetGuard(false);
        world.Step();
        check(world.Stamina == 23, "steady-hand-exact-stamina-fixture");
        world.TrySetRegionalRelicEquipped(RegionalRelic.F_Stiefel, false);
        check(!world.StartDodge(0, 0) && world.Stamina == 23, "steady-hand-seven-cannot-dodge-at-23");
        world.TrySetRegionalRelicEquipped(RegionalRelic.F_Stiefel, true);
        check(world.StartDodge(0, 0) && world.Stamina == 0, "steady-hand-eight-can-dodge-at-23");
        check(CharacterProgression.TryLoad(profile.SaveString(), out var loaded) && loaded.WeaponAccuracyBp == 6525,
            "steady-hand-restored-from-saved-equipment");
        gear.SetRegionalRelicEquipped(RegionalRelic.F_Langbogen, false);
        check(profile.WeaponAccuracyBp == 10000, "steady-hand-unarmed");
        gear.SetRegionalRelicEquipped(RegionalRelic.F_Langbogen, true);
        profile.WeaponMastery.Record(WeaponFamily.Bow, WeaponMasteryRules.ExperienceForLevel(32));
        check(profile.WeaponAccuracyBp == 10000, "steady-hand-accuracy-cap");
    }

    private static void Saves(Action<bool, string> check)
    {
        var p = Dressed(RotfaehrteSet.Pieces.Concat(RusskantorSet.Pieces).ToArray());
        p.Equipment.SetRegionalRelicEquipped(RegionalRelic.K_Langstab, true);
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) &&
            RotfaehrteSet.Pieces.Concat(RusskantorSet.Pieces).All(loaded.Equipment.OwnsRegionalRelic) &&
            loaded.Equipment.IsRegionalRelicEquipped(RegionalRelic.K_Langstab) && loaded.Equipment.IsRegionalRelicEquipped(RegionalRelic.K_Stiefel),
            "hunter-cantor-kept-in-the-save");
        check(RegionalRelics.All.Count <= 63 && RegionalRelics.Bit(RegionalRelic.K_Langstab) == 1L << 58, "hunter-cantor-mask-fits-63-bits");
    }

    private static bool Pick(ArenaSimulation w, LootItem item)
    {
        w.GroundLoot.Add(new LootState { Id = 970 + w.GroundLoot.Count, Xmm = w.PlayerXmm, Zmm = w.PlayerZmm, Item = item });
        bool picked = w.TryCollectLoot();
        return picked && w.LastGearFind != RegionalRelic.None;
    }

    private static ArenaSimulation Crypt(ulong seed)
    {
        var profile = CharacterProgression.CreateEntryProfile();
        profile.Equipment.Enable(profile);
        var crypt = new ArenaSimulation(seed, profile, BigInteger.One, LootCatalog.Version1);
        foreach (var enemy in crypt.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
        crypt.ActivateSkill(3);
        while (crypt.LootOnGround) if (!crypt.TryCollectLoot()) throw new Exception("crypt loot out of reach");
        return crypt;
    }
}
