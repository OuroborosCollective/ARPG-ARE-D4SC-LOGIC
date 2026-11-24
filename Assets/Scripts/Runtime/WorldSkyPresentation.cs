using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// The sky of the outdoor regions: always night (owner decision 2026-11-24). It only displays the existing persisted
    /// Core tick as the moon's place and the clouds' drift; it never controls game time or game rules.
    /// </summary>
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
            _worldSkyMaterial = new Material(shader) { name = "ARE night sky (presentation only)" };
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
                RefreshHeroLight(false);
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

            // Owner decision 2026-11-24: the story plays in the night of the world. The persisted Core tick no longer
            // brings a day: it only lets the moon wander and the clouds drift (a full round costs exactly 14,400 Core
            // ticks). The Core clock itself and everything it rules stay as they are.
            float time = phase / (float)WorldSkyClock.TicksPerDay;
            float angle = time * Mathf.PI * 2f;
            Shader.SetGlobalFloat(EmberGainId, reduced ? .28f : 1.50f);
            Vector3 moon = MoonDirection(angle);

            // The sky shader draws the moon opposite its sun: the sun stays below the ground for good.
            _worldSkyMaterial.SetVector("_SunDirection", new Vector4(-moon.x, -moon.y, -moon.z, 0));
            _worldSkyMaterial.SetFloat("_Daylight", 0f);
            _worldSkyMaterial.SetFloat("_CloudPhase", angle);
            _worldSkyMaterial.SetFloat("_CloudAmount", reduced ? 0.0f : 0.72f);
            _worldSkyMaterial.SetFloat("_StarAmount", reduced ? 0.0f : 1.0f);
            _worldSkyMaterial.SetColor("_SunColor", new Color(1.0f, 0.44f, 0.19f));

            if (_sun != null)
            {
                // The one directional light is the moon: cold, and strong enough to draw dark, worn clothes out of
                // the night. More light, where it is wanted, comes from the moon, never from a day.
                _sun.transform.rotation = Quaternion.LookRotation(-moon, Vector3.forward);
                _sun.color = MoonColor;
                _sun.intensity = MoonIntensity;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.30f, 0.40f);
            ApplyFlatWorldSkyProbe();
            foreach (var backdrop in _worldSkyBackdrops)
                if (backdrop != null) backdrop.SetDaylight(0f);
            RefreshVillageLights(0f, reduced);
            RefreshHeroLight(true);
            // Unlit ground dressing wears the same night as the lit world around it.
            Shader.SetGlobalColor(WorldTintId, new Color(.20f, .24f, .34f, 1f));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.10f, 0.13f, 0.20f);
            RenderSettings.fogDensity = 0.0030f;
        }

        /// <summary>The moon's light: cold blue-white.</summary>
        public static readonly Color MoonColor = new Color(.42f, .56f, .86f);
        /// <summary>The moon's strength as the night's only key light (the day's sun had 1.5).</summary>
        public const float MoonIntensity = 0.66f;

        /// <summary>
        /// Where the moon stands for a phase of the Core day (0..2π): it circles the sky once and never sets, between 40
        /// and 62 degrees high. A fresh save starts with the moon in the south-east, behind the right shoulder of a
        /// wanderer who looks north, so it lights what the camera sees.
        /// </summary>
        public static Vector3 MoonDirection(float angle)
        {
            float height = Mathf.Deg2Rad * (51f + 11f * Mathf.Sin(angle * 2f));
            float azimuth = angle + Mathf.Deg2Rad * 120f;
            return new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(height), Mathf.Sin(height), Mathf.Cos(azimuth) * Mathf.Cos(height));
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
            DisposeHeroLight();
            DisposePostProcessing();
            Shader.SetGlobalColor(WorldTintId, Color.clear);
            if (_worldSkyMaterial != null) Destroy(_worldSkyMaterial);
            _worldSkyMaterial = null;
            _worldSkyOriginalCaptured = false;
            _worldSkyOutdoor = false;
        }
    }
}
