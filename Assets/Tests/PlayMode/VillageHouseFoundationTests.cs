using System;
using System.Collections;
using System.Collections.Generic;
using AreArpg.Core;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        // The private village's road stones may stand up to 8 cm above the soil.
        // A 15 cm contact band leaves room for rough masonry at the foot of a wall.
        private const float HouseContactMetres = .15f;
        private static bool HouseFootTouchesGround(float gap) => Mathf.Abs(gap) <= HouseContactMetres;

        [UnityTest, Timeout(900000)]
        public IEnumerator VillageHomeWallsAndPostsMeetTheRenderedGroundAcrossTheirFootprints()
        {
            if (Resources.Load<GameObject>("ARE/Visuals/EntryVillageWorld") == null)
                Assert.Ignore("The private EntryVillageWorld prefab is required to measure its real house foundations.");

            EnterVillageAfterScene();
            RaiseHouses(Houses);
            yield return null; // Let Destroy retire the previous village before selecting its replacement.
            var art = Find("Art_EntryVillageWorld");
            Assert.IsNotNull(art, "A present but rejected private village must fail acceptance.");
            var ground = art.transform.Find("Ground");
            Assert.IsNotNull(ground, "The private village must supply its rendered soil.");
            var soils = new List<MeshRenderer>(ground.GetComponentsInChildren<MeshRenderer>());
            Assert.IsTrue(soils.Exists(r => r.enabled && r.GetComponent<MeshFilter>()?.sharedMesh != null),
                "The soil must contain a visible mesh.");
            foreach (Transform part in art.transform)
                if (part.name == "Old village road" || part.name == "Supply court paving")
                    soils.AddRange(part.GetComponentsInChildren<MeshRenderer>());

            var failures = new List<string>();
            int homes = 0;
            for (int i = 0; i < EntryWorldLayout.BuildingCount; ++i)
            {
                var footprint = EntryWorldLayout.Building(i);
                if (!footprint.Id.StartsWith("home-", StringComparison.Ordinal)) continue;
                Transform home = art.transform.Find(footprint.Id);
                Assert.IsNotNull(home, "Missing canonical exterior shell " + footprint.Id);
                var surfaces = new List<HouseFloorFace>();
                float cx = footprint.X / 1000f, cz = footprint.Z / 1000f;
                float hw = footprint.HalfWidth / 1000f, hd = footprint.HalfDepth / 1000f;
                foreach (var soil in soils)
                {
                    Bounds box = soil.bounds;
                    if (!soil.enabled || box.max.x < cx - hw || box.min.x > cx + hw ||
                        box.max.z < cz - hd || box.min.z > cz + hd) continue;
                    ReadRenderedFaces(soil, (a, b, c) => surfaces.Add(new HouseFloorFace(a, b, c)));
                }
                var gaps = new[] { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
                var anchors = new Vector3[4];
                var missingSoil = new bool[4];
                void CheckFoot(Vector3 point)
                {
                    float x = point.x - cx, z = point.z - cz;
                    // A central post cannot stand in for all four parts of the house.
                    if (Mathf.Abs(x) < .15f * hw || Mathf.Abs(z) < .15f * hd ||
                        Mathf.Abs(x) > hw + .02f || Mathf.Abs(z) > hd + .02f) return;
                    int sector = (x > 0f ? 1 : 0) + (z > 0f ? 2 : 0);
                    float floor = float.NegativeInfinity;
                    foreach (var face in surfaces)
                        if (face.TryHeight(point, out float y)) floor = Mathf.Max(floor, y);
                    if (float.IsNegativeInfinity(floor))
                    {
                        if (!missingSoil[sector]) failures.Add(footprint.Id + " has no soil triangle at " + point.ToString("F2"));
                        missingSoil[sector] = true;
                        return;
                    }
                    float gap = point.y - floor;
                    if (gap >= gaps[sector]) return;
                    gaps[sector] = gap;
                    anchors[sector] = point;
                }
                foreach (var renderer in home.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled) continue;
                    ReadRenderedFaces(renderer, (a, b, c) =>
                    {
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        float low = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                        float rise = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - low;
                        // Sample the bottoms of upright wall and post faces. Sloped roofs, flat
                        // awnings, shelves and loose ground dressing cannot establish contact.
                        if (rise < .45f || normal.sqrMagnitude < 1e-9f ||
                            normal.y * normal.y > .16f * normal.sqrMagnitude) return;
                        if (a.y <= low + .02f) CheckFoot(a);
                        if (b.y <= low + .02f) CheckFoot(b);
                        if (c.y <= low + .02f) CheckFoot(c);
                    });
                }
                for (int sector = 0; sector < gaps.Length; ++sector)
                {
                    string label = footprint.Id + " " + ((sector & 1) == 0 ? "west" : "east") +
                        "/" + ((sector & 2) == 0 ? "south" : "north");
                    float gap = gaps[sector];
                    if (float.IsPositiveInfinity(gap))
                        failures.Add(label + " has no measurable load-bearing foot over the visible ground.");
                    else
                    {
                        string contact = label + " at " + anchors[sector].ToString("F3") + " signed gap " + gap.ToString("F3") + " m";
                        Debug.Log("VILLAGE_HOUSE_CONTACT " + contact);
                        if (!HouseFootTouchesGround(gap)) failures.Add(contact);
                    }
                }
                ++homes;
            }
            Assert.GreaterOrEqual(homes, VillagePlaceLayout.PlaceCount, "The village must expose its canonical houses.");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // Horizontal coordinates select the actual point on a rendered triangle, including
        // ramps and uneven paving. Render bounds or a nominal zero plane cannot provide it.
        private readonly struct HouseFloorFace
        {
            private readonly Vector3 _a, _ab, _ac;
            private readonly float _det;
            public HouseFloorFace(Vector3 a, Vector3 b, Vector3 c)
            {
                _a = a; _ab = b - a; _ac = c - a;
                _det = _ab.x * _ac.z - _ab.z * _ac.x;
            }
            public bool TryHeight(Vector3 p, out float height)
            {
                height = 0f;
                if (Mathf.Abs(_det) < 1e-7f) return false;
                Vector3 d = p - _a;
                float u = (d.x * _ac.z - d.z * _ac.x) / _det;
                float v = (_ab.x * d.z - _ab.z * d.x) / _det;
                if (u < -.00001f || v < -.00001f || u + v > 1.00001f) return false;
                height = _a.y + _ab.y * u + _ac.y * v;
                return true;
            }
        }

        [Test]
        public void HouseGroundProbeMeasuresTheSlopeAtTheFootAndRejectsMissingSoil()
        {
            var slope = new HouseFloorFace(new Vector3(0, 0, 0), new Vector3(4, 2, 0), new Vector3(0, 1, 4));
            var grounded = new Vector3(1f, .75f, 1f);
            Assert.IsTrue(slope.TryHeight(grounded, out float floor));
            Assert.AreEqual(grounded.y, floor, .00001f);
            Assert.IsTrue(HouseFootTouchesGround(grounded.y - floor));
            Assert.IsFalse(HouseFootTouchesGround(grounded.y + 1f - floor), "A floating support must fail.");
            Assert.IsFalse(HouseFootTouchesGround(grounded.y - 1f - floor), "A buried support must fail.");
            Assert.IsFalse(slope.TryHeight(new Vector3(3, 0, 3), out _), "A hole inside a mesh's XZ bounds has no soil.");
        }

        private static void ReadRenderedFaces(MeshRenderer renderer, Action<Vector3, Vector3, Vector3> visit)
        {
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            Assert.IsNotNull(mesh, renderer.name + " is enabled but has no render mesh.");
            Matrix4x4 world = renderer.transform.localToWorldMatrix;
            // Passive village prefabs intentionally have no physics components. Read the actual
            // render triangles without adding colliders or changing an imported mesh's read flag.
#if UNITY_EDITOR
            using (var meshes = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
#else
            if (!mesh.isReadable) Assert.Inconclusive("Private render mesh needs the Editor read-only accessor: " + mesh.name);
            using (var meshes = Mesh.AcquireReadOnlyMeshData(mesh))
#endif
            {
                var data = meshes[0];
                var points = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                try
                {
                    data.GetVertices(points);
                    for (int i = 0; i < points.Length; ++i) points[i] = world.MultiplyPoint3x4(points[i]);
                    for (int i = 0; i < data.subMeshCount; ++i)
                    {
                        var submesh = data.GetSubMesh(i);
                        if (submesh.topology != MeshTopology.Triangles) continue;
                        int lodZeroIndices = (int)mesh.GetIndexCount(i);
                        using (var indices = new NativeArray<int>(lodZeroIndices, Allocator.Temp))
                        {
                            data.GetIndices(indices, i);
                            for (int p = 0; p < indices.Length; p += 3)
                                visit(points[indices[p]], points[indices[p + 1]], points[indices[p + 2]]);
                        }
                    }
                }
                finally { points.Dispose(); }
            }
        }
    }
}
