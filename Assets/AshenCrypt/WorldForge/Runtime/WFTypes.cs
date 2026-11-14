using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    public enum QualityPreset { PC_ThirdPerson, Android_ThirdPerson }

    public enum PoiType { Landmark, Settlement, NpcHome, NpcWorkplace, SpawnZone, DungeonEntrance, Waypoint, Shrine, ResourceNode }

    public enum PathMode { Ignore, OnlyNearPath, AvoidPath }

    public enum CurvatureMode { Ignore, Concave, Convex }

    public enum SpawnKind { TerrainTree, Detail, GameObject }

    /// <summary>A road or trail through the world. Points are world X/Z in metres, origin = south-west corner.</summary>
    [Serializable]
    public class WFPath
    {
        public string name = "Path";
        public bool enabled = true;
        public List<Vector2> points = new List<Vector2>();
        [Min(0.5f)] public float width = 4f;
        [Tooltip("How far the path bed is pressed into the terrain (metres).")]
        public float carveDepth = 0.5f;
        [Tooltip("Small raised shoulder on both sides of the path (metres).")]
        public float shoulderHeight = 0.25f;
        [Range(0f, 1f), Tooltip("How strongly the cross-slope is flattened under the path.")]
        public float flatten = 0.7f;
        [Tooltip("Subdivide the polyline as a Catmull-Rom spline.")]
        public bool smooth = true;
    }

    /// <summary>
    /// Point of interest. Becomes an ForgedPoi component in the scene that gameplay,
    /// quest and NPC-lifecycle systems can query (homes, workplaces, spawn zones, dungeon entrances...).
    /// </summary>
    [Serializable]
    public class WFPoiDefinition
    {
        public string id = "poi";
        public PoiType type = PoiType.Landmark;
        public Vector2 position;
        [Min(0f)] public float radius = 8f;
        [Min(0f), Tooltip("Flatten the terrain inside this radius (0 = off). Use for camps, villages, entrances.")]
        public float flattenRadius = 0f;
        [Min(0f)] public float flattenFalloff = 6f;
        [Min(0f), Tooltip("No trees, details or objects are spawned inside this radius.")]
        public float clearSpawnRadius = 10f;
        [Tooltip("Free-form comma separated tags, e.g. 'npc:smith, faction:ashborn'.")]
        public string tags = "";
    }

    /// <summary>Shared mask settings used by texture rules and spawn rules.</summary>
    [Serializable]
    public class WFMask
    {
        public bool useHeight = false;
        public float heightMin = 0f;
        public float heightMax = 1000f;
        [Min(0f)] public float heightFalloff = 1f;

        public bool useSlope = false;
        [Range(0f, 90f)] public float slopeMinDeg = 0f;
        [Range(0f, 90f)] public float slopeMaxDeg = 90f;
        [Min(0f)] public float slopeFalloffDeg = 4f;

        public bool useNoise = false;
        [Min(0.5f)] public float noiseScale = 24f;
        [Range(0f, 1f)] public float noiseThreshold = 0.5f;
        [Min(0.01f)] public float noiseContrast = 6f;
        public int noiseSeedOffset = 0;
        public bool invertNoise = false;

        public PathMode pathMode = PathMode.Ignore;
        [Min(0f), Tooltip("Metres from the path centre line.")]
        public float pathDistance = 3f;
        [Min(0f)] public float pathFalloff = 2f;
        [Min(0f), Tooltip("OnlyNearPath: also keep this distance from the centre line (roadside props).")]
        public float pathInnerClearance = 0f;

        public CurvatureMode curvatureMode = CurvatureMode.Ignore;
        [Min(0f)] public float curvatureStrength = 4f;
    }

    [Serializable]
    public class WFTextureRule
    {
        public string name = "Layer";
        public bool enabled = true;
        public TerrainLayer terrainLayer;
        [Min(0f)] public float weight = 1f;
        public WFMask mask = new WFMask();
    }

    [Serializable]
    public class WFSpawnRule
    {
        public string name = "Spawner";
        public bool enabled = true;
        public SpawnKind kind = SpawnKind.TerrainTree;

        [Header("Content")]
        [Tooltip("TerrainTree: prefabs with a LODGroup or a MeshRenderer on the root. GameObject: any prefab.")]
        public List<GameObject> prefabs = new List<GameObject>();
        [Tooltip("Detail only: grass/billboard texture (used when no prefab is given).")]
        public Texture2D detailTexture;

        [Header("Distribution")]
        [Min(0f), Tooltip("Target instances per 100 m² before masks are applied.")]
        public float densityPer100m2 = 1f;
        [Min(0.1f), Tooltip("Minimum distance between two instances of this rule (trees/objects).")]
        public float minSpacing = 3f;
        [Min(0f), Tooltip("Instances of later rules keep this distance from instances of this rule.")]
        public float clearRadius = 1f;
        [Min(0)] public int maxCount = 0;
        public int seedOffset = 0;
        public WFMask mask = new WFMask();
        [Tooltip("Optional: only spawn where this texture rule (by name) has at least the given weight.")]
        public string requiredTextureRule = "";
        [Range(0f, 1f)] public float requiredTextureWeight = 0.35f;
        public bool respectPoiClearance = true;

        [Header("Transform")]
        public float scaleMin = 0.85f;
        public float scaleMax = 1.2f;
        public bool randomYaw = true;
        [Range(0f, 1f)] public float alignToNormal = 0f;
        [Tooltip("Push the object into the ground (metres) to hide floating pivots on slopes.")]
        public float sinkDepth = 0.05f;

        [Header("GameObject options")]
        public bool markBatchingStatic = true;
        public bool addMeshColliderIfMissing = false;

        [Header("Core map (authoritative simulation)")]
        [Tooltip("Export instances of this rule as movement obstacles into the core map.")]
        public bool coreObstacle = true;
        [Min(0f), Tooltip("0 = automatic (terrain trees: 0.3 m × scale, objects: from renderer bounds).")]
        public float coreObstacleRadius = 0f;
        [Tooltip("Instances also block line of sight in the core map (tall trees, walls).")]
        public bool coreBlocksSight = false;

        [Header("Detail options")]
        public bool detailAsMesh = false;
        public float detailMinWidth = 0.6f;
        public float detailMaxWidth = 1.1f;
        public float detailMinHeight = 0.35f;
        public float detailMaxHeight = 0.8f;
        public Color detailHealthyColor = new Color(0.55f, 0.56f, 0.5f, 1f);
        public Color detailDryColor = new Color(0.36f, 0.35f, 0.32f, 1f);
        [Range(0, 16)] public int detailMaxPerCell = 6;
    }
}
