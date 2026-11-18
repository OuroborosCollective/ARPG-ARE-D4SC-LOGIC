using System;
using System.Collections.Generic;
using System.Linq;
using Fadenwerk;
using Fadenwerk.EditorTools;

/// <summary>
/// Engine-free verification of the Fadenwerk authoring rules: the content validator, the deterministic reward
/// rolls, the dialogue runner's choice gate and the quest accept/report/complete cycle. Runs the real package
/// sources against a minimal UnityEngine shim (no Unity engine needed). This is not Unity Editor evidence.
/// </summary>
internal static class Program
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }

    static FadenwerkDatabase Db() { var db = new FadenwerkDatabase(); db.Sanitize(); return db; }

    static QuestDef Quest(string id, string giver, string turnIn = null, params string[] prereq)
    {
        var q = new QuestDef { id = id, title = id, giverNpcId = giver, turnInNpcId = turnIn ?? "" };
        q.prerequisiteQuestIds = new List<string>(prereq);
        q.objectives = new List<ObjectiveDef> { new ObjectiveDef { type = FwObjective.Kill, targetId = "wolf", count = 1 } };
        return q;
    }

    static ItemEntry Item(string id) { return new ItemEntry { id = id, displayName = id }; }

    static int Main()
    {
        try
        {
            ValidatorAcceptsAWellFormedDatabase();
            ValidatorRejectsMissingIdsAndDuplicates();
            ValidatorRejectsDanglingReferences();
            ValidatorRejectsUnreachableAndDeadEndQuests();
            ValidatorRejectsInvalidObjectivesAndRewards();
            ValidatorChecksKitAndItemRules();
            RewardsAreDeterministicPerSeed();
            RewardsHonourGuaranteedAndWeightedEntries();
            DialogueRunnerGatesForeignAndStaleChoices();
            ManagerRunsTheFullQuestCycle();
            Console.WriteLine("ALL PASS: " + checks + " real Fadenwerk assertions");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    static void ValidatorAcceptsAWellFormedDatabase()
    {
        var db = Db();
        db.items.Add(Item("bandage"));
        db.quests.Add(Quest("mill", "ilyan", "mara"));
        db.quests.Add(Quest("mill2", "ilyan", prereq: "mill"));
        db.dialogues.Add(new DialogueDef
        {
            id = "ilyan.chat", startNodeId = "a",
            nodes = new List<DialogueNode> { new DialogueNode { id = "a", choices = new List<DialogueChoice> { new DialogueChoice { text = "ok", action = FwAction.None } } } }
        });
        db.kits.Add(new RewardKitDef { id = "k", name = "k", goldMin = 1, goldMax = 2, xpMin = 0, xpMax = 1, rolls = 1,
            entries = new List<KitEntry> { new KitEntry { itemId = "bandage", minQuantity = 1, maxQuantity = 1, weight = 100 } } });
        var issues = FadenwerkValidator.Run(db, new List<FadenwerkNpc>(), new List<FadenwerkMarker>());
        Check(issues.All(i => i.level != "error"), "validator-accepts-well-formed-database");
    }

    static void ValidatorRejectsMissingIdsAndDuplicates()
    {
        var db = Db();
        db.quests.Add(Quest("", "ilyan"));
        db.quests.Add(Quest("dup", "ilyan"));
        db.quests.Add(Quest("dup", "ilyan"));
        db.quests.Add(new QuestDef { id = "bad id", title = "t", giverNpcId = "ilyan", objectives = new List<ObjectiveDef> { new ObjectiveDef { type = FwObjective.Kill, targetId = "x", count = 1 } } });
        var issues = FadenwerkValidator.Run(db, new List<FadenwerkNpc>(), new List<FadenwerkMarker>());
        Check(issues.Any(i => i.message.Contains("ohne ID")), "validator-rejects-missing-id");
        Check(issues.Any(i => i.message.Contains("doppelt")), "validator-rejects-duplicate-id");
        Check(issues.Any(i => i.message.Contains("nur Buchstaben")), "validator-rejects-invalid-id-characters");
    }

    static void ValidatorRejectsDanglingReferences()
    {
        var db = Db();
        db.quests.Add(Quest("mill", "ghost", prereq: "nowhere"));
        db.dialogues.Add(new DialogueDef { id = "d", startNodeId = "a",
            nodes = new List<DialogueNode> { new DialogueNode { id = "a", choices = new List<DialogueChoice> {
                new DialogueChoice { text = "x", action = FwAction.OfferQuest, actionParam = "missing" } } } } });
        var issues = FadenwerkValidator.Run(db, new List<FadenwerkNpc>(), new List<FadenwerkMarker>());
        Check(issues.Any(i => i.message.Contains("Voraussetzung") && i.message.Contains("existiert nicht")), "validator-rejects-missing-prerequisite");
        Check(issues.Any(i => i.message.Contains("Quest \"missing\" existiert nicht")), "validator-rejects-dangling-dialogue-quest");
    }

    static void ValidatorRejectsUnreachableAndDeadEndQuests()
    {
        var db = Db();
        db.quests.Add(Quest("a", "ilyan", prereq: "b"));
        db.quests.Add(Quest("b", "ilyan", prereq: "a"));
        db.quests.Add(new QuestDef { id = "empty", title = "e", giverNpcId = "ilyan" });
        var issues = FadenwerkValidator.Run(db, new List<FadenwerkNpc>(), new List<FadenwerkMarker>());
        Check(issues.Any(i => i.message.Contains("setzt sich selbst voraus")) == false, "validator-allows-two-cycle-not-self");
        Check(issues.Any(i => i.message.Contains("keine Ziele")), "validator-rejects-quest-without-objectives");
        db.quests.Clear();
        db.quests.Add(Quest("self", "ilyan", prereq: "self"));
        Check(FadenwerkValidator.Run(db, new List<FadenwerkNpc>(), new List<FadenwerkMarker>())
            .Any(i => i.message.Contains("setzt sich selbst voraus")), "validator-rejects-self-prerequisite");
    }

    static void ValidatorRejectsInvalidObjectivesAndRewards()
    {
        var db = Db();
        var q = Quest("q", "ilyan");
        q.objectives = new List<ObjectiveDef> {
            new ObjectiveDef { type = "dance", targetId = "x", count = 1 },
            new ObjectiveDef { type = FwObjective.Kill, targetId = "x", count = 0 },
            new ObjectiveDef { type = FwObjective.Collect, targetId = "ghostitem", count = 1 } };
        db.quests.Add(q);
        var issues = FadenwerkValidator.Run(db, new List<FadenwerkNpc>(), new List<FadenwerkMarker>());
        Check(issues.Any(i => i.message.Contains("unbekannte Art")), "validator-rejects-unknown-objective-type");
        Check(issues.Any(i => i.message.Contains("mindestens 1")), "validator-rejects-zero-count");
        Check(issues.Any(i => i.message.Contains("fehlt im Item-Katalog")), "validator-rejects-collect-of-unknown-item");
    }

    static void ValidatorChecksKitAndItemRules()
    {
        var db = Db();
        db.items.Add(Item("bandage"));
        db.kits.Add(new RewardKitDef { id = "bad", name = "bad", goldMin = 5, goldMax = 1, xpMin = 0, xpMax = 0, rolls = 1,
            entries = new List<KitEntry> { new KitEntry { itemId = "missing", minQuantity = 2, maxQuantity = 1, weight = -1 } } });
        var issues = FadenwerkValidator.Run(db, new List<FadenwerkNpc>(), new List<FadenwerkMarker>());
        Check(issues.Any(i => i.message.Contains("Gold min > max")), "validator-rejects-gold-min-over-max");
        Check(issues.Any(i => i.message.Contains("Menge min > max")), "validator-rejects-quantity-min-over-max");
        Check(issues.Any(i => i.message.Contains("Gewicht darf nicht negativ")), "validator-rejects-negative-weight");
        Check(issues.Any(i => i.message.Contains("fehlt im Katalog")), "validator-rejects-kit-of-unknown-item");
    }

    static void RewardsAreDeterministicPerSeed()
    {
        var db = Db();
        db.items.Add(Item("bandage"));
        db.kits.Add(new RewardKitDef { id = "k", goldMin = 1, goldMax = 100, xpMin = 0, xpMax = 50, rolls = 3,
            entries = new List<KitEntry> {
                new KitEntry { itemId = "bandage", minQuantity = 1, maxQuantity = 5, weight = 100 },
                new KitEntry { itemId = "bandage", minQuantity = 1, maxQuantity = 2, weight = 50, guaranteed = true } } });
        var a = FadenwerkRewards.Roll(db, "k", "seed-1");
        var b = FadenwerkRewards.Roll(db, "k", "seed-1");
        var c = FadenwerkRewards.Roll(db, "k", "seed-2");
        Check(a.gold == b.gold && a.xp == b.xp && a.items.Sum(i => i.quantity) == b.items.Sum(i => i.quantity), "rewards-deterministic-per-seed");
        Check(a.gold != c.gold || a.xp != c.xp || a.items.Sum(i => i.quantity) != c.items.Sum(i => i.quantity), "rewards-differ-by-seed");
        Check(a.kitId == "k" && a.seed == "seed-1", "rewards-carry-kit-and-seed");
        Check(a.items.Count == 1 && a.items[0].displayName == "bandage", "rewards-merge-same-item");
    }

    static void RewardsHonourGuaranteedAndWeightedEntries()
    {
        var db = Db();
        db.items.Add(Item("guaranteed"));
        db.items.Add(Item("rare"));
        db.kits.Add(new RewardKitDef { id = "k", rolls = 1,
            entries = new List<KitEntry> {
                new KitEntry { itemId = "guaranteed", minQuantity = 2, maxQuantity = 2, guaranteed = true },
                new KitEntry { itemId = "rare", minQuantity = 1, maxQuantity = 1, weight = 0 } } });
        var r = FadenwerkRewards.Roll(db, "k", "any");
        Check(r.items.Count == 1 && r.items[0].itemId == "guaranteed" && r.items[0].quantity == 2, "rewards-guaranteed-always-included");
        Check(r.items.All(i => i.itemId != "rare"), "rewards-zero-weight-never-drawn");
        Check(FadenwerkRewards.Roll(db, "missing", "s").items.Count == 0, "rewards-unknown-kit-is-empty");
        Check(FadenwerkRewards.Roll(null, "k", "s").items.Count == 0, "rewards-null-database-is-empty");
    }

    static void DialogueRunnerGatesForeignAndStaleChoices()
    {
        var db = Db();
        db.quests.Add(Quest("mill", "healer"));
        var allowed = new DialogueChoice { text = "give", action = FwAction.SetFlag, actionParam = "gave", conditionQuestId = "mill", conditionState = "active" };
        var hidden = new DialogueChoice { text = "claim", action = FwAction.SetFlag, actionParam = "stole", requiresFlag = "never_set" };
        db.dialogues.Add(new DialogueDef { id = "healer.chat", startNodeId = "start",
            nodes = new List<DialogueNode> { new DialogueNode { id = "start", choices = new List<DialogueChoice> { allowed, hidden } } } });
        var owner = new UnityEngine.GameObject();
        var mgr = owner.AddComponentStub<FadenwerkManager>();
        mgr.database = db;
        var runner = new FadenwerkDialogueRunner();
        Check(runner.Start(mgr, "healer.chat", null), "dialogue-starts");
        Check(runner.VisibleChoices().Count == 0, "dialogue-hides-flag-gated-and-condition-gated-choice");
        Check(mgr.AcceptQuest("mill"), "quest-accepted-for-condition");
        var visible = runner.VisibleChoices();
        Check(visible.Count == 1 && ReferenceEquals(visible[0], allowed), "dialogue-shows-choice-when-condition-holds");
        runner.Choose(new DialogueChoice { action = FwAction.SetFlag, actionParam = "foreign" });
        Check(!mgr.HasFlag("foreign"), "dialogue-rejects-foreign-choice");
        runner.Choose(allowed);
        Check(mgr.HasFlag("gave"), "dialogue-executes-visible-choice");
        Check(!mgr.HasFlag("stole"), "dialogue-never-executes-hidden-choice");
    }

    static void ManagerRunsTheFullQuestCycle()
    {
        var db = Db();
        db.items.Add(Item("bandage"));
        var q = new QuestDef { id = "mill", title = "mill", giverNpcId = "healer", turnInNpcId = "healer",
            rewardKitId = "k", requiredLevel = 1 };
        q.objectives = new List<ObjectiveDef> { new ObjectiveDef { type = FwObjective.Kill, targetId = "wolf", count = 2 } };
        db.quests.Add(q);
        db.kits.Add(new RewardKitDef { id = "k", goldMin = 5, goldMax = 5, xpMin = 10, xpMax = 10,
            entries = new List<KitEntry> { new KitEntry { itemId = "bandage", minQuantity = 1, maxQuantity = 1, guaranteed = true } } });
        var owner = new UnityEngine.GameObject();
        var mgr = owner.AddComponentStub<FadenwerkManager>();
        mgr.database = db;
        Check(mgr.GetOffersFor("healer").Any(x => x.id == "mill"), "manager-offers-quest-at-giver");
        Check(mgr.AcceptQuest("mill") && !mgr.AcceptQuest("mill"), "manager-accepts-once");
        Check(mgr.IsActive("mill") && !mgr.IsCompleted("mill"), "manager-quest-active");
        Check(mgr.ReportKill("wolf", 1) && mgr.GetProgress("mill", 0) == 1, "manager-records-partial-progress");
        Check(!mgr.IsReadyToTurnIn("mill"), "manager-not-ready-at-partial");
        mgr.ReportKill("wolf", 1);
        Check(mgr.IsReadyToTurnIn("mill"), "manager-ready-when-objective-met");
        Check(mgr.GetTurnInsFor("healer").Any(x => x.id == "mill"), "manager-offers-turn-in");
        var reward = mgr.CompleteQuest("mill");
        Check(reward != null && reward.gold == 5 && reward.xp == 10 && reward.items.Count == 1, "manager-grants-reward-once");
        Check(mgr.IsCompleted("mill") && !mgr.IsActive("mill"), "manager-quest-completed");
        Check(mgr.CompleteQuest("mill") == null, "manager-completion-is-one-shot");
        string saved = mgr.SaveToJson();
        mgr.LoadFromJson(saved);
        Check(mgr.IsCompleted("mill"), "manager-save-roundtrip-keeps-completion");
    }
}

internal static class ShimFactory
{
    public static T AddComponentStub<T>(this UnityEngine.GameObject go) where T : UnityEngine.MonoBehaviour, new() => new T();
}
