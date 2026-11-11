using System;
using System.IO;
using AreArpg.Core;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Composes private, passive first-two-act art against Core-owned footprints.</summary>
    public static class EntryWorldArtBuilder
    {
        private const string Output = "Assets/Resources/ARE/Visuals/";
        private static System.Collections.Generic.Dictionary<string, Material> _materials;

        /// <summary>Fail on missing reviewed inputs instead of publishing a partial blockout.</summary>
        [MenuItem("ARE ARPG/Art/Build Entry Worlds")]
        public static void Build()
        {
            EntryCombatArtBuilder.Prepare();
            AreVillageArtBuilder.Build();
            _materials = EntryMaterialBaker.Build();
            Save("EntryCryptWorld", Crypt);
            Save("EntryVillageWorld", Village);
            Save("EntryRation", root => Fit(root, "AshCrate", "Supply parcel", Vector3.zero, new Vector3(.28f, .23f, .28f)));
            Save("EntryHero", root => Actor(root, EntryCombatArtBuilder.HeroKey, "Hero", 1.9f));
            for (int i = 0; i < 3; i++)
            {
                int id = i;
                Save("EntryNpc" + id, root => Actor(root, id == 2 ? "Hero" : "Npc" + id, "Npc" + id, id == 2 ? 2.05f : 1.9f));
                Save("EntryEnemy" + id, root => Actor(root, "Enemy" + id, "Enemy" + id, id == 2 ? 2.6f : 1.6f));
            }
            AssetDatabase.SaveAssets();
            AuditInputs();
            Debug.Log("ARE_ENTRY_ART_BUILT: crypt, village, supply parcels; private render-only prefabs.");
        }

        /// <summary>Preserve character proportions, ground the feet and bind the existing passive animation controller.</summary>
        private static void Actor(Transform parent, string sourceKey, string paletteKey, float height)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Output + sourceKey + ".prefab");
            if (source == null) throw new InvalidOperationException("Missing private actor: " + sourceKey);
            var art = UnityEngine.Object.Instantiate(source, parent, false);
            art.transform.localPosition = Vector3.zero;
            foreach (var animator in art.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null && animator.avatar != null && animator.avatar.isHuman)
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "Hero.prefab").GetComponentInChildren<Animator>(true).runtimeAnimatorController;
                animator.applyRootMotion = false;
            }
            if (!(paletteKey == "Hero" && EntryCombatArtBuilder.HasHero) && _materials.TryGetValue(paletteKey, out var material))
                foreach (Renderer renderer in art.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }
            if (paletteKey == "Hero") EntryCombatArtBuilder.BindHero(art);
            Bounds bounds = RenderBounds(art);
            art.transform.localScale *= height / bounds.size.y;
            bounds = RenderBounds(art);
            art.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        /// <summary>Write import diagnostics to the private Editor log for visual integration review.</summary>
        private static void AuditInputs()
        {
            foreach (string key in new[] { "Hero", "Npc0", "Npc1", "Npc2", "AshStoneFloor", "AshCrate", "EntryHero" })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Output + key + ".prefab");
                if (source == null) continue;
                var instance = UnityEngine.Object.Instantiate(source);
                try
                {
                    Debug.Log("ENTRY_AUDIT " + key + " scale=" + instance.transform.localScale + " bounds=" + RenderBounds(instance));
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                        foreach (Material material in renderer.sharedMaterials)
                            Debug.Log("ENTRY_MATERIAL " + key + " renderer=" + renderer.name + " material=" +
                                (material == null ? "MISSING" : material.name + " shader=" + material.shader.name + " texture=" +
                                (material.mainTexture == null ? "NONE" : material.mainTexture.name)));
                    foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
                        Debug.Log("ENTRY_ANIMATOR " + key + " avatar=" + (animator.avatar != null) + " controller=" +
                            (animator.runtimeAnimatorController == null ? "NONE" : animator.runtimeAnimatorController.name));
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }

        /// <summary>Vault floor, perimeter masonry and the four established Core pillars.</summary>
        private static void Crypt(Transform root)
        {
            for (int x = -4; x <= 4; x++)
                for (int z = -4; z <= 4; z++)
                    Fit(root, EntryCombatArtBuilder.Key("Floor", "AshStoneFloor"), "Vault paving", new Vector3(x * 6f, -.3f, z * 6f), new Vector3(6f, .28f, 6f));
            for (int segment = -3; segment <= 3; segment++)
            {
                Fit(root, EntryCombatArtBuilder.Key("Wall", "AshStoneWall"), "West vault wall", new Vector3(-28f, 0f, segment * 8f), new Vector3(1.2f, 4.2f, 8f), EntryCombatArtBuilder.WallYaw);
                Fit(root, EntryCombatArtBuilder.Key("Wall", "AshStoneWall"), "East vault wall", new Vector3(28f, 0f, segment * 8f), new Vector3(1.2f, 4.2f, 8f), EntryCombatArtBuilder.WallYaw);
                Fit(root, EntryCombatArtBuilder.Key("Wall", "AshStoneWall"), "South vault wall", new Vector3(segment * 8f, 0f, -28f), new Vector3(8f, 4.2f, 1.2f));
                if (segment != 0) Fit(root, EntryCombatArtBuilder.Key("Wall", "AshStoneWall"), "North vault wall", new Vector3(segment * 8f, 0f, 28f), new Vector3(8f, 4.2f, 1.2f));
            }
            foreach (int x in new[] { -7, 7 })
                foreach (int z in new[] { -7, 7 })
                    Fit(root, EntryCombatArtBuilder.Key("Pillar", "AshMonument"), "Vault monument", new Vector3(x, 0, z), new Vector3(2.4f, 3.8f, 2.4f));
            Fit(root, EntryCombatArtBuilder.Key("Arch", "AshRuin"), "Crypt exit arch", new Vector3(0, 0, 25), new Vector3(5.5f, 4f, 2f));
            foreach (int side in new[] { -1, 1 })
                Fit(root, "AshLantern", "Exit lantern", new Vector3(side * 3.2f, 0, 23), new Vector3(.6f, 1.7f, .6f));
        }

        /// <summary>Approach road and square framed by Kingdom houses; opaque buildings match collision bounds.</summary>
        private static void Village(Transform root)
        {
            AreVillageArtBuilder.Ground(root);
            string[] keys = { "VillageSupplyHall", "VillageWatchtower", "VillageMarketStall", "VillageMarketStall" };
            string[] labels = { "Aschengrenze_Supply_Hall", "Aschengrenze_Watchtower", "Aschengrenze_Market_West", "Aschengrenze_Market_East" };
            float[] heights = { 5f, 7f, 3f, 3f };
            for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
            {
                var b = EntryWorldLayout.Building(i);
                Fit(root, i < 4 ? keys[i] : "VillageHome" + (i - 4), i < 4 ? labels[i] : b.Id,
                    new Vector3(b.X / 1000f, 0, b.Z / 1000f),
                    new Vector3(b.HalfWidth / 500f, i < 4 ? heights[i] : 6f, b.HalfDepth / 500f));
            }
            for (int z = -24; z <= 12; z += 4)
                Fit(root, "AshStoneFloor", "Old village road", new Vector3(0, -.25f, z), new Vector3(3.8f, .23f, 4f));
            for (int x = -10; x <= -2; x += 4)
                Fit(root, "AshStoneFloor", "Supply court paving", new Vector3(x, -.24f, -11f), new Vector3(4f, .23f, 2f));
            string[] homes = { "building.04", "building.05", "building.06", "building.07", "building.08", "building.10" };
            for (int i = 0; i < homes.Length; i++)
            {
                int side = i < 3 ? -1 : 1;
                AreVillageArtBuilder.Piece(root, homes[i],
                    new Vector3(side * 35, 0, -14 + (i % 3) * 15), -side * 90, .6f);
            }
            for (int i = 0; i < 8; i++)
                AreVillageArtBuilder.Piece(root, "tree.01",
                    new Vector3(i % 2 == 0 ? -31 : 31, 0, -25 + (i / 2) * 14), i * 37, .5f);
            // Low, traversable undergrowth dresses the road without inventing solid obstacles.
            Vector2[] shrubs = { new Vector2(-6, -21), new Vector2(-7, -16), new Vector2(5, -19),
                new Vector2(7, -9), new Vector2(9, 7), new Vector2(-18, -14), new Vector2(-18, 4),
                new Vector2(5, -24), new Vector2(-4, -25), new Vector2(7, 3), new Vector2(-5, 3) };
            for (int i = 0; i < shrubs.Length; i++)
                AreVillageArtBuilder.Piece(root, i % 2 == 0 ? "bush.01" : "bush.02",
                    new Vector3(shrubs[i].x, 0, shrubs[i].y), i * 41f, .48f);
            Fit(root, "AshCrate", "Mara supply chest",
                new Vector3(ArenaSimulation.CampXmm / 1000f + 1.5f, 0, ArenaSimulation.CampZmm / 1000f + .5f), new Vector3(1.4f, .5f, 1.2f));
        }

        /// <summary>Normalize rendered bounds into the designated presentation volume without physics.</summary>
        private static void Fit(Transform parent, string key, string label, Vector3 foot, Vector3 size, float yaw = 0f)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Output + key + ".prefab");
            if (source == null) throw new InvalidOperationException("Missing private entry asset: " + key);
            var holder = new GameObject(label);
            holder.transform.SetParent(parent, false);
            GameObject art = UnityEngine.Object.Instantiate(source, holder.transform, false);
            art.transform.localPosition = Vector3.zero;
            art.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            if (_materials.TryGetValue(key, out var material))
                foreach (Renderer renderer in art.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }
            Bounds bounds = RenderBounds(art);
            if (bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0)
                throw new InvalidOperationException("Empty entry art: " + key);
            holder.transform.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            Vector3 basePoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            holder.transform.localPosition = foot - Vector3.Scale(basePoint, holder.transform.localScale);
        }

        /// <summary>Collect actual imported mesh extents; no nominal primitive dimensions.</summary>
        private static Bounds RenderBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Entry asset has no renderer: " + root.name);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>Validate every component before writing the private prefab.</summary>
        private static void Save(string key, Action<Transform> compose)
        {
            var root = new GameObject(key);
            try
            {
                compose(root.transform);
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    if (!(component is Transform || component is MeshFilter || component is Renderer || component is LODGroup || component is Animator))
                        throw new InvalidOperationException("Non-passive entry art: " + key);
                if (PrefabUtility.SaveAsPrefabAsset(root, Output + key + ".prefab") == null)
                    throw new IOException("Could not save private entry art: " + key);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
