using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Owner report 2026-11-15: greeting Ilyan makes him flee, asking him does not advance, rations loop without progress.</summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        /// <summary>
        /// Plays the first village like a player through the shared HUD routing table and the story journal. Owner rule
        /// 2026-11-15: refilling Mara's camp is one of the single ration decisions; afterwards the panel is gone for good.
        /// </summary>
        [UnityTest]
        public IEnumerator VillageGreetingAskingAndRationsBehaveLikeTheShownObjective()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            Assert.IsTrue(Game.IsVillage && Game.CampDecisionOpen, "After the arrival scene Mara's rations wait for one decision");
            var ilyan = Game.Npcs.Find(n => n.Id == 0);
            Debug.Log("QUEST_START objective=" + Game.HungerObjective + " stage=" + Game.Progression.HungerStage);

            // Walk to Ilyan with real ticks; he may move while we approach. A normal greeting must not make him flee.
            for (int attempt = 0; attempt < 6 && Distance(ilyan) > 1800; attempt++) MoveNear(ilyan.Xmm, ilyan.Zmm);
            LogIlyan("before-greet", ilyan);
            Route(ArenaHudControl.NpcGreet);
            yield return RunFrames(3);
            LogIlyan("after-greet", ilyan);
            for (int t = 0; t < 40; t++) { Game.Step(); if (t % 5 == 0) LogIlyan("greet+" + t, ilyan); }
            Assert.AreNotEqual(NpcBehavior.Flee, ilyan.Behavior, "A normal greeting must not make Ilyan flee");
            Assert.AreNotEqual(NpcDisposition.Hostile, ilyan.Memory.Disposition(ilyan.Personality), "A normal greeting must not make Ilyan hostile");

            // First way: hand rations to Mara through the HUD until her camp is full.
            var mara = Game.Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
            for (int attempt = 0; attempt < 6 && Distance(mara) > 1800; attempt++) MoveNear(mara.Xmm, mara.Zmm);
            yield return RunFrames(1);
            Assert.IsTrue(CampPanel().activeSelf, "The ration panel is offered while the decision is open");
            int accepted = 0, missingAtStart = SocialLedger.CampCapacity - Game.Progression.Social.CampProvisions;
            Assert.Greater(missingAtStart, 0, "A fresh village begins with stolen rations");
            for (int i = 0; i < 8; i++)
            {
                var before = Game.Progression.Social.Provisions;
                Route(ArenaHudControl.Donate);
                yield return RunFrames(3);
                for (int t = 0; t < 12; t++) Game.Step();
                if (Game.Progression.Social.Provisions < before) accepted++;
                Debug.Log("QUEST_DONATE " + i + " provisions=" + Game.Progression.Social.Provisions + " camp=" + Game.Progression.Social.CampProvisions +
                    " stage=" + Game.Progression.HungerStage + " objective=" + Game.HungerObjective + " message=" + Message());
            }
            Assert.AreEqual(missingAtStart, accepted, "Exactly the missing rations are accepted; afterwards nothing is consumed");
            Assert.IsTrue(Game.Progression.Social.CampRefilled, "Refilling Mara's camp completes the recorded side task");
            Assert.IsTrue(Game.Progression.CampDecisionMade && !Game.CampPanelVisible, "The refill is the ration decision");
            var archive = Game.Progression.Social.Forge.Archive;
            var donation = archive.Events.Last(e => e.Context == "camp.donation");
            var decision = archive.Events.Single(e => e.Context == "camp.decision.replaced");
            Assert.AreEqual(donation.Id, decision.CauseEventId, "The HUD refill must reference the final accepted ration");
            Assert.IsTrue(CharacterProgression.TryLoad(Game.Progression.SaveString(), out var restored));
            Assert.AreEqual(donation.Id, restored.Social.Forge.Archive.Events.Single(e => e.Id == decision.Id).CauseEventId);
            Assert.IsFalse(CampPanel().activeSelf, "The ration panel disappears once the decision is made");
            AssertRationButtonsGone();
            Assert.IsTrue(ArenaRuntime.QuestMilestones(Game.Progression).Exists(m => m.Key.Contains("fehlende Rationen") && m.Value), "The quest log shows the decision");

            // The hidden panel cannot steal either: routed taps on the former theft button do nothing.
            int stock = Game.Progression.Social.CampProvisions;
            Route(ArenaHudControl.TakeRation); yield return RunFrames(3);
            Route(ArenaHudControl.TakeRation); yield return RunFrames(3);
            Assert.AreEqual(stock, Game.Progression.Social.CampProvisions, "After the decision nothing can be taken from the camp");

            // The story continues: asking Ilyan through the journal action the objective points to.
            for (int attempt = 0; attempt < 6 && Distance(ilyan) > 1800; attempt++) MoveNear(ilyan.Xmm, ilyan.Zmm);
            LogIlyan("before-ask", ilyan);
            typeof(ArenaRuntime).GetField("_pendingHungerAction", Flags).SetValue(_runtime, (int)HungerAction.HearIlyan);
            yield return RunFrames(3);
            Debug.Log("QUEST_ASK stage=" + Game.Progression.HungerStage + " message=" + Message());
            Assert.AreEqual(1, Game.Progression.HungerStage, "Asking Ilyan as the objective shows must advance the story");
        }

        /// <summary>One mistaken hostile tap must not lock the main quest that requires talking to Ilyan.</summary>
        [UnityTest]
        public IEnumerator OneMistakenAttackDoesNotLockIlyansMainQuest()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            var ilyan = Game.Npcs.Find(n => n.Id == 0);
            MoveNear(ilyan.Xmm, ilyan.Zmm);
            Route(ArenaHudControl.NpcAttack);
            yield return RunFrames(3);
            LogIlyan("after-attack", ilyan);
            Assert.AreEqual(NpcBehavior.Flee, ilyan.Behavior, "Right after a hostile act Ilyan first flees");
            Assert.AreEqual(NpcDisposition.Hostile, ilyan.Mood);
            for (int t = 1; t <= (int)ArenaSimulation.CalmIntervalTicks * 2 + 20; t++) { Game.SetMoveInput(0, 0); Game.Step(); if (t % 300 == 0) LogIlyan("wait+" + t, ilyan); }
            var relation = Game.Progression.Social.Forge.Archive.Get("ilyan").RelationshipTo("player");
            Assert.Less(relation.Hostility, NpcAgent.FleeHostility, "Without further violence Ilyan calms down");
            Assert.Less(relation.Trust, 0, "The deed stays remembered as lost trust");
            Assert.AreEqual(System.Numerics.BigInteger.One, ilyan.Memory.HostileActions, "The legacy record of the deed is untouched");
            Assert.AreNotEqual(NpcDisposition.Hostile, ilyan.Mood);

            // Approach normally: he must not run away again, greeting works and the main quest continues.
            MoveNear(ilyan.Xmm, ilyan.Zmm);
            for (int t = 0; t < 10; t++) Game.Step();
            LogIlyan("approach", ilyan);
            Assert.AreNotEqual(NpcBehavior.Flee, ilyan.Behavior, "A calmed Ilyan must not flee from an approaching player");
            Route(ArenaHudControl.NpcGreet);
            yield return RunFrames(3);
            LogIlyan("greet", ilyan);
            StringAssert.Contains("Uebergriff", Message(), "His greeting still remembers the deed");
            for (int t = 0; t < 12; t++) Game.Step();
            Assert.AreNotEqual(NpcBehavior.Flee, ilyan.Behavior);
            MoveNear(ilyan.Xmm, ilyan.Zmm);
            typeof(ArenaRuntime).GetField("_pendingHungerAction", Flags).SetValue(_runtime, (int)HungerAction.HearIlyan);
            yield return RunFrames(3);
            LogIlyan("ask stage=" + Game.Progression.HungerStage, ilyan);
            Assert.AreEqual(1, Game.Progression.HungerStage, "The main quest stays reachable after one mistaken attack");
        }

        private long Distance(NpcAgent npc)
        { return (long)Mathf.Sqrt((npc.Xmm - Game.PlayerXmm) * (float)(npc.Xmm - Game.PlayerXmm) + (npc.Zmm - Game.PlayerZmm) * (float)(npc.Zmm - Game.PlayerZmm)); }

        /// <summary>Walk like a player with fixed ticks: along the village road first, then sideways to the target.</summary>
        private void MoveNear(long x, long z)
        {
            Walk(Game.PlayerXmm, z, 700);
            Walk(x + (x < Game.PlayerXmm ? 1100 : -1100), z, 700);
            Debug.Log("QUEST_PLAYER pos=" + Game.PlayerXmm + "," + Game.PlayerZmm + " target=" + x + "," + z);
        }

        private void Walk(long x, long z, long tolerance)
        {
            for (int tick = 0; tick < 250; tick++)
            {
                long dx = x - Game.PlayerXmm, dz = z - Game.PlayerZmm;
                if (System.Math.Abs(dx) < tolerance && System.Math.Abs(dz) < tolerance) break;
                Game.SetMoveInput(System.Math.Abs(dx) < tolerance ? 0 : System.Math.Sign(dx) * 10000, System.Math.Abs(dz) < tolerance ? 0 : System.Math.Sign(dz) * 10000);
                Game.Step();
            }
            Game.SetMoveInput(0, 0);
        }

        private void Route(ArenaHudControl control)
        { typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags).Invoke(_runtime, new object[] { control }); }

        private IEnumerator RunFrames(int frames)
        {
            _runtime.enabled = true;
            for (int i = 0; i < frames * 4; i++) yield return null;
            _runtime.enabled = false;
        }

        private string Message() { return (string)typeof(ArenaRuntime).GetField("_message", Flags).GetValue(_runtime); }

        private GameObject CampPanel() { return (GameObject)typeof(ArenaRuntime).GetField("_hudCamp", Flags).GetValue(_runtime); }

        /// <summary>The shared touch table, fed exactly like the runtime feeds it, offers no ration control any more.</summary>
        private void AssertRationButtonsGone()
        {
            var layout = new ArenaHudLayout(1340, 800, new Rect(0, 0, 1340, 800));
            bool open = Game.CampPanelVisible, canTake = Game.Progression.EntryAct == 2 && open;
            Assert.AreNotEqual(ArenaHudControl.Donate, layout.HitTest(layout.DonationRect.center, false, false, open, false, canTake, Game.CanDonateCampRation),
                "No donation button after the decision");
            Assert.AreNotEqual(ArenaHudControl.TakeRation, layout.HitTest(layout.TakeRationRect.center, false, false, open, false, canTake, Game.CanDonateCampRation),
                "No theft button after the decision");
        }

        private void LogIlyan(string label, NpcAgent ilyan)
        {
            var relation = Game.Progression.Social.Forge?.Archive.Get("ilyan")?.RelationshipTo("player");
            Debug.Log("QUEST_ILYAN " + label + " behavior=" + ilyan.Behavior + " dist=" + Distance(ilyan) + " pos=" + ilyan.Xmm + "," + ilyan.Zmm +
                " cooldown=" + ilyan.InteractionCooldownTicks + " disposition=" + ilyan.Memory.Disposition(ilyan.Personality) + " personality=" + ilyan.Personality +
                " neg=" + ilyan.Memory.NegativePoints + " pos=" + ilyan.Memory.PositivePoints +
                (relation == null ? "" : " hostility=" + relation.Hostility + " fear=" + relation.Fear + " trust=" + relation.Trust) + " message=" + Message());
        }
    }
}
