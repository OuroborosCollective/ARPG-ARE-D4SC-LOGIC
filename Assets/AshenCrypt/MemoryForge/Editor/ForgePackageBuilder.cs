using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AshenCrypt.MemoryForge.Samples;
using AshenCrypt.MemoryForge.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AshenCrypt.MemoryForge.Editor
{
    public static class ForgePackageBuilder
    {
        public const string Root="Assets/AshenCrypt/MemoryForge";
        public const string DemoPath=Root+"/Samples/RememberingVillage/RememberingVillage.unity";
        [MenuItem("Tools/Memory Forge/Open remembering-village example")]
        public static void OpenDemo()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(DemoPath))BuildDemo();
            EditorSceneManager.OpenScene(DemoPath);
        }
        public static void BuildDemo()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Memory Forge - Remembering Village").AddComponent<RememberingVillage>();
            EditorSceneManager.SaveScene(scene,DemoPath);
            CreateDefaults();AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Memory Forge/Create example controller assets")]
        public static void CreateDefaults()
        {
            string folder=Root+"/Samples/RememberingVillage/Controllers";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var registry=ForgeDefaults.CreateRegistry();var entries=new List<ForgeRegistryEntry>();
            foreach(var controller in registry.Versions)
            {
                string path=folder+"/"+controller.ArchetypeId+"-v1.asset";var asset=AssetDatabase.LoadAssetAtPath<ForgeControllerAsset>(path);
                if(asset==null)
                {
                    asset=ScriptableObject.CreateInstance<ForgeControllerAsset>();
                    var cases=new List<RegressionCase>{new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>()),ForgeAction.Wait),new RegressionCase(new ObservationFrame(0,Array.Empty<Observation>()),ForgeAction.None)};
                    if(ActionSet.Contains(controller.Archetype.AllowedActions,ForgeAction.Rest))cases.Add(new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Rest,ForgeAction.Travel),new[]{new Observation(Feature.Fatigue,10000)}),ForgeAction.Rest));
                    if(ActionSet.Contains(controller.Archetype.AllowedActions,ForgeAction.Attack))cases.Add(new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Attack,ForgeAction.Flee),new[]{new Observation(Feature.Danger,10000),new Observation(Feature.Hostility,10000)}),ForgeAction.Attack));
                    asset.CopyCandidate(controller,cases);asset.SuiteId="ashen.utility-v1";
                    if(!asset.ReviewAndSeal(out var error))throw new InvalidOperationException(error);
                    AssetDatabase.CreateAsset(asset,path);
                }
                entries.Add(new ForgeRegistryEntry{Controller=asset,ForNewSpawns=true});
            }
            string registryPath=folder+"/DefaultRegistry.asset";
            if(AssetDatabase.LoadAssetAtPath<ForgeRegistryAsset>(registryPath)==null)
            {
                var config=ScriptableObject.CreateInstance<ForgeRegistryAsset>();config.Entries=entries;config.Build();AssetDatabase.CreateAsset(config,registryPath);
            }
            AssetDatabase.SaveAssets();
        }
        public static void ExportBatch()
        {
            string output=Environment.GetEnvironmentVariable("FORGE_PACKAGE_OUTPUT");
            if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Set FORGE_PACKAGE_OUTPUT to an explicit .unitypackage path");
            if(!File.Exists(DemoPath))BuildDemo();else CreateDefaults();
            var files=Directory.GetFiles(Root,"*",SearchOption.AllDirectories);
            foreach(string file in files.Where(f=>f.EndsWith(".cs",StringComparison.Ordinal)))
                if(File.ReadAllText(file).Contains("using AreArpg."))throw new InvalidOperationException("Package must not depend on the game core: "+file);
            foreach(string asset in AssetDatabase.GetDependencies(AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith(Root+"/",StringComparison.Ordinal)).ToArray(),true))
                if(asset.StartsWith("Assets/",StringComparison.Ordinal)&&!asset.StartsWith(Root+"/",StringComparison.Ordinal)&&asset!=Root)throw new InvalidOperationException("External asset dependency: "+asset);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            AssetDatabase.ExportPackage(Root,output,ExportPackageOptions.Recurse);
            Debug.Log("Memory Forge exported to "+output+"; only the independently owned package root was selected.");
        }
    }
}
