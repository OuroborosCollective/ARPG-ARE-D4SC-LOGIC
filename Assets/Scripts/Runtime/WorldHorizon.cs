using System;
using System.Runtime.InteropServices;
using AreArpg.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.UnityClient
{
    /// <summary>One passive, streaming horizon for the current Ashen Entry location.</summary>
    public sealed class WorldHorizon : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct HorizonVertex
        {
            public Vector3 Position;
            public Color32 Color;
        }

        private ArenaRuntime _arena;
        private Transform _hero;
        private MeshFilter _filter;
        private Material _material;
        private ArenaSimulation _shownIn;
        private string _wanted, _loading;
        private ResourceRequest _request;

        public static void Attach(ArenaRuntime arena, Transform hero)
        {
            var shader = Resources.Load<Shader>("WeltferneHorizon");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("Weltferne: horizon shader unavailable; keeping the existing scene backdrop.");
                return;
            }
            var host = new GameObject("Weltferne");
            host.transform.SetParent(arena.transform, false);
            var horizon = host.AddComponent<WorldHorizon>();
            horizon._arena = arena;
            horizon._hero = hero;
            horizon._filter = host.AddComponent<MeshFilter>();
            horizon._material = new Material(shader) { name = "Weltferne baked stone and ash" };
            bool detail = true;
            foreach (string surface in new[] { "masonry", "rock", "timber", "earth" })
            {
                var texture = Resources.Load<Texture2D>("Weltferne/Detail/" + surface);
                if (texture == null) detail = false;
                else horizon._material.SetTexture("_" + surface, texture);
            }
            horizon._material.SetFloat("_HasDetail", detail ? 1f : 0f);
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = horizon._material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
        }

        private void LateUpdate()
        {
            var game = _arena != null && _arena.isActiveAndEnabled ? _arena.Simulation : null;
            string site = WorldHorizonSite.For(game);
            if (site != _wanted)
            {
                _wanted = site;
                ReleaseMesh();
                BeginLoad();
            }
            else if (!ReferenceEquals(_shownIn, game) && _filter.sharedMesh != null) SetGenericRidges(false);
            _shownIn = game;
            if (_wanted == null || _hero == null) return;
            // Retain gentle parallax without reaching the scenery on long woodland routes. Keep
            // bearings fixed to world north when the player turns, and the baseline fixed on stairs.
            var position = _hero.position;
            position.y = 0f;
            transform.position = position - Vector3.ClampMagnitude(position * .22f, 9f);
        }

        private void BeginLoad()
        {
            if (_request != null || _wanted == null) return;
            _loading = _wanted;
            _request = Resources.LoadAsync<TextAsset>("Weltferne/" + _loading);
            _request.completed += Loaded;
        }

        private void Loaded(AsyncOperation operation)
        {
            var asset = ((ResourceRequest)operation).asset as TextAsset;
            try
            {
                if (this != null && _loading == _wanted)
                {
                    if (asset != null)
                    {
                        _filter.sharedMesh = Decode(asset);
                        SetGenericRidges(false);
                    }
                    else Debug.LogWarning("Weltferne: missing horizon resource " + _loading);
                }
            }
            catch (Exception error) when (error is FormatException || error is UnityException)
            {
                Debug.LogError("Weltferne: unable to display " + _loading + ". " + error.Message);
            }
            finally
            {
                if (asset != null) Resources.UnloadAsset(asset);
                _request = null;
            }
            if (this != null && _loading != _wanted) BeginLoad();
        }

        // WFH1, little-endian triangle count, then 3 × (float xyz, byte rgba) per triangle.
        // Read the TextAsset's native storage directly; only the active mesh remains on the GPU.
        private static Mesh Decode(TextAsset asset)
        {
            var data = asset.GetData<byte>();
            if (!BitConverter.IsLittleEndian || data.Length < 8 || data[0] != 'W' || data[1] != 'F' || data[2] != 'H' || data[3] != '1')
                throw new FormatException("Invalid WFH1 header.");
            int triangleCount = data[4] | data[5] << 8 | data[6] << 16 | data[7] << 24;
            if (triangleCount < 1 || 8L + triangleCount * 48L != data.Length)
                throw new FormatException("Triangle count does not match the WFH1 payload.");

            var vertices = data.GetSubArray(8, data.Length - 8).Reinterpret<HorizonVertex>(1);
            var bounds = new Bounds(vertices[0].Position, Vector3.zero);
            int vertexCount = vertices.Length;
            for (int i = 0; i < vertexCount; ++i)
            {
                Vector3 point = vertices[i].Position;
                if (float.IsNaN(point.sqrMagnitude) || float.IsInfinity(point.sqrMagnitude))
                    throw new FormatException("Non-finite horizon coordinates.");
                bounds.Encapsulate(point);
            }
            var mesh = new Mesh { name = "Weltferne/" + asset.name };
            try
            {
                mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertexBufferParams(vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4));
                mesh.SetVertexBufferData(vertices, 0, 0, vertexCount);
                var indices = new NativeArray<int>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                try
                {
                    for (int i = 0; i < vertexCount; ++i) indices[i] = i;
                    mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
                }
                finally { indices.Dispose(); }
                mesh.bounds = bounds;
                mesh.UploadMeshData(true);
                return mesh;
            }
            catch
            {
                Destroy(mesh);
                throw;
            }
        }

        private void ReleaseMesh()
        {
            if (_filter == null || _filter.sharedMesh == null) return;
            var mesh = _filter.sharedMesh;
            _filter.sharedMesh = null;
            Destroy(mesh);
            SetGenericRidges(true);
        }

        private void SetGenericRidges(bool visible)
        {
            foreach (var backdrop in FindObjectsByType<OutdoorBackdrop>(FindObjectsSortMode.None))
                if (backdrop.gameObject.scene == gameObject.scene) backdrop.SetDistantRidgesVisible(visible);
        }

        private void OnDisable()
        {
            _wanted = null;
            _shownIn = null;
            ReleaseMesh();
        }

        private void OnDestroy()
        {
            ReleaseMesh();
            if (_material != null) Destroy(_material);
        }
    }
}
