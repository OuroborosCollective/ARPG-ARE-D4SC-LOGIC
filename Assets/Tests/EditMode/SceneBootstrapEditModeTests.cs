using System.Linq;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreArpg.EditMode.Tests
{
    public sealed class SceneBootstrapEditModeTests
    {
        [TestCase("Assets/AshenCrypt/MemoryForge/Samples/RememberingVillage/RememberingVillage.unity", false)]
        [TestCase("", false)]
        [TestCase("Assets/Scenes/Arena.unity", true)]
        [TestCase("Assets/Scenes/AshenEntry.unity", true)]
        public void AutomaticGameBootstrapOnlyOwnsTheTwoGameScenes(string path, bool ownsScene)
        {
            var previous=SceneManager.GetActiveScene();
            var existing=Object.FindObjectsByType<ArenaRuntime>(FindObjectsSortMode.None);
            foreach(var runtime in existing)runtime.gameObject.SetActive(false);
            var scene=path.Length==0?EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive):EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var install=typeof(ArenaRuntime).GetMethod("InstallInAnyScene",BindingFlags.Static|BindingFlags.NonPublic);
                install.Invoke(null,null);install.Invoke(null,null);
                int count=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<ArenaRuntime>(true).Length);
                Assert.AreEqual(ownsScene?1:0,count,"Opening an independent package example must not start the RPG or its save authority");
            }
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
                foreach(var runtime in existing)if(runtime!=null)runtime.gameObject.SetActive(true);
            }
        }
    }
}
