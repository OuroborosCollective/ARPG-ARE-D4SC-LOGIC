using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// Owner rule 2026-11-15: Mara's missing rations are decided once. The ration panel exists only between the arrival
    /// scene and that decision; review fixes (#47) keep the scene in charge of all input while it plays.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator StealingFromMaraIsTheDecisionAndRemovesTheRationPanel()
        {
            EnterVillageAfterScene();
            var mara = Game.Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
            for (int attempt = 0; attempt < 6 && Distance(mara) > 1800; attempt++) MoveNear(mara.Xmm, mara.Zmm);
            yield return RunFrames(1);
            Assert.IsTrue(Game.CampDecisionOpen && CampPanel().activeSelf, "The ration panel is offered after the scene");
            int stock = Game.Progression.Social.CampProvisions;
            Route(ArenaHudControl.TakeRation);
            yield return RunFrames(3);
            Assert.AreEqual(stock, Game.Progression.Social.CampProvisions, "A single tap on theft must only ask for confirmation");
            StringAssert.Contains("erneut tippen", Message());
            Route(ArenaHudControl.TakeRation);
            yield return RunFrames(3);
            Assert.AreEqual(stock - 1, Game.Progression.Social.CampProvisions, "The confirmed second tap performs the theft");
            Assert.IsFalse(Game.GameOver);
            Assert.IsTrue(Game.Progression.CampDecisionMade && !Game.CampPanelVisible, "The theft is the ration decision");
            Assert.IsFalse(CampPanel().activeSelf, "The ration panel disappears after the theft");
            AssertRationButtonsGone();

            // Neither a second theft nor a late donation is possible any more.
            int after = Game.Progression.Social.CampProvisions;
            var provisions = Game.Progression.Social.Provisions;
            for (int attempt = 0; attempt < 6 && Distance(mara) > 1800; attempt++) MoveNear(mara.Xmm, mara.Zmm);
            Route(ArenaHudControl.TakeRation); yield return RunFrames(3);
            Route(ArenaHudControl.TakeRation); yield return RunFrames(3);
            Route(ArenaHudControl.Donate); yield return RunFrames(3);
            Assert.AreEqual(after, Game.Progression.Social.CampProvisions, "No second theft and no donation after the decision");
            Assert.AreEqual(provisions, Game.Progression.Social.Provisions);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), NativeJourneyMaps, out var saved));
            Assert.IsTrue(saved.CampDecisionMade, "The saved game keeps the decision");
            Assert.IsFalse(new ArenaSimulation(1, saved, System.Numerics.BigInteger.One, LootCatalog.Version1, NativeJourneyMaps).CampPanelVisible,
                "A loaded save does not offer the rations again");
        }

        [UnityTest]
        public IEnumerator AskingIlyanIsTheDecisionAndRemovesTheRationPanel()
        {
            EnterVillageAfterScene();
            yield return RunFrames(1);
            Assert.IsTrue(Game.CampDecisionOpen && CampPanel().activeSelf, "The ration panel is offered after the scene");
            var ilyan = Game.Npcs.Find(n => n.Id == 0);
            for (int attempt = 0; attempt < 6 && Distance(ilyan) > 1800; attempt++) MoveNear(ilyan.Xmm, ilyan.Zmm);
            typeof(ArenaRuntime).GetField("_pendingHungerAction", Flags).SetValue(_runtime, (int)HungerAction.HearIlyan);
            yield return RunFrames(3);
            Assert.AreEqual(1, Game.Progression.HungerStage, "Asking Ilyan starts the investigation");
            Assert.IsTrue(Game.Progression.CampDecisionMade && !Game.CampPanelVisible, "The investigation is the ration decision");
            Assert.IsFalse(CampPanel().activeSelf, "The ration panel disappears after asking Ilyan");
            AssertRationButtonsGone();

            var mara = Game.Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
            for (int attempt = 0; attempt < 6 && Distance(mara) > 1800; attempt++) MoveNear(mara.Xmm, mara.Zmm);
            int stock = Game.Progression.Social.CampProvisions;
            var provisions = Game.Progression.Social.Provisions;
            Route(ArenaHudControl.Donate); yield return RunFrames(3);
            Route(ArenaHudControl.TakeRation); yield return RunFrames(3);
            Route(ArenaHudControl.TakeRation); yield return RunFrames(3);
            Assert.AreEqual(stock, Game.Progression.Social.CampProvisions, "No ration action after the decision");
            Assert.AreEqual(provisions, Game.Progression.Social.Provisions);
        }

        /// <summary>Review fix (#47): no journal or choice over the scene, and nothing queued before or during it acts afterwards.</summary>
        [UnityTest]
        public IEnumerator ArrivalSceneOwnsAllInputUntilItEnds()
        {
            _runtime.enabled = false;
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStoryWithArrivalScene();
            Assert.IsTrue(Game.ArrivalScenePending);
            var provisions = Game.Progression.Social.Provisions;
            var pendingStory = typeof(ArenaRuntime).GetField("_pendingHungerAction", Flags);
            var pendingDonation = typeof(ArenaRuntime).GetField("_pendingCampDonation", Flags);
            var imgui = typeof(ArenaRuntime).GetProperty("ImguiLayerVisible", Flags);
            pendingStory.SetValue(_runtime, (int)HungerAction.HearIlyan);
            _runtime.enabled = true;
            yield return null; yield return null;
            Assert.IsTrue(_runtime.SceneOpen);
            Assert.IsFalse((bool)imgui.GetValue(_runtime), "Neither journal nor choices draw over the scene");
            Assert.AreEqual(-1, (int)pendingStory.GetValue(_runtime), "A story action queued before the scene is dropped");
            Assert.IsFalse(CampPanel().activeInHierarchy, "No ration choice while the scene plays");

            // Something slipping into the queues during the scene is dropped as well.
            pendingStory.SetValue(_runtime, (int)HungerAction.HearIlyan);
            pendingDonation.SetValue(_runtime, true);
            var advance = typeof(ArenaRuntime).GetMethod("AdvanceArrivalScene", Flags);
            for (int line = 0; line < ArrivalScene.Lines.Length; line++)
            {
                yield return new WaitForSecondsRealtime(.65f);
                advance.Invoke(_runtime, null);
                yield return null;
            }
            Assert.IsFalse(_runtime.SceneOpen);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.AreEqual(0, Game.Progression.HungerStage, "No story action queued during the scene runs once it ends");
            Assert.AreEqual(provisions, Game.Progression.Social.Provisions, "No donation queued during the scene runs once it ends");
            Assert.IsTrue((bool)imgui.GetValue(_runtime), "The journal is available again after the scene");
            var journal = typeof(ArenaRuntime).GetField("_hungerJournalOpen", Flags);
            Assert.IsTrue((bool)journal.GetValue(_runtime), "After the scene the journal explains both ways first");
            Assert.IsTrue(Game.CampDecisionOpen, "The single ration decision is open now");
            journal.SetValue(_runtime, false);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(CampPanel().activeInHierarchy, "Once the journal is closed the ration panel is offered");
        }

        private void EnterVillageAfterScene()
        {
            _runtime.enabled = false;
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            Assert.IsTrue(Game.IsVillage && !Game.ArrivalScenePending);
        }
    }
}
