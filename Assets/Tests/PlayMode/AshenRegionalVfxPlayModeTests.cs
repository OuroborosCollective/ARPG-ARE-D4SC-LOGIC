using System.Collections;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator ForestMillCryptAndEmberRiftReuseBoundedNativeParticles()
        {
            _runtime.enabled = false;
            var audio = _runtime.GetComponentInChildren<AshenAtmosphere>();
            Assert.IsNotNull(audio);
            var ash = audio.transform.Find("Falling ash").GetComponent<ParticleSystem>();
            var fog = audio.transform.Find("Low crypt mist").GetComponent<ParticleSystem>();
            var sparks = audio.transform.Find("Confirmed hit motes").GetComponent<ParticleSystem>();
            ulong hash = Game.ComputeWorldHash();
            string save = Game.Progression.SaveString();
            int oldRegion = Game.CurrentAreaId;
            int initialEmitterCount = audio.GetComponentsInChildren<ParticleSystem>().Length;
            try
            {
                Assert.IsTrue(ash.colorOverLifetime.enabled && fog.colorOverLifetime.enabled && sparks.colorOverLifetime.enabled,
                    "Atmosphere particles fade in and out instead of popping");
                Assert.AreEqual(0f, fog.colorOverLifetime.color.gradient.Evaluate(0f).a, 1e-4f, "Mist is born invisible");
                Assert.AreEqual(0f, fog.colorOverLifetime.color.gradient.Evaluate(1f).a, 1e-4f, "Mist dies invisible");
                Assert.IsTrue(ash.noise.enabled && fog.noise.enabled, "Ash flutters and mist curls");
                Assert.AreEqual(ParticleSystemNoiseQuality.Low, ash.noise.quality, "Turbulence stays on the mobile budget");
                audio.EnterRegion(11);
                Assert.Greater(audio.AshEmitterHeight, 3f, "Falling ash is born above the hero");
                Assert.IsTrue(ash.isPlaying, "Forest retains sparse falling ash");
                Assert.IsTrue(fog.isPlaying, "Forest needs distinct cold forest mist");
                Color forestFog = fog.main.startColor.color;
                audio.EnterRegion(4);
                Assert.IsTrue(ash.isPlaying);
                Assert.IsTrue(fog.isPlaying, "Old mill needs damp low-lying mist");
                Assert.AreNotEqual(forestFog, fog.main.startColor.color);
                Color millFog = fog.main.startColor.color;
                // Owner brief 2026-11-24: fog lies on the ground of the village and the road, in wider and denser banks than anywhere else.
                foreach (int openGround in new[] { 2, 3 })
                {
                    audio.EnterRegion(openGround);
                    Assert.IsTrue(ash.isPlaying && fog.isPlaying, "Village and road keep the ash snow and get ground fog");
                    Assert.Greater(fog.main.startColor.color.a, millFog.a, "The ground fog is denser than the mill's damp mist");
                    Assert.IsTrue(fog.main.startSize3D, "Ground fog lies in banks, not in round puffs");
                    Assert.Greater(fog.main.startSizeX.constantMin, 3f, "Ground fog lies in wide banks");
                    Assert.Less(fog.main.startSizeY.constantMax, 2f, "Ground fog stays low");
                    Assert.Greater(fog.main.startColor.color.b, fog.main.startColor.color.r, "Ground fog is cold, lit by the moon");
                    Assert.LessOrEqual(fog.main.maxParticles, 24, "Ground fog stays on the mist budget");
                }
                audio.EnterRegion(6);
                Assert.IsFalse(ash.isPlaying, "Interior crypt must not rain outdoor ash");
                Assert.IsTrue(fog.isPlaying);
                audio.EnterRegion(12);
                Assert.IsTrue(ash.isPlaying, "Ember rift uses existing ash emitter for embers");
                Assert.IsFalse(fog.isPlaying);
                var block = new MaterialPropertyBlock();
                ash.GetComponent<ParticleSystemRenderer>().GetPropertyBlock(block);
                Assert.Greater(block.GetFloat("_Glow"), .5f, "Rift ember color needs emissive lift");
                Assert.Greater(ash.velocityOverLifetime.y.constant, 0f, "Rift embers rise instead of falling");
                Assert.IsTrue(ash.sizeOverLifetime.enabled, "Embers shrink while they cool");
                Assert.Less(audio.AshEmitterHeight, 1f, "Rising embers are born at the ground, inside the view");
                Assert.Less(ash.main.startSize.constantMin, ash.main.startSize.constantMax, "Embers vary in size");
                Assert.LessOrEqual(ash.main.maxParticles, 160);
                Assert.LessOrEqual(fog.main.maxParticles, 24);
                Assert.LessOrEqual(sparks.main.maxParticles, 30);
                Assert.AreEqual(initialEmitterCount, audio.GetComponentsInChildren<ParticleSystem>().Length,
                    "Regional changes never spawn new particle systems");
                Assert.AreEqual(hash, Game.ComputeWorldHash(), "Particles cannot alter core world state");
                Assert.AreEqual(save, Game.Progression.SaveString(), "Particles cannot alter the saved timeline");
            }
            finally { audio.EnterRegion(oldRegion); }
            yield return null;
        }
    }
}
