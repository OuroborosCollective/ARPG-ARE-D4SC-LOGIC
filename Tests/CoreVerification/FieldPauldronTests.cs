using System;
using System.Globalization;
using System.Linq;
using AreArpg.Core;

internal static class FieldPauldronTests
{
    // Captured on c600181 from the same six-tick fight at seed 77 with equipment already active.
    private const string PrePauldronEntrySave = "AC16|QUM4fDQxMHwxN3wxfDF8MHwwfDB8MXwzMnwzMXwwfDB8NCwwLDg7MCwwLDAsMCwwLDYwMCw1MDAsNDUwLDYsMCx4OzAsMCwwLDAsMCw4MDAsMTUwLDEwMCwxNSwwLHg7MCwwLDAsMCwwLDgwMCwxMDAsMTAwLDEwLDAseHwxLDAwMDAwMDAwMDAwMDAwNEQsMSw2LDIsMSwxLGlyb24uc2hhcmQsMix3ZWFwb24udXBncmFkZSwxN3wxLDF8MTk4QTQ0ODgyREI1MjdDRA==|1,1,0,0|FA36D62A8F127245";

    private static ArenaSimulation Entry(ulong seed, CharacterProgression hero)
    {
        hero.Equipment.Enable(hero);
        return new ArenaSimulation(seed, hero, 1, LootCatalog.Version1);
    }

    private static MonsterState FightEntryKnight(ArenaSimulation world)
    {
        var knight = world.Monsters.Single(m => m.Id == 2 && m.Kind == 1);
        for (int t = 0; t < 160 && !knight.IsDead && !world.GameOver; t++)
        {
            world.SetMoveInput(knight.Xmm - world.PlayerXmm, knight.Zmm - world.PlayerZmm);
            world.Step();
            world.ActivateSkill(0);
            world.ActivateSkill(1);
            if (world.PlayerHealth < world.MaxPlayerHealth / 2) world.ActivateSkill(2);
        }
        return knight;
    }

    private static bool LoadWithEquipmentToken(string saved, string token)
    {
        string original = WeaponMasteryTests.Unwrapped(saved);
        var f = original.Split('|');
        if (f.Length != 4 || f[0] != "AC16") throw new Exception("Expected AC16 equipment envelope");
        string body = f[0] + "|" + f[1] + "|" + token;
        string modified = body + "|" + LootCatalog.HashText(body).ToString("X16", CultureInfo.InvariantCulture);
        return CharacterProgression.TryLoad(WeaponMasteryTests.Rewrap(saved, modified), out _);
    }

