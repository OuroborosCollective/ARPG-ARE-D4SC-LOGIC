using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Review fixes for the bounded World Forge journey authoring (PR #44).</summary>
    public sealed class JourneyAuthoringSafetyTests
    {
        private static Type Find(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        private static MethodInfo Tool(string method) =>
            Find("AreArpg.EditorTools.JourneyWorldAuthoring").GetMethod(method, BindingFlags.Public | BindingFlags.Static);

        [Test]
        public void FailedAuthoringRestoresItsFolderByteForByte()
        {
            const string folder = "Assets/JourneyAuthoringSafety_Test";
            Assert.IsFalse(Directory.Exists(folder), "Test output must be isolated");
            Directory.CreateDirectory(folder + "/Generated");
            File.WriteAllText(folder + "/Profile.txt", "original");
            File.WriteAllBytes(folder + "/Generated/terrain.bytes", new byte[] { 1, 2, 3 });
            try
            {
                var snapshot = (string)Tool("SnapshotFolder").Invoke(null, new object[] { folder, "Temp/JourneyAuthoringSafety_Backup" });
                File.WriteAllText(folder + "/Profile.txt", "rewritten");
                File.WriteAllBytes(folder + "/Generated/terrain.bytes", new byte[] { 9 });
                File.WriteAllText(folder + "/Created.txt", "new");
                File.WriteAllText(folder + "/Scratch.txt", "kept for recovery");
                Tool("RestoreFolder").Invoke(null, new object[] { snapshot, folder, folder + "/Scratch.txt" });
                Assert.AreEqual("original", File.ReadAllText(folder + "/Profile.txt"));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(folder + "/Generated/terrain.bytes"));
                Assert.IsFalse(File.Exists(folder + "/Created.txt"), "Files created by the failed run are removed");
                Assert.IsTrue(File.Exists(folder + "/Scratch.txt"), "An explicitly kept recovery file survives");
                Assert.IsNull(Tool("SnapshotFolder").Invoke(null, new object[] { folder + "/Missing", "Temp/JourneyAuthoringSafety_None" }));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                if (File.Exists(folder + ".meta")) File.Delete(folder + ".meta");
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void AuthoringRefusesAProfileThatAnOpenWorldUses()
        {
            const string folder = "Assets/JourneyAuthoringOpenWorld_Test";
            Assert.IsFalse(AssetDatabase.IsValidFolder(folder), "Test output must be isolated");
            AssetDatabase.CreateFolder("Assets", "JourneyAuthoringOpenWorld_Test");
            var previous = SceneManager.GetActiveScene();
            Scene scene = default;
            try
            {
                var profileType = Find("AshenCrypt.WorldForge.WorldProfile");
                var profile = ScriptableObject.CreateInstance(profileType);
                AssetDatabase.CreateAsset(profile, folder + "/Profile.asset");
                var reject = Tool("RejectLoadedWorldFor");
                Assert.DoesNotThrow(() => reject.Invoke(null, new object[] { folder + "/Profile.asset" }), "No open world uses the profile yet");
                Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/Arena.unity", folder + "/open.unity"));
                scene = EditorSceneManager.OpenScene(folder + "/open.unity", OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var world = new GameObject("Open world").AddComponent(Find("AshenCrypt.WorldForge.ForgedWorld"));
                world.GetType().GetField("profile").SetValue(world, profile);
                var error = Assert.Throws<TargetInvocationException>(() => reject.Invoke(null, new object[] { folder + "/Profile.asset" }));
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException, "Generation must not write into another open world");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
