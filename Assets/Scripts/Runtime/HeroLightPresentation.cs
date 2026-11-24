using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// A soft circle of light around the wanderer under the night sky, like a carried lantern (owner decision
    /// 2026-11-24): away from the torches his dark clothes sank into black. One point light without shadows that stays
    /// at the hero's shoulder height, a good step towards the camera, so it falls on the side the player sees; presentation only, it
    /// changes no rule and no saved state.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string HeroLightName = "ARE Hero light";
        /// <summary>Reach of the circle in metres and its strength: enough to read the hero, not enough to light the village.</summary>
        public const float HeroLightRange = 6.5f, HeroLightIntensity = 1.8f;
        /// <summary>Height above the hero's middle and the step towards the camera, in metres.</summary>
        private const float HeroLightHeight = .6f, HeroLightLead = 1.2f;
        private static readonly Color HeroLightColor = new Color(1f, .78f, .52f);
        private Light _heroLight;

        /// <summary>Called by the sky: on under the open night, off wherever a place has its own authored light.</summary>
        private void RefreshHeroLight(bool night)
        {
            if (!night || _hero == null)
            {
                if (_heroLight != null) _heroLight.enabled = false;
                return;
            }
            if (_heroLight == null)
            {
                var holder = new GameObject(HeroLightName);
                _heroLight = holder.AddComponent<Light>();
                _heroLight.type = LightType.Point;
                _heroLight.shadows = LightShadows.None;
                _heroLight.color = HeroLightColor;
                _heroLight.range = HeroLightRange;
                _heroLight.intensity = HeroLightIntensity;
            }
            _heroLight.enabled = true;
            FollowHeroLight();
        }

        /// <summary>Once per frame: at shoulder height, a good step from the hero towards the camera.</summary>
        private void FollowHeroLight()
        {
            if (_heroLight == null || !_heroLight.enabled || _hero == null) return;
            Vector3 towardsCamera = _camera != null ? _camera.transform.position - _hero.position : Vector3.zero;
            towardsCamera.y = 0f;
            towardsCamera = towardsCamera.sqrMagnitude > .0001f ? towardsCamera.normalized * HeroLightLead : Vector3.zero;
            _heroLight.transform.position = _hero.position + Vector3.up * HeroLightHeight + towardsCamera;
        }

        private void DisposeHeroLight()
        {
            if (_heroLight != null) Destroy(_heroLight.gameObject);
            _heroLight = null;
        }
    }
}
