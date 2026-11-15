using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    /// <summary>
    /// Scene marker for a point of interest. Keeps a static registry so runtime systems
    /// (NPC life cycles, quest logic, spawners) can query homes, workplaces or spawn zones without searching the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class ForgedPoi : MonoBehaviour
    {
        private static readonly List<ForgedPoi> Registry = new List<ForgedPoi>();

        public string poiId = "poi";
        public PoiType type = PoiType.Landmark;
        [Min(0f)] public float radius = 8f;
        public string tags = "";

        public static IReadOnlyList<ForgedPoi> All { get { return Registry; } }

        private void OnEnable() { if (!Registry.Contains(this)) Registry.Add(this); }
        private void OnDisable() { Registry.Remove(this); }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tags) || string.IsNullOrEmpty(tag)) return false;
            foreach (string t in tags.Split(','))
                if (string.Equals(t.Trim(), tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 d = worldPosition - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        public static List<ForgedPoi> FindByType(PoiType poiType)
        {
            var result = new List<ForgedPoi>();
            foreach (ForgedPoi p in Registry) if (p != null && p.type == poiType) result.Add(p);
            return result;
        }

        public static ForgedPoi FindNearest(Vector3 position, PoiType poiType)
        {
            ForgedPoi best = null; float bestD = float.MaxValue;
            foreach (ForgedPoi p in Registry)
            {
                if (p == null || p.type != poiType) continue;
                float d = (p.transform.position - position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor(type);
            Gizmos.DrawWireSphere(transform.position, 0.6f);
            DrawCircle(transform.position, radius);
        }

        private static void DrawCircle(Vector3 c, float r)
        {
            const int seg = 32;
            Vector3 prev = c + new Vector3(r, 0.1f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0.1f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        public static Color GizmoColor(PoiType t)
        {
            switch (t)
            {
                case PoiType.Settlement: return new Color(1f, 0.75f, 0.3f);
                case PoiType.NpcHome: return new Color(0.4f, 0.9f, 0.5f);
                case PoiType.NpcWorkplace: return new Color(0.3f, 0.7f, 1f);
                case PoiType.SpawnZone: return new Color(1f, 0.25f, 0.2f);
                case PoiType.DungeonEntrance: return new Color(0.75f, 0.35f, 1f);
                case PoiType.Waypoint: return new Color(0.6f, 0.95f, 1f);
                case PoiType.Shrine: return new Color(1f, 1f, 0.6f);
                case PoiType.ResourceNode: return new Color(0.7f, 0.55f, 0.35f);
                default: return Color.white;
            }
        }
    }
}
