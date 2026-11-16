using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Passive Viking wood perimeter, tree silhouettes and a rolling land horizon.
    /// Real privately licensed mesh inputs only; the 27-m ArenaSimulation boundary
    /// continues to own all collision and north-gate passage rules.
    /// </summary>
    public static class AreVillagePalisadeBuilder
    {
        private const string Prefab = "Assets/Resources/ARE/Visuals/EntryVillageWorld.prefab";
        private const string RidgeAsset = "Assets/Resources/ARE/Visuals/Village/VillageRollingRidge.asset";
        private const string WallsName = "VillagePalisadeRimPass1";
        private const string ForestName = "VillageForestBehindPalisadePass1";
        private const string RidgeName = "VillageRollingHorizonPass1";
        private const float WallBoundary = 26.5f;
        private const float WallScale = 0.87f;
        private const string WallKey = "hero.wall";
        private static readonly string[] Trees =
        {
            "tree.02", "tree.03", "tree.06", "tree.01", "tree.04", "tree.05"
        };

        private readonly struct PlantedTree
        {
            public readonly int Xmm, Zmm, ScalePermille, QuarterTurn;
            public PlantedTree(int x, int z, int scale, int turns)
            { Xmm = x; Zmm = z; ScalePermille = scale; QuarterTurn = turns; }
        }

        // All anchors are outside the existing 27-m gameplay boundary.
        // 22 fixed trees, not random scatter or seed-dependent runtime world state.
        private static readonly PlantedTree[] Plantings =
        {
            new PlantedTree(-51000, 39000, 720, 0), new PlantedTree(-39500, 34800, 840, 1),
            new PlantedTree(-29200, 40700, 700, 2), new PlantedTree(-21500, 32200, 900, 3),
            new PlantedTree(-8500, 35000, 860, 1), new PlantedTree(8500, 35500, 950, 0),
            new PlantedTree(20500, 33000, 860, 2), new PlantedTree(31000, 40000, 720, 3),
            new PlantedTree(39500, 34000, 850, 1), new PlantedTree(51000, 38700, 720, 0),
            new PlantedTree(-35500, -21000, 790, 1), new PlantedTree(-32800, -7500, 940, 2),
            new PlantedTree(-35500, 7600, 880, 0), new PlantedTree(-39200, 21600, 870, 3),
            new PlantedTree(35900, -21400, 850, 2), new PlantedTree(32000, -7800, 920, 1),
            new PlantedTree(34400, 6700, 820, 0), new PlantedTree(37800, 20600, 760, 3),
            new PlantedTree(-22000, -35500, 730, 0), new PlantedTree(-8700, -38500, 860, 1),
            new PlantedTree(8700, -37600, 910, 3), new PlantedTree(22000, -34900, 770, 2)
        };

        [MenuItem("ARE ARPG/Art/Apply Viking Palisade and Forest Rim")]
        public static void ApplyPalisadeForest()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) == null)
                throw new InvalidOperationException("Reviewed private village prefab missing.");
            AreVillageArtBuilder.LoadCatalog();
            GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
            bool modified = false;
            try
            {
                Transform hero = root.transform.Find("VillageHeroPass1");
                Transform path = root.transform.Find("VillageApproachPass1");
                if (hero == null || hero.childCount != 8 || path == null || path.childCount != 1)
                    throw new InvalidOperationException("Hero/approach prerequisite missing or changed.");

                Transform oldWalls = root.transform.Find(WallsName);
                Transform oldForest = root.transform.Find(ForestName);
                Transform oldRidge = root.transform.Find(RidgeName);
                if (oldWalls != null || oldForest != null || oldRidge != null)
                {
                    if (oldWalls == null || oldForest == null || oldRidge == null)
                        throw new InvalidOperationException("Partial prior village perimeter. Refusing overwrite.");
                    ValidateExisting(oldWalls, oldForest, oldRidge);
                    Debug.Log("ARE_VILLAGE_PALISADE_REUSED: original passive perimeter validated");
                    return;
                }

                if (AssetDatabase.LoadAssetAtPath<Mesh>(RidgeAsset) != null)
                    throw new InvalidOperationException("Unowned rolling horizon asset; refusing overwrite.");

                Transform ground = root.transform.Find("Ground");
                Material earth = ground != null ? ground.GetComponent<MeshRenderer>()?.sharedMaterial : null;
                if (earth == null || earth.mainTexture == null)
                    throw new InvalidOperationException("Reviewed ground albedo missing.");

                Transform walls = NewChild(root.transform, WallsName);
                Transform west = NewChild(walls, "WestWall");
                Transform east = NewChild(walls, "EastWall");
                Transform south = NewChild(walls, "SouthWall");
                Transform northwest = NewChild(walls, "NorthWestWall");
                Transform northeast = NewChild(walls, "NorthEastWall");

                // Source panel's long axis is Z. The north-gate arch at (0,24m) stays open.
                for (int i = -5; i <= 5; i++)
                {
                    float at = i * 4.5f;
                    PlaceMesh(west, WallKey, new Vector3(-WallBoundary, 0f, at), 0f,
                        WallScale, "WestWallPanel_" + (i + 5));
                    PlaceMesh(east, WallKey, new Vector3(WallBoundary, 0f, at), 0f,
                        WallScale, "EastWallPanel_" + (i + 5));
                    PlaceMesh(south, WallKey, new Vector3(at, 0f, -WallBoundary), 90f,
                        WallScale, "SouthWallPanel_" + (i + 5));
                }
                for (int i = 0; i < 5; i++)
                {
                    float x = 5f + i * 4.5f;
                    PlaceMesh(northwest, WallKey, new Vector3(-x, 0f, WallBoundary), 90f,
                        WallScale, "NorthWestPanel_" + i);
                    PlaceMesh(northeast, WallKey, new Vector3(x, 0f, WallBoundary), 90f,
                        WallScale, "NorthEastPanel_" + i);
                }

                Transform forest = NewChild(root.transform, ForestName);
                for (int i = 0; i < Plantings.Length; i++)
                {
                    PlantedTree tree = Plantings[i];
                    string source = Trees[i % Trees.Length];
                    // Private source, quarter-turn rotation, positive bounded scale.
                    PlaceMesh(forest, source,
                        new Vector3(tree.Xmm / 1000f, 0f, tree.Zmm / 1000f),
                        90f * tree.QuarterTurn, tree.ScalePermille / 1000f,
                        "PalisadeForestTree_" + i.ToString("D2"));
                }

                Transform backdrop = NewChild(root.transform, RidgeName);
                Mesh ridge = CreateRollingRidge();
                Directory.CreateDirectory(Path.GetDirectoryName(RidgeAsset));
                AssetDatabase.CreateAsset(ridge, RidgeAsset);
                var mound = new GameObject("OneBatchedEarthRidge");
                mound.transform.SetParent(backdrop, false);
                mound.AddComponent<MeshFilter>().sharedMesh = ridge;
                MeshRenderer mr = mound.AddComponent<MeshRenderer>();
                mr.sharedMaterial = earth;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

                ValidateExisting(walls, forest, backdrop);
                if (PrefabUtility.SaveAsPrefabAsset(root, Prefab) == null)
                    throw new IOException("Could not save private reviewed village perimeter.");
                modified = true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (!modified) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Prefab, ImportAssetOptions.ForceUpdate);
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (saved == null)
                throw new IOException("Village prefab unavailable after save.");
            ValidateExisting(saved.transform.Find(WallsName), saved.transform.Find(ForestName),
                saved.transform.Find(RidgeName));
            Debug.Log("ARE_VILLAGE_PALISADE_BUILT: 43 real wooden wall panels, 22 private trees, one earth ridge; no gameplay collision");
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result.transform;
        }

        private static void PlaceMesh(Transform parent, string assetKey, Vector3 at, float yaw,
            float scale, string name)
        {
            int before = parent.childCount;
            AreVillageArtBuilder.Piece(parent, assetKey, at, yaw, scale);
            if (parent.childCount != before + 1)
                throw new InvalidOperationException("Private source has no real LOD0 art: " + assetKey);
            Transform mesh = parent.GetChild(before);
            mesh.name = name;
            int rendered = 0;
            foreach (Component component in mesh.GetComponentsInChildren<Component>(true))
            {
                if (!(component is Transform || component is MeshFilter || component is MeshRenderer))
                    throw new InvalidOperationException("Forbidden executable/physics component on perimeter");
                if (component is MeshRenderer renderer)
                {
                    if (renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null)
                        throw new InvalidOperationException("Missing actual Viking texture for " + assetKey);
                    rendered++;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }
            if (rendered == 0)
                throw new InvalidOperationException("No source mesh renderer on " + assetKey);
        }

        private static Mesh CreateRollingRidge()
        {
            const int Around = 64;
            float[] radii = { 32f, 45f, 67f, 106f };
            var vertices = new List<Vector3>(Around * radii.Length);
            var uv = new List<Vector2>(Around * radii.Length);
            var triangles = new List<int>(Around * 6 * (radii.Length - 1));
            for (int layer = 0; layer < radii.Length; layer++)
            {
                for (int k = 0; k < Around; k++)
                {
                    float angle = (2f * Mathf.PI * k) / Around;
                    float sin = Mathf.Sin(angle);
                    float cos = Mathf.Cos(angle);
                    // Rounded rolling ridgeline around the 27-m square arena; no hard 60-m ground edge.
                    float unit = Mathf.Max(Mathf.Abs(sin), Mathf.Abs(cos));
                    float wave = Mathf.Sin(angle * 3f + .4f) * 1.1f +
                        Mathf.Cos(angle * 7f + .7f) * .55f;
                    float radius = radii[layer] + (layer == 0 ? 0f : wave * 1.1f);
                    float x = radius * sin / unit;
                    float z = radius * cos / unit;
                    float y = layer == 0 ? -.14f :
                        layer == 1 ? 2.6f + wave * .65f :
                        layer == 2 ? 8.7f + wave * 1.4f :
                        8.2f + wave * 2.2f;
                    vertices.Add(new Vector3(x, y, z));
                    uv.Add(new Vector2((x + 60f) * (22f / 120f),
                        (z + 60f) * (22f / 120f)));
                }
            }
            for (int ring = 0; ring < radii.Length - 1; ring++)
            {
                for (int k = 0; k < Around; k++)
                {
                    int a = ring * Around + k;
                    int next = ring * Around + ((k + 1) % Around);
                    int outward = (ring + 1) * Around + k;
                    int outerNext = (ring + 1) * Around + ((k + 1) % Around);
                    // Winding faces upward. Presentation-only, no terrain or gameplay authority.
                    triangles.Add(a); triangles.Add(outward); triangles.Add(next);
                    triangles.Add(next); triangles.Add(outward); triangles.Add(outerNext);
                }
            }
            Mesh mesh = new Mesh { name = "ARE naturalized outer world horizon, visual only" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ValidateExisting(Transform walls, Transform forest, Transform ridge)
        {
            if (walls == null || forest == null || ridge == null ||
                walls.childCount != 5 || forest.childCount != Plantings.Length ||
                ridge.GetComponentsInChildren<MeshFilter>(true).Length != 1)
                throw new InvalidOperationException("Perimeter composition incomplete");
            int wallRenders=0, treeRenders=0, tris=0;
            foreach (Transform parent in new[] { walls, forest, ridge })
            {
                foreach (Component component in parent.GetComponentsInChildren<Component>(true))
                {
                    if (!(component is Transform || component is MeshFilter || component is MeshRenderer))
                        throw new InvalidOperationException("Non-passive component in perimeter: " + component.GetType().Name);
                    if (component is MeshRenderer renderer)
                    {
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null || renderer.sharedMaterial == null ||
                            renderer.sharedMaterial.mainTexture == null)
                            throw new InvalidOperationException("Perimeter not bound to real textured geometry");
                        tris += filter.sharedMesh.triangles.Length / 3;
                        if (parent == walls) wallRenders++;
                        if (parent == forest) treeRenders++;
                    }
                }
            }
            if (wallRenders < 35 || wallRenders > 90 ||
                treeRenders < 22 || treeRenders > 110 || tris > 650000)
                throw new InvalidOperationException("Village visual perimeter exceeds expected mobile budget");
        }
    }
}
