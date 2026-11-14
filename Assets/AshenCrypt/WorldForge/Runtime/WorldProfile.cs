using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    /// <summary>
    /// Complete, reproducible description of a world: grid, seed, height stack, paths, POIs and biome.
    /// Same profile + same seed = same world.
    /// </summary>
    [CreateAssetMenu(menuName = "World Forge/World Profile", fileName = "WF_WorldProfile")]
    public class WorldProfile : ScriptableObject
    {
        [Header("World grid")]
        [Range(1, 32)] public int tilesX = 4;
        [Range(1, 32)] public int tilesZ = 4;
        [Min(16f)] public float tileSize = 64f;
        [Min(4f)] public float maxHeight = 32f;
        [Tooltip("Per tile, must be 2^n + 1 (33, 65, 129, 257, 513, 1025).")]
        public int heightmapResolution = 129;
        [Tooltip("Splatmap resolution per tile, power of two (Unity requirement).")]
        public int alphamapResolution = 128;
        [Tooltip("Grass/detail resolution per tile.")]
        public int detailResolution = 128;
        public int detailResolutionPerPatch = 32;
        public int seed = 731904;
        public QualityPreset quality = QualityPreset.PC_ThirdPerson;

        [Header("Height stack (top to bottom)")]
        [SerializeReference] public List<WFHeightLayer> heightLayers = new List<WFHeightLayer>();

        [Header("Paths, POIs, biome")]
        public List<WFPath> paths = new List<WFPath>();
        public List<WFPoiDefinition> pois = new List<WFPoiDefinition>();
        public BiomeProfile biome;

        [Header("ARPG")]
        [Range(10f, 60f), Tooltip("Used by the walkability report and as NavMesh agent max slope.")]
        public float maxWalkableSlope = 38f;
        public bool createPoiObjects = true;
        [Tooltip("Write the integer core map (.bytes) after 'Generate All' / 'Respawn'.")]
        public bool exportCoreMapAfterBuild = true;
        [Tooltip("Requires the com.unity.ai.navigation package; detected automatically.")]
        public bool bakeNavMeshAfterBuild = false;

        [Header("Output")]
        public string rootObjectName = "WorldForge_World";
        [Tooltip("TerrainData assets are written here.")]
        public string outputFolder = "Assets/WorldForgeData/Generated";
        [Tooltip("Optional terrain material. Empty = URP 'Terrain/Lit' is created automatically when URP is active.")]
        public Material terrainMaterialOverride;

        public float WorldSizeX { get { return tilesX * tileSize; } }
        public float WorldSizeZ { get { return tilesZ * tileSize; } }
        public int GlobalSamplesX { get { return tilesX * (heightmapResolution - 1) + 1; } }
        public int GlobalSamplesZ { get { return tilesZ * (heightmapResolution - 1) + 1; } }
        public float SampleSpacing { get { return tileSize / (heightmapResolution - 1); } }

        public static int ValidHeightmapResolution(int value)
        {
            int[] allowed = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
            int best = allowed[0];
            foreach (int a in allowed) if (Math.Abs(a - value) < Math.Abs(best - value)) best = a;
            return best;
        }

        private void OnValidate()
        {
            heightmapResolution = ValidHeightmapResolution(heightmapResolution);
            alphamapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(16, alphamapResolution)), 16, 2048);
            detailResolution = Mathf.Clamp(detailResolution, 16, 4048);
            detailResolutionPerPatch = Mathf.Clamp(detailResolutionPerPatch, 8, 128);
            if (string.IsNullOrEmpty(rootObjectName)) rootObjectName = "WorldForge_World";
            if (string.IsNullOrEmpty(outputFolder) || !outputFolder.StartsWith("Assets")) outputFolder = "Assets/WorldForgeData/Generated";
        }

        /// <summary>Evaluates the full height stack on a fresh grid. Deterministic.</summary>
        public WFHeightContext EvaluateHeights(Action<string, float> progress = null)
        {
            var ctx = new WFHeightContext(this);
            int count = heightLayers != null ? heightLayers.Count : 0;
            for (int i = 0; i < count; i++)
            {
                WFHeightLayer layer = heightLayers[i];
                if (layer == null || !layer.enabled) continue;
                if (progress != null) progress(layer.DisplayName, i / (float)Math.Max(1, count));
                layer.Apply(ctx);
            }
            ctx.ClampToRange();
            return ctx;
        }
    }
}
