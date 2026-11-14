using System.Collections.Generic;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    /// <summary>Texture rules (splatmaps) and spawn rules (trees, grass, objects) of one biome.</summary>
    [CreateAssetMenu(menuName = "World Forge/Biome Profile", fileName = "WF_Biome")]
    public class BiomeProfile : ScriptableObject
    {
        [Tooltip("Applied in order; weights are normalised per splat pixel.")]
        public List<WFTextureRule> textureRules = new List<WFTextureRule>();
        [Range(0.5f, 4f), Tooltip("Higher = crisper transitions between texture layers.")]
        public float textureSharpness = 1.5f;

        [Tooltip("Processed in order. Earlier rules reserve space (clearRadius) for later ones, so put large objects first.")]
        public List<WFSpawnRule> spawnRules = new List<WFSpawnRule>();

        /// <summary>Unique TerrainLayers in rule order; index = splatmap channel.</summary>
        public List<TerrainLayer> CollectTerrainLayers(out int[] ruleToLayer)
        {
            var layers = new List<TerrainLayer>();
            ruleToLayer = new int[textureRules.Count];
            for (int r = 0; r < textureRules.Count; r++)
            {
                WFTextureRule rule = textureRules[r];
                ruleToLayer[r] = -1;
                if (rule == null || rule.terrainLayer == null) continue;
                int idx = layers.IndexOf(rule.terrainLayer);
                if (idx < 0) { layers.Add(rule.terrainLayer); idx = layers.Count - 1; }
                ruleToLayer[r] = idx;
            }
            return layers;
        }

        public int FindTextureRule(string ruleName)
        {
            if (string.IsNullOrEmpty(ruleName)) return -1;
            for (int r = 0; r < textureRules.Count; r++)
                if (textureRules[r] != null && textureRules[r].name == ruleName) return r;
            return -1;
        }
    }
}
