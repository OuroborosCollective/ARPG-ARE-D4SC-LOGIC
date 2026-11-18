using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;

/// <summary>Fadenwerk content table and validator on the Core side: version, checksum, reachability,
/// completability, reward bounds and a pilot quest that runs like the code path.</summary>
internal static class QuestContentTests
{
    private static QuestContent Quest(string id, params string[] prerequisites) =>
        new QuestContent { Id = id, Title = id, GiverNpcId = "npc", Prerequisites = prerequisites.ToList(), ObjectiveCount = 2, RewardMin = 10, RewardMax = 50 };

    public static void Run(Action<bool, string> require)
    {
        // The validator accepts a well-formed table.
        var good = new QuestContentTable();
        good.Add(Quest("a"));
        good.Add(Quest("b", "a"));
        good.Add(Quest("c", "b"));
        require(QuestContentValidator.Validate(good).Count == 0, "quest-table-valid");

        // Missing prerequisite is unreachable.
        var missing = new QuestContentTable(); missing.Add(Quest("a", "ghost"));
        require(QuestContentValidator.Validate(missing).Any(p => p.Contains("unreachable")), "quest-validator-unreachable-prerequisite");

        // A prerequisite cycle is unreachable for every member.
        var cycle = new QuestContentTable(); cycle.Add(Quest("a", "b")); cycle.Add(Quest("b", "a"));
        require(QuestContentValidator.Validate(cycle).Any(p => p.Contains("cycle")), "quest-validator-cycle");

        // No objectives -> cannot be completed.
        var empty = new QuestContentTable();
        empty.Add(new QuestContent { Id = "a", Title = "a", GiverNpcId = "npc", ObjectiveCount = 0, RewardMin = 0, RewardMax = 0 });
        require(QuestContentValidator.Validate(empty).Any(p => p.Contains("objective")), "quest-validator-no-objectives");

        // Reward bounds: min > max and out-of-range totals are rejected.
        var badReward = new QuestContentTable();
        badReward.Add(new QuestContent { Id = "a", Title = "a", GiverNpcId = "npc", ObjectiveCount = 1, RewardMin = 100, RewardMax = 10 });
        require(QuestContentValidator.Validate(badReward).Any(p => p.Contains("reward")), "quest-validator-reward-min-max");
        var hugeReward = new QuestContentTable();
        hugeReward.Add(new QuestContent { Id = "a", Title = "a", GiverNpcId = "npc", ObjectiveCount = 1, RewardMin = 0, RewardMax = QuestContentValidator.MaxReward + 1 });
        require(QuestContentValidator.Validate(hugeReward).Any(p => p.Contains("reward")), "quest-validator-reward-bound");

        // Duplicate ids are rejected.
        var dup = new QuestContentTable(); dup.Add(Quest("a")); dup.Add(Quest("a"));
        require(QuestContentValidator.Validate(dup).Any(p => p.Contains("duplicate")), "quest-validator-duplicate-id");

        // Version and checksum: stable, and a tampered table changes the checksum.
        string sig = good.Signature();
        var good2 = new QuestContentTable();
        good2.Add(Quest("a")); good2.Add(Quest("b", "a")); good2.Add(Quest("c", "b"));
        require(good2.Signature() == sig, "quest-table-signature-stable");
        var tampered = new QuestContentTable();
        tampered.Add(Quest("a")); tampered.Add(Quest("b", "a")); tampered.Add(Quest("c", "b"));
        tampered.Quests[2].RewardMax = 999;
        require(tampered.Signature() != sig, "quest-table-signature-detects-change");
        require(good.Version == 1, "quest-table-version");

        // The pilot quest (ashen.hunger) is in the table and its state matches the code path.
        var pilot = QuestContentTable.Pilot();
        require(pilot.Find("ashen.hunger") != null, "quest-pilot-present");
        require(QuestContentValidator.Validate(pilot).Count == 0, "quest-pilot-valid");
        var fresh = new CharacterProgression();
        require(QuestContentTable.PilotStatus(fresh) == AuthoredHungerBinding.Status(fresh), "quest-pilot-status-matches-code-fresh");
        var active = CharacterProgression.CreateEntryProfile();
        active.DiscoverHungerClue(); active.TravelHunger(2, false);
        require(QuestContentTable.PilotStatus(active) == AuthoredHungerBinding.Status(active), "quest-pilot-status-matches-code-active");

        // The table version and checksum are stored as their own AC22 section; old saves stay readable.
        require(!new CharacterProgression().SaveString().StartsWith("AC22|"), "quest-fresh-save-stays-legacy");
        var carrier = new CharacterProgression();
        carrier.SetContentTable(pilot);
        string saved = carrier.SaveString();
        require(saved.StartsWith("AC22|"), "quest-table-writes-section");
        require(CharacterProgression.TryLoad(saved, out var loaded) && loaded.ContentTableVersion == pilot.Version &&
            loaded.ContentTableChecksum == pilot.Checksum, "quest-table-roundtrips");
        require(loaded.SaveString() == saved, "quest-table-canonical");
        // An old save without a content section still loads and reports no table.
        var legacy = new CharacterProgression();
        require(CharacterProgression.TryLoad(legacy.SaveString(), out var old) && old.ContentTableVersion == 0, "quest-old-save-readable");
    }
}
