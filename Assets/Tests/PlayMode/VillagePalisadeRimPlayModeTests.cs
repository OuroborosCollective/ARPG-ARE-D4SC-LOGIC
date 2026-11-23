using System;
using System.Collections;
using System.Linq;
using AreArpg.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator PalisadeHidesFlatWorldEdgeAndCoreStillOwnsTravelCollision()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Native private art and actual GameView capture required");
            _runtime.enabled = false;
            foreach (MonsterState monster in Game.Monsters)
            {
                monster.Xmm = 1000;
                monster.Zmm = 0;
                monster.Health = 1;
            }
            Resolve(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000);
            TravelStory();
            Assert.IsTrue(Game.IsVillage);
            yield return null;

            var wall=UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .SingleOrDefault(t=>t.name=="VillagePalisadeRimPass1");
            var trees=UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .SingleOrDefault(t=>t.name=="VillageForestBehindPalisadePass1");
            var ridge=UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .SingleOrDefault(t=>t.name=="VillageRollingHorizonPass1");
            Assert.IsNotNull(wall);
            Assert.IsNotNull(trees);
            Assert.IsNotNull(ridge);
            Assert.IsTrue(wall.gameObject.activeInHierarchy);
            Assert.IsTrue(trees.gameObject.activeInHierarchy);
            Assert.IsEmpty(wall.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(trees.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(ridge.GetComponentsInChildren<Collider>(true));

            // The existing 27-m Core arena limit, not art-physics, stops movement through the wall.
            Assert.IsFalse(Game.CanStand(ArenaSimulation.ArenaLimitMm + 1,0));
            Assert.IsFalse(Game.CanStand(-ArenaSimulation.ArenaLimitMm - 1,0));
            Assert.IsFalse(Game.CanStand(0,-ArenaSimulation.ArenaLimitMm - 1));
            Assert.IsFalse(Game.CanStand(9000,ArenaSimulation.ArenaLimitMm + 1));
            Assert.IsTrue(Game.CanStand(0,ArenaSimulation.PassageZmm),
                "Existing north portal remains traversable to its interaction radius");

            ulong hash=Game.ComputeWorldHash();
            string save=Game.Progression.SaveString();
            yield return Capture("village-palisade-forest-day.png");
            Assert.AreEqual(hash,Game.ComputeWorldHash());
            Assert.AreEqual(save,Game.Progression.SaveString());
        }
    }
}
