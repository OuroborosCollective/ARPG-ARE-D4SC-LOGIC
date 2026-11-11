using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Builds passive village visuals from the privately configured art catalog.
    /// Output goes to the git-ignored private overlay
    /// (Resources/ARE/Visuals); no licensed source asset is copied into versioned folders.
    /// Every static output is flattened to MeshFilter + MeshRenderer on LOD0, so no script,
    /// collider or LOD component of the vendor prefabs can reach the game.
    /// </summary>
    public static class AreVillageArtBuilder
    {
        private const string CatalogPath = "Assets/PrivateArt/entry-authoring.json";
        [System.Serializable] private sealed class ArtEntry { public string id; public string assetPath; }
        [System.Serializable] private sealed class ArtCatalog { public ArtEntry[] entries; }
        private static readonly Dictionary<string, string> Inputs = new Dictionary<string, string>();

        /// <summary>Resolve private source locations locally; source filenames never enter public code.</summary>
        private static void LoadCatalog()
        {
            Inputs.Clear();
            var catalog = JsonUtility.FromJson<ArtCatalog>(File.ReadAllText(CatalogPath));
            if (catalog == null || catalog.entries == null) throw new System.InvalidOperationException("Missing private art catalog entries");
            foreach (var entry in catalog.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || string.IsNullOrEmpty(entry.assetPath) ||
                    !entry.assetPath.StartsWith("Assets/PrivateArt/", System.StringComparison.Ordinal) || entry.assetPath.Contains(".."))
                    throw new System.InvalidOperationException("Invalid private art catalog entry");
                Inputs.Add(entry.id, entry.assetPath);
            }
        }

        /// <summary>Fail on absent content rather than accepting an incomplete scene.</summary>
        private static string InputPath(string key)
        {
            if (!Inputs.TryGetValue(key, out var path)) throw new System.InvalidOperationException("Missing private art key: " + key);
            return path;
        }
        private const string Output = "Assets/Resources/ARE/Visuals/";
        private const float HouseScale = 0.6f;

        /// <summary>Compose reviewed private inputs into passive village prefabs.</summary>
        [MenuItem("ARE ARPG/Art/Build Aschengrenze Village Visuals")]
        public static void Build()
        {
            LoadCatalog();
            Directory.CreateDirectory(Output + "Village");
            AssetDatabase.Refresh();

            Save("VillageSupplyHall", root => Piece(root, "building.01", Vector3.zero, 90f, HouseScale));
            Save("VillageWatchtower", root => Piece(root, "building.02", Vector3.zero, 0f, 0.5f));
            Save("VillageMarketStall", root => Piece(root, "building.03", Vector3.zero, 0f, 0.4f));
            string[] homes = { "building.04", "building.05", "building.06", "building.07", "building.08", "building.10" };
            for (int i = 0; i < homes.Length; i++)
            {
                string key = homes[i];
                Save("VillageHome" + i, root => Piece(root, key, Vector3.zero, 0f, HouseScale));
            }
            Save("VillageBackdrop", BuildBackdrop);
            AssetDatabase.SaveAssets();
            Debug.Log("ARE ARPG: Aschengrenze village visuals rebuilt in the private overlay.");
        }

        /// <summary>Houses frame the playable square; nothing here is collision or navigation.</summary>
        private static void BuildBackdrop(Transform root)
        {
            Ground(root);
            // West row, facing the square.
            Piece(root, "building.04", new Vector3(-24f, 0f, -16f), 90f, HouseScale);
            Piece(root, "building.05", new Vector3(-23.5f, 0f, 7f), 90f, HouseScale);
            Piece(root, "building.06", new Vector3(-24f, 0f, 16f), 90f, HouseScale);
            // East row.
            Piece(root, "building.07", new Vector3(22f, 0f, -12f), -90f, HouseScale);
            Piece(root, "building.08", new Vector3(21f, 0f, -1.5f), -90f, HouseScale);
            Piece(root, "building.09", new Vector3(22.5f, 0f, 9f), -90f, HouseScale);
            Piece(root, "building.10", new Vector3(22f, 0f, 20f), -90f, HouseScale);
            // North edge beside the pass.
            Piece(root, "building.11", new Vector3(-12f, 0f, 28f), 180f, HouseScale);
            Piece(root, "building.12", new Vector3(13f, 0f, 29f), 180f, HouseScale);
            Piece(root, "building.13", new Vector3(-24f, 0f, 29f), 135f, HouseScale);
            // South edge behind the camera's usual view.
            Piece(root, "building.14", new Vector3(9f, 0f, -23f), 0f, HouseScale);
            Piece(root, "building.15", new Vector3(-14f, 0f, -24f), 0f, HouseScale);
            Piece(root, "building.16", new Vector3(17f, 0f, -22f), -45f, HouseScale);

            string[] trees =
            {
                "tree.01", "tree.02", "tree.03",
                "tree.04", "tree.05", "tree.06"
            };
            Vector2[] spots =
            {
                new Vector2(-19f, -21f), new Vector2(15f, -17f), new Vector2(17f, 4.5f), new Vector2(-19f, 11f),
                new Vector2(8f, 22f), new Vector2(-7f, 23f), new Vector2(26f, 27f), new Vector2(-27f, -6f),
                new Vector2(4f, -18f), new Vector2(27f, -20f), new Vector2(-18f, 23f), new Vector2(19f, 14.5f)
            };
            for (int index = 0; index < spots.Length; index++)
                Piece(root, trees[index % trees.Length],
                    new Vector3(spots[index].x, 0f, spots[index].y), index * 47f, 0.55f);
            string[] bushes = { "bush.01", "bush.02", "bush.03", "bush.04" };
            Vector2[] bushSpots =
            {
                new Vector2(-17.5f, -13f), new Vector2(-18f, 3f), new Vector2(15f, -6f), new Vector2(16f, 17f),
                new Vector2(-6f, -17f), new Vector2(5f, 20.5f), new Vector2(-12f, 20f), new Vector2(12f, -20f)
            };
            for (int index = 0; index < bushSpots.Length; index++)
                Piece(root, bushes[index % bushes.Length],
                    new Vector3(bushSpots[index].x, 0f, bushSpots[index].y), index * 71f, 0.6f);
        }

        /// <summary>Trodden earth over the crypt slab; just below the road strips.</summary>
        internal static void Ground(Transform root)
        {
            string materialPath = Output + "Village/VillageGround.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(InputPath("ground.base"));
            material.mainTextureScale = new Vector2(22f, 22f);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(InputPath("ground.normal"));
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", new Vector2(22f, 22f));
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", 0.08f);
            material.color = new Color(0.62f, 0.60f, 0.58f);
            EditorUtility.SetDirty(material);

            var ground = new GameObject("Ground");
            ground.transform.SetParent(root, false);
            ground.transform.localPosition = new Vector3(0f, -0.32f, 0f);
            ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(120f, 120f, 1f);
            ground.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Save only to the ignored private overlay and release temporary scene objects.</summary>
        private static void Save(string key, System.Action<Transform> compose)
        {
            var root = new GameObject(key);
            try
            {
                compose(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, Output + key + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Adds the LOD0 meshes of one vendor asset as plain renderers. <paramref name="basePoint"/>
        /// is where the centre of the asset's footprint touches the ground.
        /// </summary>
        internal static void Piece(Transform root, string relativePath, Vector3 basePoint, float yaw, float scale)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(InputPath(relativePath));
            if (source == null)
            {
                throw new System.InvalidOperationException("Missing private art key: " + relativePath);
            }
            var lowerLods = new HashSet<Renderer>();
            foreach (LODGroup group in source.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                for (int level = 1; level < lods.Length; level++)
                    foreach (Renderer renderer in lods[level].renderers)
                        if (renderer != null) lowerLods.Add(renderer);
            }

            var piece = new GameObject(Path.GetFileNameWithoutExtension(relativePath));
            piece.transform.SetParent(root, false);
            Matrix4x4 toSource = source.transform.worldToLocalMatrix;
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.name;
                if (lowerLods.Contains(renderer) || name.EndsWith("_LOD1") || name.EndsWith("_LOD2") ||
                    name.EndsWith("_LOD3") || name.EndsWith("_SP") || name.Contains("Billboard"))
                    continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Matrix4x4 local = toSource * renderer.transform.localToWorldMatrix;
                var part = new GameObject(name);
                part.transform.SetParent(piece.transform, false);
                part.transform.localPosition = local.GetColumn(3);
                part.transform.localRotation = local.rotation;
                part.transform.localScale = local.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer copy = part.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                Bounds partBounds = TransformBounds(local, filter.sharedMesh.bounds);
                if (any) bounds.Encapsulate(partBounds); else bounds = partBounds;
                any = true;
            }
            if (!any)
            {
                Object.DestroyImmediate(piece);
                return;
            }
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            piece.transform.localRotation = rotation;
            piece.transform.localScale = Vector3.one * scale;
            piece.transform.localPosition = basePoint - rotation * (foot * scale);
        }

        /// <summary>Transform all eight mesh-bound corners, including rotated or scaled source meshes.</summary>
        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds source)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = source.extents;
                if ((corner & 1) != 0) offset.x = -offset.x;
                if ((corner & 2) != 0) offset.y = -offset.y;
                if ((corner & 4) != 0) offset.z = -offset.z;
                result.Encapsulate(matrix.MultiplyPoint3x4(source.center + offset));
            }
            return result;
        }
    }
}
