using System.Collections.Generic;
using AshenCrypt.WorldForge.Core;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.UnityClient
{
    /// <summary>Read-only outer terrain and distant pines. The authored village and road floors own their foreground.</summary>
    public sealed class BorderlandLandscape : MonoBehaviour
    {
        private Mesh _pine;
        private Material _foliage;

        public static void Build(Transform chapter, string region)
        {
            var bytes = Resources.Load<TextAsset>("ARE/RegionalMaps/" + region);
            var ground = WorldSurfacePresentation.AshSnow;
            if (bytes == null || ground == null) return;
            var root = new GameObject("WorldForge_" + region);
            root.transform.SetParent(chapter, false);
            var landscape = root.AddComponent<BorderlandLandscape>();
            var map = ForgedWorldMap.FromBytes(bytes.bytes);
            bool forest = region == RegionNetwork.Waldweg;
            var centre = new Vector2(forest ? 46 : 28, forest ? 58 : 28);
            landscape.BuildTerrain(map, centre, ground);
            landscape.BuildGroves(map, centre);
        }

        private static float Surface(ForgedWorldMap map, Vector2 centre, float x, float z)
        {
            float rim = Mathf.Max(Mathf.Abs(x) - centre.x, Mathf.Abs(z) - centre.y);
            return -.37f + Mathf.SmoothStep(0, 1, Mathf.Clamp01(rim / 24f)) *
                (.35f + map.HeightMm(Mathf.RoundToInt(x * 1000), Mathf.RoundToInt(z * 1000)) * .001f);
        }

        private void BuildTerrain(ForgedWorldMap map, Vector2 centre, Material ground)
        {
            const int steps = 64, stride = steps + 1;
            var vertices = new Vector3[stride * stride];
            var indices = new List<int>(steps * steps * 6);
            float x0 = map.OriginXmm * .001f, z0 = map.OriginZmm * .001f;
            float stepX = map.WidthMm * .001f / steps, stepZ = map.DepthMm * .001f / steps;
            for (int z = 0; z <= steps; z++)
                for (int x = 0; x <= steps; x++)
                {
                    int i = z * stride + x;
                    float wx = x0 + x * stepX, wz = z0 + z * stepZ;
                    vertices[i] = new Vector3(wx, Surface(map, centre, wx, wz), wz);
                    if (x == steps || z == steps || (Mathf.Abs(wx + stepX / 2) < centre.x - 3 &&
                        Mathf.Abs(wz + stepZ / 2) < centre.y - 3)) continue;
                    indices.Add(i); indices.Add(i + stride); indices.Add(i + 1);
                    indices.Add(i + 1); indices.Add(i + stride); indices.Add(i + stride + 1);
                }
            var mesh = new Mesh { name = "WorldForge Außenrelief", vertices = vertices };
            mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            gameObject.AddComponent<WoodlandMeshOwner>().Mesh = mesh;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ground;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private bool PrepareTrees(out Matrix4x4 basis, out List<Matrix4x4>[] batches)
        {
            basis = Matrix4x4.identity;
            batches = new List<Matrix4x4>[4];
            var visual = BorderlandArt.LoadVisual("AshPine");
            if (visual == null) return false;
            LOD[] lods = visual.GetComponent<LODGroup>().GetLODs();
            var tree = lods[lods.Length - 1].renderers[0];
            _pine = tree.GetComponent<MeshFilter>().sharedMesh;
            _foliage = tree.sharedMaterial;
            for (int i = 0; i < 4; i++) batches[i] = new List<Matrix4x4>();
            // Model importer axes and scale are identical to the baked prefab.
            basis = visual.transform.parent.worldToLocalMatrix * tree.localToWorldMatrix;
            return true;
        }

        private void BuildGroves(ForgedWorldMap map, Vector2 centre)
        {
            if (!PrepareTrees(out var basis, out var batches)) return;
            int index = 0;
            foreach (var anchor in map.Obstacles)
            {
                float x = anchor.Xmm * .001f, z = anchor.Zmm * .001f;
                if (Mathf.Abs(x) < centre.x + 4 && Mathf.Abs(z) < centre.y + 4) continue;
                float height = Surface(map, centre, x, z);
                float size = .89f + (index % 11) * .049f;
                batches[(x >= 0 ? 1 : 0) + (z >= 0 ? 2 : 0)].Add(
                    Matrix4x4.TRS(new Vector3(x, height, z), Quaternion.Euler(0, index * 137.5f, 0), Vector3.one * size) * basis);
                index++;
            }
            PublishTrees(batches);
        }

        public static void BuildJourneyStands(Transform parent, JourneyRouteLayout route)
        {
            var root = new GameObject("Journey_AshPine_Stands");
            root.transform.SetParent(parent, false);
            var landscape = root.AddComponent<BorderlandLandscape>();
            if (!landscape.PrepareTrees(out var basis, out var batches)) return;

            // Existing clear cells and their shoulders stay entirely open, including on old maps.
            // A small coordinate jitter breaks up the grid without changing the route RNG stream.
            for (int z = 2; z < route.Size - 2; z += 3)
                for (int x = 2; x < route.Size - 2; x += 3)
                {
                    int sample = 1367*x + 4967*z + 337*x*z;
                    if (sample % 5 < 2 || route.IsFloor(x,z) || route.IsFloor(x-1,z) ||
                        route.IsFloor(x+1,z) || route.IsFloor(x,z-1) || route.IsFloor(x,z+1)) continue;
                    float cell = route.CellMm * .001f;
                    float wx = route.Centre(x)*.001f + ((sample % 37)/36f-.5f)*cell*.68f;
                    float wz = route.Centre(z)*.001f + (((sample/31) % 41)/40f-.5f)*cell*.68f;
                    float wy = route.HeightMm(Mathf.RoundToInt(wx*1000), Mathf.RoundToInt(wz*1000))*.001f;
                    float scale = .85f + (sample % 53)*.012f;
                    var stand = Matrix4x4.TRS(new Vector3(wx,wy,wz), Quaternion.Euler(0,sample%360,0), Vector3.one*scale);
                    batches[(wx >= 0 ? 1 : 0) + (wz >= 0 ? 2 : 0)].Add(stand*basis);
                }
            landscape.PublishTrees(batches);
        }

        private void PublishTrees(List<Matrix4x4>[] batches)
        {
            // Permanent MeshRenderers participate in every camera pass, including explicit URP renders.
            // Only the source pine remains readable on Android. The combined vertex buffers go to the GPU.
            for (int sector = 0; sector < batches.Length; sector++)
            {
                var stands = batches[sector];
                if (stands.Count == 0) continue;
                var instances = new CombineInstance[stands.Count];
                for (int i = 0; i < instances.Length; i++)
                    instances[i] = new CombineInstance { mesh = _pine, transform = transform.worldToLocalMatrix * stands[i] };
                var surface = new Mesh { name = "AshPine_Stand_" + sector };
                if ((long)_pine.vertexCount * instances.Length > 65535) surface.indexFormat = IndexFormat.UInt32;
                surface.CombineMeshes(instances, true, true);
                surface.RecalculateBounds();
                surface.UploadMeshData(!Application.isEditor);
                var stand = new GameObject(surface.name);
                stand.transform.SetParent(transform, false);
                stand.AddComponent<WoodlandMeshOwner>().Mesh = surface;
                stand.AddComponent<MeshFilter>().sharedMesh = surface;
                var renderer = stand.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _foliage;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
