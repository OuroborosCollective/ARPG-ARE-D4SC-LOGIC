using System;
using System.Collections.Generic;
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

        /// <summary>Review follow-up: an existing but empty folder gets a real snapshot, so its restore cannot mask the failure.</summary>
        [Test]
        public void EmptyFolderSnapshotRestoresWithoutError()
        {
            const string folder = "Assets/JourneyAuthoringEmpty_Test";
            Assert.IsFalse(Directory.Exists(folder), "Test output must be isolated");
            Directory.CreateDirectory(folder);
            try
            {
                var snapshot = (string)Tool("SnapshotFolder").Invoke(null, new object[] { folder, "Temp/JourneyAuthoringEmpty_Backup" });
                Assert.IsTrue(snapshot != null && Directory.Exists(snapshot), "An empty folder still has a snapshot to restore from");
                File.WriteAllText(folder + "/Created.txt", "new");
                Assert.DoesNotThrow(() => Tool("RestoreFolder").Invoke(null, new object[] { snapshot, folder, null }));
                Assert.IsFalse(File.Exists(folder + "/Created.txt"), "Files created by the failed run are removed");
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                if (File.Exists(folder + ".meta")) File.Delete(folder + ".meta");
                AssetDatabase.Refresh();
            }
        }

        /// <summary>Review follow-up: unsaved edits are refused before the run or its rollback can touch them.</summary>
        [Test]
        public void UnsavedAuthoringEditsAreRefusedBeforeAnythingIsTouched()
        {
            const string folder = "Assets/JourneyAuthoringDirty_Test";
            Assert.IsFalse(AssetDatabase.IsValidFolder(folder), "Test output must be isolated");
            AssetDatabase.CreateFolder("Assets", "JourneyAuthoringDirty_Test");
            try
            {
                var profile = ScriptableObject.CreateInstance(Find("AshenCrypt.WorldForge.WorldProfile"));
                var seedField = profile.GetType().GetField("seed");
                seedField.SetValue(profile, 5);
                AssetDatabase.CreateAsset(profile, folder + "/Profile.asset");
                var biome = ScriptableObject.CreateInstance(Find("AshenCrypt.WorldForge.BiomeProfile"));
                AssetDatabase.CreateAsset(biome, folder + "/Biome.asset");
                AssetDatabase.SaveAssets();
                byte[] biomeBytes = File.ReadAllBytes(folder + "/Biome.asset");
                var reject = Tool("RejectUnsavedAuthoringEdits");
                Assert.DoesNotThrow(() => reject.Invoke(null, new object[] { folder }), "Saved authoring assets are accepted");

                EditorUtility.SetDirty(biome);
                var error = Assert.Throws<TargetInvocationException>(() => reject.Invoke(null, new object[] { folder }));
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
                error = Assert.Throws<TargetInvocationException>(() => Tool("AuthorSeed").Invoke(null, new object[] {
                    731999, folder, folder + "/Maps", new Dictionary<int, byte[]>(), (Func<Scene, string, bool>)((s, p) => EditorSceneManager.SaveScene(s, p)) }));
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException, "Authoring refuses a folder with unsaved edits");
                Assert.AreEqual(5, seedField.GetValue(profile), "The profile was not rewritten before the refusal");
                Assert.IsTrue(EditorUtility.IsDirty(biome), "The unsaved edit is still there");
                CollectionAssert.AreEqual(biomeBytes, File.ReadAllBytes(folder + "/Biome.asset"), "Nothing was restored over the unsaved asset");
                Assert.IsFalse(File.Exists(folder + "/GenerationScratch.unity"), "No scratch scene was created");
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        /// <summary>
        /// Review follow-up: when only saving the authored scene fails, the verified generated world is kept for recovery —
        /// saved into the scratch scene when possible, otherwise left open — and the folder is not rolled back.
        /// </summary>
        [Test, Timeout(900000)]
        public void FailedSceneSaveKeepsTheGeneratedWorldForRecovery()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ARE/Visuals/WoodlandTreeA.prefab") == null)
                Assert.Ignore("Requires the private woodland visuals of the native workspace.");
            const string root = "Assets/JourneyAuthoringSave_Test";
            Assert.IsFalse(AssetDatabase.IsValidFolder(root), "Test output must be isolated");
            AssetDatabase.CreateFolder("Assets", "JourneyAuthoringSave_Test");
            var previous = SceneManager.GetActiveScene();
            var author = Tool("AuthorSeed");
            const int seed = 731904;
            try
            {
                // A successful run saves the authored scene and removes its scratch scene.
                string ok = root + "/Saved";
                author.Invoke(null, new object[] { seed, ok, root + "/Maps1", new Dictionary<int, byte[]>(), (Func<Scene, string, bool>)((s, p) => EditorSceneManager.SaveScene(s, p)) });
                Assert.IsTrue(File.Exists(ok + "/Authored.unity") && !File.Exists(ok + "/GenerationScratch.unity"));

                // Saving the authored scene fails, saving the scratch scene works: the world is saved there for recovery.
                string rescued = root + "/Rescued";
                var error = Assert.Throws<TargetInvocationException>(() => author.Invoke(null, new object[] { seed, rescued, root + "/Maps2", new Dictionary<int, byte[]>(),
                    (Func<Scene, string, bool>)((s, p) => !p.EndsWith("/Authored.unity") && EditorSceneManager.SaveScene(s, p)) }));
                StringAssert.Contains("saved for recovery", error.InnerException.Message);
                Assert.IsFalse(File.Exists(rescued + "/Authored.unity"));
                StringAssert.Contains("Journey_WorldForge_" + seed, File.ReadAllText(rescued + "/GenerationScratch.unity"), "The recovery scene holds the generated world");
                Assert.IsFalse(SceneManager.GetSceneByPath(rescued + "/GenerationScratch.unity").isLoaded, "A saved recovery scene is closed");
                Assert.IsNotEmpty(Directory.GetFiles(rescued + "/Generated", "*.asset"), "The generated terrain assets are kept, not rolled back");
                Assert.IsTrue(File.Exists(rescued + "/Profile.asset"));

                // Neither save works: the generated world stays open in the editor and nothing is rolled back.
                string open = root + "/Open";
                error = Assert.Throws<TargetInvocationException>(() => author.Invoke(null, new object[] { seed, open, root + "/Maps3", new Dictionary<int, byte[]>(),
                    (Func<Scene, string, bool>)((s, p) => false) }));
                StringAssert.Contains("stays open", error.InnerException.Message);
                var kept = SceneManager.GetSceneByPath(open + "/GenerationScratch.unity");
                Assert.IsTrue(kept.isLoaded, "The unsaved generated world stays open for manual saving");
                Assert.IsTrue(kept.GetRootGameObjects().Any(g => g.name == "Journey_WorldForge_" + seed), "The open scene holds the generated world");
                Assert.IsNotEmpty(Directory.GetFiles(open + "/Generated", "*.asset"), "The generated terrain assets are kept, not rolled back");
                Assert.AreEqual(previous, SceneManager.GetActiveScene(), "The caller's active scene is restored");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s.path.StartsWith(root + "/")) EditorSceneManager.CloseScene(s, true);
                }
                AssetDatabase.DeleteAsset(root);
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
