using System.Collections;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator WorldInteractionMaraWaitsForAnExplicitAnswer()
        {
            _runtime.enabled = false;
            Assert.AreEqual(3, ArrangeCryptKill().Kills);
            CollectCryptLoot();
            Game.SetMoveInput(0, 10000);
            for (int i = 0; i < 50 && !Game.AtWorldPassage; i++) Game.Step();
            CallWorldInteraction("AdvanceCrypt");
            CrossCourt();
            Assert.IsTrue(Game.CompleteArrivalScene());
            var mara = Game.Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
            for (int i = 0; i < 80 && !Game.CanTalkToWorldNpc(mara.Id); i++)
            {
                long dx = mara.Xmm - Game.PlayerXmm, dz = mara.Zmm - Game.PlayerZmm;
                Game.SetMoveInput(System.Math.Sign(dx) * 10000, System.Math.Sign(dz) * 10000);
                Game.Step();
            }
            Game.SetMoveInput(0, 0);
            Assert.IsTrue(Game.CanTalkToWorldNpc(mara.Id));
            // Arrange an empty place in the camp for the real ration transaction.
            int stock = Game.Progression.Social.CampProvisions;
            Assert.IsTrue(Game.CanDonateCampRation);
            CallWorldInteraction("UpdatePresentation", 1f);
            Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();
            Assert.IsNull(WorldRuntimeField("_worldConversation"), "Approach alone never opens a dialogue");

            string before = Game.Progression.SaveString();
            TapWorldLabel("Npc", mara.Id);
            Assert.AreEqual(before, Game.Progression.SaveString(), "Opening the dialogue cannot accept a quest or spend a ration");
            CallWorldInteraction("UpdateHud");
            Assert.IsFalse(((GameObject)WorldRuntimeField("_hudGameplay")).activeSelf);
            CallWorldInteraction("UpdateWorldCamera");
            var camera = Camera.main.GetComponent<ArenaCameraRig>();
            camera.AdvancePresentation(.1f);
            Assert.AreEqual(HeroFraming.Dialogue, camera.Framing);

            CallWorldInteraction("RefreshWorldAnswers");
            var answers = (IList)WorldRuntimeField("_worldAnswers");
            CallWorldInteraction("SelectWorldAnswer", answers.Count - 1);
            Assert.IsNull(WorldRuntimeField("_pendingWorldInteraction"));
            Assert.AreEqual(before, Game.Progression.SaveString(), "Leaving the dialogue must leave the save unchanged");
            CallWorldInteraction("UpdateHud");
            Assert.IsTrue(((GameObject)WorldRuntimeField("_hudGameplay")).activeSelf);
            CallWorldInteraction("UpdateWorldCamera");
            camera.AdvancePresentation(.1f);
            Assert.AreNotEqual(HeroFraming.Dialogue, camera.Framing);

            TapWorldLabel("Npc", mara.Id);
            CallWorldInteraction("RefreshWorldAnswers");
            int donation = -1;
            for (int i = 0; i < answers.Count; i++)
                if ((string)answers[i].GetType().GetField("Item1").GetValue(answers[i]) == "Eine Ration übergeben")
                    donation = i;
            Assert.GreaterOrEqual(donation, 0);
            CallWorldInteraction("SelectWorldAnswer", donation);
            _runtime.enabled = true;
            for (int frame = 0; frame < 120 && Game.Progression.Social.CampProvisions == stock; frame++) yield return null;
            Assert.AreEqual(stock + 1, Game.Progression.Social.CampProvisions);
            Assert.IsNull(WorldRuntimeField("_worldConversation"));
        }

        [UnityTest]
        public IEnumerator WorldInteractionBreaksTheTappedVaseAndKeepsTheHud()
        {
            _runtime.enabled = false;
            Assert.AreEqual(3, ArrangeCryptKill().Kills);
            CollectCryptLoot();
            Game.SetMoveInput(0, 10000);
            for (int i = 0; i < 50 && !Game.AtWorldPassage; i++) Game.Step();
            CallWorldInteraction("AdvanceCrypt");
            // Authoritative foot positions are deliberately arranged for the two-vase overlap.
            SetWorldPosition(KilnYard.SideGateXmm - 400, KilnYard.SideGateZmm);
            var yard = Game.TryEnterKilnYard();
            Assert.IsNotNull(yard);
            CallWorldInteraction("InstallNewRun", yard, "Brennerhof");
            SetWorldPosition(-3950, -5700);
            CallWorldInteraction("UpdatePresentation", 1f);
            Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();
            Assert.AreEqual(0, Game.NearKilnVase.Value.Id);
            TapWorldLabel("Vase", 1);
            Assert.IsNull(WorldRuntimeField("_worldConversation"), "Vases are a direct action");
            _runtime.enabled = true;
            for (int frame = 0; frame < 120 && !Game.Progression.KilnVaseBroken(1); frame++) yield return null;
            Assert.IsTrue(Game.Progression.KilnVaseBroken(1));
            Assert.IsFalse(Game.Progression.KilnVaseBroken(0));
            Assert.IsTrue(((GameObject)WorldRuntimeField("_hudGameplay")).activeSelf);
        }

        private object CallWorldInteraction(string name, params object[] args) =>
            typeof(ArenaRuntime).GetMethod(name, Flags).Invoke(_runtime, args);

        private object WorldRuntimeField(string name) => typeof(ArenaRuntime).GetField(name, Flags).GetValue(_runtime);

        private void SetWorldPosition(long x, long z)
        {
            foreach (string name in new[] { "PlayerXmm", "PrevPlayerXmm" })
                typeof(ArenaSimulation).GetProperty(name).SetValue(Game, x);
            foreach (string name in new[] { "PlayerZmm", "PrevPlayerZmm" })
                typeof(ArenaSimulation).GetProperty(name).SetValue(Game, z);
        }

        private void TapWorldLabel(string kind, int id)
        {
            var layout = (ArenaHudLayout)CallWorldInteraction("CurrentHudLayout");
            CallWorldInteraction("RefreshWorldTargets", layout);
            foreach (object target in (IList)WorldRuntimeField("_worldTargets"))
            {
                var type = target.GetType();
                if (type.GetField("Kind").GetValue(target).ToString() != kind ||
                    (int)type.GetField("Id").GetValue(target) != id) continue;
                var hit = (Rect)type.GetField("Hit").GetValue(target);
                Assert.GreaterOrEqual(hit.height, 48f, "The target must have a usable touch label");
                Vector2 point = hit.center;
                Vector2 screen = new Vector2(layout.SafeArea.xMin + point.x * layout.Scale,
                    layout.SafeArea.yMax - point.y * layout.Scale);
                Assert.IsTrue((bool)CallWorldInteraction("TryTapWorldTarget", screen, point));
                return;
            }
            Assert.Fail("Missing visible interaction: " + kind + " " + id);
        }
    }
}
