using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed class AschengrenzeArrivalTests
    {
        [UnityTest]
        public IEnumerator ArrivalBuildsRecognizableVillageAndPreservesCoreOwners()
        {
            ArenaRuntime runtime = Object.FindObjectOfType<ArenaRuntime>();
            if (runtime == null)
                runtime = new GameObject("Aschengrenze_RealRuntime_Smoke").AddComponent<ArenaRuntime>();
            yield return null;

            string[] landmarks =
            {
                "Aschengrenze_Approach_Road",
                "Aschengrenze_Market_Square",
                "Aschengrenze_Supply_Hall",
                "Aschengrenze_Watchtower",
                "Aschengrenze_Signal_Beacon"
            };
            foreach (string name in landmarks)
            {
                GameObject landmark = GameObject.Find(name);
                Assert.IsNotNull(landmark, "Missing actual Unity village landmark: " + name);
                foreach (Collider collider in landmark.GetComponentsInChildren<Collider>(true))
                    Assert.IsFalse(collider.enabled, "Projection-only landmark cannot own collision: " + name);
            }

            Assert.IsNotNull(GameObject.Find("Passage_Arch"), "Existing north passage must survive");
            Assert.IsNotNull(GameObject.Find("ARE_NPC_0"), "The existing adventurer remains a real NPC");
            Assert.IsNotNull(GameObject.Find("ARE_NPC_1"), "The existing provider remains a real NPC");
            Assert.IsNotNull(GameObject.Find("ARE_NPC_2"), "The existing guard remains a real NPC");
            Assert.AreEqual(100, ArenaSimulation.TickMs);
        }
    }
}
