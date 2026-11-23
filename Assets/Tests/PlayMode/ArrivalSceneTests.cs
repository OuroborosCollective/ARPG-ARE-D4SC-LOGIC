using System.Collections;
using System.Collections.Generic;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Owner decision 2026-11-15: a fixed, non-skippable arrival scene explains the missing rations before any choice.</summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator ArrivalSceneExplainsTheMissingRationsBeforeAnyChoice()
        {
            _runtime.enabled = false; UseDeviceFrameClock();
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Resolve(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStoryWithArrivalScene();
            Assert.IsTrue(Game.IsVillage && Game.ArrivalScenePending, "A first village arrival owes the player the scene");
            _runtime.enabled = true;
            yield return null; yield return null;
            Assert.IsTrue(_runtime.SceneOpen, "The scene starts on arrival");
            Assert.AreEqual(0, _runtime.SceneLineIndex);
            var gameplay = (GameObject)typeof(ArenaRuntime).GetField("_hudGameplay", Flags).GetValue(_runtime);
            Assert.IsFalse(gameplay.activeSelf, "Choice buttons stay hidden while the situation is explained");
            ulong tick = Game.CurrentTick;
            for (int i = 0; i < 12; i++) yield return null;
            Assert.AreEqual(tick, Game.CurrentTick, "The simulation is paused while the scene plays");

            var advance = typeof(ArenaRuntime).GetMethod("AdvanceArrivalScene", Flags);
            advance.Invoke(_runtime, null); advance.Invoke(_runtime, null);
            Assert.AreEqual(0, _runtime.SceneLineIndex, "Hasty taps cannot skip a line before it was readable");
            UseFrameClock(.25f); // the line-by-line walk below only waits for game time
            var speakers = new HashSet<string>();
            var rig = Camera.main.GetComponent<ArenaCameraRig>();
            for (int line = 0; line < ArrivalScene.Lines.Length; line++)
            {
                Assert.AreEqual(line, _runtime.SceneLineIndex);
                var speakerLabel = (UnityEngine.UI.Text)typeof(ArenaRuntime).GetField("_sceneSpeaker", Flags).GetValue(_runtime);
                var lineLabel = (UnityEngine.UI.Text)typeof(ArenaRuntime).GetField("_sceneText", Flags).GetValue(_runtime);
                Assert.IsTrue(lineLabel != null && lineLabel.gameObject.activeInHierarchy, "The subtitle must stay on screen");
                Assert.AreEqual(ArrivalScene.Lines[line].Text, lineLabel.text);
                Assert.IsTrue(rig.Cinematic, "The camera frames the speaker");
                var npcVisuals = (Dictionary<int, Transform>)typeof(ArenaRuntime).GetField("_npcVisuals", Flags).GetValue(_runtime);
                var speakerVisual = npcVisuals[ArrivalScene.Lines[line].SpeakerId];
                Vector3 toCamera = rig.transform.position - speakerVisual.position; toCamera.y = 0f;
                Assert.Greater(Vector3.Dot(speakerVisual.forward, toCamera.normalized), .3f, "The speaker faces the camera, not away from it");
                speakers.Add(speakerLabel.text);
                if (line == 0 || line == 3) { yield return new WaitForSeconds(.3f); yield return Capture("arrival-scene-line" + line + ".png"); }
                yield return new WaitForSeconds(.65f);
                advance.Invoke(_runtime, null);
                yield return null;
            }
            CollectionAssert.AreEquivalent(new[] { "Mara", "Dorn", "Ilyan" }, speakers);
            Assert.IsFalse(_runtime.SceneOpen);
            Assert.IsFalse(rig.Cinematic);
            Assert.IsFalse(Game.ArrivalScenePending, "The witnessed scene is recorded by the Core");
            StringAssert.Contains("Zwei Wege", Message());
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), NativeJourneyMaps, out var saved));
            Assert.IsFalse(new ArenaSimulation(1, saved, System.Numerics.BigInteger.One, LootCatalog.Version1, NativeJourneyMaps).ArrivalScenePending,
                "A loaded save does not replay the scene");
            for (int i = 0; i < 20; i++) yield return null;
            Assert.Greater(Game.CurrentTick, tick, "The simulation resumes after the scene");
        }
    }
}
