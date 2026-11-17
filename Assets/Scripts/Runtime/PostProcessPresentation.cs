using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// One global URP volume for the final image: neutral tonemapping, a restrained bloom so flames, embers and
    /// legendary auras glow, a light vignette and a small contrast lift. Presentation only. The reduced-effects
    /// preference switches the whole pass off; depth of field, motion blur and film grain stay off for mobile.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string PostProcessVolumeName = "ARE_Image_Finish";
        private Volume _imageVolume;
        private VolumeProfile _imageProfile;
        private bool _imageFinishOn;

        private void InitializePostProcessing()
        {
            if (_camera == null || UniversalRenderPipeline.asset == null) return;
            _imageProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _imageProfile.name = "ARE image finish (presentation only)";

            var tone = _imageProfile.Add<Tonemapping>();
            tone.mode.overrideState = true; tone.mode.value = TonemappingMode.Neutral;

            var bloom = _imageProfile.Add<Bloom>();
            bloom.threshold.overrideState = true; bloom.threshold.value = UniversalRenderPipeline.asset.supportsHDR ? 1.0f : .85f;
            bloom.intensity.overrideState = true; bloom.intensity.value = .55f;
            bloom.scatter.overrideState = true; bloom.scatter.value = .62f;
            bloom.tint.overrideState = true; bloom.tint.value = new Color(1f, .93f, .84f);
            bloom.highQualityFiltering.overrideState = true; bloom.highQualityFiltering.value = false;
            bloom.maxIterations.overrideState = true; bloom.maxIterations.value = 3;
            bloom.downscale.overrideState = true; bloom.downscale.value = BloomDownscaleMode.Quarter;

            var colour = _imageProfile.Add<ColorAdjustments>();
            colour.contrast.overrideState = true; colour.contrast.value = 9f;
            colour.saturation.overrideState = true; colour.saturation.value = 7f;

            var vignette = _imageProfile.Add<Vignette>();
            vignette.intensity.overrideState = true; vignette.intensity.value = .24f;
            vignette.smoothness.overrideState = true; vignette.smoothness.value = .42f;

            var go = new GameObject(PostProcessVolumeName);
            go.transform.SetParent(transform, false);
            go.layer = 0; // inside the camera's default volume mask
            _imageVolume = go.AddComponent<Volume>();
            _imageVolume.isGlobal = true; _imageVolume.priority = 0f; _imageVolume.sharedProfile = _imageProfile;
            _imageFinishOn = false;
            RefreshPostProcessing(true);
        }

        private void RefreshPostProcessing(bool force = false)
        {
            if (_imageVolume == null || _camera == null) return;
            bool on = _atmosphere == null || !_atmosphere.ReducedEffects;
            if (!force && on == _imageFinishOn) return;
            _imageFinishOn = on;
            _imageVolume.enabled = on;
            if (_camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) || on)
            {
                if (data == null) data = _camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = on;
                data.volumeLayerMask |= 1;
            }
        }

        private void DisposePostProcessing()
        {
            if (_imageVolume != null) Destroy(_imageVolume.gameObject);
            if (_imageProfile != null) Destroy(_imageProfile);
            _imageVolume = null; _imageProfile = null;
        }
    }
}
