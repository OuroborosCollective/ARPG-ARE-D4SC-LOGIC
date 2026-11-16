using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Non-authoritative earth feather around the existing imported stone road.
    /// One batched mesh, one transparent mobile material, no collision, no runtime script.
    /// The reviewed village, private art and Fable player camera remain the owners.
    /// </summary>
    public static class AreVillageApproachBuilder
    {
        private const string PrefabPath = "Assets/Resources/ARE/Visuals/EntryVillageWorld.prefab";
        private const string ShaderPath = "Assets/Resources/AREVillageApproachFade.shader";
        private const string MeshPath = "Assets/Resources/ARE/Visuals/Village/VillageApproachFeather.asset";
        private const string MaterialPath = "Assets/Resources/ARE/Visuals/Village/VillageApproachFeather.mat";
        private const string HolderName = "VillageApproachPass1";
        private const string MeshName = "EarthStoneFeather";
        private const float HeightOnRoad = .045f;
        private const float HeightAtEdge = .032f;
        private const float HeightOnEarth = .006f;
        private static readonly float[] Longitudinal = { 0f, .15f, .5f, .85f, 1f };
        private static readonly byte[] LongitudinalAlpha = { 0, 255, 255, 255, 0 };

        [MenuItem("ARE ARPG/Art/Apply Village Earth-Paving Transition")]
        public static void ApplyVillageApproach()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Missing original private village prefab");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported || shader.name != "ARE/VisualOnly/VillageEarthFeather")
                throw new InvalidOperationException("Village feather URP shader missing or unsupported");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform hero = root.transform.Find("VillageHeroPass1");
                if (hero == null || hero.childCount != 8)
                    throw new InvalidOperationException("Must preserve the eight existing village hero groups");
                Transform previous = root.transform.Find(HolderName);
                if (previous != null)
                {
                    VerifyExisting(previous);
                    Debug.Log("ARE_VILLAGE_APPROACH_REUSED");
                    return;
                }

                var ground = root.transform.Find("Ground");
                MeshRenderer groundRenderer = ground != null ? ground.GetComponent<MeshRenderer>() : null;
                Texture groundTexture = groundRenderer != null && groundRenderer.sharedMaterial != null
                    ? groundRenderer.sharedMaterial.mainTexture : null;
                if (groundTexture == null)
                    throw new InvalidOperationException("No reviewed ground texture for earth feather");
                if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null ||
                    AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
                    throw new InvalidOperationException("Unowned pre-existing feather assets; refusing to overwrite");

                Bounds road = BoundsForNamedChildren(root.transform, "Old village road");
                Bounds court = BoundsForNamedChildren(root.transform, "Supply court paving");
                if (road.size.x < 2f || road.size.x > 6f || road.size.z < 28f || road.size.z > 55f ||
                    court.size.x < 7f || court.size.x > 18f || court.size.z < 1f || court.size.z > 7f)
                    throw new InvalidOperationException("Real imported paving bounds differ from approved approach geometry");

                Mesh mesh = CreateFeather(road, court);
                Material material = new Material(shader) { name = "ARE village earth-paving feather" };
                material.SetTexture("_BaseMap", groundTexture);
                material.SetColor("_BaseColor", new Color(.79f, .76f, .7f, 1f));
                Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
                AssetDatabase.CreateAsset(mesh, MeshPath);
                AssetDatabase.CreateAsset(material, MaterialPath);

                GameObject holder = new GameObject(HolderName);
                holder.transform.SetParent(root.transform, false);
                var piece = new GameObject(MeshName);
                piece.transform.SetParent(holder.transform, false);
                piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                VerifyExisting(holder.transform);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new IOException("Unable to save reviewed private village art");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (saved == null || saved.transform.Find(HolderName) == null)
                throw new IOException("Private road feather did not persist on independent prefab readback");
            Debug.Log("ARE_VILLAGE_APPROACH_BUILT: one passive batched earth feather, Core untouched");
        }

        private static void VerifyExisting(Transform root)
        {
            if (root.childCount != 1)
                throw new InvalidOperationException("Unexpected village approach child count");
            if (root.GetComponentsInChildren<MeshFilter>(true).Length != 1 ||
                root.GetComponentsInChildren<MeshRenderer>(true).Length != 1)
                throw new InvalidOperationException("Approach rendering must be one batched mesh");
            foreach (Component part in root.GetComponentsInChildren<Component>(true))
                if (!(part is Transform || part is MeshFilter || part is MeshRenderer))
                    throw new InvalidOperationException("Non-passive component in village approach: " + part.GetType().Name);
            MeshFilter filter = root.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer renderer = root.GetComponentInChildren<MeshRenderer>(true);
            if (filter.sharedMesh == null || filter.sharedMesh.vertexCount < 48 ||
                renderer.sharedMaterial == null ||
                renderer.sharedMaterial.shader.name != "ARE/VisualOnly/VillageEarthFeather")
                throw new InvalidOperationException("Incomplete saved village approach");
        }

        private static Bounds BoundsForNamedChildren(Transform root, string label)
        {
            bool found = false;
            Bounds combined = new Bounds();
            foreach (Transform child in root)
            {
                if (child.name != label) continue;
                foreach (MeshRenderer renderer in child.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!found) combined = renderer.bounds;
                    else combined.Encapsulate(renderer.bounds);
                    found = true;
                }
            }
            if (!found) throw new InvalidOperationException("Existing imported paving missing: " + label);
            return combined;
        }

        private static Mesh CreateFeather(Bounds road, Bounds court)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            // Dirt overlay covers the abrupt stone margins, then fades into the original earth.
            AddShoulder(vertices, uvs, colors, triangles, road.min.x, road.min.z, road.max.z, -1, true);
            AddShoulder(vertices, uvs, colors, triangles, road.max.x, road.min.z, road.max.z, +1, true);
            AddShoulder(vertices, uvs, colors, triangles, court.min.z, court.min.x, court.max.x, -1, false);
            AddShoulder(vertices, uvs, colors, triangles, court.max.z, court.min.x, court.max.x, +1, false);
            var mesh = new Mesh { name = "ARE village pavement-earth feather (visual only)" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddShoulder(
            List<Vector3> vertices, List<Vector2> uvs, List<Color32> colors,
            List<int> triangles, float edge, float alongMin, float alongMax,
            int outward, bool alongZ)
        {
            int start = vertices.Count;
            const float wiggle = .065f;
            for (int row = 0; row < Longitudinal.Length; row++)
            {
                float t = Longitudinal[row];
                float along = Mathf.Lerp(alongMin, alongMax, t);
                // A reproducible irregular edge, never a random value or a Core input.
                float irregular = row == 0 || row == Longitudinal.Length - 1
                    ? 0f : (row % 2 == 0 ? -wiggle : wiggle);
                for (int cross = 0; cross < 3; cross++)
                {
                    float across = cross == 0 ? -.52f : (cross == 1 ? .08f : 1.48f);
                    float sideways = edge + outward * (across + irregular);
                    float x = alongZ ? sideways : along;
                    float z = alongZ ? along : sideways;
                    float height = cross == 0 ? HeightOnRoad :
                        (cross == 1 ? HeightAtEdge : HeightOnEarth);
                    vertices.Add(new Vector3(x, height, z));
                    // Match the 22 repeats on the existing 120-m earth plane.
                    uvs.Add(new Vector2((x + 60f) * (22f / 120f), (z + 60f) * (22f / 120f)));
                    byte alpha = cross == 1 ? (byte)((210 * LongitudinalAlpha[row]) / 255) : (byte)0;
                    colors.Add(new Color32(255, 255, 255, alpha));
                }
            }
            for (int row = 0; row < Longitudinal.Length - 1; row++)
            {
                for (int cross = 0; cross < 2; cross++)
                {
                    int p = start + row * 3 + cross;
                    triangles.Add(p);
                    triangles.Add(p + 3);
                    triangles.Add(p + 1);
                    triangles.Add(p + 1);
                    triangles.Add(p + 3);
                    triangles.Add(p + 4);
                }
            }
        }
    }
}
