using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Actual Unity Play Mode smoke: boots the same client component used in the build.</summary>
    public sealed class ArenaBootSmokeTests
    {
        [UnityTest]
        public IEnumerator ClientBuildsArenaWithHeroCameraAndEnemies()
        {
            var runtime = Object.FindObjectOfType<ArenaRuntime>();
            if (runtime == null)
                runtime = new GameObject("ARE_ARPG_PlayMode_Smoke").AddComponent<ArenaRuntime>();

            yield return null;

            Assert.IsNotNull(runtime, "The arena runtime must exist in Play Mode");
            Assert.IsNotNull(GameObject.Find("Wanderer"), "The actual player visual was not built");
            Assert.IsNotNull(GameObject.Find("Enemy_1"), "The first enemy visual was not built");
            Assert.IsNotNull(GameObject.Find("Enemy_3"), "The boss visual was not built");
            Assert.IsNotNull(Camera.main, "The playable scene needs a main camera");
        }

        [Test]
        public void SharedSimulationCanBeDrivenWithoutUnityPhysics()
        {
            var world = new ArenaSimulation(0xBEEFUL);
            world.SetMoveInput(10000, 0);
            world.Step();
            Assert.Greater(world.PlayerXmm, 0);
            Assert.AreEqual(100, ArenaSimulation.TickMs);
        }

        [UnityTest]
        public IEnumerator ClientBuildsPassageAndKeepsSimulationAdvancing()
        {
            var runtime = Object.FindObjectOfType<ArenaRuntime>();
            if (runtime == null)
                runtime = new GameObject("ARE_ARPG_Passage_Smoke").AddComponent<ArenaRuntime>();
            yield return null;
            Assert.IsNotNull(GameObject.Find("Passage_Arch"), "The passage must be part of the actual world");
            var field = typeof(ArenaRuntime).GetField("_game", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var game = (ArenaSimulation)field.GetValue(runtime);
            ulong before = game.CurrentTick;
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(game.CurrentTick, before, "The client must actually advance its 100-ms core");
            Assert.IsTrue(runtime.isActiveAndEnabled);
        }

        [Test]
        public void NewPracticeFormatRoundTripsInUnity()
        {
            var progress = new CharacterProgression();
            progress.EarnExperience(System.Numerics.BigInteger.Pow(10, 50));
            string saved = progress.SaveString();
            Assert.IsTrue(saved.StartsWith("AC5|"));
            Assert.IsTrue(CharacterProgression.TryLoad(saved, out var loaded));
            Assert.AreEqual(progress.StableHash, loaded.StableHash);
            Assert.Greater(loaded.Level, new System.Numerics.BigInteger(ulong.MaxValue));
        }

        [UnityTest]
        public IEnumerator ClientProjectsAnAutonomousNpc()
        {
            var runtime = Object.FindObjectOfType<ArenaRuntime>();
            if (runtime == null) runtime = new GameObject("ARE_ARPG_NPC_Smoke").AddComponent<ArenaRuntime>();
            yield return null;
            Assert.IsNotNull(GameObject.Find("ARE_NPC_0"));
            var field = typeof(ArenaRuntime).GetField("_game", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var game = (ArenaSimulation)field.GetValue(runtime);
            var npc = game.Npcs[0];
            long x = npc.Xmm, z = npc.Zmm;
            yield return new WaitForSeconds(0.35f);
            Assert.IsTrue(npc.Xmm != x || npc.Zmm != z, "The real client must advance the adventurer's own activity");
        }

        [Test]
        public void PersonalNpcMemoryRoundTripsInUnity()
        {
            var world = new ArenaSimulation(137);
            world.Npcs[0].Xmm = 1000; world.Npcs[0].Zmm = 0;
            Assert.IsTrue(world.InteractNpc(0, NpcAction.Help).Accepted);
            Assert.IsTrue(CharacterProgression.TryLoad(world.Progression.SaveString(), out var loaded));
            Assert.AreEqual(new System.Numerics.BigInteger(8), loaded.Social.GetMemory(0).PositivePoints);
            Assert.AreEqual(world.Progression.StableHash, loaded.StableHash);
        }
    }
}

