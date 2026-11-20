using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AreArpg.Core;
using AshenCrypt.WorldForge;
using AshenCrypt.WorldForge.Core;
using AshenCrypt.WorldForge.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreArpg.EditorTools
{
    /// <summary>Bounded real World Forge authoring. No runtime progression, reward or scene authority.</summary>
    public static class JourneyWorldAuthoring
    {
        public const string Output="Assets/WorldForgeData/JourneyPilot";
        public const string Maps="Assets/Resources/ARE/JourneyMaps";
        public const int Revision=1;

        [MenuItem("ARE/World/Author Journey Pilot A-B-A")]
        public static void BuildPilot()
        {
            var prior=SceneManager.GetActiveScene();
            var rows=new List<object>();
            var canonical=new Dictionary<int,byte[]>();
            try
            {
                foreach(int seed in new[]{731904,731905,731904})
                    rows.Add(AuthorSeed(seed,Output+"/Seed"+seed.ToString(CultureInfo.InvariantCulture),Maps,canonical,(scene,path)=>EditorSceneManager.SaveScene(scene,path)));
                if(canonical[731904].SequenceEqual(canonical[731905]))throw new InvalidOperationException("Different seeds produced identical maps");
                string evidence=Environment.GetEnvironmentVariable("ARE_JOURNEY_AUTHOR_REPORT");
                if(string.IsNullOrEmpty(evidence))evidence="Library/JourneyPilotAuthoring.json";
                File.WriteAllText(evidence,Newtonsoft.Json.JsonConvert.SerializeObject(new{tool="World Forge",revision=Revision,unity=Application.unityVersion,
                    repeatedSeedIdentical=true,differentSeedVaries=true,runs=rows},Newtonsoft.Json.Formatting.Indented));
                Debug.Log("Journey World Forge A-B-A completed: "+evidence);
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
            finally{if(prior.IsValid()&&prior.isLoaded)SceneManager.SetActiveScene(prior);}
        }

        [MenuItem("Tools/World Forge/Grenzregion/256-m-Reiseroute backen")]
        public static void BuildBorderlandRoute()
        {
            const int seed = 731906;
            const string folder = "Assets/PrivateArt/BorderlandRouteAuthoring";
            var canonical = new Dictionary<int, byte[]>();
            for (int i = 0; i < 2; i++)
            {
                var row = AuthorRegion(seed, folder, Maps, canonical,
                    (scene, path) => EditorSceneManager.SaveScene(scene, path), 256);
                Debug.Log("BORDERLAND_ROUTE " + Newtonsoft.Json.JsonConvert.SerializeObject(row));
            }
        }

        [MenuItem("Tools/World Forge/Grenzregion/Außenlandschaften backen")]
        public static void BuildRegionalLandscapes()
        {
            string[] regions = { RegionNetwork.Aschengrenze, RegionNetwork.Aschenstrasse,
                RegionNetwork.AlteMuehle, RegionNetwork.Waldweg, RegionNetwork.Aschenzoll };
            for (int i = 0; i < regions.Length; i++)
            {
                var canonical = new Dictionary<int, byte[]>();
                string folder = "Assets/PrivateArt/BorderlandLandscapeAuthoring/Site" + i;
                for (int pass = 0; pass < 2; pass++)
                {
                    var row = AuthorRegion(842011 + i, folder, "Assets/Resources/ARE/RegionalMaps", canonical,
                        (scene, path) => EditorSceneManager.SaveScene(scene, path), 256, regions[i]);
                    Debug.Log("BORDERLAND_LANDSCAPE " + Newtonsoft.Json.JsonConvert.SerializeObject(row));
                }
            }
        }

        /// <summary>
        /// Author one seed into its folder. Any failure restores the folder byte for byte, with one exception: when only
        /// saving the authored scene fails, the published map has already been verified, so the generated world and its
        /// assets are kept for recovery — saved into the scratch scene when possible, otherwise left open in the editor.
        /// </summary>
        public static object AuthorSeed(int seed, string folder, string maps, Dictionary<int,byte[]> canonical, Func<Scene,string,bool> save)
        {
            return AuthorRegion(seed, folder, maps, canonical, save, 128);
        }

        private static object AuthorRegion(int seed,string folder,string maps,Dictionary<int,byte[]> canonical,Func<Scene,string,bool> save, int extent, string region = null)
        {
            var prior=SceneManager.GetActiveScene();
            if (extent != 128 && extent != 256) throw new ArgumentOutOfRangeException(nameof(extent));
            string scratch=folder+"/GenerationScratch.unity";
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scratch)!=null)throw new InvalidOperationException("Previous authoring scratch scene needs review: "+scratch);
            // Never write into a world that another open scene already uses for this profile.
            RejectLoadedWorldFor(folder+"/Profile.asset");
            // Refuse before anything is touched, so neither the run nor its rollback can overwrite unsaved edits.
            RejectUnsavedAuthoringEdits(folder);
            EnsureFolder(folder);EnsureFolder(maps);
            // Profile, biome and terrain are rewritten before the immutable map can be compared; keep a byte copy.
            string snapshot=SnapshotFolder(folder,"Temp/JourneyAuthoringBackup/"+folder.Replace('/','_'));
            if(!AssetDatabase.CopyAsset("Assets/Scenes/Arena.unity",scratch))throw new InvalidOperationException("Missing saved empty authoring template");
            var loadedAssets=CaptureLoadedAuthoringAssets(folder);
            Scene scene=default;
            bool completed=false,recovering=false,rescued=false;
            try
            {
                scene=EditorSceneManager.OpenScene(scratch,OpenSceneMode.Additive);
                if(scene.rootCount!=0)throw new InvalidOperationException("Authoring template must remain empty");
                SceneManager.SetActiveScene(scene);
                // Only this saved additive scene is the generation target. All other open scenes stay open.
                var p=CreateRegionProfile(seed,folder,extent,region);
                var root=new GameObject(p.rootObjectName);
                root.transform.position=new Vector3(-extent / 2, 0, -extent / 2);
                var world=root.AddComponent<ForgedWorld>();world.profile=p;
                var report=WFTerrainBuilder.GenerateAll(p);
                var map=WFCoreMapExporter.Build(p,world,report);
                if(map.NodesX!=129||map.NodesZ!=129||map.WidthMm!=extent*1000L||map.DepthMm!=extent*1000L)
                    throw new InvalidOperationException("World Forge dimensions differ from the selected regional profile");
                byte[] bytes=map.ToBytes();
                if(canonical.TryGetValue(seed,out var first)&&!first.SequenceEqual(bytes))
                    throw new InvalidOperationException("Repeated World Forge seed changed canonical bytes");
                canonical[seed]=bytes;
                string prefix = extent == 128 ? "/Wald_v1_" : "/Wald_256_";
                string path = maps + (region == null ? prefix + seed.ToString(CultureInfo.InvariantCulture) : "/" + region) + ".bytes";
                // Published identities are immutable: a different map requires a new authored version, and a matching
                // published map is never rewritten (an interrupted rewrite could leave a partial file outside the snapshot).
                if(File.Exists(path))
                {
                    if(!File.ReadAllBytes(path).SequenceEqual(bytes))
                        throw new InvalidOperationException("Existing map differs; retain it and author a new version: "+path);
                }
                else File.WriteAllBytes(path,bytes);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                world.coreMap=AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if(!save(scene,folder+"/Authored.unity"))
                {
                    // The map is verified and published, so the generated assets still reproduce it: keep them, never roll back.
                    recovering=true;
                    rescued=save(scene,scratch);
                    throw new InvalidOperationException(rescued?"Authored scene could not be saved; the generated world was saved for recovery: "+scratch:
                        "Authored scene could not be saved; the generated world stays open in the editor for manual saving: "+scratch);
                }
                var row=new {seed,revision=Revision,profile=AssetDatabase.GetAssetPath(p),scene=scene.path,map=path,
                    hash=map.ContentHash.ToString("X16"),sha256=Sha(bytes),nodes=map.NodesX*map.NodesZ,
                    areaSquareMetres=extent*extent,obstacles=map.Obstacles.Count,pois=map.Pois.Count,
                    paths=p.paths.Count,seconds=report.seconds,report.treeInstances,report.gameObjects,
                    warnings=report.warnings.ToArray(),manualTerrainEdits=0};
                completed=true;
                return row;
            }
            finally
            {
                try
                {
                    if(prior.IsValid()&&prior.isLoaded)SceneManager.SetActiveScene(prior);
                    if(scene.IsValid()&&scene.isLoaded&&!(recovering&&!rescued))EditorSceneManager.CloseScene(scene,true);
                    if(!recovering)AssetDatabase.DeleteAsset(scratch);
                    if(!completed&&!recovering)
                    {
                        try { RestoreFolder(snapshot,folder,null); }
                        finally { RestoreLoadedAuthoringAssets(loadedAssets); }
                    }
                }
                finally { ReleaseLoadedAuthoringAssets(loadedAssets); }
            }
        }

        private sealed class LoadedAuthoringAsset
        {
            public UnityEngine.Object target;
            public UnityEngine.Object before;
        }

        // Editor-only rollback: snapshots never enter deterministic simulation.
        private static List<LoadedAuthoringAsset> CaptureLoadedAuthoringAssets(string folder)
        {
            var result=new List<LoadedAuthoringAsset>();
            if(!AssetDatabase.IsValidFolder(folder))return result;
            try
            {
                foreach(string guid in AssetDatabase.FindAssets("",new[]{folder}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    if(AssetDatabase.IsValidFolder(path))continue;
                    var asset=AssetDatabase.LoadMainAssetAtPath(path);
                    if(asset is ScriptableObject || asset is TerrainData)
                        result.Add(new LoadedAuthoringAsset{target=asset,before=UnityEngine.Object.Instantiate(asset)});
                }
                return result;
            }
            catch { ReleaseLoadedAuthoringAssets(result); throw; }
        }

        private static void RestoreLoadedAuthoringAssets(List<LoadedAuthoringAsset> snapshots)
        {
            foreach(var item in snapshots)
            {
                if(item.target==null || item.before==null)continue;
                EditorUtility.CopySerialized(item.before,item.target);
                EditorUtility.ClearDirty(item.target);
            }
        }

        private static void ReleaseLoadedAuthoringAssets(List<LoadedAuthoringAsset> snapshots)
        {
            foreach(var item in snapshots)
                if(item.before!=null)UnityEngine.Object.DestroyImmediate(item.before);
        }

        /// <summary>Throw before any change when an asset of this authoring folder has unsaved in-memory edits.</summary>
        public static void RejectUnsavedAuthoringEdits(string folder)
        {
            if(!AssetDatabase.IsValidFolder(folder))return;
            foreach(string guid in AssetDatabase.FindAssets("",new[]{folder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(AssetDatabase.IsValidFolder(path))continue;
                var asset=AssetDatabase.LoadMainAssetAtPath(path);
                if(asset!=null&&EditorUtility.IsDirty(asset))
                    throw new InvalidOperationException("Authoring output has unsaved edits; save or revert them first: "+path);
            }
        }

        /// <summary>Throw when a loaded scene contains a world that already uses the profile at this path.</summary>
        public static void RejectLoadedWorldFor(string profilePath)
        {
            var existing=AssetDatabase.LoadAssetAtPath<WorldProfile>(profilePath);
            if(existing==null)return;
            foreach(var world in UnityEngine.Object.FindObjectsByType<ForgedWorld>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(world.profile==existing)
                    throw new InvalidOperationException("Close the open scene that already uses "+profilePath+" before authoring: "+world.gameObject.scene.path);
        }

        /// <summary>Copy every file of an authoring folder (with metas) outside Assets; null when the folder does not exist.</summary>
        public static string SnapshotFolder(string folder,string backup)
        {
            if(!Directory.Exists(folder))return null;
            if(Directory.Exists(backup))Directory.Delete(backup,true);
            // An existing empty folder still gets an (empty) snapshot, so a later restore finds it.
            Directory.CreateDirectory(backup);
            foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
            {
                string target=System.IO.Path.Combine(backup,System.IO.Path.GetRelativePath(folder,file));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                File.Copy(file,target,true);
            }
            return backup;
        }

        /// <summary>
        /// Restore disk bytes; AuthorSeed restores the loaded objects using their pre-run in-memory checkpoint.
        /// Existing user edits are rejected before capture and cannot be overwritten by this rollback.
        /// </summary>
        public static void RestoreFolder(string backup,string folder,string keep)
        {
            if(backup==null||!Directory.Exists(folder))return;
            string kept=keep==null?null:System.IO.Path.GetFullPath(keep);
            bool changed=false;
            foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
            {
                string full=System.IO.Path.GetFullPath(file);
                if(kept!=null&&(full==kept||full==kept+".meta"))continue;
                if(!File.Exists(System.IO.Path.Combine(backup,System.IO.Path.GetRelativePath(folder,file)))){File.Delete(file);changed=true;}
            }
            if(Directory.Exists(backup))
                foreach(string file in Directory.GetFiles(backup,"*",SearchOption.AllDirectories))
                {
                    string target=System.IO.Path.Combine(folder,System.IO.Path.GetRelativePath(backup,file));
                    if(File.Exists(target)&&File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(file)))continue;
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                    File.Copy(file,target,true);changed=true;
                }
            if(changed)AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        }

        public static WorldProfile CreateProfile(int seed, string folder)
        {
            return CreateRegionProfile(seed, folder, 128, null);
        }

        private static WorldProfile CreateRegionProfile(int seed,string folder,int extent,string region)
        {
            var p=LoadOrCreate<WorldProfile>(folder+"/Profile.asset");
            p.name="Journey_Wald_v1_"+seed;p.tilesX=p.tilesZ=1;p.tileSize=extent;p.maxHeight=12;
            p.heightmapResolution=129;p.alphamapResolution=64;p.detailResolution=32;p.detailResolutionPerPatch=16;
            p.seed=seed;p.quality=QualityPreset.Android_ThirdPerson;p.maxWalkableSlope=30;
            p.rootObjectName="Journey_WorldForge_"+seed;p.outputFolder=folder+"/Generated";
            p.createPoiObjects=true;p.exportCoreMapAfterBuild=false;p.bakeNavMeshAfterBuild=false;
            p.heightLayers=new List<WFHeightLayer>{
                new WFNoiseLayer{label="Low woodland relief",noiseType=NoiseType.FBm,scale=40,octaves=3,amplitude=3,baseHeight=1,warp=3},
                new WFPathCarveLayer{label="Authored woodland connections",strength=1,gradeSmoothing=8},
                new WFFlattenPoiLayer{label="Safe clearings",opacity=1}};
            p.paths=new List<WFPath>{
                Path("Western route",new Vector2(64,8),new Vector2(44,32),new Vector2(40,64),new Vector2(48,96),new Vector2(64,120)),
                Path("Eastern route",new Vector2(64,8),new Vector2(84,32),new Vector2(88,64),new Vector2(80,96),new Vector2(64,120)),
                Path("Cave branch",new Vector2(40,64),new Vector2(12,64)),
                Path("Treasure branch",new Vector2(88,64),new Vector2(112,64)),
                Path("Crossing",new Vector2(40,64),new Vector2(64,64),new Vector2(88,64))};
            p.pois=new List<WFPoiDefinition>{
                Poi("arrival",PoiType.Waypoint,64,8,"exit:aschenzoll"),
                Poi("onward",PoiType.Waypoint,64,120,"exit:onward"),
                Poi("cave",PoiType.DungeonEntrance,12,64,"cave:side.v1"),
                Poi("treasure",PoiType.Landmark,112,64,"treasure"),
                Poi("rest",PoiType.Landmark,64,64,"rest"),
                Poi("west.south",PoiType.SpawnZone,44,32,"encounter"),
                Poi("west.north",PoiType.SpawnZone,48,96,"encounter"),
                Poi("east.south",PoiType.SpawnZone,84,32,"encounter"),
                Poi("east.north",PoiType.SpawnZone,80,96,"encounter")};
            if (extent == 256)
            {
                p.name = "Journey_Wald_256_" + seed;
                foreach (var path in p.paths)
                    for (int i = 0; i < path.points.Count; i++) path.points[i] *= 2f;
                foreach (var poi in p.pois) poi.position *= 2f;
            }
            if (region != null)
            {
                float bend = region == RegionNetwork.AlteMuehle ? -63 : region == RegionNetwork.Waldweg ? 78 :
                    region == RegionNetwork.Aschenzoll ? 48 : region == RegionNetwork.Aschengrenze ? -35 : 26;
                p.name = region;
                ((WFNoiseLayer)p.heightLayers[0]).amplitude = region == RegionNetwork.Waldweg ? 9 :
                    region == RegionNetwork.AlteMuehle ? 6 : 4;
                var south = new Vector2(128, 12);
                var north = new Vector2(128 + bend * .55f, 244);
                var centre = new Vector2(128, 128);
                var west = new Vector2(46, 124 + bend * .4f);
                var east = new Vector2(218, 140 - bend * .5f);
                p.paths = new List<WFPath> {
                    Path("Hauptweg", south, new Vector2(128 - bend, 72), centre, new Vector2(128 + bend, 185), north),
                    Path("Westschleife", south, west, north), Path("Querverbindung", west, centre, east) };
                p.pois = new List<WFPoiDefinition> { Poi("arrival", PoiType.Waypoint, south.x, south.y, "arrival"),
                    Poi("centre", PoiType.Landmark, centre.x, centre.y, "centre"),
                    Poi("onward", PoiType.Waypoint, north.x, north.y, "onward"),
                    Poi("west", PoiType.Landmark, west.x, west.y, "vista"), Poi("east", PoiType.Landmark, east.x, east.y, "vista") };
            }
            // Persist the fresh profile before the next asset is created: an import in between reloads an unsaved
            // asset from disk, which silently restored the 4 x 64 m defaults instead of the 128 m pilot.
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
            var biome=LoadOrCreate<BiomeProfile>(folder+"/Biome.asset");
            biome.spawnRules.Clear();biome.textureRules.Clear();
            foreach(string key in region == null ? new[]{"WoodlandTreeA","WoodlandTreeB"} : new[]{"AshPine"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ARE/" + (region == null ? "Visuals/" : "Borderland/") + key + ".prefab");
                if(prefab==null)throw new InvalidOperationException("Missing existing private visual: "+key);
                var renderers=prefab.GetComponentsInChildren<Renderer>();
                if(renderers.Length==0)throw new InvalidOperationException("Tree has no renderers: "+key);
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float scale=5f/Mathf.Max(.1f,bounds.size.y);
                biome.spawnRules.Add(new WFSpawnRule{name=key,kind=SpawnKind.GameObject,prefabs=new List<GameObject>{prefab},
                    densityPer100m2=.6f,minSpacing=6,clearRadius=2,maxCount=region == null ? 70 : region == RegionNetwork.Waldweg ? 270 : 160,scaleMin=scale,scaleMax=scale*1.35f,
                    coreObstacle=true,coreObstacleRadius=.5f/scale,coreBlocksSight=true,alignToNormal=0,sinkDepth=0,
                    mask=new WFMask{pathMode=PathMode.AvoidPath,pathDistance=6,pathFalloff=2,useSlope=true,slopeMaxDeg=25}});
            }
            p.biome=biome;
            EditorUtility.SetDirty(biome);AssetDatabase.SaveAssetIfDirty(biome);
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
            return p;
        }
        private static WFPath Path(string name,params Vector2[] points)=>new WFPath{name=name,width=7,carveDepth=.08f,shoulderHeight=0,flatten=.8f,smooth=false,points=points.ToList()};
        private static WFPoiDefinition Poi(string id,PoiType kind,float x,float z,string tags)=>new WFPoiDefinition{id=id,type=kind,position=new Vector2(x,z),radius=4,flattenRadius=5,flattenFalloff=4,clearSpawnRadius=8,tags=tags};
        private static T LoadOrCreate<T>(string path) where T:ScriptableObject
        {
            var found=AssetDatabase.LoadAssetAtPath<T>(path);
            if(found!=null){if(EditorUtility.IsDirty(found))throw new InvalidOperationException("Authoring output has unsaved edits: "+path);return found;}
            var result=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(result,path);return result;
        }
        private static void EnsureFolder(string folder)
        {
            if(AssetDatabase.IsValidFolder(folder))return;
            string parent=System.IO.Path.GetDirectoryName(folder).Replace('\\','/');EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(folder));
        }
        private static string Sha(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
