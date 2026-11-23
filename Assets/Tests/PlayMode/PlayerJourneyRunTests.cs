using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// The first chapter played end to end with nothing but a phone's stick and taps (owner request 2026-11-23): crypt,
    /// Aschengrenze, Aschestraße, the mill, back to the village for the supply decision and up to the mill again.
    /// Nothing is moved, healed or called behind the game's back. After every passage the app is closed and reopened,
    /// so each area has to come back from its save alone.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        /// <summary>A phone drawing 10 frames a second: one Core tick per frame, so the run is the same every time.</summary>
        private const float PlayerRunFrame = .1f;
        private RenderTexture _stampView;

        /// <summary>
        /// Draw the 3D view into a stamp-sized picture. The server has no graphics card, and a full-size picture per frame
        /// would stretch the run to an hour; play, HUD hit tests, saves and every Update still run unchanged.
        /// </summary>
        private void DrawStampSized()
        {
            var camera = (Camera)typeof(ArenaRuntime).GetField("_camera", Flags).GetValue(_runtime);
            if (camera == null) return;
            if (_stampView == null) _stampView = new RenderTexture(192, 108, 16) { name = "PlayerRun_StampView" };
            camera.targetTexture = _stampView;
            // The shadow map is drawn at full resolution whatever the picture size; the stamp view needs none.
            var urp = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urp != null) urp.renderShadows = false;
        }

        [UnityTest, Category("PlayerRun"), Timeout(3600000)]
        public IEnumerator APlayerFinishesTheMillChapterWithStickAndTapsAlone()
        {
            Vector2? hud = ArenaRuntime.HudScreenOverride;
            var player = new JourneyPlayer(() => _runtime, 12000);
            ArenaRuntime.HudScreenOverride = new Vector2(1920f, 1080f);
            PlayerDevice.Scripted = player.Phone;
            Time.captureDeltaTime = PlayerRunFrame;
            DrawStampSized();
            try
            {
                Assert.AreEqual(1, player.Game.Progression.EntryAct, "a new game starts in the crypt");
                player.Note("new game");

                // Act I: clear the crypt, pick up what falls, leave through the north passage.
                yield return player.Settle();
                yield return player.FightAll("crypt");
                yield return player.PickUpAll();
                yield return player.Travel("crypt passage", ArenaSimulation.PassageZmm, 2);
                yield return ReopenApp(player, "crypt to village");

                // Act II: listen to the arrival, then ask Ilyan about his secret.
                yield return player.Settle();
                yield return player.Story(HungerAction.HearIlyan, () => NpcSpot(player, 0), 1800,
                    () => player.Game.Progression.HungerStage >= 1);
                yield return player.Travel("village north gate", ArenaSimulation.PassageZmm, 3);
                yield return ReopenApp(player, "village to ash road");

                // Act III: read the waymark, save the hunter from the bear, walk on to the mill.
                yield return player.Settle();
                yield return player.Story(HungerAction.InspectTrail, () => new Vector2Int(0, -2000), 1500,
                    () => player.Game.Progression.HungerStage >= 2);
                yield return player.RescueHunter();
                yield return player.Travel("road to the mill", ArenaSimulation.PassageZmm, 4);
                yield return ReopenApp(player, "ash road to mill");

                // Act IV: search the shelter, then go back to the village with what was found.
                yield return player.Settle();
                yield return player.Story(HungerAction.InspectShelter, () => new Vector2Int(-4000, -2000), 1500,
                    () => player.Game.Progression.HungerStage >= 3);
                yield return player.Travel("mill south path", ArenaSimulation.ReturnPassageZmm, 3);
                yield return ReopenApp(player, "mill to ash road");
                yield return player.Travel("ash road south", ArenaSimulation.ReturnPassageZmm, 2);
                yield return ReopenApp(player, "ash road to village");

                // Act II again: keep Ilyan's secret and refill Mara's store, or keep silent if the rations are short.
                yield return player.Settle();
                var social = player.Game.Progression.Social;
                bool refill = social.Provisions >= SocialLedger.CampCapacity - social.CampProvisions;
                yield return player.Story(refill ? HungerAction.Replace : HungerAction.Protect,
                    () => NpcSpot(player, refill ? SocialLedger.MaraNpcId : 0), 1800,
                    () => player.Game.Progression.SupplyChoice != HungerChoice.Undecided);
                yield return player.Travel("village north gate", ArenaSimulation.PassageZmm, 3);
                yield return ReopenApp(player, "village to ash road (second time)");
                yield return player.Travel("road to the mill", ArenaSimulation.PassageZmm, 4);
                yield return ReopenApp(player, "ash road to mill (second time)");

                // Act IV again: one bed is empty. The chapter ends and Orsel trusts the player.
                yield return player.Settle();
                yield return player.Story(HungerAction.InspectBeds, () => new Vector2Int(-4000, 5000), 1500,
                    () => player.Game.Progression.HungerStage >= 4);
                var p = player.Game.Progression;
                Assert.AreEqual(4, p.EntryAct);
                Assert.AreEqual(4, p.HungerStage, "the mill chapter is complete");
                Assert.AreEqual(refill ? HungerChoice.Replace : HungerChoice.Protect, p.SupplyChoice);
                Assert.IsTrue(p.Road.Rescued && p.Road.FavorClaimed, "the hunter lives and was thanked");
                Assert.IsTrue(p.Village.OrselTeachesUnlocked, "after the mill Orsel offers her teachings");
                yield return ReopenApp(player, "chapter end", atPassage: false);
                Assert.AreEqual(4, player.Game.Progression.HungerStage, "the finished chapter survives a restart of the app");
                player.Note("chapter complete · deaths " + player.Deaths + " · story taps " + player.StoryTaps + " · taps " + player.Phone.TapCount);
            }
            finally
            {
                PlayerDevice.Scripted = null;
                ArenaRuntime.HudScreenOverride = hud;
                Time.captureDeltaTime = 0f;
                var camera = _runtime != null ? (Camera)typeof(ArenaRuntime).GetField("_camera", Flags).GetValue(_runtime) : null;
                if (camera != null && camera.targetTexture == _stampView) camera.targetTexture = null;
                if (_stampView != null) { _stampView.Release(); Object.Destroy(_stampView); _stampView = null; }
                TestContext.WriteLine(string.Join("\n", player.Trail));
            }
        }

        /// <summary>Where a village figure stands right now.</summary>
        private static Vector2Int NpcSpot(JourneyPlayer player, int id)
        {
            var npc = player.Game.Npcs.Find(n => n.Id == id);
            Assert.IsNotNull(npc, "figure " + id + " is not in the village: " + player.Describe());
            return new Vector2Int((int)npc.Xmm, (int)npc.Zmm);
        }

        /// <summary>
        /// Close the app and open it again: the save alone must bring the player back. Right after a passage the player
        /// stands on the new checkpoint; in the middle of an area the game resumes at that area's checkpoint.
        /// </summary>
        private IEnumerator ReopenApp(JourneyPlayer player, string passage, bool atPassage = true)
        {
            player.Phone.LiftAll();
            yield return player.Frame();
            var before = player.Game;
            var p = before.Progression;
            int act = p.EntryAct, stage = p.HungerStage;
            var items = new ulong[p.Items.Count];
            for (int i = 0; i < items.Length; i++) items[i] = p.Items[i].StableHash;
            var provisions = p.Social.Provisions;
            var choice = p.SupplyChoice;
            bool rescued = p.Road.Rescued, thanked = p.Road.FavorClaimed;
            long x = before.PlayerXmm, z = before.PlayerZmm;
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), NativeJourneyMaps, out var saved),
                passage + ": the checkpoint written at the passage reads back");
            Assert.AreEqual(act, saved.EntryAct, passage + ": the save holds the new area");
            foreach (var root in _scene.GetRootGameObjects()) Object.Destroy(root);
            yield return null;
            _runtime = new GameObject("Player_Run_Runtime").AddComponent<ArenaRuntime>();
            DrawStampSized();
            yield return null;
            var after = player.Game;
            var q = after.Progression;
            Assert.AreEqual(act, q.EntryAct, passage + ": reopened in the same area");
            Assert.AreEqual(stage, q.HungerStage, passage + ": the story stage survives");
            Assert.AreEqual(choice, q.SupplyChoice, passage + ": the supply decision survives");
            var carried = new ulong[q.Items.Count];
            for (int i = 0; i < carried.Length; i++) carried[i] = q.Items[i].StableHash;
            CollectionAssert.AreEqual(items, carried, passage + ": the same carried items survive, in the same order");
            Assert.AreEqual(provisions, q.Social.Provisions, passage + ": the rations survive");
            Assert.AreEqual(rescued, q.Road.Rescued, passage + ": the hunter's fate survives");
            Assert.AreEqual(thanked, q.Road.FavorClaimed, passage + ": the hunter's thanks survive");
            if (!atPassage) { x = player.ArrivalX; z = player.ArrivalZ; }
            string where = atPassage ? "where the passage left them" : "at the area's checkpoint, where its passage put them";
            Assert.AreEqual(x, after.PlayerXmm, passage + ": the player stands " + where + " (x)");
            Assert.AreEqual(z, after.PlayerZmm, passage + ": the player stands " + where + " (z)");
            player.Note("app closed and reopened after " + passage);
        }
    }
}