    public static void Run(Action<bool,string> require)
    {
        require(CharacterProgression.TryLoad(PrePauldronEntrySave, out var legacy) &&
            legacy.SaveString() == PrePauldronEntrySave && legacy.Equipment.Active &&
            !legacy.Equipment.FieldPauldronOwned && legacy.Items.Single().DefinitionId == "iron.shard",
            "feldschulter-real-pre-upgrade-equipped-profile-keeps-exact-bytes-and-iron-shard");

        var hero = CharacterProgression.CreateEntryProfile();
        var world = Entry(77, hero);
        var replay = Entry(77, CharacterProgression.CreateEntryProfile());
        require(!world.TrySetFieldPauldronEquipped(true) && !world.TryCollectLoot(),
            "feldschulter-empty-inventory-cannot-equip-or-pick-up");
        var knight = FightEntryKnight(world);
        FightEntryKnight(replay);
        var drop = world.GroundLoot.Single();
        require(knight.IsDead && knight.MaxHealth == 650 && world.CurrentTick == 6 && drop.HasFieldPauldron &&
            drop.Item.InstanceId == "loot.1,000000000000004D,1,6,2,1,1" && !hero.Equipment.FieldPauldronOwned,
            "feldschulter-six-normal-combat-ticks-spawn-authored-knight-drop-with-unchanged-receipt");
        require(world.ComputeWorldHash() == replay.ComputeWorldHash() && !world.TrySetFieldPauldronEquipped(true),
            "feldschulter-pending-drop-is-deterministic-and-cannot-be-worn");

        require(world.TryCollectLoot() && replay.TryCollectLoot() && !world.TryCollectLoot(),
            "feldschulter-entry-knight-kill-and-ground-pickup");
        require(hero.Equipment.Token().StartsWith("2,", StringComparison.Ordinal),
            "feldschulter-collected-armour-receipt-must-survive-the-equipment-save");
        require(hero.Equipment.FieldPauldronOrdinal == drop.Item.KillOrdinal && hero.Equipment.FieldPauldronOwned &&
            !hero.Equipment.FieldPauldronEquipped && hero.Items.Count == 1 && hero.LootCollected == 1 &&
            hero.WeaponBonus == 17 && hero.Items[0].Token() == legacy.Items[0].Token() &&
            hero.Equipment.ForItem(hero.Items[0]).Definition.Id == legacy.Equipment.ForItem(legacy.Items[0]).Definition.Id,
            "feldschulter-single-provenance-no-auto-equip-or-second-shard-weapon-roll");
        string unworn = hero.SaveString();
        require(CharacterProgression.TryLoad(unworn, out var bag) && unworn == replay.Progression.SaveString() &&
            bag.SaveString() == unworn && bag.Equipment.FieldPauldronOwned && !bag.Equipment.FieldPauldronEquipped,
            "feldschulter-unequipped-pickup-survives-reload-before-first-equip");
        int cooldown = world.AttackCooldown(0);
        long range = world.AttackRange(0);
        int maxHp = world.MaxPlayerHealth;
        require(world.TrySetFieldPauldronEquipped(true) && replay.TrySetFieldPauldronEquipped(true) &&
            !world.TrySetFieldPauldronEquipped(true) && world.AttackCooldown(0) == cooldown &&
            world.AttackRange(0) == range && world.MaxPlayerHealth == maxHp && world.WeaponBonus == 17,
            "feldschulter-equip-is-idempotent-and-leaves-combat-values-intact");
        string worn = hero.SaveString();
        require(CharacterProgression.TryLoad(worn, out var loaded) && worn != unworn && worn == replay.Progression.SaveString() &&
            world.ComputeWorldHash() == replay.ComputeWorldHash() &&
            loaded.SaveString() == worn && loaded.Equipment.FieldPauldronEquipped && loaded.Equipment.FieldPauldronOrdinal == 1,
            "feldschulter-replay-and-loaded-equipped-ordinal-are-byte-identical");
        // Drive the same environmental damage path through two independently deserialized characters.
        var wearing = Entry(77, loaded);
        var carrying = Entry(77, bag);
        wearing.ApplyEnvironmentalDamage(100);
        carrying.ApplyEnvironmentalDamage(100);
        require(wearing.PlayerHealth == carrying.PlayerHealth && wearing.PlayerHealth == maxHp - 100,
            "feldschulter-actual-damage-is-identical-while-worn-or-carried");
        wearing.ApplyEnvironmentalDamage(wearing.PlayerHealth);
        require(wearing.GameOver && !wearing.TrySetFieldPauldronEquipped(false) &&
            loaded.Equipment.FieldPauldronEquipped, "feldschulter-given-defeat-no-equipment-mutation");
        require(!LoadWithEquipmentToken(worn, "2,1,0,0,2,1") && !LoadWithEquipmentToken(worn, "2,1,0,0,01,1") &&
            !LoadWithEquipmentToken(worn, "2,1,0,0,1,2") && !LoadWithEquipmentToken(worn, "2,2,0,0,1,1"),
            "feldschulter-rehashed-unowned-noncanonical-slot-and-pre-activation-receipts-rejected");
        require(world.TrySetFieldPauldronEquipped(false) && !world.TrySetFieldPauldronEquipped(false) &&
            hero.SaveString() == unworn, "feldschulter-unequip-restores-previous-save-exactly");

        var again = Entry(77, hero);
        FightEntryKnight(again);
        require(again.GroundLoot.All(l => !l.HasFieldPauldron) && again.TryCollectLoot() &&
            hero.Equipment.FieldPauldronOrdinal == 1 && hero.Items.Count == 2,
            "feldschulter-repeat-eligible-kill-only-grants-original-shard");
        var arenaOnly = Entry(77, new CharacterProgression());
        FightEntryKnight(arenaOnly);
        require(arenaOnly.GroundLoot.All(l => !l.HasFieldPauldron),
            "feldschulter-same-enemy-id-outside-story-gate-is-ineligible");

        CheckWoodland(require);
    }

