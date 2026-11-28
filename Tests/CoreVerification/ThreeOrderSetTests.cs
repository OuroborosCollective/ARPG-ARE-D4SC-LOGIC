using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

internal static class ThreeOrderSetTests
{
    internal static void Run(Action<bool, string> check)
    {
        var crypt = CryptDungeonTests.Enter();
        var forest = WoodlandTests.Enter();
        var outside = new ArenaSimulation(19, CharacterProgression.CreateEntryProfile(), 1, LootCatalog.Version1);
        check(outside.LocalOrderSet == null, "orders-ordinary-entry-does-not-roll");
        check(crypt.LocalOrderSet == ThreeOrderSet.Rostkeil && forest.LocalOrderSet == ThreeOrderSet.Moorvogt, "orders-floor-one-and-forest-route");
        crypt.Progression.Dungeon.Depth = 2;
        check(crypt.LocalOrderSet == null, "orders-no-unassigned-crypt-floor");
        crypt.Progression.Dungeon.Depth = 3;
        check(crypt.LocalOrderSet == ThreeOrderSet.Konvent, "orders-floor-three-route");

        foreach (var set in ThreeOrderSet.All)
        {
            var world = set == ThreeOrderSet.Moorvogt ? forest : crypt;
            if (world.InDungeon) world.Progression.Dungeon.Depth = set == ThreeOrderSet.Rostkeil ? 1 : 3;
            CheckPickups(world, set, check);
            CheckWearing(set, check);
        }
        CheckTokens(check);
    }

    private static void CheckPickups(ArenaSimulation world, ThreeOrderSet set, Action<bool, string> check)
    {
        var p = world.Progression;
        var gear = p.Equipment;
        gear.Enable(p);
        int hits = 0;
        var empty = new WeaponEquipment();
        for (ulong seed = 1; seed <= 1500; seed++)
        {
            var probe = LootCatalog.Version1.GenerateFlagged(seed, 1, seed, 1, 0, seed, 0);
            if (probe == null) throw new Exception("Expected revision-one receipt");
            var id = set.Roll(probe, empty);
            if (id != set.Roll(probe, empty)) throw new Exception("Non-reproducible set roll");
            if (id != RegionalRelic.None) hits++;
        }
        check(Math.Abs(hits / 1500.0 - set.ChanceBp / 10000.0) < .04, set.Name + "-conditional-rate");
        for (ulong seed = 1; seed <= 600 && !set.Pieces.All(gear.OwnsRegionalRelic); seed++)
        {
            p.RecordKill();
            var item = LootCatalog.Version1.GenerateFlagged(seed, 1, seed, 1, 1, p.LootOrdinal, 0);
            var expected = set.Roll(item, gear);
            bool oldFind = (AschenwachtGear.Open(p) && AschenwachtGear.Roll(item, gear) != RegionalRelic.None)
                || NachtvesperSet.Roll(item, gear) != RegionalRelic.None || RotfaehrteSet.Roll(item, gear, world.InHuntingGrounds) != RegionalRelic.None
                || RusskantorSet.Roll(item, gear, false) != RegionalRelic.None;
            world.GroundLoot.Add(new LootState { Id = 9900, Xmm = world.PlayerXmm, Zmm = world.PlayerZmm, Item = item });
            if (!world.TryCollectLoot()) throw new Exception("Receipt not collected");
            if (!oldFind) check(world.LastGearFind == expected, set.Name + "-pickup-matches-receipt-" + seed);
            else check(ThreeOrderSet.For(world.LastGearFind) == null, set.Name + "-older-drop-keeps-priority-" + seed);
        }
        check(set.Pieces.All(gear.OwnsRegionalRelic), set.Name + "-all-five-found-by-pickup");
        var last = p.Items.Last();
        check(set.Roll(last, gear) == RegionalRelic.None && !world.TryCollectLoot(), set.Name + "-depleted-set-and-idempotent-pickup");
    }

    private static void CheckWearing(ThreeOrderSet set, Action<bool, string> check)
    {
        var p = CharacterProgression.CreateEntryProfile();
        p.UseCombatV2();
        var world = new ArenaSimulation(42, p, 1, LootCatalog.Version1);
        int baseCost = world.DodgeStaminaCost, baseHealing = world.WundschlussBaseHealing;
        var slots = new ArmorPiece[5];
        for (int i = 0; i < 5; i++)
        {
            var id = set.Pieces[i];
            check(p.Equipment.ClaimRegionalRelic(p, id) && world.TrySetRegionalRelicEquipped(id, true), set.Name + "-equip-" + i);
            slots[i] = set.Armor(id);
            int tier = i == 4 ? 2 : i >= 2 ? 1 : 0;
            check(p.WornProtectionBp == ArmorRules.Protection(slots) + (set == ThreeOrderSet.Rostkeil ? tier * 200 : 0) &&
                p.WornHealingEfficiencyBp == ArmorRules.HealingEfficiency(slots) &&
                world.DodgeStaminaCost == baseCost - (set == ThreeOrderSet.Moorvogt ? tier : 0) &&
                world.WundschlussBaseHealing == baseHealing + (set == ThreeOrderSet.Konvent ? tier * 5 : 0), set.Name + "-balance-" + i);
        }
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && loaded.SaveString() == p.SaveString() &&
            set.Pieces.All(loaded.Equipment.IsRegionalRelicEquipped), set.Name + "-all-worn-roundtrip");
        check(world.TrySetRegionalRelicEquipped(set.Pieces[1], false) && set.Tier(p.Equipment) == 1, set.Name + "-removing-breaks-five");
    }

    private static void CheckTokens(Action<bool, string> check)
    {
        var p = CharacterProgression.CreateEntryProfile();
        p.Equipment.ClaimRegionalRelic(p, RegionalRelic.K_Langstab);
        string old = p.Equipment.Token();
        check(old.StartsWith("3,") && WeaponEquipment.TryRead(old, p, out var legacy) && legacy.Token() == old, "orders-preserve-v3-exactly");
        p.Equipment.ClaimRegionalRelic(p, RegionalRelic.Konvent_Fuesse);
        p.Equipment.SetRegionalRelicEquipped(RegionalRelic.Konvent_Fuesse, true);
        string token = p.Equipment.Token();
        check(token.StartsWith("4,") && WeaponEquipment.TryRead(token, p, out var gear) &&
            gear.IsRegionalRelicEquipped(RegionalRelic.Konvent_Fuesse) && gear.OwnsRegionalRelic(RegionalRelic.K_Langstab), "orders-bit-beyond-64");
        check(!WeaponEquipment.TryRead("3" + token.Substring(1), p, out _) && !WeaponEquipment.TryRead("4" + old.Substring(1), p, out _), "orders-version-cannot-alias");
        var f = token.Split(',');
        f[6] = (BigInteger.One << 74).ToString();
        check(!WeaponEquipment.TryRead(string.Join(",", f), p, out _), "orders-reject-unknown-bit");
    }
}
