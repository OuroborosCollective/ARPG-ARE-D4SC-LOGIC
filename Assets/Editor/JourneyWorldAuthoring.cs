using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        /// <summary>
        /// Author one seed into its folder. Any failure restores the folder byte for byte, with one exception: when only
        /// saving the authored scene fails, the published map has already been verified, so the generated world and its
        /// assets are kept for recovery — saved into the scratch scene when possible, otherwise left open in the editor.
        /// </summary>
        public static object AuthorSeed(int seed,string folder,string maps,Dictionary<int,byte[]> canonical,Func<Scene,string,bool> save)
        {
            var prior=SceneManager.GetActiveScene();
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
            Scene scene=default;
            bool completed=false,recovering=false,rescued=false;
            try
            {
                scene=EditorSceneManager.OpenScene(scratch,OpenSceneMode.Additive);
                if(scene.rootCount!=0)throw new InvalidOperationException("Authoring template must remain empty");
                SceneManager.SetActiveScene(scene);
                // Only this saved additive scene is the generation target. All other open scenes stay open.
                var p=CreateProfile(seed,folder);
                var root=new GameObject(p.rootObjectName);
                root.transform.position=new Vector3(-64,0,-64);
                var world=root.AddComponent<ForgedWorld>();world.profile=p;
                var report=WFTerrainBuilder.GenerateAll(p);
                var map=WFCoreMapExporter.Build(p,world,report);
                if(map.NodesX!=129||map.NodesZ!=129||map.WidthMm!=128000||map.DepthMm!=128000)
                    throw new InvalidOperationException("Pilot map dimensions differ from 128m / 129 nodes");
                byte[] bytes=map.ToBytes();
                if(canonical.TryGetValue(seed,out var first)&&!first.SequenceEqual(bytes))
                    throw new InvalidOperationException("Repeated World Forge seed changed canonical bytes");
                canonical[seed]=bytes;
                string path=maps+"/Wald_v1_"+seed.ToString(CultureInfo.InvariantCulture)+".bytes";
                // Published identities are immutable: a different map requires a new authored version.
                if(File.Exists(path)&&!File.ReadAllBytes(path).SequenceEqual(bytes))
                    throw new InvalidOperationException("Existing map differs; retain it and author a new version: "+path);
                File.WriteAllBytes(path,bytes);
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
                    areaSquareMetres=16384,obstacles=map.Obstacles.Count,pois=map.Pois.Count,
                    paths=p.paths.Count,seconds=report.seconds,report.treeInstances,report.gameObjects,
                    warnings=report.warnings.ToArray(),manualTerrainEdits=0};
                completed=true;
                return row;
            }
            finally
            {
                if(prior.IsValid()&&prior.isLoaded)SceneManager.SetActiveScene(prior);
                // A generated world that could not be saved anywhere stays open; every other run closes its scene.
                if(scene.IsValid()&&scene.isLoaded&&!(recovering&&!rescued))EditorSceneManager.CloseScene(scene,true);
                if(!recovering)AssetDatabase.DeleteAsset(scratch);
                // A failed run must leave the authoring assets able to reproduce the retained published map.
                if(!completed&&!recovering)RestoreFolder(snapshot,folder,null);
            }
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
        /// Restore the snapshot byte for byte; files created since are removed except an explicitly kept one. Files whose
        /// bytes are unchanged are not touched, so they are neither reimported nor reloaded.
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

        public static WorldProfile CreateProfile(int seed,string folder)
        {
            var p=LoadOrCreate<WorldProfile>(folder+"/Profile.asset");
            p.name="Journey_Wald_v1_"+seed;p.tilesX=p.tilesZ=1;p.tileSize=128;p.maxHeight=12;
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
            var biome=LoadOrCreate<BiomeProfile>(folder+"/Biome.asset");
            biome.spawnRules.Clear();biome.textureRules.Clear();
            foreach(string key in new[]{"WoodlandTreeA","WoodlandTreeB"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ARE/Visuals/"+key+".prefab");
                if(prefab==null)throw new InvalidOperationException("Missing existing private visual: "+key);
                var renderers=prefab.GetComponentsInChildren<Renderer>();
                if(renderers.Length==0)throw new InvalidOperationException("Tree has no renderers: "+key);
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float scale=5f/Mathf.Max(.1f,bounds.size.y);
                biome.spawnRules.Add(new WFSpawnRule{name=key,kind=SpawnKind.GameObject,prefabs=new List<GameObject>{prefab},
                    densityPer100m2=.6f,minSpacing=6,clearRadius=2,maxCount=70,scaleMin=scale,scaleMax=scale*1.35f,
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
