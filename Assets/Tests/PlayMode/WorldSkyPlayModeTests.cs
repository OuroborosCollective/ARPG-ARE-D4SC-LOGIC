using System.Collections;
using System.Linq;
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
        /// <summary>
        /// Owner decision 2026-11-24: the story plays in the night of the world. The saved Core tick no longer brings a
        /// day; it only moves the moon and the clouds. The moon never sets, and a light goes with the wanderer.
        /// </summary>
        [UnityTest]
        public IEnumerator VillageSkyStaysNightAndOnlyTheMoonFollowsTheCoreTicks()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000);
            TravelStory();
            Assert.IsTrue(Game.IsVillage, "The sky must appear only after entering the outdoor village");
            var material = (Material)typeof(ArenaRuntime).GetField("_worldSkyMaterial", Flags).GetValue(_runtime);
            Assert.IsNotNull(material, "The imported shader must create a real render material");
            var refresh = typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags);
            refresh.Invoke(_runtime, null);
            Assert.AreSame(material, RenderSettings.skybox);
            Assert.AreEqual(CameraClearFlags.Skybox, Camera.main.clearFlags);
            var moonLight = (Light)typeof(ArenaRuntime).GetField("_sun", Flags).GetValue(_runtime);

            // Test fixture moves ONLY the persisted canonical Core clock, not wall time or frames.
            var forge = Game.Progression.Social.Forge;
            Assert.IsNotNull(forge);
            var counter = typeof(NpcForgeState).GetProperty("ProcessedTicks",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(counter);
            ulong original = forge.ProcessedTicks;
            try
            {
                Vector3 last = Vector3.up; // no moon shines upwards: the first hour differs from it too
                foreach (ulong hour in new[] { 0UL, 3600UL, 7200UL, 10800UL })
                {
                    counter.SetValue(forge, hour);
                    ulong before = Game.ComputeWorldHash();
                    string checkpoint = Game.Progression.SaveString();
                    refresh.Invoke(_runtime, null);
                    string label = "Core tick " + hour + ": ";
                    Assert.AreEqual(0f, material.GetFloat("_Daylight"), 1e-4f, label + "no hour of the Core day brings daylight");
                    Assert.Greater(material.GetFloat("_StarAmount"), 0.5f, label + "the stars stay");
                    Assert.AreEqual(ArenaRuntime.MoonIntensity, moonLight.intensity, 1e-4f, label + "the moon is the one key light");
                    Assert.Greater(moonLight.color.b, moonLight.color.r, label + "moonlight is cold");
                    Assert.Less(moonLight.transform.forward.y, -.6f, label + "the moon never sets: it lights the ground from high above");
                    Vector4 sun = material.GetVector("_SunDirection");
                    Assert.Less(Vector3.Angle(-(Vector3)sun, -moonLight.transform.forward), .5f, label + "the light comes from the moon the sky shows");
                    Assert.Greater(Vector3.Angle(last, moonLight.transform.forward), 20f, label + "the moon wanders with the Core clock");
                    last = moonLight.transform.forward;
                    Assert.That(material.GetFloat("_CloudPhase"), Is.EqualTo(hour / (float)WorldSkyClock.TicksPerDay * Mathf.PI * 2f).Within(.001f),
                        label + "cloud motion follows the saved tick phase");
                    Assert.That(Shader.GetGlobalFloat("_AREEmberGain"), Is.EqualTo(1.50f).Within(.01f), label + "embers glow as at night");
                    Assert.Less(RenderSettings.ambientLight.maxColorComponent, .45f, label + "the air stays dark");
                    Assert.AreEqual(before, Game.ComputeWorldHash(), "RenderSettings cannot write authoritative rules");
                    Assert.AreEqual(checkpoint, Game.Progression.SaveString(), "Rendering cannot change a saved checkpoint");
                    yield return Capture("village-world-sky-hour" + hour / 600UL + ".png");
                }

                // A light goes with the wanderer under the night sky: one point light without shadows, over his head.
                var heroLight = GameObject.Find(ArenaRuntime.HeroLightName)?.GetComponent<Light>();
                Assert.IsNotNull(heroLight, "The wanderer carries a light under the night sky");
                Assert.IsTrue(heroLight.enabled && heroLight.type == LightType.Point && heroLight.shadows == LightShadows.None);
                Assert.Greater(heroLight.color.r, heroLight.color.b, "A warm light against the cold moon");
                Vector3 offset = heroLight.transform.position - Find("Wanderer").transform.position;
                Assert.That(offset.y, Is.InRange(.3f, 1.5f), "The light hangs at the wanderer's shoulder");
                Assert.Less(new Vector2(offset.x, offset.z).magnitude, 1.6f, "The light stays with the wanderer");
                Vector3 toCamera = Camera.main.transform.position - Find("Wanderer").transform.position; toCamera.y = 0f;
                Assert.Greater(Vector3.Dot(new Vector3(offset.x, 0f, offset.z), toCamera.normalized), .5f, "The light stands on the side the player sees");
                Assert.AreEqual(1, Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(light => light.name == ArenaRuntime.HeroLightName),
                    "Refreshing the sky never adds a second hero light");
            }
            finally
            {
                counter.SetValue(forge, original);
                refresh.Invoke(_runtime, null);
            }
        }

        [UnityTest]
        public IEnumerator SkyRebindsAfterSameAreaRebuildAndKeepsInteriorLight()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
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
            typeof(CharacterProgression).GetProperty("EntryAct").SetValue(Game.Progression, 5);
            Assert.AreEqual(5, Game.CurrentAreaId, "The old storehouse is an interior; the mill lies under the open night sky");
            rebuild.Invoke(_runtime, null);
            Color authoredFog = RenderSettings.fogColor;
            Color authoredAmbient = RenderSettings.ambientLight;
            refresh.Invoke(_runtime, null);
            Assert.AreSame((Light)sunField.GetValue(_runtime), RenderSettings.sun,
                "An interior must retain its new authored directional light");
            Assert.AreEqual(authoredFog, RenderSettings.fogColor);
            Assert.AreEqual(authoredAmbient, RenderSettings.ambientLight);
            Assert.AreNotEqual(CameraClearFlags.Skybox, Camera.main.clearFlags);
            var heroLight = GameObject.Find(ArenaRuntime.HeroLightName)?.GetComponent<Light>();
            Assert.IsTrue(heroLight == null || !heroLight.enabled, "A place with its own authored light needs no light around the wanderer");
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

            ArrangeCryptKill();
            CollectCryptLoot();
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
