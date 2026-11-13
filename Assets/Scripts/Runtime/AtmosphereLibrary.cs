using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Reviewed local media only; vendor scripts and online services are not loaded.</summary>
    public sealed class AtmosphereLibrary : ScriptableObject
    {
        [System.Serializable] public struct ClipEntry { public string Key; public AudioClip Clip; }
        public ClipEntry[] Clips = new ClipEntry[0];
        public AudioClip Find(string key)
        {
            foreach (var item in Clips) if (item.Key == key) return item.Clip;
            return null;
        }
    }

    public enum GroundSurface { Sand, Grass, Wood, Stone, Water }

    /// <summary>Projection map shared by visible surface patches and footsteps; never changes collision.</summary>
    public static class AtmosphereSurfaces
    {
        public static GroundSurface At(int act, long x, long z)
        {
            if (act == 8) return GroundSurface.Grass;
            if (act == 3 && System.Math.Abs(x) >= 6000 && System.Math.Abs(x) <= 10000 && System.Math.Abs(z) <= 18000)
                return GroundSurface.Grass;
            if (act == 4 && x >= -6500 && x <= -1500 && z >= -4000 && z <= 0) return GroundSurface.Wood;
            if (act == 4 && x >= -7000 && x <= -3000 && z >= 7000 && z <= 10000) return GroundSurface.Water;
            if (act == 2 || act == 3 || act == 4) return System.Math.Abs(x) <= 2500 ? GroundSurface.Stone : GroundSurface.Sand;
            return GroundSurface.Stone;
        }
    }
}
