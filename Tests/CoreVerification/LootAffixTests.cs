using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using AreArpg.Core;

internal static class LootAffixTests
{
    private static ArenaSimulation World(ulong seed = 123)
    {
        return new ArenaSimulation(seed, null, BigInteger.One, LootCatalog.Version1);
    }

    private static void KillFirst(ArenaSimulation world)
    {
        world.Monsters[0].Xmm = 1000;
        world.Monsters[0].Zmm = 0;
        world.Monsters[0].Health = 1;
        if (world.ActivateSkill(0).Kills != 1) throw new Exception("fixture must use canonical kill");
    }

    private static string Rechecksum(string payload)
    {
        ulong h = 14695981039346656037UL;
        unchecked { foreach (byte b in Encoding.UTF8.GetBytes(payload)) { h ^= b; h *= 1099511628211UL; } }
        return payload + "|" + h.ToString("X16", CultureInfo.InvariantCulture);
    }

    public static void Run(Action<bool, string> require)
    {
        var a = World(); var b = World();
        require(a.GroundLoot.Count == 0 && a.Progression.Items.Count == 0, "affix-no-item-before-kill");
        KillFirst(a); KillFirst(b);
        LootItem item = a.GroundLoot[0].Item;
        require(item != null && item.InstanceId == b.GroundLoot[0].Item.InstanceId, "affix-identical-kill-identical-id");
        require(item.Rarity == b.GroundLoot[0].Item.Rarity && item.AffixId == "weapon.upgrade" && item.AffixValue == 8,
            "affix-valid-rarity-and-legacy-eight-point-affix");
        require(item.CatalogRevision == 1 && item.DefinitionId == "ash.shard", "affix-versioned-definition");
        require(a.ComputeWorldHash() == b.ComputeWorldHash(), "affix-replay-kill-hash");
        a.ActivateSkill(0);
        require(a.GroundLoot.Count == 1 && a.Progression.LifetimeKills == 1, "affix-kill-idempotent");
        require(a.TryCollectLoot() && b.TryCollectLoot(), "affix-pickup-real-drop");
        require(a.Progression.Items.Count == 1 && a.Progression.Items[0].InstanceId == item.InstanceId,
            "affix-persist-owned-instance");
        require(a.WeaponBonus == 8 && a.LootCollected == 1 && !a.TryCollectLoot(), "affix-preserve-legacy-reward-once");
        string save = a.Progression.SaveString();
        require(save.StartsWith("AC7|"), "affix-save-version-does-not-claim-reserved-ac6");
        require(CharacterProgression.TryLoad(save, out var loaded) && loaded.SaveString() == save, "affix-save-exact-roundtrip");
        require(loaded.Items[0].InstanceId == item.InstanceId && loaded.StableHash == a.Progression.StableHash,
            "affix-save-preserves-id-and-hash");
        var resumed = new ArenaSimulation(123, loaded, BigInteger.One, LootCatalog.Version1);
        var continued = new ArenaSimulation(123, a.Progression, BigInteger.One, LootCatalog.Version1);
        KillFirst(resumed); KillFirst(continued);
        require(resumed.GroundLoot[0].Item.InstanceId != item.InstanceId, "affix-new-kill-after-restart-unique");
        require(resumed.ComputeWorldHash() == continued.ComputeWorldHash(), "affix-checkpoint-resume-matches-unserialized-profile");
        require(resumed.TryCollectLoot() && resumed.Progression.Items.Count == 2, "affix-two-items-after-restart");
        require(CharacterProgression.TryLoad(resumed.Progression.SaveString(), out var twice) && twice.Items.Count == 2,
            "affix-two-items-persist");
        var c = World(124); KillFirst(c);
        require(item.InstanceId != c.GroundLoot[0].Item.InstanceId, "affix-seed-in-instance-identity");
        var legacy = new ArenaSimulation(123); KillFirst(legacy);
        require(legacy.GroundLoot[0].Item == null && legacy.TryCollectLoot() && legacy.WeaponBonus == 8,
            "affix-legacy-loot-mode-remains-supported");
        require(legacy.Progression.SaveString().StartsWith("AC5|"), "affix-legacy-save-format-unchanged");
        require(CharacterProgression.TryLoad(legacy.Progression.SaveString(), out var migrated) && migrated.Items.Count == 0,
            "affix-ac5-load-starts-without-fabricated-items");
        var imported = new ArenaSimulation(123, migrated, BigInteger.One, LootCatalog.Version1); KillFirst(imported);
        require(imported.TryCollectLoot() && imported.WeaponBonus == 16 && imported.LootCollected == 2,
            "affix-old-progression-preserved-on-first-new-pickup");
        string[] fields = save.Split('|');
        fields[14] = fields[14] + ";" + fields[14];
        require(!CharacterProgression.TryLoad(Rechecksum(string.Join("|", fields, 0, 15)), out _),
            "affix-valid-checksum-does-not-allow-duplicate-instance");
        fields = save.Split('|'); fields[14] = fields[14].Replace("weapon.upgrade", "unknown.affix");
        require(!CharacterProgression.TryLoad(Rechecksum(string.Join("|", fields, 0, 15)), out _),
            "affix-valid-checksum-does-not-allow-unknown-affix");
        fields = save.Split('|'); fields[14] = fields[14].Replace("ash.shard", "unknown.item");
        require(!CharacterProgression.TryLoad(Rechecksum(string.Join("|", fields, 0, 15)), out _),
            "affix-valid-checksum-does-not-allow-unknown-item");
        var replayA = World(); var replayB = World();
        KillFirst(replayA); KillFirst(replayB); replayA.TryCollectLoot(); replayB.TryCollectLoot();
        for (int tick = 0; tick < 100; tick++)
        {
            replayA.Step(); replayB.Step();
            require(replayA.ComputeWorldHash() == replayB.ComputeWorldHash(), "affix-replay-tick-" + tick);
        }
        var baseline = new ArenaSimulation(123);
        require(baseline.ComputeWorldHash() == 778990259245021324UL, "affix-legacy-spawn-golden-hash");
        KillFirst(baseline);
        require(baseline.ComputeWorldHash() == 12708209387939169364UL, "affix-legacy-kill-golden-hash");
        baseline.TryCollectLoot();
        require(baseline.ComputeWorldHash() == 4613376337469592607UL, "affix-legacy-pickup-golden-hash");
        var beforeCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            require(CharacterProgression.TryLoad(save, out var cultureLoad) && cultureLoad.SaveString() == save,
                "affix-save-culture-independent");
        }
        finally { CultureInfo.CurrentCulture = beforeCulture; }
        var storm = World();
        foreach (var enemy in storm.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
        require(storm.ActivateSkill(3).Kills == 3, "affix-multiple-kills-same-tick");
        require(storm.GroundLoot[0].Item.InstanceId != storm.GroundLoot[1].Item.InstanceId &&
            storm.GroundLoot[1].Item.InstanceId != storm.GroundLoot[2].Item.InstanceId, "affix-same-tick-unique-items");
        storm.GroundLoot.Reverse();
        require(storm.TryCollectLoot() && storm.TryCollectLoot() && storm.TryCollectLoot(), "affix-reverse-pickup-order");
        require(storm.WeaponBonus == 59 && storm.Progression.Items[0].KillOrdinal == 1 &&
            CharacterProgression.TryLoad(storm.Progression.SaveString(), out _), "affix-canonical-receipt-order-and-legacy-bonuses");
        var rows = new[] { new LootDropDefinition("b", 0, "ash.shard", ItemRarity.Rare, 1, 8),
            new LootDropDefinition("a", 0, "ash.shard", ItemRarity.Magic, 3, 8),
            new LootDropDefinition("excluded", 1, "iron.shard", ItemRarity.Magic, 100, 17) };
        var table = new LootDropTable(rows);
        Array.Reverse(rows);
        var reverse = new LootDropTable(rows);
        for (uint roll = 0; roll < 4; roll++)
        {
            require(table.Select(0, roll).Id == reverse.Select(0, roll).Id, "affix-table-order-independent-" + roll);
            require(table.Select(0, roll).Id == (roll < 3 ? "a" : "b"), "affix-eligible-weight-boundary-" + roll);
        }
        bool rejected = false;
        try { new LootDropTable(new LootDropDefinition("bad", 0, "ash.shard", ItemRarity.Magic, 0, 8)); }
        catch (ArgumentException) { rejected = true; }
        require(rejected, "affix-zero-weight-rejected");
        require(ArenaSimulation.TickMs == 100, "affix-keeps-ten-hz");
    }
}
