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
        public IEnumerator VillageHeroArrivalKeepsCoreAndShowsEightPassiveVikingGroups()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private village overlay and real Unity camera");
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
            var hero = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .SingleOrDefault(t => t.name == "VillageHeroPass1");
            Assert.IsNotNull(hero, "Real village art must include native-composed hero groups");
            Assert.AreEqual(8, hero.childCount);
            Assert.IsEmpty(hero.GetComponentsInChildren<Collider>(true),
                "Decorative Viking props cannot change canonical Core walkability");
            Assert.IsEmpty(hero.GetComponentsInChildren<MonoBehaviour>(true),
                "Decorative art must not install a second gameplay authority");
            foreach (Transform piece in hero)
            {
                Assert.IsNotEmpty(piece.GetComponentsInChildren<MeshRenderer>(true),
                    "Hero group must be visibly rendered: " + piece.name);
            }
            ulong worldHashBefore = Game.ComputeWorldHash();
            string saveBefore = Game.Progression.SaveString();
            yield return Capture("village-hero-pass1-day.png");
            // Independent native camera proof: focus the actual imported gate without
            // changing the Fable gameplay camera contract or any Core world state.
            Transform gate = hero.Find("north-gate");
            Assert.IsNotNull(gate);
            MeshRenderer gateRenderer = gate.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(gateRenderer);
            Bounds gateBounds = gateRenderer.bounds;
            Debug.Log("ARE_NATIVE_GATE_MEASURE bounds=" + gateBounds.size.ToString("F3") +
                " center=" + gateBounds.center.ToString("F3") +
                " active=" + gate.gameObject.activeInHierarchy +
                " enabled=" + gateRenderer.enabled +
                " shader=" + gateRenderer.sharedMaterial.shader.name);
            Assert.IsTrue(gate.gameObject.activeInHierarchy, "The gate must be active, not just serialized");
            Assert.IsTrue(gateRenderer.enabled, "Gate renderer must actually be enabled");
            Assert.Greater(gateBounds.size.x, 2f, "An arrival gate should exceed two metres in visible width");
            Assert.Greater(gateBounds.size.y, 1.5f, "An arrival gate should be recognizably upright");
            Camera camera = (Camera)typeof(AreArpg.UnityClient.ArenaRuntime).GetField("_camera", Flags).GetValue(_runtime);
            Vector3 previousCameraPosition = camera.transform.position;
            Quaternion previousCameraRotation = camera.transform.rotation;
            try
            {
                camera.transform.position = gateBounds.center + new Vector3(0f, 4.2f, -19f);
                camera.transform.LookAt(gateBounds.center);
                Vector3 screen = camera.WorldToViewportPoint(gateBounds.center);
                Assert.Greater(screen.z, 0f);
                Assert.That(screen.x, Is.InRange(0.4f, 0.6f));
                Assert.That(screen.y, Is.InRange(0.3f, 0.7f));
                yield return Capture("village-hero-pass1-gate.png",
                    overridePosition: gateBounds.center + new Vector3(0f, 4.2f, -19f),
                    overrideFocus: gateBounds.center);
            }
            finally
            {
                camera.transform.position = previousCameraPosition;
                camera.transform.rotation = previousCameraRotation;
            }
            // A camera position assertion alone is not image evidence. Detect a
            // rig reset between coroutine frames by comparing rendered pixels.
            string dir = System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            var dayPixels = new Texture2D(2, 2, TextureFormat.RGB24, false);
            var gatePixels = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                Assert.IsTrue(dayPixels.LoadImage(System.IO.File.ReadAllBytes(
                    System.IO.Path.Combine(dir, "village-hero-pass1-day.png"))));
                Assert.IsTrue(gatePixels.LoadImage(System.IO.File.ReadAllBytes(
                    System.IO.Path.Combine(dir, "village-hero-pass1-gate.png"))));
                Assert.AreEqual(dayPixels.width, gatePixels.width);
                Assert.AreEqual(dayPixels.height, gatePixels.height);
                var one = dayPixels.GetPixels32();
                var two = gatePixels.GetPixels32();
                int visiblyDifferent = 0;
                for (int i = 0; i < one.Length; i += 128)
                {
                    int distance = System.Math.Abs((int)one[i].r - two[i].r) +
                        System.Math.Abs((int)one[i].g - two[i].g) +
                        System.Math.Abs((int)one[i].b - two[i].b);
                    if (distance > 80) visiblyDifferent++;
                }
                Assert.Greater(visiblyDifferent, 100,
                    "Dedicated gate capture must meaningfully differ from default camera image");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dayPixels);
                UnityEngine.Object.DestroyImmediate(gatePixels);
            }
            Assert.AreEqual(worldHashBefore, Game.ComputeWorldHash(),
                "Camera and passive art cannot alter the deterministic simulation");
            Assert.AreEqual(saveBefore, Game.Progression.SaveString(),
                "Camera and passive art cannot write a save");
        }
    }
}
