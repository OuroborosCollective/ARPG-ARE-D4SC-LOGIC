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
    }
}