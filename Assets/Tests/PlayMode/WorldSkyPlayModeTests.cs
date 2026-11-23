using System.Collections;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator VillageSkyProjectsCoreTicksWithoutWritingAnyRules()
        {
            _runtime.enabled = false;
            foreach (var monster in Game.Monsters)
            {
                monster.Xmm = 1000;
                monster.Zmm = 0;
                monster.Health = 1;
            }
            Resolve(3);
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(Game.TryCollectLoot(), "The ordinary crypt-to-village transition must remain playable");
            MoveStory(0, 24000);
            TravelStory();
            Assert.IsTrue(Game.IsVillage, "The sky must appear only after entering the outdoor village");
            var material = (Material)typeof(ArenaRuntime).GetField("_worldSkyMaterial", Flags).GetValue(_runtime);
            Assert.IsNotNull(material, "The imported shader must create a real render material");
            typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags).Invoke(_runtime, null);
            Assert.AreSame(material, RenderSettings.skybox);
            Assert.AreEqual(CameraClearFlags.Skybox, Camera.main.clearFlags);
            float lightAtStart = material.GetFloat("_Daylight");
            Assert.Greater(lightAtStart, .20f, "Fresh outdoor gameplay starts in daylight");
            yield return Capture("village-world-sky-day.png");

            // Test fixture moves ONLY the persisted canonical Core clock, not wall time or frames.
            var forge = Game.Progression.Social.Forge;
            Assert.IsNotNull(forge);
            var counter = typeof(NpcForgeState).GetProperty("ProcessedTicks",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(counter);
            ulong original = forge.ProcessedTicks;
            try
            {
                counter.SetValue(forge, 7200UL);
                ulong before = Game.ComputeWorldHash();
                string checkpoint = Game.Progression.SaveString();
                typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags).Invoke(_runtime, null);
                Assert.Less(material.GetFloat("_Daylight"), .05f, "Half a 24-minute day later is night");
                Assert.Greater(material.GetFloat("_StarAmount"), 0.5f);
                var nightLight = (Light)typeof(ArenaRuntime).GetField("_sun", Flags).GetValue(_runtime);
                Assert.Less(nightLight.transform.forward.y, -.1f,
                    "At night the directional proxy must illuminate from the visible moon above the ground");
                Assert.That(material.GetFloat("_CloudPhase"), Is.EqualTo(Mathf.PI).Within(.001f),
                    "Cloud motion follows the same saved tick phase as the sun");
                Assert.AreEqual(before, Game.ComputeWorldHash(), "RenderSettings cannot write authoritative rules");
                Assert.AreEqual(checkpoint, Game.Progression.SaveString(), "Rendering cannot change a saved checkpoint");
                yield return Capture("village-world-sky-night.png");
                foreach (ulong twilightTick in new[] { 3600UL, 10800UL })
                {
                    counter.SetValue(forge, twilightTick);
                    ulong twilightHash = Game.ComputeWorldHash();
                    string twilightSave = Game.Progression.SaveString();
                    typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags).Invoke(_runtime, null);
                    Assert.That(material.GetFloat("_Daylight"), Is.InRange(.05f, .95f),
                        "Morning and evening must be between full day and full night");
                    Assert.Less(nightLight.transform.forward.y, -.30f,
                        "Twilight key light needs enough elevation for readable ground and actors");
                    Assert.AreEqual(twilightHash, Game.ComputeWorldHash());
                    Assert.AreEqual(twilightSave, Game.Progression.SaveString());
                    yield return Capture(twilightTick == 3600UL
                        ? "village-world-sky-evening.png" : "village-world-sky-morning.png");
                }
            }
            finally
            {
                counter.SetValue(forge, original);
                typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags).Invoke(_runtime, null);
            }
        }

        [UnityTest]
        public IEnumerator SkyRebindsAfterSameAreaRebuildAndKeepsInteriorLight()
        {
            _runtime.enabled = false;
            foreach (var monster in Game.Monsters)
            {
                monster.Xmm = 1000; monster.Zmm = 0; monster.Health = 1;
            }
            Resolve(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            Assert.IsTrue(Game.IsVillage);
            var refresh = typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags);
            var rebuild = typeof(ArenaRuntime).GetMethod("BuildWorld", Flags);
            var sunField = typeof(ArenaRuntime).GetField("_sun", Flags);
            refresh.Invoke(_runtime, null);
            var previousSun = (Light)sunField.GetValue(_runtime);
            ulong hash = Game.ComputeWorldHash();
            string save = Game.Progression.SaveString();
            rebuild.Invoke(_runtime, null);
            refresh.Invoke(_runtime, null);
            var replacementSun = (Light)sunField.GetValue(_runtime);
            Assert.AreNotSame(previousSun, replacementSun);
            Assert.AreSame(replacementSun, RenderSettings.sun,
                "Rebuilding at the same saved tick must bind the new directional light");
            Assert.AreEqual(hash, Game.ComputeWorldHash());
            Assert.AreEqual(save, Game.Progression.SaveString());

            // Select an interior only inside this fixture; rendering remains a consumer of Core state.
            typeof(CharacterProgression).GetProperty("EntryAct").SetValue(Game.Progression, 4);
            Assert.AreEqual(4, Game.CurrentAreaId);
            rebuild.Invoke(_runtime, null);
            Color authoredFog = RenderSettings.fogColor;
            Color authoredAmbient = RenderSettings.ambientLight;
            refresh.Invoke(_runtime, null);
            Assert.AreSame((Light)sunField.GetValue(_runtime), RenderSettings.sun,
                "An interior must retain its new authored directional light");
            Assert.AreEqual(authoredFog, RenderSettings.fogColor);
            Assert.AreEqual(authoredAmbient, RenderSettings.ambientLight);
            Assert.AreNotEqual(CameraClearFlags.Skybox, Camera.main.clearFlags);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkyDisposalRestoresLightingOutsideShader()
        {
            _runtime.enabled = false;
            var sun = (Light)typeof(ArenaRuntime).GetField("_sun", Flags).GetValue(_runtime);
            Assert.IsNotNull(sun);
            Color ambientBefore = RenderSettings.ambientLight;
            var ambientProbeBefore = RenderSettings.ambientProbe;
            Color fogBefore = RenderSettings.fogColor;
            float fogDensityBefore = RenderSettings.fogDensity;
            var fogModeBefore = RenderSettings.fogMode;
            var ambientModeBefore = RenderSettings.ambientMode;
            Color sunColorBefore = sun.color;
            float sunIntensityBefore = sun.intensity;

            foreach (var monster in Game.Monsters)
            {
                monster.Xmm = 1000;
                monster.Zmm = 0;
                monster.Health = 1;
            }
            Resolve(3);
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000);
            TravelStory();
            Assert.IsTrue(Game.IsVillage);
            typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags).Invoke(_runtime, null);
            Assert.Greater(Vector4.Distance(RenderSettings.ambientLight, ambientBefore), 0.01f,
                "Outdoor rendering must demonstrably override indoor ambient");
            typeof(ArenaRuntime).GetMethod("DisposeWorldSky", Flags).Invoke(_runtime, null);
            Assert.Less(Vector4.Distance(RenderSettings.ambientLight, ambientBefore), .0001f);
            Assert.Less(Vector4.Distance(RenderSettings.fogColor, fogBefore), .0001f);
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(fogDensityBefore).Within(.00001f));
            Assert.AreEqual(fogModeBefore, RenderSettings.fogMode);
            Assert.AreEqual(ambientModeBefore, RenderSettings.ambientMode);
            Assert.AreEqual(ambientProbeBefore, RenderSettings.ambientProbe, "Disposal must restore the ambient probe too");
            Assert.Less(Vector4.Distance(sun.color, sunColorBefore), .0001f);
            Assert.That(sun.intensity, Is.EqualTo(sunIntensityBefore).Within(.0001f));
            yield return null;
        }

    }
}
