using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Hair under a hat or hood. The pack lays every hair part over the bare head, so tufts stick out through the cloth
    /// of a hood. A recipe that asks for it has those tufts cut: seen from the middle of the head, hair that lies
    /// behind the hat's cloth is outside it; where that happens away from the hat's rim, the hair has gone through the
    /// cloth and its triangles are left out. Hair that leaves the hat at its rim (a fringe at the face, long hair below
    /// the hood) stays.
    /// </summary>
    internal static class FigureHairTuck
    {
        /// <summary>Hair this close to the hat's rim has come out from under it, not through it (metres).</summary>
        const float RimKeep = .015f;
        /// <summary>A hit this close before the hair itself is the hair touching the cloth from inside (share of the way).</summary>
        const float Touch = .97f;

        /// <summary>
        /// Drops the hair triangles that stick out through the hat. <paramref name="vertices"/> are the mesh's vertices
        /// in the shape the figure is built in; <paramref name="ranges"/> name the part of each vertex range
        /// (start, count). Returns how many triangles were left out.
        /// </summary>
        public static int Apply(Mesh mesh, Vector3[] vertices, List<KeyValuePair<string, Vector2Int>> ranges)
        {
            var part = new byte[vertices.Length]; // 1 hat, 2 hair, 3 head
            Vector3 middle = Vector3.zero;
            int headCount = 0;
            foreach (KeyValuePair<string, Vector2Int> range in ranges)
            {
                byte kind = range.Key == "Hat" ? (byte)1 : range.Key.StartsWith("Hair", StringComparison.Ordinal) ? (byte)2 : range.Key == "Head" ? (byte)3 : (byte)0;
                for (int i = range.Value.x; i < range.Value.x + range.Value.y && i < part.Length; i++)
                {
                    part[i] = kind;
                    if (kind == 3) { middle += vertices[i]; headCount++; }
                }
            }
            if (headCount == 0) return 0;
            middle /= headCount;

            var hat = new List<int>();
            var triangles = new List<int[]>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] indices = mesh.GetTriangles(sub);
                triangles.Add(indices);
                for (int t = 0; t + 2 < indices.Length; t += 3)
                    if (part[indices[t]] == 1 && part[indices[t + 1]] == 1 && part[indices[t + 2]] == 1)
                    { hat.Add(indices[t]); hat.Add(indices[t + 1]); hat.Add(indices[t + 2]); }
            }
            if (hat.Count == 0) return 0;

            // The rim: edges of the hat that belong to one triangle only. Vertices are doubled along texture seams, so
            // they are told apart by their place, not by their number.
            var place = new Dictionary<Vector3Int, int>();
            Func<int, int> welded = index =>
            {
                Vector3 v = vertices[index];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * 20000f), Mathf.RoundToInt(v.y * 20000f), Mathf.RoundToInt(v.z * 20000f));
                int id;
                if (!place.TryGetValue(key, out id)) { id = index; place.Add(key, id); }
                return id;
            };
            var uses = new Dictionary<long, int>();
            for (int t = 0; t < hat.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = welded(hat[t + e]), b = welded(hat[t + (e + 1) % 3]);
                    if (a == b) continue;
                    long edge = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int count;
                    uses.TryGetValue(edge, out count);
                    uses[edge] = count + 1;
                }
            var rim = new List<Vector3>();
            foreach (KeyValuePair<long, int> edge in uses)
                if (edge.Value == 1) { rim.Add(vertices[(int)(edge.Key >> 32)]); rim.Add(vertices[(int)(edge.Key & 0xffffffff)]); }

            var through = new bool[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                if (part[i] != 2) continue;
                Vector3 way = vertices[i] - middle;
                float nearest = float.MaxValue;
                for (int t = 0; t < hat.Count; t += 3)
                {
                    float at;
                    if (Crosses(middle, way, vertices[hat[t]], vertices[hat[t + 1]], vertices[hat[t + 2]], out at) && at < nearest) nearest = at;
                }
                if (nearest >= Touch) continue; // no cloth between the head's middle and this hair
                Vector3 hit = middle + way * nearest;
                bool atRim = false;
                for (int r = 0; r < rim.Count && !atRim; r += 2) atRim = Distance(hit, rim[r], rim[r + 1]) <= RimKeep;
                through[i] = !atRim;
            }

            int dropped = 0;
            for (int sub = 0; sub < triangles.Count; sub++)
            {
                int[] indices = triangles[sub];
                var kept = new List<int>(indices.Length);
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    if (through[indices[t]] || through[indices[t + 1]] || through[indices[t + 2]]) { dropped++; continue; }
                    kept.Add(indices[t]); kept.Add(indices[t + 1]); kept.Add(indices[t + 2]);
                }
                if (kept.Count != indices.Length) mesh.SetTriangles(kept, sub);
            }
            return dropped;
        }

        /// <summary>Where the way from a point crosses a triangle, as a share of the way (Möller–Trumbore); false when it does not.</summary>
        static bool Crosses(Vector3 from, Vector3 way, Vector3 a, Vector3 b, Vector3 c, out float at)
        {
            at = 0f;
            Vector3 ab = b - a, ac = c - a, p = Vector3.Cross(way, ac);
            float det = Vector3.Dot(ab, p);
            if (det > -1e-12f && det < 1e-12f) return false;
            float inverse = 1f / det;
            Vector3 s = from - a;
            float u = Vector3.Dot(s, p) * inverse;
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(s, ab);
            float v = Vector3.Dot(way, q) * inverse;
            if (v < 0f || u + v > 1f) return false;
            at = Vector3.Dot(ac, q) * inverse;
            return at > 0f;
        }

        static float Distance(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float length = ab.sqrMagnitude;
            float along = length < 1e-12f ? 0f : Mathf.Clamp01(Vector3.Dot(point - a, ab) / length);
            return (point - (a + ab * along)).magnitude;
        }
    }
}
