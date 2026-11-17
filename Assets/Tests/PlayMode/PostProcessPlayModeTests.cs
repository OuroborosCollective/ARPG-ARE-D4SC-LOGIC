using System.Collections;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator ImageFinishIsOneMobileSafeVolumeAndObeysReducedEffects()
        {
            _runtime.enabled = false;
            ulong hash = Game.ComputeWorldHash();
            string save = Game.Progression.SaveString();
            var camera = (Camera)typeof(ArenaRuntime).GetField("_camera", Flags).GetValue(_runtime);
            var refresh = typeof(ArenaRuntime).GetMethod("RefreshPostProcessing", Flags);
            refresh.Invoke(_runtime, new object[] { true });
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            Assert.AreEqual(1, volumes.Length, "Exactly one image volume exists");
            var volume = volumes[0];
            Assert.AreEqual(ArenaRuntime.PostProcessVolumeName, volume.name);
            Assert.IsTrue(volume.isGlobal && volume.enabled);
            var data = camera.GetUniversalAdditionalCameraData();
            Assert.IsTrue(data.renderPostProcessing, "The gameplay camera renders the finish pass");
            Assert.AreNotEqual(0, data.volumeLayerMask & (1 << volume.gameObject.layer), "The camera sees the volume layer");
            var profile = volume.sharedProfile;
            Assert.IsTrue(profile.TryGet<Bloom>(out var bloom) && bloom.intensity.overrideState && bloom.intensity.value > 0f);
            Assert.IsFalse(bloom.highQualityFiltering.value, "Bloom stays on the mobile path");
            Assert.LessOrEqual(bloom.maxIterations.value, 3);
            Assert.IsTrue(profile.TryGet<Tonemapping>(out var tone) && tone.mode.value == TonemappingMode.Neutral);
            Assert.IsTrue(profile.Has<Vignette>() && profile.Has<ColorAdjustments>());
            Assert.IsFalse(profile.Has<DepthOfField>() || profile.Has<MotionBlur>() || profile.Has<FilmGrain>(),
                "Expensive effects stay off on mobile");
            yield return Capture("image-finish-on.png");

            var atmosphere = _runtime.GetComponentInChildren<AshenAtmosphere>();
            bool muted = atmosphere.Muted, reduced = atmosphere.ReducedEffects;
            atmosphere.SetPreferences(atmosphere.MusicVolume, atmosphere.VoiceVolume, atmosphere.EffectsVolume, muted, true);
            try
            {
                refresh.Invoke(_runtime, new object[] { false });
                Assert.IsFalse(data.renderPostProcessing, "Reduced effects switch the finish pass off");
                Assert.IsFalse(volume.enabled);
            }
            finally
            {
                atmosphere.SetPreferences(atmosphere.MusicVolume, atmosphere.VoiceVolume, atmosphere.EffectsVolume, muted, reduced);
                refresh.Invoke(_runtime, new object[] { false });
            }
            Assert.AreEqual(hash, Game.ComputeWorldHash(), "The image finish cannot alter core world state");
            Assert.AreEqual(save, Game.Progression.SaveString());
        }
    }
}