    private static void CheckWoodland(Action<bool,string> require)
    {
        // A placement/low-HP fixture isolating two concurrent candidates on a real seeded forest floor.
        var forest = WoodlandTests.Enter();
        forest.Progression.Equipment.Enable(forest.Progression);
        var knights = forest.Monsters.Where(m => m.Kind == 1).Take(2).ToArray();
        var ghoul = forest.Monsters.First(m => m.Kind == 0);
        var enemies = new[] {ghoul, knights[0], knights[1]};
        for (int index = 0; index < enemies.Length; index++)
        {
            var target = enemies[index];
            target.Xmm = forest.PlayerXmm + 700;
            target.Zmm = forest.PlayerZmm;
            target.Health = 1;
            for (int t = 0; t < 20 && !target.IsDead; t++) { forest.ActivateSkill(0); forest.Step(); }
        }
        var drops = forest.GroundLoot.ToArray();
        require(drops.Length == 3 && !drops[0].HasFieldPauldron && drops[1].HasFieldPauldron &&
            !drops[2].HasFieldPauldron, "feldschulter-first-woodland-knight-only-in-a-pile-with-ghoul-and-two-knights");
        string pending = forest.Progression.SaveString();
        var equipmentEnvelope = WeaponMasteryTests.Unwrapped(pending);
        if (!equipmentEnvelope.StartsWith("AC16|", StringComparison.Ordinal))
            throw new Exception("Expected outer equipment checkpoint for pending woodland armour");
        var bareWoodland = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(equipmentEnvelope.Split('|')[1]));
        require(bareWoodland.StartsWith("AC15|", StringComparison.Ordinal) &&
            !CharacterProgression.TryLoad(bareWoodland, out var rejectedBase) && rejectedBase == null,
            "feldschulter-checksummed-pending-woodland-without-equipment-authority-rejected");
        require(!CharacterProgression.TryLoad(bareWoodland, JourneyPilotTests.Maps(), out var rejectedWithMaps) &&
            rejectedWithMaps == null, "feldschulter-map-aware-loader-also-rejects-pending-without-equipment");
        require(!LoadWithEquipmentToken(pending, "1," + (drops[1].Item.KillOrdinal + 1) + ",0,0"),
            "feldschulter-pending-receipt-cannot-predate-equipment-activation");
        var restored = WoodlandTests.Reload(forest);
        require(restored.Progression.SaveString() == pending && restored.GroundLoot.Single(l => l.HasFieldPauldron).Id == knights[0].Id &&
            !restored.Progression.Equipment.FieldPauldronOwned, "feldschulter-pending-woodland-drop-reloads-on-same-corpse");
        require(restored.TryCollectLoot() && !restored.Progression.Equipment.FieldPauldronOwned &&
            restored.TryCollectLoot() && restored.Progression.Equipment.FieldPauldronOrdinal == drops[1].Item.KillOrdinal &&
            restored.TryCollectLoot() && restored.TrySetFieldPauldronEquipped(true),
            "feldschulter-wrong-ground-shard-does-not-grant-armour-and-next-knight-awards-once");
        var equipment = restored.Progression.Equipment;
        string worn = restored.Progression.SaveString();
        var loaded = WoodlandTests.Reload(restored);
        require(loaded.Progression.SaveString() == worn && loaded.Progression.Equipment.FieldPauldronEquipped &&
            loaded.Progression.Equipment.FieldPauldronOrdinal == equipment.FieldPauldronOrdinal,
            "feldschulter-woodland-gear-and-origin-persist-alongside-AC15");
        require(!WeaponEquipment.TryRead("2," + equipment.FirstOrdinal + ",0,0," + drops[0].Item.KillOrdinal + ",1",
            restored.Progression, out _), "feldschulter-existing-but-wrong-forest-source-rejected");
        string field = forest.Progression.Woodland.Token();
        string withoutId = field.Substring(0, field.LastIndexOf(';') + 1);
        require(!WoodlandState.TryRead(withoutId + ghoul.Id, 8, forest.Progression.LootOrdinal, forest.Progression.Items,
            Array.Empty<LootState>(), out _), "feldschulter-forged-pending-field-marker-must-point-to-a-fallen-knight");
    }
}
