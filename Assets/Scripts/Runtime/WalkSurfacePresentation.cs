using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// The Core village is flat, but its private art lays paving and earth a few centimetres above that plane. Actors
    /// stand on the surface the player sees instead of inside it. Presentation only: nothing here reaches the Core.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private struct WalkSurface { public Matrix4x4 ToLocal; public Bounds Local; public float Top; }

        private readonly List<WalkSurface> _walkSurfaces = new List<WalkSurface>();

        /// <summary>Thin, wide pieces close to the ground (paving, laid earth) are walkable; roofs, walls and props are
        /// not. Uses renderer bounds only, so it also works for meshes without read access in a player build.</summary>
        private void CollectWalkSurfaces(Transform art)
        {
            _walkSurfaces.Clear();
            if (art == null) return;
            foreach (var renderer in art.GetComponentsInChildren<MeshRenderer>())
            {
                Bounds world = renderer.bounds;
                if (!renderer.enabled || world.size.y >= .6f || world.max.y > .3f || world.max.y <= .005f) continue;
                Vector3 scale = renderer.transform.lossyScale;
                Bounds local = renderer.localBounds;
                if (Mathf.Abs(local.size.x * scale.x) <= 3f || Mathf.Abs(local.size.z * scale.z) <= 3f) continue;
                _walkSurfaces.Add(new WalkSurface { ToLocal = renderer.transform.worldToLocalMatrix, Local = local, Top = world.max.y });
            }
        }

        /// <summary>Highest walkable art surface under a point in the village, otherwise 0.</summary>
        private float WalkSurfaceHeight(float x, float z)
        {
            if (_game == null || !_game.IsVillage) return 0f;
            float top = 0f;
            for (int i = 0; i < _walkSurfaces.Count; i++)
            {
                WalkSurface s = _walkSurfaces[i];
                if (s.Top <= top) continue;
                Vector3 p = s.ToLocal.MultiplyPoint3x4(new Vector3(x, s.Top, z));
                if (p.x >= s.Local.min.x && p.x <= s.Local.max.x && p.z >= s.Local.min.z && p.z <= s.Local.max.z) top = s.Top;
            }
            return top;
        }
    }
}
