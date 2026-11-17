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
        internal static void LoadCatalog()
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
        /// <summary>Visible village ground. Houses, trees, shrubs and grounded actors stand at this height.</summary>
        internal const float GroundSurfaceY = 0f;
        /// <summary>Paving tops sit just above the ground so roads read as laid stones, not as a raised slab.</summary>
        internal const float PavingRise = 0.02f;

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


        [System.Serializable]
        private sealed class HeroPlan
        {
            public string schema;
            public bool visualOnly;
            public HeroPlacement[] placements;
        }

        [System.Serializable]
        private sealed class HeroPlacement
        {
            public string id;
            public string sourceKey;
            public int xMm, zMm;
            public int maxWidthMm, maxHeightMm, maxDepthMm, yawQuarterTurns;
            public bool passageArch;
        }

        private const string HeroLayoutPath = "docs/visual-pass-1/village-hero-layout.json";
        private const string HeroDecorationRoot = "VillageHeroPass1";

        /// <summary>
        /// Add only eight approved passive source compositions to the existing privately
        /// built village prefab. No gameplay, physics, UI, save or original vendor asset is edited.
        /// Existing hero compositions are verified but never overwritten.
        /// </summary>
        [MenuItem("ARE ARPG/Art/Apply Village Hero View")]
        public static void ApplyVillageHeroView()
        {
            LoadCatalog();
            var plan = JsonUtility.FromJson<HeroPlan>(File.ReadAllText(HeroLayoutPath));
            if (plan == null || plan.schema != "ashen-crypt.visual-pass-1-village-hero.v1" ||
                !plan.visualOnly || plan.placements == null || plan.placements.Length != 8)
                throw new System.InvalidOperationException("Village hero plan schema/ownership invalid");

            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            int arches = 0;
            foreach (HeroPlacement item in plan.placements)
            {
                ValidateHeroPlacement(item, ids);
                if (item.passageArch) arches++;
                // Fail before loading or saving a prefab if a source is absent.
                if (AssetDatabase.LoadAssetAtPath<GameObject>(InputPath(item.sourceKey)) == null)
                    throw new System.InvalidOperationException("Missing private hero art: " + item.sourceKey);
            }
            if (arches != 1) throw new System.InvalidOperationException("Hero view requires exactly one passage arch");

            string path = Output + "EntryVillageWorld.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new System.InvalidOperationException("Missing privately authored EntryVillageWorld");
            GameObject loaded = PrefabUtility.LoadPrefabContents(path);
            bool modified = false;
            try
            {
                Transform existing = loaded.transform.Find(HeroDecorationRoot);
                if (existing != null)
                {
                    ValidateHeroChildren(existing, ids);
                    Debug.Log("ARE_VILLAGE_HERO_REUSE validated previous eight passive placements");
                    return;
                }
                GameObject holder = new GameObject(HeroDecorationRoot);
                holder.transform.SetParent(loaded.transform, false);
                foreach (HeroPlacement item in plan.placements)
                {
                    int before = holder.transform.childCount;
                    Vector3 target = new Vector3(item.xMm / 1000f, 0f, item.zMm / 1000f);
                    Piece(holder.transform, item.sourceKey, target, 90f * item.yawQuarterTurns, 1f);
                    if (holder.transform.childCount != before + 1)
                        throw new System.InvalidOperationException("Hero source has no LOD0 geometry: " + item.id);
                    Transform composed = holder.transform.GetChild(before);
                    composed.name = item.id;
                    Bounds bounds = HeroBounds(composed, out int renderers);
                    if (renderers == 0 || bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0)
                        throw new System.InvalidOperationException("Hero source is empty: " + item.id);
                    float uniform = Mathf.Min(4f, Mathf.Min(item.maxWidthMm / (1000f * bounds.size.x),
                        Mathf.Min(item.maxHeightMm / (1000f * bounds.size.y),
                            item.maxDepthMm / (1000f * bounds.size.z))));
                    if (uniform <= 0f || float.IsNaN(uniform) || float.IsInfinity(uniform))
                        throw new System.InvalidOperationException("Invalid hero scaling: " + item.id);
                    composed.localScale *= uniform;
                    bounds = HeroBounds(composed, out renderers);
                    composed.position += new Vector3(target.x - bounds.center.x,
                        target.y - bounds.min.y, target.z - bounds.center.z);
                    bounds = HeroBounds(composed, out renderers);
                    if (Mathf.Abs(bounds.min.y - target.y) > .015f ||
                        Mathf.Abs(bounds.center.x - target.x) > .015f ||
                        Mathf.Abs(bounds.center.z - target.z) > .015f ||
                        bounds.size.x * 1000f > item.maxWidthMm + 15 ||
                        bounds.size.y * 1000f > item.maxHeightMm + 15 ||
                        bounds.size.z * 1000f > item.maxDepthMm + 15)
                        throw new System.InvalidOperationException("Hero renderer outside approved envelope: " + item.id);
                }
                ValidateHeroChildren(holder.transform, ids);
                if (PrefabUtility.SaveAsPrefabAsset(loaded, path) == null)
                    throw new System.IO.IOException("Saving the private village hero prefab failed");
                modified = true;
            }
            finally { PrefabUtility.UnloadPrefabContents(loaded); }
            if (modified)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.transform.Find(HeroDecorationRoot) == null ||
                    prefab.transform.Find(HeroDecorationRoot).childCount != 8)
                    throw new System.IO.IOException("Independent native prefab readback failed");
                Debug.Log("ARE_VILLAGE_HERO_BUILT 8 passive placements, original Core/collision/scene unchanged");
            }
        }

        private static void ValidateHeroPlacement(HeroPlacement item, HashSet<string> ids)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id) ||
                string.IsNullOrWhiteSpace(item.sourceKey) ||
                !item.sourceKey.StartsWith("hero.", System.StringComparison.Ordinal) ||
                item.sourceKey.Contains("/") || item.sourceKey.Contains(".."))
                throw new System.InvalidOperationException("Duplicate or invalid hero placement identifier");
            if (item.maxWidthMm < 1 || item.maxWidthMm > 7000 ||
                item.maxHeightMm < 1 || item.maxHeightMm > 6500 ||
                item.maxDepthMm < 1 || item.maxDepthMm > 5500 ||
                (item.maxWidthMm & 1) != 0 || (item.maxDepthMm & 1) != 0 ||
                item.yawQuarterTurns < 0 || item.yawQuarterTurns > 3 ||
                Mathf.Abs(item.xMm) > 35000 || Mathf.Abs(item.zMm) > 35000)
                throw new System.InvalidOperationException("Invalid hero millimetre bounds: " + item.id);
            long hw = item.maxWidthMm / 2;
            long hd = item.maxDepthMm / 2;
            if (item.passageArch)
            {
                if (item.id != "north-gate" || item.xMm != 0 || item.zMm != 24000 ||
                    item.maxWidthMm != 6400 || item.maxDepthMm != 2000)
                    throw new System.InvalidOperationException("Only canonical north gate may cover the passage");
            }
            else if ((long)System.Math.Abs(item.xMm) - hw < 3500 &&
                     (long)item.zMm - hd < 24000 && (long)item.zMm + hd > -24000)
                throw new System.InvalidOperationException("Hero props obstruct the arrival corridor: " + item.id);
            for (int i = 0; i < AreArpg.Core.EntryWorldLayout.BuildingCount; i++)
            {
                var building = AreArpg.Core.EntryWorldLayout.Building(i);
                if (System.Math.Abs((long)item.xMm - building.X) < hw + building.HalfWidth + 500 &&
                    System.Math.Abs((long)item.zMm - building.Z) < hd + building.HalfDepth + 500)
                    throw new System.InvalidOperationException("Hero art overlaps Core building " + building.Id);
            }
        }

        private static Bounds HeroBounds(Transform root, out int renderers)
        {
            Bounds bounds = new Bounds();
            renderers = 0;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter mesh = renderer.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null)
                    throw new System.InvalidOperationException("Hero source has a missing mesh");
                if (renderer.sharedMaterials.Length == 0)
                    throw new System.InvalidOperationException("Hero source has no materials");
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null || material.shader == null ||
                        material.shader.name == "Hidden/InternalErrorShader")
                        throw new System.InvalidOperationException("Hero source has a broken shader/material");
                if (renderers == 0) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                renderers++;
            }
            return bounds;
        }

        private static void ValidateHeroChildren(Transform holder, HashSet<string> ids)
        {
            if (holder.childCount != ids.Count)
                throw new System.InvalidOperationException("Wrong village hero composition count");
            int totalRenderers = 0;
            foreach (Transform entry in holder)
            {
                if (!ids.Contains(entry.name))
                    throw new System.InvalidOperationException("Unexpected village hero art: " + entry.name);
                HeroBounds(entry, out int renderers);
                totalRenderers += renderers;
                foreach (Component component in entry.GetComponentsInChildren<Component>(true))
                    if (!(component is Transform || component is MeshFilter || component is MeshRenderer))
                        throw new System.InvalidOperationException("Non-passive village hero component: " + component.GetType().Name);
            }
            if (totalRenderers == 0)
                throw new System.InvalidOperationException("No visible village hero geometry");
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

        /// <summary>Trodden earth at the height every village piece stands on; road strips rise only slightly above it.</summary>
        internal static void Ground(Transform root)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(InputPath("ground.base"));
            if (texture == null) throw new System.InvalidOperationException("Missing required ground texture");
            string materialPath = Output + "Village/VillageGround.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.mainTexture = texture;
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
            ground.transform.localPosition = new Vector3(0f, GroundSurfaceY, 0f);
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
