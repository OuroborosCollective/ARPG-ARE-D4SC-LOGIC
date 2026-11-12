using System.Collections;
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
        public IEnumerator StorySpeechPlaysOnlyAfterAcceptedContactAndStopsOnTravel()
        {
            _runtime.enabled = false;
            var audio = _runtime.GetComponentInChildren<AshenAtmosphere>();
            Assert.IsTrue(audio.HasMedia, "The reviewed audio library must actually be imported.");
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            var rejected = Game.ActOnHunger(HungerAction.HearIlyan);
            audio.AcceptedSpeech(rejected); Assert.AreEqual(0, audio.SpeechCount);
            var ilyan = Game.Npcs.Find(n => n.Id == 0); ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan);
            var voice = Find("NPC speech").GetComponent<AudioSource>();
            Assert.IsNotNull(voice.clip); Assert.Greater(voice.clip.length, 2);
            Assert.IsTrue(voice.isPlaying, "Native Unity must start the actual speech clip.");
            Assert.AreEqual("voice_0_8", voice.clip.name); StringAssert.Contains("Aschestraße", audio.Subtitle);
            int before = audio.SpeechCount;
            yield return StoryAction(HungerAction.HearIlyan);
            Assert.AreEqual(before, audio.SpeechCount, "Repeated rejected story action cannot replay speech.");
            ulong hash = Game.ComputeWorldHash(); string save = Game.Progression.SaveString();
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(hash, Game.ComputeWorldHash()); Assert.AreEqual(save, Game.Progression.SaveString());
            MoveStory(0, 24000); TravelStory(); yield return null;
            Assert.IsFalse(voice.isPlaying); Assert.IsEmpty(audio.Subtitle);
            yield return new WaitForSeconds(2f);
            int playingMusic = 0;
            foreach (var source in audio.GetComponentsInChildren<AudioSource>()) if (source.loop && source.name.StartsWith("Music") && source.isPlaying) playingMusic++;
            Assert.AreEqual(1, playingMusic, "Crossfade must retire the previous music source.");
            Assert.AreEqual(11, audio.GetComponentsInChildren<AudioSource>().Length);
            yield return Capture("atmosphere-ash-road.png");
        }

        [UnityTest]
        public IEnumerator MovementAndCombatUseRealMediaWithoutAllocatingSources()
        {
            _runtime.enabled = false;
            var audio = _runtime.GetComponentInChildren<AshenAtmosphere>();
            var library = Resources.Load<AtmosphereLibrary>("AshenAtmosphere"); Assert.IsNotNull(library);
            foreach (string key in new[] { "music_road", "music_crypt", "ambient_wind", "ambient_village", "ambient_mill", "water_mill", "swing", "impact", "hurt", "spell",
                "step_sand_0", "step_sand_1", "step_grass_0", "step_grass_1", "step_wood_0", "step_wood_1", "step_stone_0", "step_stone_1", "step_water_0", "step_water_1" })
                Assert.IsNotNull(library.Find(key), "Missing actual media: " + key);
            int before = audio.FootstepCount;
            Game.SetMoveInput(0, 0);
            for (int i = 0; i < 4; i++) { long x = Game.PlayerXmm, z = Game.PlayerZmm; int hp = Game.PlayerHealth; Game.Step(); audio.ObserveTick(Game, x, z, hp); }
            Assert.AreEqual(before, audio.FootstepCount, "Standing still must be silent.");
            Game.SetMoveInput(10000, 0);
            for (int i = 0; i < 8; i++) { long x = Game.PlayerXmm, z = Game.PlayerZmm; int hp = Game.PlayerHealth; Game.Step(); audio.ObserveTick(Game, x, z, hp); }
            Assert.Greater(audio.FootstepCount, before); StringAssert.StartsWith("step_stone", audio.LastEffectKey);
            Game.SetMoveInput(0, 0);
            var skill = Game.ActivateSkill(0); Assert.IsTrue(skill.Activated); audio.Skill(skill, 0, Game);
            Assert.AreEqual(skill.Hits > 0 ? "impact" : "swing", audio.LastEffectKey);
            for (int i = 0; i < 30; i++) audio.Effect("impact", Vector3.zero, .5f);
            Assert.AreEqual(11, audio.GetComponentsInChildren<AudioSource>().Length, "Effect pool must stay bounded.");
            Assert.AreEqual(GroundSurface.Grass, AtmosphereSurfaces.At(3, 8000, 0));
            Assert.AreEqual(GroundSurface.Sand, AtmosphereSurfaces.At(3, 4000, 0));
            Assert.AreEqual(GroundSurface.Wood, AtmosphereSurfaces.At(4, -4000, -2000));
            Assert.AreEqual(GroundSurface.Water, AtmosphereSurfaces.At(4, -5000, 8000));
            foreach (var p in audio.GetComponentsInChildren<ParticleSystem>())
            { Assert.LessOrEqual(p.main.maxParticles, 160); Assert.IsTrue(p.GetComponent<Renderer>().sharedMaterial.shader.isSupported); }
            yield return new WaitForSeconds(.2f);
            audio.Suspend(true);
            foreach (var source in audio.GetComponentsInChildren<AudioSource>()) Assert.IsFalse(source.isPlaying);
            audio.Suspend(false);
            yield return Capture("atmosphere-crypt-mist.png");
        }

        [UnityTest]
        public IEnumerator NewlyAcceptedDonationsEachReplaceTheActiveResponse()
        {
            _runtime.enabled = false;
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3); for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            var audio = _runtime.GetComponentInChildren<AshenAtmosphere>();
            for (int i = 0; i < 2; i++)
            {
                var mara = Game.Npcs.Find(n => n.Id == 1); mara.Xmm = Game.PlayerXmm + 1000; mara.Zmm = Game.PlayerZmm;
                typeof(ArenaRuntime).GetField("_pendingCampDonation", Flags).SetValue(_runtime, true); TickOnce();
                Assert.AreEqual(9 + i, Game.Progression.Social.CampProvisions, "Each transaction must actually be accepted.");
                Assert.AreEqual(i + 1, audio.SpeechCount, "Newly accepted help must replace, not suppress, the response.");
                for (int tick = 0; tick < 10; tick++) TickOnce();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AcceptedSpeechWaitsForAppResume()
        {
            _runtime.enabled = false;
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3); for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            var audio = _runtime.GetComponentInChildren<AshenAtmosphere>();
            var ilyan = Game.Npcs.Find(n => n.Id == 0); ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            audio.Suspend(true);
            yield return StoryAction(HungerAction.HearIlyan);
            Assert.AreEqual(0, audio.SpeechCount, "An accepted line must wait until the app resumes.");
            Assert.IsEmpty(audio.Subtitle);
            foreach (var source in audio.GetComponentsInChildren<AudioSource>()) Assert.IsFalse(source.isPlaying);
            audio.Suspend(false); yield return null;
            Assert.AreEqual(1, audio.SpeechCount); StringAssert.Contains("Aschestraße", audio.Subtitle);
            Assert.IsTrue(Find("NPC speech").GetComponent<AudioSource>().isPlaying);
        }

        [UnityTest]
        public IEnumerator SubtitlesRemainClearOfAvailableMasteryControls()
        {
            foreach (int width in new[] { 960, 1280 })
            {
                var layout = new ArenaHudLayout(width, 540, new Rect(0, 0, width, 540));
                var rect = ArenaRuntime.SpeechSubtitleRect(layout, false, true);
                for (int i = 0; i < 3; i++) Assert.IsFalse(rect.Overlaps(layout.MasteryRect(i)), "Subtitle obscures mastery at width " + width);
                Assert.IsFalse(rect.Overlaps(layout.PickupRect));
                Assert.IsFalse(rect.Overlaps(layout.CampPanelRect));
                Assert.IsFalse(rect.Overlaps(layout.NpcPanelRect));
                for (int i = 0; i < 4; i++) Assert.IsFalse(rect.Overlaps(layout.SkillRect(i)));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConfirmedHitMotesContinueAgingAfterAppResume()
        {
            _runtime.enabled = false;
            var audio = _runtime.GetComponentInChildren<AshenAtmosphere>();
            Game.Monsters[0].Xmm = Game.PlayerXmm + 1000; Game.Monsters[0].Zmm = Game.PlayerZmm;
            var hit = Game.ActivateSkill(0); Assert.Greater(hit.Hits, 0); audio.Skill(hit, 0, Game);
            var sparks = Find("Confirmed hit motes").GetComponent<ParticleSystem>();
            yield return null;
            var particles = new ParticleSystem.Particle[30]; Assert.Greater(sparks.GetParticles(particles), 0);
            audio.Suspend(true); float before = particles[0].remainingLifetime;
            audio.Suspend(false); yield return new WaitForSeconds(.15f);
            int count = sparks.GetParticles(particles);
            Assert.IsTrue(count == 0 || particles[0].remainingLifetime < before - .05f, "Paused hit particles must resume real simulation.");
        }

        [UnityTest]
        public IEnumerator AudioPreferencesAndReducedParticlesRemainPresentationOnly()
        {
            _runtime.enabled = false;
            var audio = _runtime.GetComponentInChildren<AshenAtmosphere>();
            string[] suffixes = { "music", "voice", "effects", "mute", "reduced" };
            bool[] had = new bool[5]; float[] values = new float[5];
            for (int i = 0; i < 5; i++) { string key = AshenAtmosphere.PreferencePrefix + suffixes[i]; had[i] = PlayerPrefs.HasKey(key); values[i] = i < 3 ? PlayerPrefs.GetFloat(key) : PlayerPrefs.GetInt(key); }
            try
            {
                ulong hash = Game.ComputeWorldHash(); string save = Game.Progression.SaveString();
                audio.SetPreferences(.25f, .5f, .75f, true, true); yield return null;
                Assert.AreEqual(.5f, PlayerPrefs.GetFloat(AshenAtmosphere.PreferencePrefix + "voice"));
                Assert.AreEqual(1, PlayerPrefs.GetInt(AshenAtmosphere.PreferencePrefix + "mute"));
                foreach (var source in audio.GetComponentsInChildren<AudioSource>()) Assert.IsTrue(source.mute);
                Assert.AreEqual(40, Find("Falling ash").GetComponent<ParticleSystem>().main.maxParticles);
                Assert.AreEqual(6, Find("Low crypt mist").GetComponent<ParticleSystem>().main.maxParticles);
                Assert.AreEqual(hash, Game.ComputeWorldHash()); Assert.AreEqual(save, Game.Progression.SaveString());
                var layout = new ArenaHudLayout(960, 540, new Rect(0, 0, 960, 540));
                Assert.IsFalse(ArenaRuntime.AudioToggleRect(layout).Overlaps(layout.CameraPanelRect));
                Assert.IsFalse(ArenaRuntime.AudioToggleRect(layout).Overlaps(layout.NpcPanelRect));
            }
            finally
            {
                for (int i = 0; i < 5; i++)
                { string key = AshenAtmosphere.PreferencePrefix + suffixes[i]; if (!had[i]) PlayerPrefs.DeleteKey(key); else if (i < 3) PlayerPrefs.SetFloat(key, values[i]); else PlayerPrefs.SetInt(key, (int)values[i]); }
                PlayerPrefs.Save();
            }
        }
    }
}
