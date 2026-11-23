using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Bau-Modus: a published plan reaches the running game live, as passive scenery only.</summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator BauModusPlanReachesTheRunningVillageLive()
        {
            _runtime.enabled = false;
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Resolve(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            Assert.IsTrue(Game.IsVillage);
            yield return null;
            Assert.AreEqual("aschengrenze", BuildLayerLive.ActiveArea, "the running village owns the Aschengrenze plan");

            var plan = BuildLayoutRules.Empty("aschengrenze");
            plan.revision = 99;
            plan.pieces.Add(new BuildPiece { id = "b0001", key = "VillageHome1", x = 2f, z = 4f, rot = 45f, scale = 1f });
            plan.pieces.Add(new BuildPiece { id = "b0002", key = "GrassVerge", x = -2f, z = -2f, rot = 0f, scale = 1f, w = 6f, d = 3f });
            BuildLayerLive.Publish("aschengrenze", plan);
            yield return null;
            Assert.AreEqual(2, BuildLayerLive.ActivePieces, "runtime readback of realised pieces");
            Assert.AreEqual(99, BuildLayerLive.ActiveRevision);
            GameObject layer = GameObject.Find(BuildLayer.RootPrefix + "aschengrenze");
            Assert.IsNotNull(layer);
            Assert.AreEqual(2, BuildLayer.CountPieces(layer.transform));
            Transform house = layer.transform.Find("Bau_b0001_VillageHome1");
            Assert.AreEqual(2f, house.position.x, 0.01f);
            Assert.AreEqual(4f, house.position.z, 0.01f);
            Assert.AreEqual(45f, house.eulerAngles.y, 0.1f);
            foreach (Collider collider in layer.GetComponentsInChildren<Collider>(true))
                Assert.IsFalse(collider.enabled, "placed scenery never collides: " + collider.name);

            var other = BuildLayoutRules.Empty("krypta");
            other.pieces.Add(new BuildPiece { id = "b0001", key = "AshCrate", x = 0f, z = 0f, scale = 1f });
            BuildLayerLive.Publish("krypta", other);
            yield return null;
            Assert.AreEqual(2, BuildLayerLive.ActivePieces, "another area's plan never leaks into the village");

            BuildLayerLive.PublishGhost("aschengrenze", new BuildPiece { key = "AshLantern", x = 0f, z = 0f, scale = 1f });
            Assert.IsTrue(BuildLayerLive.GhostVisible);
            Assert.IsNotNull(GameObject.Find(BuildLayer.GhostName));
            BuildLayerLive.PublishGhost("aschengrenze", null);
            yield return null;
            Assert.IsFalse(BuildLayerLive.GhostVisible);
            Assert.IsNull(GameObject.Find(BuildLayer.GhostName));

            Assert.IsTrue(EntryWorldLayout.CanStand(2000, 4000), "the Core walkability is unchanged by scenery");

            BuildLayerLive.Publish("aschengrenze", BuildLayoutRules.Empty("aschengrenze"));
            yield return null;
            Assert.AreEqual(0, BuildLayerLive.ActivePieces);
            Assert.IsNull(GameObject.Find(BuildLayer.RootPrefix + "aschengrenze"));
        }
    }
}
