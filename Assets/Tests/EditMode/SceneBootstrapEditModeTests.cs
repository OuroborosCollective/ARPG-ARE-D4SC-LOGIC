using System.Linq;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreArpg.EditMode.Tests
{
    public sealed class SceneBootstrapEditModeTests
    {
        [TestCase("Assets/AshenCrypt/MemoryForge/Samples/RememberingVillage/RememberingVillage.unity", false)]
        [TestCase("temporary-unowned-scene", false)]
        [TestCase("Assets/Scenes/Arena.unity", true)]
        [TestCase("Assets/Scenes/AshenEntry.unity", true)]
        public void AutomaticGameBootstrapOnlyOwnsTheTwoGameScenes(string path, bool ownsScene)
        {
            var previous=SceneManager.GetActiveScene();
            var existing=Object.FindObjectsByType<ArenaRuntime>(FindObjectsSortMode.None);
            foreach(var runtime in existing)runtime.gameObject.SetActive(false);
            Scene scene=default;
            string temporaryPath=null;
            bool ownsTemporary=false;
            try
            {
                if(path=="temporary-unowned-scene")
                {
                    temporaryPath=AssetDatabase.GenerateUniqueAssetPath("Assets/ForgeBootstrap-Unowned.unity");
                    Assert.IsFalse(System.IO.File.Exists(temporaryPath),"Do not overwrite an unimported existing scene");
                    ownsTemporary=AssetDatabase.CopyAsset("Assets/Scenes/Arena.unity",temporaryPath);
                    Assert.IsTrue(ownsTemporary);
                    path=temporaryPath;
                }
                scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var install=typeof(ArenaRuntime).GetMethod("InstallInAnyScene",BindingFlags.Static|BindingFlags.NonPublic);
                install.Invoke(null,null);install.Invoke(null,null);
                int count=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<ArenaRuntime>(true).Length);
                Assert.AreEqual(ownsScene?1:0,count,"Opening an independent package example must not start the RPG or its save authority");
            }
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
                if(ownsTemporary)AssetDatabase.DeleteAsset(temporaryPath);
                foreach(var runtime in existing)if(runtime!=null)runtime.gameObject.SetActive(true);
            }
        }
    }
}
