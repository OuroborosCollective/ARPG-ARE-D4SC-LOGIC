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
        public IEnumerator VillageApproachShowsNativeRoadFeatherWithoutChangingWorldOrSave()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires private native village art and real GameView capture");
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

            var finish = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .SingleOrDefault(t => t.name == "VillageApproachPass1");
            Assert.IsNotNull(finish, "Real active village must load new earth-to-stone finish");
            Assert.IsTrue(finish.gameObject.activeInHierarchy);
            Assert.IsEmpty(finish.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(finish.GetComponentsInChildren<MonoBehaviour>(true));
            var mesh = finish.GetComponentInChildren<MeshFilter>(true);
            var renderer = finish.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(mesh);
            Assert.IsNotNull(renderer);
            Assert.IsTrue(renderer.enabled);
            Assert.IsNotNull(mesh.sharedMesh);
            Assert.AreEqual("ARE/VisualOnly/VillageEarthFeather", renderer.sharedMaterial.shader.name);

            var earth = Find(AreArpg.UnityClient.AschengrenzeArt.BackdropLabel).transform
                .Find("Art_EntryVillageWorld/Ground").GetComponent<MeshRenderer>();
            Assert.AreEqual("ARE/World/Schneeasche", earth.sharedMaterial.shader.name,
                "The occupied village square must share the outdoor ash treatment.");
            Assert.IsTrue(earth.sharedMaterial.IsKeywordEnabled("ARE_VILLAGE_EARTH"));
            var original = Resources.Load<GameObject>("ARE/Visuals/EntryVillageWorld")
                .transform.Find("Ground").GetComponent<MeshRenderer>().sharedMaterial;
            var bindings = new MaterialPropertyBlock();
            earth.GetPropertyBlock(bindings);
            Assert.AreSame(original.GetTexture("_BaseMap"), bindings.GetTexture("_VillageEarth"));
            Vector2 scale = original.GetTextureScale("_BaseMap");
            Vector2 offset = original.GetTextureOffset("_BaseMap");
            Assert.AreEqual(new Vector4(scale.x, scale.y, offset.x, offset.y),
                bindings.GetVector("_VillageEarth_ST"));

            ulong hashBefore = Game.ComputeWorldHash();
            string saveBefore = Game.Progression.SaveString();
            yield return Capture("village-approach-day.png");
            Assert.AreEqual(hashBefore, Game.ComputeWorldHash(),
                "Visual feather may not affect authoritative 100-ms simulation");
            Assert.AreEqual(saveBefore, Game.Progression.SaveString(),
                "Visual feather may not change persisted progression");
        }
    }
}
