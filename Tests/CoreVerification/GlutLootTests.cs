using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;

internal static class GlutLootTests
{
    public static void Run(Action<bool, string> check)
    {
        var ids = new HashSet<string>();
        var qualities = new HashSet<ItemRarity>();
        var affixes = new HashSet<WeaponAffix>();
        var values = new HashSet<int>();
        for (int n = 1; n <= 3000; n++)
        {
            int kind = n % 4;
            var receipt = LootCatalog.Version3.Generate(71, 4, (ulong)n, 5, kind, n);
            if (receipt == null) continue;
            var weapon = new WeaponItem(receipt);
            if (!weapon.Definition.IsGlutArmory) continue;
            ids.Add(weapon.Definition.Id);
            qualities.Add(weapon.Rarity);
            affixes.Add(weapon.Affix);
            values.Add(weapon.Value);
            if (weapon.Value <= 0 || (weapon.SecondValue > 0) != (weapon.Rarity >= ItemRarity.Rare) ||
                (weapon.SecondValue > 0 && weapon.Affix == weapon.SecondAffix) ||
                !LootItem.TryRead(receipt.Token(), out var saved) || new WeaponItem(saved).AffixDescription != weapon.AffixDescription ||
                new WeaponItem(saved).Definition.Id != weapon.Definition.Id)
                throw new Exception("Inconsistent Glut receipt: " + receipt.InstanceId);
        }
        check(ids.Count == 21 && qualities.Count == 4 && affixes.Count == 4 && values.Count > 100, "glut-sample-covers-models-rarities-and-affix-values");
        bool equipped = false;
        for (ulong seed = 1; seed <= 100 && !equipped; seed++)
        {
            var p = CharacterProgression.CreateEntryProfile();
            p.Equipment.Enable(p);
            var w = new ArenaSimulation(seed, p, 1, LootCatalog.Version3);
            var knight = w.Monsters.Single(m => m.Id == 2);
            knight.Xmm = w.PlayerXmm + 750; knight.Zmm = w.PlayerZmm; knight.Health = 1;
            if (w.ActivateSkill(0).Kills != 1) throw new Exception("Expected canonical kill");
            var drop = w.GroundLoot.Single(l => l.HasFieldPauldron);
            var gear = p.Equipment.ForItem(drop.Item);
            if (!gear.Definition.IsGlutArmory) continue;
            if (!w.TryCollectLoot() || !w.TryEquipWeapon(gear.Ordinal)) throw new Exception("Glut pickup/equip");
            string save = p.SaveString();
            if (!CharacterProgression.TryLoad(save, out var restored) || restored.SaveString() != save) throw new Exception("Glut save");
            var copy = restored.Equipment.Weapon ?? restored.Equipment.Shield;
            equipped = copy.Definition.Id == gear.Definition.Id && copy.AffixDescription == gear.AffixDescription && copy.Rarity == gear.Rarity;
            Console.WriteLine("GLUT_PICKUP seed=" + seed + " item=" + copy.Definition.Name + " rarity=" + copy.Rarity + " affix=" + copy.AffixDescription);
        }
        check(equipped, "glut-real-kill-pickup-equip-load");
        foreach (var catalog in new[] { LootCatalog.Version1, LootCatalog.Version2 })
        {
            var old = catalog.Generate(79, 1, 3, 7, 3, 1);
            check(LootItem.TryRead(old.Token(), out var restored) && !new WeaponItem(restored).Definition.IsGlutArmory, "glut-preserves-legacy-" + catalog.Revision);
        }
    }
}
