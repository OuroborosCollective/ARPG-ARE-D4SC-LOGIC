using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    public class WFBuildReport
    {
        public int tiles, treeInstances, detailCells, gameObjects, pois;
        public float minHeight, maxHeight, unwalkableFraction;
        public double seconds;
        public readonly List<string> warnings = new List<string>();
        public readonly List<string> notes = new List<string>();
        public readonly Dictionary<string, int> perRule = new Dictionary<string, int>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tiles: " + tiles + "   Height range: " + minHeight.ToString("0.00") + " – " + maxHeight.ToString("0.00") + " m");
            sb.AppendLine("Unwalkable (steeper than limit): " + (unwalkableFraction * 100f).ToString("0.0") + " %");
            sb.AppendLine("Trees: " + treeInstances + "   Objects: " + gameObjects + "   Detail cells: " + detailCells + "   POIs: " + pois);
            foreach (var kv in perRule) sb.AppendLine("  • " + kv.Key + ": " + kv.Value);
            foreach (string n in notes) sb.AppendLine("✓ " + n);
            foreach (string w in warnings) sb.AppendLine("⚠ " + w);
            sb.Append("Time: " + seconds.ToString("0.0") + " s");
            return sb.ToString();
        }
    }

    /// <summary>Turns a WorldProfile into Unity terrains, splatmaps, trees, details, objects and POIs.</summary>
    public static class WFTerrainBuilder
    {
        private const string SpawnRootName = "WF_Spawned";
        private const string PoiRootName = "WF_POIs";

        // ------------------------------------------------------------------ Public pipeline

        public static WFBuildReport GenerateAll(WorldProfile p)
        {
            var report = new WFBuildReport();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Validate(p);
                PrepareStampTextures(p);
                WFHeightContext ctx = p.EvaluateHeights((label, t) => Progress("Height stack: " + label, t * 0.4f));
                ForgedWorld world = BuildTerrains(p, ctx);
                Progress("Analysing slopes and paths", 0.45f);
                var fields = new WFWorldFields(ctx);
                PaintTextures(p, world, fields, report);
                Spawn(p, world, fields, report);
                CreatePois(p, world, ctx, report);
                if (p.bakeNavMeshAfterBuild) TryBakeNavMesh(world, report);
                if (p.exportCoreMapAfterBuild) ExportCoreMapSafe(p, world, report);
                FillStats(p, ctx, fields, report);
                report.tiles = p.tilesX * p.tilesZ;
                MarkDirty(world);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                report.seconds = watch.Elapsed.TotalSeconds;
            }
            return report;
        }

        public static WFBuildReport GenerateHeightsOnly(WorldProfile p)
        {
            var report = new WFBuildReport();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Validate(p);
                PrepareStampTextures(p);
                WFHeightContext ctx = p.EvaluateHeights((label, t) => Progress("Height stack: " + label, t * 0.9f));
                ForgedWorld world = BuildTerrains(p, ctx);
                FillStats(p, ctx, new WFWorldFields(ctx), report);
                report.tiles = p.tilesX * p.tilesZ;
                MarkDirty(world);
            }
            finally { EditorUtility.ClearProgressBar(); report.seconds = watch.Elapsed.TotalSeconds; }
            return report;
        }

        /// <summary>Re-textures the current terrain (including any manual sculpting done after generation).</summary>
        public static WFBuildReport RepaintTextures(WorldProfile p)
        {
            return RunOnExisting(p, (world, ctx, fields, report) => PaintTextures(p, world, fields, report));
        }

        /// <summary>Re-spawns trees, details and objects on the current terrain.</summary>
        public static WFBuildReport Respawn(WorldProfile p)
        {
            return RunOnExisting(p, (world, ctx, fields, report) =>
            {
                Spawn(p, world, fields, report);
                CreatePois(p, world, ctx, report);
                if (p.exportCoreMapAfterBuild) ExportCoreMapSafe(p, world, report);
            });
        }

        public static void ClearSpawned(WorldProfile p)
        {
            ForgedWorld world = FindWorld(p);
            if (world == null) return;
            ClearSpawnedInternal(world);
            MarkDirty(world);
        }

        public static WFBuildReport BakeNavMesh(WorldProfile p)
        {
            var report = new WFBuildReport();
            ForgedWorld world = FindWorld(p);
            if (world == null) { report.warnings.Add("No generated world in the open scene."); return report; }
            TryBakeNavMesh(world, report);
            return report;
        }

        public static WFHeightContext PreviewHeights(WorldProfile p)
        {
            try
            {
                PrepareStampTextures(p);
                return p.EvaluateHeights((label, t) => Progress("Preview: " + label, t));
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        // ------------------------------------------------------------------ World root and tiles

        public static ForgedWorld FindWorld(WorldProfile p)
        {
            foreach (ForgedWorld w in UnityEngine.Object.FindObjectsByType<ForgedWorld>(FindObjectsSortMode.None))
                if (w.profile == p) return w;
            return null;
        }

        private static ForgedWorld FindOrCreateWorld(WorldProfile p)
        {
            ForgedWorld world = FindWorld(p);
            if (world != null) return world;
            var go = new GameObject(p.rootObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create World Forge World");
            world = go.AddComponent<ForgedWorld>();
            world.profile = p;
            return world;
        }

        private static string TileName(int x, int z) { return "WF_Terrain_x" + x.ToString("00") + "_z" + z.ToString("00"); }

        private static ForgedWorld BuildTerrains(WorldProfile p, WFHeightContext ctx)
        {
            WFEditorUtil.EnsureFolder(p.outputFolder);
            ForgedWorld world = FindOrCreateWorld(p);
            world.tilesX = p.tilesX; world.tilesZ = p.tilesZ; world.tileSize = p.tileSize;
            world.tiles = new List<Terrain>(new Terrain[p.tilesX * p.tilesZ]);
            Material mat = ResolveTerrainMaterial(p);
            int res = p.heightmapResolution;
            string prefix = WFEditorUtil.SafeName(p.name);

            for (int tz = 0; tz < p.tilesZ; tz++)
            {
                for (int tx = 0; tx < p.tilesX; tx++)
                {
                    Progress("Terrain tile " + tx + "," + tz, 0.4f + 0.05f * (tz * p.tilesX + tx) / (p.tilesX * p.tilesZ));
                    string dataPath = p.outputFolder + "/WF_TD_" + prefix + "_x" + tx.ToString("00") + "_z" + tz.ToString("00") + ".asset";
                    TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
                    if (data == null)
                    {
                        data = new TerrainData();
                        AssetDatabase.CreateAsset(data, dataPath);
                    }
                    data.heightmapResolution = res;
                    data.size = new Vector3(p.tileSize, p.maxHeight, p.tileSize);
                    var heights = new float[res, res];
                    int ox = tx * (res - 1), oz = tz * (res - 1);
                    float inv = 1f / p.maxHeight;
                    for (int y = 0; y < res; y++)
                        for (int x = 0; x < res; x++)
                            heights[y, x] = ctx.Heights[(oz + y) * ctx.Width + ox + x] * inv;
                    data.SetHeights(0, 0, heights);
                    EditorUtility.SetDirty(data);

                    Transform existing = world.transform.Find(TileName(tx, tz));
                    Terrain terrain = existing != null ? existing.GetComponent<Terrain>() : null;
                    if (terrain == null)
                    {
                        GameObject go = Terrain.CreateTerrainGameObject(data);
                        go.name = TileName(tx, tz);
                        go.transform.SetParent(world.transform, false);
                        terrain = go.GetComponent<Terrain>();
                    }
                    terrain.terrainData = data;
                    var col = terrain.GetComponent<TerrainCollider>();
                    if (col != null) col.terrainData = data;
                    terrain.transform.localPosition = new Vector3(tx * p.tileSize, 0f, tz * p.tileSize);
                    if (mat != null) terrain.materialTemplate = mat;
                    ApplyQuality(terrain, p.quality);
                    terrain.allowAutoConnect = true;
                    terrain.groupingID = 7310;
                    world.tiles[tz * p.tilesX + tx] = terrain;
                }
            }
            for (int tz = 0; tz < p.tilesZ; tz++)
                for (int tx = 0; tx < p.tilesX; tx++)
                    world.GetTile(tx, tz).SetNeighbors(world.GetTile(tx - 1, tz), world.GetTile(tx, tz + 1), world.GetTile(tx + 1, tz), world.GetTile(tx, tz - 1));
            Terrain.SetConnectivityDirty();
            AssetDatabase.SaveAssets();
            return world;
        }

        private static void ApplyQuality(Terrain t, QualityPreset q)
        {
            t.drawInstanced = true;
            if (q == QualityPreset.Android_ThirdPerson)
            {
                t.heightmapPixelError = 10f; t.basemapDistance = 90f; t.treeDistance = 260f;
                t.treeBillboardDistance = 70f; t.detailObjectDistance = 45f; t.detailObjectDensity = 0.55f;
                t.terrainData.baseMapResolution = 256;
            }
            else
            {
                t.heightmapPixelError = 5f; t.basemapDistance = 250f; t.treeDistance = 600f;
                t.treeBillboardDistance = 150f; t.detailObjectDistance = 100f; t.detailObjectDensity = 1f;
                t.terrainData.baseMapResolution = 512;
            }
        }

        private static Material ResolveTerrainMaterial(WorldProfile p)
        {
            if (p.terrainMaterialOverride != null) return p.terrainMaterialOverride;
            Shader urp = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (urp == null) return null;
            string path = p.outputFolder + "/WF_TerrainLit_URP.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urp) { name = "WF_TerrainLit_URP" };
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        /// <summary>Reads the current terrain heights back into a global grid (keeps manual sculpting).</summary>
        public static WFHeightContext ReadFromTerrains(WorldProfile p, ForgedWorld world)
        {
            int res = p.heightmapResolution;
            var ctx = new WFHeightContext(p);
            for (int tz = 0; tz < p.tilesZ; tz++)
                for (int tx = 0; tx < p.tilesX; tx++)
                {
                    Terrain t = world.GetTile(tx, tz);
                    if (t == null || t.terrainData == null)
                        throw new InvalidOperationException("Tile " + tx + "," + tz + " is missing. Run 'Generate All' first.");
                    TerrainData d = t.terrainData;
                    if (d.heightmapResolution != res)
                        throw new InvalidOperationException("Tile " + tx + "," + tz + " has heightmap resolution " + d.heightmapResolution + " but the profile expects " + res + ". Regenerate the terrain.");
                    float[,] hm = d.GetHeights(0, 0, res, res);
                    float scale = d.size.y;
                    int ox = tx * (res - 1), oz = tz * (res - 1);
                    for (int y = 0; y < res; y++)
                        for (int x = 0; x < res; x++)
                            ctx.Heights[(oz + y) * ctx.Width + ox + x] = hm[y, x] * scale;
                }
            return ctx;
        }

        private static WFBuildReport RunOnExisting(WorldProfile p, Action<ForgedWorld, WFHeightContext, WFWorldFields, WFBuildReport> step)
        {
            var report = new WFBuildReport();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Validate(p);
                ForgedWorld world = FindWorld(p);
                if (world == null) throw new InvalidOperationException("No generated world for this profile in the open scene. Run 'Generate All' first.");
                WFHeightContext ctx = ReadFromTerrains(p, world);
                var fields = new WFWorldFields(ctx);
                step(world, ctx, fields, report);
                FillStats(p, ctx, fields, report);
                report.tiles = p.tilesX * p.tilesZ;
                MarkDirty(world);
            }
            finally { EditorUtility.ClearProgressBar(); report.seconds = watch.Elapsed.TotalSeconds; }
            return report;
        }

        // ------------------------------------------------------------------ Texturing

        private static void PaintTextures(WorldProfile p, ForgedWorld world, WFWorldFields fields, WFBuildReport report)
        {
            BiomeProfile biome = p.biome;
            if (biome == null || biome.textureRules.Count == 0) { report.warnings.Add("No biome / texture rules assigned – texturing skipped."); return; }
            int[] ruleToLayer;
            List<TerrainLayer> layers = biome.CollectTerrainLayers(out ruleToLayer);
            if (layers.Count == 0) { report.warnings.Add("Texture rules have no TerrainLayers – texturing skipped."); return; }
            if (layers.Count > 4 && p.quality == QualityPreset.Android_ThirdPerson)
                report.warnings.Add(layers.Count + " terrain layers: URP renders more than 4 layers in an extra pass (costly on Android).");

            var ruleWeights = new float[biome.textureRules.Count];
            TerrainLayer[] layerArray = layers.ToArray();
            for (int tz = 0; tz < p.tilesZ; tz++)
            {
                for (int tx = 0; tx < p.tilesX; tx++)
                {
                    Progress("Texturing tile " + tx + "," + tz, 0.5f + 0.15f * (tz * p.tilesX + tx) / (p.tilesX * p.tilesZ));
                    TerrainData d = world.GetTile(tx, tz).terrainData;
                    d.terrainLayers = layerArray;
                    d.alphamapResolution = p.alphamapResolution;
                    int res = d.alphamapResolution; // read back: Unity may clamp the value
                    var alpha = new float[res, res, layers.Count];
                    float step = p.tileSize / (res - 1);
                    for (int y = 0; y < res; y++)
                    {
                        float wz = tz * p.tileSize + y * step;
                        for (int x = 0; x < res; x++)
                        {
                            float wx = tx * p.tileSize + x * step;
                            WFRules.TextureWeights(biome.textureRules, fields.At(wx, wz), p.seed, biome.textureSharpness, ruleWeights);
                            for (int r = 0; r < ruleWeights.Length; r++)
                            {
                                int li = ruleToLayer[r];
                                if (li >= 0) alpha[y, x, li] += ruleWeights[r];
                            }
                        }
                    }
                    d.SetAlphamaps(0, 0, alpha);
                    EditorUtility.SetDirty(d);
                }
            }
        }

        // ------------------------------------------------------------------ Spawning

        private static void ClearSpawnedInternal(ForgedWorld world)
        {
            foreach (Terrain t in world.tiles)
            {
                if (t == null || t.terrainData == null) continue;
                t.terrainData.SetTreeInstances(new TreeInstance[0], false);
                t.terrainData.detailPrototypes = new DetailPrototype[0];
                EditorUtility.SetDirty(t.terrainData);
            }
            Transform old = world.transform.Find(SpawnRootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        private static void Spawn(WorldProfile p, ForgedWorld world, WFWorldFields fields, WFBuildReport report)
        {
            BiomeProfile biome = p.biome;
            ClearSpawnedInternal(world);
            if (biome == null || biome.spawnRules.Count == 0) { report.warnings.Add("No spawn rules – spawning skipped."); return; }

            int tileCount = p.tilesX * p.tilesZ;
            float area = p.WorldSizeX * p.WorldSizeZ;
            var occupancy = new WFOccupancy(8f);
            var poiOccupancy = new WFOccupancy(8f);
            if (p.pois != null)
                foreach (WFPoiDefinition poi in p.pois)
                    if (poi != null) poiOccupancy.Add(poi.position.x, poi.position.y, poi.clearSpawnRadius);

            // Tree prototypes shared by all tiles.
            var treePrefabs = new List<GameObject>();
            foreach (WFSpawnRule rule in biome.spawnRules)
            {
                if (rule == null || !rule.enabled || rule.kind != SpawnKind.TerrainTree) continue;
                foreach (GameObject go in rule.prefabs)
                {
                    string why;
                    if (!WFEditorUtil.IsValidTreePrototype(go, out why)) { report.warnings.Add("Tree prefab skipped: " + why); continue; }
                    if (!treePrefabs.Contains(go)) treePrefabs.Add(go);
                }
            }
            var trees = new List<TreeInstance>[tileCount];
            for (int i = 0; i < tileCount; i++) trees[i] = new List<TreeInstance>();

            // Detail prototypes, one per detail rule.
            var detailProtos = new List<DetailPrototype>();
            var detailRuleIndex = new Dictionary<int, int>();
            var detailMaps = new Dictionary<int, int[][,]>();

            Transform spawnRoot = null;
            var ruleWeights = new float[biome.textureRules.Count];

            for (int r = 0; r < biome.spawnRules.Count; r++)
            {
                WFSpawnRule rule = biome.spawnRules[r];
                if (rule == null || !rule.enabled) continue;
                Progress("Spawning: " + rule.name, 0.65f + 0.3f * r / Math.Max(1, biome.spawnRules.Count));
                int reqTex = biome.FindTextureRule(rule.requiredTextureRule);
                int placed = 0;

                if (rule.kind == SpawnKind.Detail)
                {
                    DetailPrototype proto = MakeDetailPrototype(rule, report);
                    if (proto == null) continue;
                    detailRuleIndex[r] = detailProtos.Count;
                    detailProtos.Add(proto);
                    int dres = p.detailResolution;
                    var maps = new int[tileCount][,];
                    float cellSize = p.tileSize / dres;
                    float expectedPerCell = rule.densityPer100m2 / 100f * cellSize * cellSize;
                    for (int tz = 0; tz < p.tilesZ; tz++)
                        for (int tx = 0; tx < p.tilesX; tx++)
                        {
                            var map = new int[dres, dres];
                            var rng = new WFRandom(WFMath.MixSeed(p.seed, 8000 + r, rule.seedOffset, tz * p.tilesX + tx));
                            for (int y = 0; y < dres; y++)
                                for (int x = 0; x < dres; x++)
                                {
                                    float roll = rng.NextFloat();
                                    float wx = tx * p.tileSize + (x + 0.5f) * cellSize;
                                    float wz = tz * p.tileSize + (y + 0.5f) * cellSize;
                                    WFWorldFields.Sample s = fields.At(wx, wz);
                                    float m = WFRules.EvaluateMask(rule.mask, s, p.seed);
                                    if (m <= 0f) continue;
                                    if (reqTex >= 0)
                                    {
                                        WFRules.TextureWeights(biome.textureRules, s, p.seed, biome.textureSharpness, ruleWeights);
                                        if (ruleWeights[reqTex] < rule.requiredTextureWeight) continue;
                                    }
                                    if (rule.respectPoiClearance && poiOccupancy.IsBlocked(wx, wz, 0f)) continue;
                                    int count = Mathf.FloorToInt(expectedPerCell * m + roll);
                                    count = Mathf.Clamp(count, 0, rule.detailMaxPerCell);
                                    if (count > 0) { map[y, x] = count; placed++; }
                                }
                            maps[tz * p.tilesX + tx] = map;
                        }
                    detailMaps[r] = maps;
                    report.detailCells += placed;
                    report.perRule[rule.name] = placed;
                    continue;
                }

                if (rule.prefabs == null || rule.prefabs.Count == 0) { report.warnings.Add("Rule '" + rule.name + "' has no prefabs."); continue; }
                List<Vector2> points = WFPoisson.Generate(p.WorldSizeX, p.WorldSizeZ, rule.minSpacing, WFMath.MixSeed(p.seed, 5000 + r, rule.seedOffset));
                float poissonDensity = points.Count / Mathf.Max(1f, area);
                float keepBase = WFMath.Saturate((rule.densityPer100m2 / 100f) / Mathf.Max(1e-6f, poissonDensity));
                var prng = new WFRandom(WFMath.MixSeed(p.seed, 6000 + r, rule.seedOffset));
                Transform ruleRoot = null;

                foreach (Vector2 pt in points)
                {
                    // Draw all random numbers first so mask edits do not reshuffle the rest of the world.
                    float roll = prng.NextFloat();
                    float yaw = rule.randomYaw ? prng.NextFloat() * 360f : 0f;
                    float scale = WFMath.Lerp(rule.scaleMin, rule.scaleMax, prng.NextFloat());
                    int pick = prng.Range(0, rule.prefabs.Count);
                    if (rule.maxCount > 0 && placed >= rule.maxCount) break;

                    WFWorldFields.Sample s = fields.At(pt.x, pt.y);
                    float m = WFRules.EvaluateMask(rule.mask, s, p.seed);
                    if (roll >= keepBase * m) continue;
                    if (reqTex >= 0)
                    {
                        WFRules.TextureWeights(biome.textureRules, s, p.seed, biome.textureSharpness, ruleWeights);
                        if (ruleWeights[reqTex] < rule.requiredTextureWeight) continue;
                    }
                    if (rule.respectPoiClearance && poiOccupancy.IsBlocked(pt.x, pt.y, 0f)) continue;
                    if (occupancy.IsBlocked(pt.x, pt.y, rule.clearRadius * 0.5f)) continue;

                    GameObject prefab = rule.prefabs[pick];
                    if (prefab == null) continue;
                    int tx = Mathf.Clamp(Mathf.FloorToInt(pt.x / p.tileSize), 0, p.tilesX - 1);
                    int tz = Mathf.Clamp(Mathf.FloorToInt(pt.y / p.tileSize), 0, p.tilesZ - 1);

                    if (rule.kind == SpawnKind.TerrainTree)
                    {
                        int proto = treePrefabs.IndexOf(prefab);
                        if (proto < 0) continue;
                        var ti = new TreeInstance
                        {
                            position = new Vector3((pt.x - tx * p.tileSize) / p.tileSize, s.height / p.maxHeight, (pt.y - tz * p.tileSize) / p.tileSize),
                            widthScale = scale,
                            heightScale = scale,
                            rotation = yaw * Mathf.Deg2Rad,
                            color = Color.white,
                            lightmapColor = Color.white,
                            prototypeIndex = proto
                        };
                        trees[tz * p.tilesX + tx].Add(ti);
                        report.treeInstances++;
                    }
                    else
                    {
                        if (spawnRoot == null) spawnRoot = GetOrCreateChild(world.transform, SpawnRootName);
                        if (ruleRoot == null) ruleRoot = GetOrCreateChild(spawnRoot, WFEditorUtil.SafeName(rule.name));
                        Transform tileRoot = GetOrCreateChild(ruleRoot, "Tile_x" + tx.ToString("00") + "_z" + tz.ToString("00"));
                        var go = PrefabUtility.InstantiatePrefab(prefab, tileRoot) as GameObject;
                        if (go == null) continue;
                        Vector3 up = Vector3.up;
                        if (rule.alignToNormal > 0f) up = Vector3.Slerp(Vector3.up, fields.NormalAt(pt.x, pt.y), rule.alignToNormal);
                        go.transform.position = world.transform.position + new Vector3(pt.x, s.height - rule.sinkDepth, pt.y);
                        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, yaw, 0f);
                        go.transform.localScale = Vector3.one * scale;
                        if (rule.markBatchingStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                        if (rule.addMeshColliderIfMissing && go.GetComponentInChildren<Collider>() == null)
                            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                            {
                                var mc = mf.gameObject.AddComponent<MeshCollider>();
                                mc.sharedMesh = mf.sharedMesh;
                            }
                        report.gameObjects++;
                    }
                    occupancy.Add(pt.x, pt.y, rule.clearRadius);
                    placed++;
                }
                report.perRule[rule.name] = placed;
            }

            // Write per-tile tree and detail data.
            GameObject[] treeArray = treePrefabs.ToArray();
            DetailPrototype[] detailArray = detailProtos.ToArray();
            for (int tz = 0; tz < p.tilesZ; tz++)
                for (int tx = 0; tx < p.tilesX; tx++)
                {
                    int ti = tz * p.tilesX + tx;
                    TerrainData d = world.GetTile(tx, tz).terrainData;
                    var protos = new TreePrototype[treeArray.Length];
                    for (int i = 0; i < treeArray.Length; i++) protos[i] = new TreePrototype { prefab = treeArray[i] };
                    d.treePrototypes = protos;
                    d.SetTreeInstances(trees[ti].ToArray(), true);

                    d.SetDetailResolution(p.detailResolution, p.detailResolutionPerPatch);
                    SetInstanceCountScatterMode(d);
                    d.detailPrototypes = detailArray;
                    foreach (var kv in detailRuleIndex)
                        d.SetDetailLayer(0, 0, kv.Value, detailMaps[kv.Key][ti]);
                    d.RefreshPrototypes();
                    EditorUtility.SetDirty(d);
                }
            if (spawnRoot != null) Undo.RegisterCreatedObjectUndo(spawnRoot.gameObject, "World Forge Spawn");
        }

        private static DetailPrototype MakeDetailPrototype(WFSpawnRule rule, WFBuildReport report)
        {
            var proto = new DetailPrototype
            {
                minWidth = rule.detailMinWidth,
                maxWidth = rule.detailMaxWidth,
                minHeight = rule.detailMinHeight,
                maxHeight = rule.detailMaxHeight,
                healthyColor = rule.detailHealthyColor,
                dryColor = rule.detailDryColor,
                noiseSpread = 0.35f
            };
            GameObject mesh = rule.prefabs != null && rule.prefabs.Count > 0 ? rule.prefabs[0] : null;
            if (rule.detailAsMesh && mesh != null)
            {
                proto.usePrototypeMesh = true;
                proto.prototype = mesh;
                proto.renderMode = DetailRenderMode.VertexLit;
                proto.useInstancing = true;
                return proto;
            }
            if (rule.detailTexture == null)
            {
                report.warnings.Add("Detail rule '" + rule.name + "' has neither a texture nor a mesh prefab.");
                return null;
            }
            proto.usePrototypeMesh = false;
            proto.prototypeTexture = rule.detailTexture;
            proto.renderMode = DetailRenderMode.Grass;
            return proto;
        }

        /// <summary>Unity 2022.2+ defaults new terrains to coverage mode; our detail maps are instance counts.</summary>
        private static void SetInstanceCountScatterMode(TerrainData d)
        {
            MethodInfo m = typeof(TerrainData).GetMethod("SetDetailScatterMode", BindingFlags.Public | BindingFlags.Instance);
            if (m == null) return;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != 1 || !ps[0].ParameterType.IsEnum) return;
            try { m.Invoke(d, new[] { Enum.Parse(ps[0].ParameterType, "InstanceCountMode") }); }
            catch (Exception e) { Debug.LogWarning("[WorldForge] Could not set detail scatter mode: " + e.Message); }
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ------------------------------------------------------------------ POIs, NavMesh, stats

        private static void CreatePois(WorldProfile p, ForgedWorld world, WFHeightContext ctx, WFBuildReport report)
        {
            Transform old = world.transform.Find(PoiRootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            if (!p.createPoiObjects || p.pois == null || p.pois.Count == 0) return;
            Transform root = GetOrCreateChild(world.transform, PoiRootName);
            foreach (WFPoiDefinition def in p.pois)
            {
                if (def == null) continue;
                var go = new GameObject("POI_" + def.type + "_" + WFEditorUtil.SafeName(def.id));
                go.transform.SetParent(root, false);
                go.transform.position = world.transform.position + new Vector3(def.position.x, ctx.SampleHeight(def.position.x, def.position.y), def.position.y);
                var poi = go.AddComponent<ForgedPoi>();
                poi.poiId = def.id; poi.type = def.type; poi.radius = def.radius; poi.tags = def.tags;
                report.pois++;
            }
            Undo.RegisterCreatedObjectUndo(root.gameObject, "World Forge POIs");
        }

        /// <summary>Bakes a NavMesh if the AI Navigation package is installed (no hard dependency).</summary>
        private static void TryBakeNavMesh(ForgedWorld world, WFBuildReport report)
        {
            Type surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (surfaceType == null)
            {
                report.warnings.Add("NavMesh not baked: install the 'AI Navigation' package (com.unity.ai.navigation).");
                return;
            }
            Component surface = world.GetComponent(surfaceType);
            if (surface == null) surface = world.gameObject.AddComponent(surfaceType);
            PropertyInfo collect = surfaceType.GetProperty("collectObjects");
            if (collect != null && collect.PropertyType.IsEnum)
            {
                try { collect.SetValue(surface, Enum.Parse(collect.PropertyType, "Children"), null); } catch { }
            }
            MethodInfo build = surfaceType.GetMethod("BuildNavMesh", Type.EmptyTypes);
            if (build == null) { report.warnings.Add("NavMeshSurface.BuildNavMesh not found."); return; }
            Progress("Baking NavMesh", 0.97f);
            build.Invoke(surface, null);
            EditorUtility.SetDirty(surface);
        }

        private static void FillStats(WorldProfile p, WFHeightContext ctx, WFWorldFields fields, WFBuildReport report)
        {
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (float h in ctx.Heights) { if (h < mn) mn = h; if (h > mx) mx = h; }
            report.minHeight = mn; report.maxHeight = mx;
            report.unwalkableFraction = fields.UnwalkableFraction(p.maxWalkableSlope);
            if (mx >= p.maxHeight - 0.01f) report.warnings.Add("Heights are clipped at maxHeight (" + p.maxHeight + " m). Lower stamps or raise maxHeight.");
        }

        private static void ExportCoreMapSafe(WorldProfile p, ForgedWorld world, WFBuildReport report)
        {
            Progress("Exporting core map", 0.98f);
            WFBuildReport r = WFCoreMapExporter.Export(p);
            foreach (var kv in r.perRule) report.perRule[kv.Key] = kv.Value;
            report.warnings.AddRange(r.warnings);
            report.notes.AddRange(r.notes);
        }

        // ------------------------------------------------------------------ Helpers

        private static void Validate(WorldProfile p)
        {
            if (p == null) throw new ArgumentNullException("profile");
            int fixedRes = WorldProfile.ValidHeightmapResolution(p.heightmapResolution);
            if (fixedRes != p.heightmapResolution) { p.heightmapResolution = fixedRes; EditorUtility.SetDirty(p); }
            if (p.heightLayers == null || p.heightLayers.Count == 0)
                Debug.LogWarning("[WorldForge] Profile has no height layers – the terrain will be flat.");
        }

        private static void PrepareStampTextures(WorldProfile p)
        {
            if (p.heightLayers == null) return;
            foreach (WFHeightLayer l in p.heightLayers)
            {
                var stamp = l as WFStampLayer;
                if (stamp != null && stamp.enabled && stamp.shape == StampShape.TextureStamp) WFEditorUtil.EnsureReadable(stamp.texture);
            }
        }

        private static void MarkDirty(ForgedWorld world)
        {
            if (world == null) return;
            EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        private static void Progress(string info, float t)
        {
            EditorUtility.DisplayProgressBar("World Forge", info, Mathf.Clamp01(t));
        }
    }
}
