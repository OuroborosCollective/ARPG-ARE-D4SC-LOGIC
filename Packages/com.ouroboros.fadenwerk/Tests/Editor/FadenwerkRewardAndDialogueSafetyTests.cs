using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fadenwerk.Tests
{
    public class FadenwerkRewardAndDialogueSafetyTests
    {
        FadenwerkDatabase _database;
        GameObject _owner;
        FadenwerkManager _manager;

        [SetUp]
        public void SetUp()
        {
            _database = ScriptableObject.CreateInstance<FadenwerkDatabase>();
            _owner = new GameObject("Fadenwerk-safety-test");
            _manager = _owner.AddComponent<FadenwerkManager>();
            _manager.dontDestroyOnLoad = false;
            _manager.database = _database;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_owner);
            UnityEngine.Object.DestroyImmediate(_database);
        }

        [Test]
        public void ManualRewardIsOneShotEvenAcrossJsonReload()
        {
            _database.kits.Add(new RewardKitDef { id = "bandage", goldMin = 3, goldMax = 3 });
            Assert.NotNull(_manager.GiveKit("bandage", "dialogue:mill:once"));
            Assert.IsNull(_manager.GiveKit("bandage", "dialogue:mill:once"));
            string saved = _manager.SaveToJson();
            _manager.LoadFromJson(saved);
            Assert.IsNull(_manager.GiveKit("bandage", "dialogue:mill:once"));
            Assert.IsNull(_manager.GiveKit("bandage", ""));
        }

        [Test]
        public void UnknownQuestConditionMustFailClosed()
        {
            Assert.IsFalse(_manager.CheckQuestState("unknown", "not-an-allowed-condition"));
        }

        [Test]
        public void DialogueRejectsForeignAndStaleChoices()
        {
            var allowed = new DialogueChoice
            {
                text = "Claim before quest starts", action = FwAction.SetFlag,
                actionParam = "wrongly_granted", conditionQuestId = "mill_quest", conditionState = "available"
            };
            var node = new DialogueNode { id = "start", choices = new List<DialogueChoice> { allowed } };
            _database.quests.Add(new QuestDef { id = "mill_quest", giverNpcId = "healer" });
            _database.dialogues.Add(new DialogueDef
            {
                id = "healer_chat", startNodeId = "start", nodes = new List<DialogueNode> { node }
            });
            var runner = new FadenwerkDialogueRunner();
            Assert.IsTrue(runner.Start(_manager, "healer_chat", null));
            runner.Choose(new DialogueChoice { action = FwAction.SetFlag, actionParam = "foreign" });
            Assert.IsFalse(_manager.HasFlag("foreign"));
            CollectionAssert.Contains(runner.VisibleChoices(), allowed);
            Assert.IsTrue(_manager.AcceptQuest("mill_quest"));
            runner.Choose(allowed);
            Assert.IsFalse(_manager.HasFlag("wrongly_granted"));
        }
    }
}
