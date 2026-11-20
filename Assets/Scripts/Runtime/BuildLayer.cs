using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Realises Bau-Modus layouts as passive scenery, in the running game and in the editor preview alike.
    /// No colliders, no scripts from art prefabs (PassiveArtVisuals rejects them), no Core data.
    /// </summary>
    public static class BuildLayer
    {
        public const string LayoutFolder = "ARE/BuildLayouts/";
        public const string RootPrefix = "ARE_BuildLayer_";
        public const string PiecePrefix = "Bau_";
        public const string GhostName = "ARE_BuildGhost";
        private static readonly Dictionary<Color, Material> FallbackMaterials = new Dictionary<Color, Material>();
        private static Material _ghostMaterial;

        /// <summary>Load the published plan of an area, or null when it has none or it is invalid.</summary>
        public static BuildLayoutData Load(string area)
        {
            if (BuildAreas.Find(area) == null) return null;
            TextAsset text = Resources.Load<TextAsset>(LayoutFolder + area);
            if (text == null) return null;
            try { return BuildLayoutRules.Parse(text.text, area); }
            catch (FormatException e)
            {
                Debug.LogError("ARE Bau-Modus: Bauplan '" + area + "' wird nicht geladen: " + e.Message);
                return null;
            }
        }

        /// <summary>Create one root holding every piece of the plan.</summary>
        public static Transform Build(BuildLayoutData data, Func<float, float, float> ground, Transform parent = null)
        {
            var root = new GameObject(RootPrefix + (data != null ? data.area : "leer")).transform;
            if (parent != null) root.SetParent(parent, false);
            if (data == null) return root;
            for (int i = 0; i < data.pieces.Count; i++) CreatePiece(data.pieces[i], root, ground, i);
            return root;
        }

        /// <summary>Number of realised pieces below a layer root (independent readback for the bridge and tests).</summary>
        public static int CountPieces(Transform root)
        {
            if (root == null) return 0;
            int count = 0;
            foreach (Transform child in root)
                if (child.name.StartsWith(PiecePrefix, StringComparison.Ordinal)) count++;
            return count;
        }

        public static Transform CreatePiece(BuildPiece piece, Transform parent, Func<float, float, float> ground, int order)
        {
            BuildCatalogEntry entry = piece != null ? BuildCatalog.Find(piece.key) : null;
            if (entry == null) return null;
            float groundY = ground != null ? ground(piece.x, piece.z) : 0f;
            var host = new GameObject(PiecePrefix + piece.id + "_" + piece.key).transform;
            if (parent != null) host.SetParent(parent, false);
            host.position = new Vector3(piece.x, groundY, piece.z);
            host.rotation = Quaternion.Euler(0f, piece.rot, 0f);
            if (entry.Kind == BuildKind.Surface) BuildSurface(host, entry, piece, order);
            else BuildPrefab(host, entry, piece.scale);
            return host;
        }

        /// <summary>A placement preview: the real piece plus a flat ring marking its footprint.</summary>
        public static Transform CreateGhost(BuildPiece piece, Func<float, float, float> ground)
        {
            BuildCatalogEntry entry = piece != null ? BuildCatalog.Find(piece.key) : null;
            if (entry == null) return null;
            var root = new GameObject(GhostName).transform;
            var shown = piece.Clone();
            if (string.IsNullOrEmpty(shown.id)) shown.id = "vorschau";
            Transform body = CreatePiece(shown, root, ground, 40);
            float footprint = entry.Kind == BuildKind.Surface ? Mathf.Max(piece.w, piece.d) : Mathf.Max(entry.Width, entry.Depth) * piece.scale;
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Vorschau-Rahmen";
            RemoveCollider(ring);
            ring.transform.SetParent(root, false);
            ring.transform.position = (body != null ? body.position : new Vector3(piece.x, 0f, piece.z)) + Vector3.up * 0.03f;
            ring.transform.localScale = new Vector3(footprint + 0.6f, 0.015f, footprint + 0.6f);
            Renderer renderer = ring.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = GhostMaterial();
            return root;
        }

        private static void BuildPrefab(Transform host, BuildCatalogEntry entry, float scale)
        {
            GameObject prefab = Resources.Load<GameObject>(entry.ResourcePath);
            if (entry.Regional) prefab = BorderlandArt.VisualChild(prefab);
            if (prefab != null && PassiveArtVisuals.TryBindPrefab(host, prefab))
            {
                PassiveArtVisuals.EnsurePlayableHumanoidLocomotion(host.GetChild(host.childCount - 1).gameObject);
            }
            else
            {
                GameObject block = GameObject.CreatePrimitive(entry.Shape);
                block.name = "Platzhalter_" + entry.Key;
                RemoveCollider(block);
                block.transform.SetParent(host, false);
                float heightScale = entry.Shape == PrimitiveType.Capsule || entry.Shape == PrimitiveType.Cylinder ? entry.Height * 0.5f : entry.Height;
                block.transform.localScale = new Vector3(entry.Width, heightScale, entry.Depth);
                block.transform.localPosition = new Vector3(0f, entry.Height * 0.5f, 0f);
                block.GetComponent<Renderer>().sharedMaterial = Fallback(entry.Fallback);
            }
            host.localScale = Vector3.one * scale;
        }

        private static void BuildSurface(Transform host, BuildCatalogEntry entry, BuildPiece piece, int order)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Flaeche_" + entry.Key;
            RemoveCollider(tile);
            tile.transform.SetParent(host, false);
            // Later surfaces lie a few millimetres higher so overlapping patches never flicker.
            tile.transform.localPosition = new Vector3(0f, 0.02f + (order % 30) * 0.002f, 0f);
            tile.transform.localScale = new Vector3(piece.w, 0.04f, piece.d);
            var renderer = tile.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Material material = Resources.Load<Material>(entry.ResourcePath);
            renderer.sharedMaterial = material != null ? material : Fallback(entry.Fallback);
            if (material != null)
            {
                // One texture repeat every 4 m instead of stretching it over the whole patch.
                var block = new MaterialPropertyBlock();
                var tiling = new Vector4(Mathf.Max(0.25f, piece.w / 4f), Mathf.Max(0.25f, piece.d / 4f), 0f, 0f);
                if (material.HasProperty("_BaseMap_ST")) block.SetVector("_BaseMap_ST", tiling);
                if (material.HasProperty("_MainTex_ST")) block.SetVector("_MainTex_ST", tiling);
                renderer.SetPropertyBlock(block);
            }
        }

        public static void Dispose(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false; // Core collision stays canonical; vendor physics never decides walkability.
            Dispose(collider);
        }

        private static Material Fallback(Color color)
        {
            Material material;
            if (FallbackMaterials.TryGetValue(color, out material) && material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { name = "BauModus_Platzhalter", hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            FallbackMaterials[color] = material;
            return material;
        }

        private static Material GhostMaterial()
        {
            if (_ghostMaterial != null) return _ghostMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            _ghostMaterial = new Material(shader) { name = "BauModus_Vorschau", hideFlags = HideFlags.DontSave };
            var color = new Color(0.35f, 1f, 0.55f);
            if (_ghostMaterial.HasProperty("_BaseColor")) _ghostMaterial.SetColor("_BaseColor", color);
            if (_ghostMaterial.HasProperty("_Color")) _ghostMaterial.SetColor("_Color", color);
            return _ghostMaterial;
        }
    }

    /// <summary>
    /// Live channel from the editor bridge to a running game. The editor publishes, the runtime projects and
    /// reports what it actually realised (independent readback).
    /// </summary>
    public static class BuildLayerLive
    {
        public static event Action<string, BuildLayoutData> LayoutChanged;
        public static event Action<string, BuildPiece> GhostChanged;

        /// <summary>Area currently shown by a running game (null outside Play Mode or in areas without a plan).</summary>
        public static string ActiveArea { get; private set; }
        public static int ActiveRevision { get; private set; }
        public static int ActivePieces { get; private set; }
        public static bool GhostVisible { get; private set; }

        public static void ReportActive(string area, int revision, int pieces)
        {
            ActiveArea = area; ActiveRevision = revision; ActivePieces = pieces;
        }

        public static void ReportGhost(bool visible) { GhostVisible = visible; }

        public static void Publish(string area, BuildLayoutData data)
        {
            Action<string, BuildLayoutData> handler = LayoutChanged;
            if (handler != null) handler(area, data);
        }

        public static void PublishGhost(string area, BuildPiece ghost)
        {
            Action<string, BuildPiece> handler = GhostChanged;
            if (handler != null) handler(area, ghost);
        }
    }
}
