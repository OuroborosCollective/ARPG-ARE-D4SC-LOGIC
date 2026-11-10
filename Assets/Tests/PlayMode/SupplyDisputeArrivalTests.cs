using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed class SupplyDisputeArrivalTests
    {
        [UnityTest]
        public IEnumerator ExistingWorldShowsMissingRationsWithoutSecondGameplayOwner()
        {
            ArenaRuntime runtime = Object.FindObjectOfType<ArenaRuntime>();
            if (runtime == null) runtime = new GameObject("SupplyDispute_Runtime_Smoke").AddComponent<ArenaRuntime>();
            yield return null;
            GameObject witness = GameObject.Find("Aschengrenze_Missing_Rations");
            Assert.IsNotNull(witness, "An actual empty storehouse shelf must be visible");
            foreach (Collider collider in witness.GetComponentsInChildren<Collider>(true))
                Assert.IsFalse(collider.enabled, "Only the Core is allowed to decide gameplay collisions");
            Assert.IsNotNull(GameObject.Find("ARE_NPC_0"));
            Assert.IsNotNull(GameObject.Find("ARE_NPC_1"));
            Assert.IsNotNull(GameObject.Find("ARE_NPC_2"));
            Assert.AreEqual(8, new ArenaSimulation(157).Progression.Social.CampProvisions,
                "The missing rations must be a real canonical stock difference");
            Assert.AreEqual(100, ArenaSimulation.TickMs);
        }
    }
}
