using System.Collections.Generic;
using AshenCrypt.WorldForge.Core;
using UnityEngine;

namespace AshenCrypt.WorldForge
{
    /// <summary>Root component of a generated world. Gives runtime code fast access to tiles and heights.</summary>
    [DisallowMultipleComponent]
    public class ForgedWorld : MonoBehaviour
    {
        public WorldProfile profile;
        public int tilesX;
        public int tilesZ;
        public float tileSize;
        [Tooltip("Row-major: index = z * tilesX + x.")]
        public List<Terrain> tiles = new List<Terrain>();
        [Tooltip("Integer map for the authoritative simulation, written by 'Export Core Map'.")]
        public TextAsset coreMap;

        private ForgedWorldMap _cachedMap;

        /// <summary>Parses (once) and returns the core map. Throws FormatException if the asset is corrupt.</summary>
        public ForgedWorldMap LoadCoreMap()
        {
            if (_cachedMap == null && coreMap != null) _cachedMap = ForgedWorldMap.FromBytes(coreMap.bytes);
            return _cachedMap;
        }

        public Terrain GetTile(int x, int z)
        {
            if (x < 0 || z < 0 || x >= tilesX || z >= tilesZ) return null;
            int i = z * tilesX + x;
            return i < tiles.Count ? tiles[i] : null;
        }

        public Terrain GetTerrainAt(Vector3 worldPosition)
        {
            if (tileSize <= 0f) return null;
            Vector3 local = worldPosition - transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / tileSize), 0, Mathf.Max(0, tilesX - 1));
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / tileSize), 0, Mathf.Max(0, tilesZ - 1));
            return GetTile(x, z);
        }

        /// <summary>Terrain height in world space at the given XZ position.</summary>
        public float SampleHeight(Vector3 worldPosition)
        {
            Terrain t = GetTerrainAt(worldPosition);
            return t != null ? t.SampleHeight(worldPosition) + t.transform.position.y : transform.position.y;
        }

        public Vector3 Snap(Vector3 worldPosition)
        {
            worldPosition.y = SampleHeight(worldPosition);
            return worldPosition;
        }
    }
}
