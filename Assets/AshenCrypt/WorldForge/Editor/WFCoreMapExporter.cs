using System.Collections.Generic;
using System.IO;
using AshenCrypt.WorldForge.Core;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    /// <summary>
    /// Bakes the current terrain, spawned obstacles and POIs into an integer ForgedWorldMap (.bytes)
    /// that the engine-free simulation can load. Unity colliders stay presentation only.
    /// </summary>
    public static class WFCoreMapExporter
    {
        public const float DefaultTreeTrunkRadius = 0.3f;

        public static WFBuildReport Export(WorldProfile p)
        {
            var report = new WFBuildReport();
            ForgedWorld world = WFTerrainBuilder.FindWorld(p);
            if (world == null) { report.warnings.Add("No generated world for this profile in the open scene."); return report; }
            ForgedWorldMap map = Build(p, world, report);
            WriteAsset(p, world, map, report);
            return report;
        }

        public static ForgedWorldMap Build(WorldProfile p, ForgedWorld world, WFBuildReport report)
        {
            WFHeightContext ctx = WFTerrainBuilder.ReadFromTerrains(p, world);
            float spacingM = ctx.Spacing;
            int spacingMm = Mathf.RoundToInt(spacingM * 1000f);
            if (Mathf.Abs(spacingM * 1000f - spacingMm) > 0.01f)
                report.warnings.Add("Height sample spacing " + spacingM + " m is not a whole number of millimetres; core map is " +
                                    "off by up to " + Mathf.Abs(spacingM * 1000f - spacingMm) * (ctx.Width - 1) + " mm at the far edge.");
            Vector3 origin = world.transform.position;
            long ox = Mm(origin.x), oz = Mm(origin.z);
            int oy = (int)Mm(origin.y);

            int nx = ctx.Width, nz = ctx.Height;
            var heights = new int[nx * nz];
            for (int i = 0; i < heights.Length; i++) heights[i] = (int)Mm(ctx.Heights[i]) + oy;

            int cx = nx - 1, cz = nz - 1, walkable = 0;
            var walk = new bool[cx * cz];
            float tanMax = Mathf.Tan(p.maxWalkableSlope * Mathf.Deg2Rad);
            for (int z = 0; z < cz; z++)
                for (int x = 0; x < cx; x++)
                {
                    float h00 = ctx.Heights[z * nx + x], h10 = ctx.Heights[z * nx + x + 1];
                    float h01 = ctx.Heights[(z + 1) * nx + x], h11 = ctx.Heights[(z + 1) * nx + x + 1];
                    float gx = ((h10 + h11) - (h00 + h01)) / (2f * spacingM);
                    float gz = ((h01 + h11) - (h00 + h10)) / (2f * spacingM);
                    bool ok = gx * gx + gz * gz <= tanMax * tanMax;
                    walk[z * cx + x] = ok;
                    if (ok) walkable++;
                }

            var obstacles = new List<ForgedObstacle>();
            CollectTreeObstacles(p, world, origin, obstacles);
            CollectObjectObstacles(p, world, obstacles);

            var pois = new List<ForgedPoiRecord>();
            if (p.pois != null)
                foreach (WFPoiDefinition d in p.pois)
                {
                    if (d == null) continue;
                    pois.Add(new ForgedPoiRecord(d.id, (ForgedPoiKind)(byte)d.type, ox + Mm(d.position.x), oz + Mm(d.position.y), (int)Mm(d.radius), d.tags));
                }

            var map = new ForgedWorldMap(ox, oz, spacingMm, nx, nz, heights, walk, obstacles, pois);
            report.perRule["Core map walkable cells %"] = Mathf.RoundToInt(100f * walkable / Mathf.Max(1, walk.Length));
            report.perRule["Core map obstacles"] = obstacles.Count;
            report.perRule["Core map POIs"] = pois.Count;
            return map;
        }

        private static void WriteAsset(WorldProfile p, ForgedWorld world, ForgedWorldMap map, WFBuildReport report)
        {
            WFEditorUtil.EnsureFolder(p.outputFolder);
            string assetPath = p.outputFolder + "/WF_" + WFEditorUtil.SafeName(p.name) + "_CoreMap.bytes";
            string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            File.WriteAllBytes(abs, map.ToBytes());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            world.coreMap = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            EditorUtility.SetDirty(world);
            report.notes.Add("Core map written: " + assetPath + "  (FNV-1a 0x" + map.ContentHash.ToString("x16") + ")");
        }

        private static void CollectTreeObstacles(WorldProfile p, ForgedWorld world, Vector3 origin, List<ForgedObstacle> outList)
        {
            if (p.biome == null) return;
            foreach (Terrain t in world.tiles)
            {
                if (t == null || t.terrainData == null) continue;
                TerrainData d = t.terrainData;
                TreePrototype[] protos = d.treePrototypes;
                var ruleOfProto = new WFSpawnRule[protos.Length];
                for (int i = 0; i < protos.Length; i++)
                    foreach (WFSpawnRule r in p.biome.spawnRules)
                        if (r != null && r.kind == SpawnKind.TerrainTree && r.prefabs.Contains(protos[i].prefab)) { ruleOfProto[i] = r; break; }
                Vector3 tp = t.transform.position, size = d.size;
                foreach (TreeInstance ti in d.treeInstances)
                {
                    if (ti.prototypeIndex < 0 || ti.prototypeIndex >= ruleOfProto.Length) continue;
                    WFSpawnRule rule = ruleOfProto[ti.prototypeIndex];
                    if (rule == null || !rule.coreObstacle) continue;
                    float radius = (rule.coreObstacleRadius > 0f ? rule.coreObstacleRadius : DefaultTreeTrunkRadius) * ti.widthScale;
                    float wx = tp.x + ti.position.x * size.x, wz = tp.z + ti.position.z * size.z;
                    outList.Add(new ForgedObstacle(Mm(wx), Mm(wz), (int)Mm(radius), Flags(rule)));
                }
            }
        }

        private static void CollectObjectObstacles(WorldProfile p, ForgedWorld world, List<ForgedObstacle> outList)
        {
            if (p.biome == null) return;
            Transform root = world.transform.Find("WF_Spawned");
            if (root == null) return;
            foreach (WFSpawnRule rule in p.biome.spawnRules)
            {
                if (rule == null || rule.kind != SpawnKind.GameObject || !rule.coreObstacle) continue;
                Transform ruleRoot = root.Find(WFEditorUtil.SafeName(rule.name));
                if (ruleRoot == null) continue;
                foreach (Transform tile in ruleRoot)
                    foreach (Transform inst in tile)
                    {
                        float radius = rule.coreObstacleRadius * inst.localScale.x;
                        bool tall = false;
                        Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
                        if (rs.Length > 0)
                        {
                            Bounds b = rs[0].bounds;
                            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                            if (radius <= 0f) radius = 0.25f * (b.size.x + b.size.z);
                            tall = b.size.y > 1.6f;
                        }
                        if (radius <= 0f) radius = 0.3f;
                        ForgedObstacleFlags f = Flags(rule);
                        if (tall) f |= ForgedObstacleFlags.BlocksSight;
                        Vector3 pos = inst.position;
                        outList.Add(new ForgedObstacle(Mm(pos.x), Mm(pos.z), (int)Mm(radius), f));
                    }
            }
        }

        private static ForgedObstacleFlags Flags(WFSpawnRule r)
        {
            return ForgedObstacleFlags.BlocksMovement | (r.coreBlocksSight ? ForgedObstacleFlags.BlocksSight : ForgedObstacleFlags.None);
        }

        private static long Mm(float metres) { return (long)System.Math.Round(metres * 1000.0, System.MidpointRounding.AwayFromZero); }
    }
}
