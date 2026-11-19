using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    // Editor-only inspection. No automatic approval, prefab mutation or world generation.
    public enum WFAssetIntakeKind { Protected, ReviewRequired, Spawn, Poi }

    public sealed class WFAssetIntakeVerdict
    {
        public readonly WFAssetIntakeKind kind;
        public readonly string reason;
        public readonly float clearanceRadius;
        public readonly Vector3 size;
        public WFAssetIntakeVerdict(WFAssetIntakeKind kind, string reason, Vector3 size)
        {
            this.kind = kind;
            this.reason = reason;
            this.size = size;
            // Half of horizontal footprint diagonal plus a one-metre clearance.
            clearanceRadius = 1f + 0.5f * Mathf.Sqrt(size.x * size.x + size.z * size.z);
        }
    }

    /// <summary>
    /// Conservative, package-specific proposal rules. Any unknown folder abstains.
    /// The package table is not a second catalog; accepted GUIDs persist on existing
    /// WFSpawnRule / WFPoiDefinition fields only.
    /// </summary>
    public static class WFAssetIntakeEvaluator
    {
        private struct PackageRule
        {
            public readonly string path;
            public readonly WFAssetIntakeKind kind;
            public PackageRule(string path, WFAssetIntakeKind kind)
            { this.path = path; this.kind = kind; }
        }

        // Only these explicit folders are permitted to suggest decorative use.
        private static readonly PackageRule[] SafePackages =
        {
            new PackageRule("Assets/PrivateArt/VisualPass1/VikingVillage/", WFAssetIntakeKind.Spawn),
            new PackageRule("Assets/PrivateArt/DesignAssets/StylizedFruitProps/", WFAssetIntakeKind.Spawn),
            new PackageRule("Assets/PrivateArt/DesignAssets/AlchemyLabProps/", WFAssetIntakeKind.Spawn),
            new PackageRule("Assets/PrivateArt/DesignAssets/SimpleMedievalProps/", WFAssetIntakeKind.Spawn),
            new PackageRule("Assets/PrivateArt/OwnerForestReview/Prefabs/", WFAssetIntakeKind.Spawn),
            new PackageRule("Assets/PrivateArt/FantasyKingdom3D/Prefabs/", WFAssetIntakeKind.Spawn)
        };
        private static readonly string[] ProtectedPackages =
        {
            "Assets/PrivateArt/WoodlandWeapons/",
            "Assets/PrivateArt/CombatIntake/",
            "Assets/PrivateArt/HunterBear/",
            "Assets/PrivateArt/Resources/"
        };
        // Runtime-owned names must never be converted into decorative spawn rules.
        private static readonly string[] ProtectedNames =
        {
            "EntryHero", "Enemy", "MillPatient", "CryptRattler", "Player",
            "Npc", "NPC", "Mara", "Ilyan", "Hunter", "Rattler"
        };

        public static bool IsProtectedPackage(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            foreach (string root in ProtectedPackages)
                if (assetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsGameBound(string name, ISet<string> runtimeBindings)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (runtimeBindings != null && runtimeBindings.Contains(name)) return true;
            foreach (string prefix in ProtectedNames)
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool HasProtectedHierarchy(GameObject prefab, ISet<string> runtimeBindings)
        {
            if (prefab == null) return true;
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                if (IsGameBound(t.name, runtimeBindings)) return true;
            return HasGameplayOrEffectComponents(prefab);
        }

        public static bool HasGameplayOrEffectComponents(GameObject prefab)
        {
            if (prefab == null) return true;
            if (prefab.GetComponentInChildren<Animator>(true) != null ||
                prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                prefab.GetComponentInChildren<ParticleSystem>(true) != null ||
                prefab.GetComponentInChildren<Animation>(true) != null)
                return true;
            foreach (Component c in prefab.GetComponentsInChildren<Component>(true))
            {
                if (c == null) return true; // Missing script cannot be silently admitted.
                if (c is MonoBehaviour) return true;
                Type componentType = c.GetType();
                if (componentType.FullName == "UnityEngine.VFX.VisualEffect") return true;
            }
            return false;
        }

        /// <summary>Real mesh-local geometry, not the unrelated prefab world position.</summary>
        public static Vector3 MeasureLocalSize(GameObject prefab)
        {
            if (prefab == null) return Vector3.zero;
            Bounds bounds = default(Bounds);
            bool hasAny = false;
            Matrix4x4 worldToRoot = prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null)
                    continue;
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 toRoot = worldToRoot * filter.transform.localToWorldMatrix;
                for (int ix = -1; ix <= 1; ix += 2)
                    for (int iy = -1; iy <= 1; iy += 2)
                        for (int iz = -1; iz <= 1; iz += 2)
                        {
                            Vector3 v = toRoot.MultiplyPoint3x4(b.center + Vector3.Scale(
                                b.extents, new Vector3(ix, iy, iz)));
                            if (!hasAny) { bounds = new Bounds(v, Vector3.zero); hasAny = true; }
                            else bounds.Encapsulate(v);
                        }
            }
            return hasAny ? bounds.size : Vector3.zero;
        }

        public static WFAssetIntakeVerdict Assess(GameObject prefab, string path,
                                                   ISet<string> runtimeBindings = null)
        {
            if (prefab == null || string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                return new WFAssetIntakeVerdict(WFAssetIntakeKind.ReviewRequired,
                    "Ungültiger oder nicht unterstützter GameObject-Assetpfad", Vector3.zero);

            if (IsProtectedPackage(path))
                return new WFAssetIntakeVerdict(WFAssetIntakeKind.Protected,
                    "Spielinhalte aus geschütztem Paket", Vector3.zero);

            if (HasProtectedHierarchy(prefab, runtimeBindings))
                return new WFAssetIntakeVerdict(WFAssetIntakeKind.Protected,
                    "Figur, Waffe, Partikel, Gameplay-Komponente oder gebundener Objektname", Vector3.zero);

            Vector3 size = MeasureLocalSize(prefab);
            if (size.x <= 0f || size.z <= 0f || float.IsNaN(size.x) || float.IsInfinity(size.x) ||
                float.IsNaN(size.y) || float.IsInfinity(size.y) ||
                float.IsNaN(size.z) || float.IsInfinity(size.z))
                return new WFAssetIntakeVerdict(WFAssetIntakeKind.ReviewRequired,
                    "Keine belastbaren Mesh-Renderer-Grenzen", size);

            foreach (PackageRule rule in SafePackages)
            {
                if (!path.StartsWith(rule.path, StringComparison.OrdinalIgnoreCase)) continue;
                // Buildings are landmarks/POIs, never automatic terrain-flattening.
                WFAssetIntakeKind kind = (size.magnitude > 12f || size.y > 8f)
                    ? WFAssetIntakeKind.Poi : rule.kind;
                return new WFAssetIntakeVerdict(kind,
                    "Paketregel " + rule.path + " und Renderer-Geometrie; nur Vorschlag", size);
            }
            return new WFAssetIntakeVerdict(WFAssetIntakeKind.ReviewRequired,
                "Unbekanntes Paket – manuelle Katalogprüfung nötig", size);
        }
    }

    public static class WFAssetIntakeApprovals
    {
        private static void ValidateGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid) || guid.Length != 32)
                throw new InvalidOperationException("ASSET_GUID_REQUIRED");
            foreach (char c in guid)
                if (!Uri.IsHexDigit(c)) throw new InvalidOperationException("INVALID_ASSET_GUID");
        }

        private static void Validate(GameObject prefab, string guid, WFAssetIntakeVerdict v)
        {
            ValidateGuid(guid);
            if (prefab == null || v == null || v.kind == WFAssetIntakeKind.Protected ||
                v.kind == WFAssetIntakeKind.ReviewRequired ||
                HasChangedSinceInspection(prefab))
                throw new InvalidOperationException("ASSET_NOT_APPROVED_FOR_DECORATION");
        }

        private static bool HasChangedSinceInspection(GameObject prefab)
        { return WFAssetIntakeEvaluator.HasGameplayOrEffectComponents(prefab); }

        public static void ConfirmSpawn(BiomeProfile biome, GameObject prefab, string guid,
                                         WFAssetIntakeVerdict verdict)
        {
            if (biome == null) throw new ArgumentNullException("biome");
            Validate(prefab, guid, verdict);
            if (verdict.kind != WFAssetIntakeKind.Spawn)
                throw new InvalidOperationException("SPAWN_CATEGORY_NOT_PROPOSED");
            foreach (WFSpawnRule r in biome.spawnRules)
                if (r != null && r.approvedAssetGuid == guid) return; // idempotent
            Undo.RecordObject(biome, "Confirm World Forge asset");
            var rule = new WFSpawnRule
            {
                name = "Asset_" + guid.Substring(0, 8), kind = SpawnKind.GameObject,
                enabled = true, approvedAssetGuid = guid, prefabs = new List<GameObject> { prefab },
                densityPer100m2 = 0.05f, minSpacing = Mathf.Max(3f, 2f * verdict.clearanceRadius),
                clearRadius = verdict.clearanceRadius,
                respectPoiClearance = true,
                coreObstacle = true, coreObstacleRadius = verdict.clearanceRadius
            };
            biome.spawnRules.Add(rule);
            EditorUtility.SetDirty(biome); // Save ownership remains with the editor/user.
        }

        public static void ConfirmPoi(WorldProfile world, GameObject prefab, string guid,
                                       WFAssetIntakeVerdict verdict)
        {
            if (world == null) throw new ArgumentNullException("world");
            Validate(prefab, guid, verdict);
            foreach (WFPoiDefinition r in world.pois)
                if (r != null && r.approvedAssetGuid == guid) return;
            Undo.RecordObject(world, "Confirm World Forge POI");
            var poi = new WFPoiDefinition
            {
                id = "asset." + guid, type = PoiType.Landmark,
                approvedAssetGuid = guid, decorativePrefab = prefab,
                intakePlacementPending = true, // configure position before actual terrain generation
                radius = verdict.clearanceRadius,
                flattenRadius = verdict.clearanceRadius,
                flattenFalloff = 2f,
                clearSpawnRadius = verdict.clearanceRadius + 1f
            };
            world.pois.Add(poi);
            EditorUtility.SetDirty(world);
        }
    }
}
