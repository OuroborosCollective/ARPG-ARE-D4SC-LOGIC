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
        public IEnumerator VillageTorchesFollowTheSavedDuskOnAMobileLightBudget()
        {
            _runtime.enabled = false;
            foreach (var monster in Game.Monsters) { monster.Xmm = 1000; monster.Zmm = 0; monster.Health = 1; }
            Resolve(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000);
            TravelStory();
            Assert.IsTrue(Game.IsVillage);
            var refresh = typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags);
            refresh.Invoke(_runtime, null);
            var root = GameObject.Find(ArenaRuntime.VillageLightRootName);
            Assert.IsNotNull(root, "The village owns its torch islands");
            var lights = root.GetComponentsInChildren<Light>(true);
            Assert.AreEqual(ArenaRuntime.VillageLightBudget, lights.Length, "Exactly the budgeted torch lights exist");
            foreach (var light in lights)
            {
                Assert.IsFalse(light.enabled, "Torches are unlit in daylight");
                Assert.AreEqual(LightShadows.None, light.shadows, "Torch lights never cast mobile shadows");
                Assert.AreEqual(LightType.Point, light.type);
            }
            foreach (var part in root.GetComponentsInChildren<Collider>(true)) Assert.Fail("Torch art must stay passive: " + part.name);
            yield return Capture("village-light-islands-day.png");

            var forge = Game.Progression.Social.Forge;
            Assert.IsNotNull(forge);
            var counter = typeof(NpcForgeState).GetProperty("ProcessedTicks",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            ulong original = forge.ProcessedTicks;
            try
            {
                counter.SetValue(forge, 7200UL);
                ulong hash = Game.ComputeWorldHash();
                string save = Game.Progression.SaveString();
                refresh.Invoke(_runtime, null);
                int lit = 0;
                foreach (var light in lights)
                    if (light.enabled) { lit++; Assert.Greater(light.intensity, 1.5f, "Night torches throw a readable warm pool"); }
                Assert.AreEqual(ArenaRuntime.VillageLightBudget, lit, "All budgeted torches burn at night");
                int flames = 0;
                foreach (var flame in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (flame.name.StartsWith("Torch flame") && flame.enabled) flames++;
                Assert.AreEqual(ArenaRuntime.VillageLightBudget, flames, "Every torch shows a flame at night");
                Assert.AreEqual(hash, Game.ComputeWorldHash(), "Torches cannot alter core world state");
                Assert.AreEqual(save, Game.Progression.SaveString(), "Torches cannot alter the saved timeline");
                yield return Capture("village-light-islands-night.png");

                counter.SetValue(forge, 3600UL);
                refresh.Invoke(_runtime, null);
                yield return Capture("village-light-islands-dusk.png");

                // The reduced-effects preference halves the dynamic light cost.
                var atmosphere = _runtime.GetComponentInChildren<AshenAtmosphere>();
                bool muted = atmosphere.Muted, reduced = atmosphere.ReducedEffects;
                atmosphere.SetPreferences(atmosphere.MusicVolume, atmosphere.VoiceVolume, atmosphere.EffectsVolume, muted, true);
                try
                {
                    counter.SetValue(forge, 7200UL);
                    refresh.Invoke(_runtime, null);
                    lit = 0;
                    foreach (var light in lights) if (light.enabled) lit++;
                    Assert.AreEqual(ArenaRuntime.VillageLightReducedBudget, lit, "Reduced effects keep only the gate pair lit");
                }
                finally { atmosphere.SetPreferences(atmosphere.MusicVolume, atmosphere.VoiceVolume, atmosphere.EffectsVolume, muted, reduced); }
            }
            finally
            {
                counter.SetValue(forge, original);
                refresh.Invoke(_runtime, null);
            }
        }
    }
}
