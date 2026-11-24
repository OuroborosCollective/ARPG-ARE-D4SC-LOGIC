using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Only displays the existing persisted Core tick; it never controls game time or game rules.</summary>
    public sealed partial class ArenaRuntime
    {
        private Material _worldSkyMaterial;
        private Material _worldSkyOriginalMaterial;
        private Light _worldSkyOriginalSun;
        private Quaternion _worldSkyOriginalSunRotation;
        private Color _worldSkyOriginalSunColor;
        private float _worldSkyOriginalSunIntensity;
        private LightShadows _worldSkyOriginalSunShadows;
        private UnityEngine.Rendering.AmbientMode _worldSkyOriginalAmbientMode;
        private Color _worldSkyOriginalAmbientLight;
        private UnityEngine.Rendering.SphericalHarmonicsL2 _worldSkyOriginalAmbientProbe;
        private OutdoorBackdrop[] _worldSkyBackdrops = System.Array.Empty<OutdoorBackdrop>();
        private bool _worldSkyOriginalFog;
        private FogMode _worldSkyOriginalFogMode;
        private Color _worldSkyOriginalFogColor;
        private float _worldSkyOriginalFogDensity;
        private CameraClearFlags _worldSkyOriginalClear;
        private bool _worldSkyOriginalCaptured;
        private bool _worldSkyOutdoor;
        private int _worldSkyArea = int.MinValue;
        private ulong _worldSkyPhase = ulong.MaxValue;
        private bool _worldSkyReduced;

        private static readonly int EmberGainId = Shader.PropertyToID("_AREEmberGain");
        private static readonly int WorldTintId = Shader.PropertyToID("_AshenWorldTint");

        private static bool IsOutdoorSkyArea(int area)
        {
            // Village, Ash Road, woodland, Aschenzoll and the onward forest road.
            // Mill, storehouse, crypt, rattler den, caves and legacy scenes keep their authored light.
            return area == 2 || area == 3 || area == 8 || area == 10 || area == 11;
        }

        private void InitializeWorldSky()
        {
            var shader = Resources.Load<Shader>("AshenWorldSky");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("ARE WorldSky: reviewed URP shader unavailable; retaining the existing backdrop.");
                return;
            }
            _worldSkyMaterial = new Material(shader) { name = "ARE day-night sky (presentation only)" };
            _worldSkyOriginalMaterial = RenderSettings.skybox;
            _worldSkyOriginalSun = RenderSettings.sun;
            if (_sun != null)
            {
                _worldSkyOriginalSunRotation = _sun.transform.rotation;
                _worldSkyOriginalSunColor = _sun.color;
                _worldSkyOriginalSunIntensity = _sun.intensity;
                _worldSkyOriginalSunShadows = _sun.shadows;
            }
            _worldSkyOriginalAmbientMode = RenderSettings.ambientMode;
            _worldSkyOriginalAmbientLight = RenderSettings.ambientLight;
            _worldSkyOriginalAmbientProbe = RenderSettings.ambientProbe;
            _worldSkyOriginalFog = RenderSettings.fog;
            _worldSkyOriginalFogMode = RenderSettings.fogMode;
            _worldSkyOriginalFogColor = RenderSettings.fogColor;
            _worldSkyOriginalFogDensity = RenderSettings.fogDensity;
            if (_camera != null) _worldSkyOriginalClear = _camera.clearFlags;
            _worldSkyOriginalCaptured = true;
            RefreshWorldSky();
        }

        private void RefreshWorldSky()
        {
            if (_game == null || _worldSkyMaterial == null) return;
            int area = _game.CurrentAreaId;
            // A room inside a village house has no sky; it counts as a place of its own, so entering and leaving switch the sky.
            VillagePlace room = _game.InsidePlace;
            bool outdoors = _entryProfile && IsOutdoorSkyArea(area) && room == VillagePlace.None;
            bool reduced = _atmosphere != null && _atmosphere.ReducedEffects;
            ulong phase = WorldSkyClock.CurrentPhaseTicks(_game);
            if (room != VillagePlace.None) area = 1000 + (int)room;
            if (area != _worldSkyArea)
            {
                _worldSkyArea = area;
                _worldSkyPhase = ulong.MaxValue;
                _worldSkyBackdrops = FindObjectsOfType<OutdoorBackdrop>();
                foreach (var backdrop in _worldSkyBackdrops)
                    backdrop.SetSkyDomeVisible(!outdoors);
            }
            if (!outdoors)
            {
                Shader.SetGlobalFloat(EmberGainId, reduced ? .28f : .80f);
                if (_worldSkyOutdoor)
                {
                    RenderSettings.skybox = _worldSkyOriginalMaterial;
                    RenderSettings.sun = _sun;
                    if (_camera != null) _camera.clearFlags = _worldSkyOriginalClear;
                    if (_sun != null) _sun.transform.rotation = _worldSkyOriginalSunRotation;
                    ApplyEntryLighting();
                    ApplyFlatWorldSkyProbe();
                    Shader.SetGlobalColor(WorldTintId, Color.clear);
                    _worldSkyOutdoor = false;
                }
                return;
            }
            if (!_worldSkyOutdoor || RenderSettings.sun != _sun)
            {
                RenderSettings.skybox = _worldSkyMaterial;
                RenderSettings.sun = _sun;
                if (_camera != null) _camera.clearFlags = CameraClearFlags.Skybox;
                _worldSkyOutdoor = true;
            }
            if (phase == _worldSkyPhase && reduced == _worldSkyReduced && _villageLightGlow >= 0f) return;
            _worldSkyPhase = phase;
            _worldSkyReduced = reduced;

            // Noon at the start of a fresh save; a full orbit costs exactly 14,400 Core ticks.
            float time = phase / (float)WorldSkyClock.TicksPerDay;
            float angle = time * Mathf.PI * 2f;
            float altitude = Mathf.Cos(angle);
            float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.23f, 0.38f, altitude));
            Shader.SetGlobalFloat(EmberGainId, reduced ? .28f : Mathf.Lerp(1.50f, .70f, daylight));
            Vector3 direction = new Vector3(Mathf.Sin(angle) * 0.84f, altitude, Mathf.Cos(angle) * 0.46f).normalized;

            _worldSkyMaterial.SetVector("_SunDirection", new Vector4(direction.x, direction.y, direction.z, 0));
            _worldSkyMaterial.SetFloat("_Daylight", daylight);
            _worldSkyMaterial.SetFloat("_CloudPhase", angle);
            _worldSkyMaterial.SetFloat("_CloudAmount", reduced ? 0.0f : 0.72f);
            _worldSkyMaterial.SetFloat("_StarAmount", reduced ? 0.0f : 1.0f);
            _worldSkyMaterial.SetColor("_SunColor", Color.Lerp(
                new Color(1.0f, 0.44f, 0.19f), new Color(1.0f, 0.84f, 0.54f), daylight));

            if (_sun != null)
            {
                // One existing directional proxy follows the sun by day and the visible moon at night.
                Vector3 lightDirection = daylight <= 0f ? -direction : direction;
                // The visible disc keeps its solar position; the single shadow proxy stays high enough
                // to read the ground at dawn/dusk without adding another mobile light.
                lightDirection.y = Mathf.Max(lightDirection.y, .50f);
                _sun.transform.rotation = Quaternion.LookRotation(-lightDirection.normalized, Vector3.forward);
                Color daylightTint = Color.Lerp(new Color(1.0f, .68f, .45f),
                                                 new Color(1.0f, .92f, .76f), daylight);
                // Night: a cold moon that still draws dark, worn clothes out of the dark (owner brief 2026-11-24:
                // cold moonlight against warm light islands).
                _sun.color = Color.Lerp(new Color(.42f, .56f, .86f), daylightTint,
                                        Mathf.Clamp01(daylight * 4f));
                _sun.intensity = Mathf.Lerp(0.66f, 1.50f, daylight);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.26f, 0.30f, 0.40f),
                                                      new Color(0.47f, 0.46f, 0.45f), daylight);
            ApplyFlatWorldSkyProbe();
            foreach (var backdrop in _worldSkyBackdrops)
                if (backdrop != null) backdrop.SetDaylight(daylight);
            RefreshVillageLights(daylight, reduced);
            // Unlit ground dressing follows the same daylight as the lit world around it.
            Color tint = Color.Lerp(new Color(.20f, .24f, .34f), Color.white, Mathf.SmoothStep(0f, 1f, daylight));
            tint.a = 1f;
            Shader.SetGlobalColor(WorldTintId, tint);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Color.Lerp(new Color(0.10f, 0.13f, 0.20f),
                                                  new Color(0.48f, 0.53f, 0.56f), daylight);
            RenderSettings.fogDensity = Mathf.Lerp(0.0030f, 0.0018f, daylight);
        }

        private static void ApplyFlatWorldSkyProbe()
        {
            if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Flat) return;
            var probe = new UnityEngine.Rendering.SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = probe;
        }

        private void DisposeWorldSky()
        {
            if (_worldSkyOriginalCaptured)
            {
                if (RenderSettings.skybox == _worldSkyMaterial)
                    RenderSettings.skybox = _worldSkyOriginalMaterial;
                if (RenderSettings.sun == _sun)
                    RenderSettings.sun = _worldSkyOriginalSun;
                if (_camera != null && _camera.clearFlags == CameraClearFlags.Skybox)
                    _camera.clearFlags = _worldSkyOriginalClear;
                RenderSettings.ambientMode = _worldSkyOriginalAmbientMode;
                RenderSettings.ambientLight = _worldSkyOriginalAmbientLight;
                RenderSettings.ambientProbe = _worldSkyOriginalAmbientProbe;
                RenderSettings.fog = _worldSkyOriginalFog;
                RenderSettings.fogMode = _worldSkyOriginalFogMode;
                RenderSettings.fogColor = _worldSkyOriginalFogColor;
                RenderSettings.fogDensity = _worldSkyOriginalFogDensity;
                if (_sun != null)
                {
                    _sun.transform.rotation = _worldSkyOriginalSunRotation;
                    _sun.color = _worldSkyOriginalSunColor;
                    _sun.intensity = _worldSkyOriginalSunIntensity;
                    _sun.shadows = _worldSkyOriginalSunShadows;
                }
                foreach (var backdrop in FindObjectsOfType<OutdoorBackdrop>())
                {
                    backdrop.SetSkyDomeVisible(true);
                    backdrop.SetDaylight(1f);
                }
            }
            WorldSurfacePresentation.ReleaseAshPaint();
            Shader.SetGlobalFloat(EmberGainId, 0f);
            DisposeVillageLights();
            DisposePostProcessing();
            Shader.SetGlobalColor(WorldTintId, Color.clear);
            if (_worldSkyMaterial != null) Destroy(_worldSkyMaterial);
            _worldSkyMaterial = null;
            _worldSkyOriginalCaptured = false;
            _worldSkyOutdoor = false;
        }
    }
}
