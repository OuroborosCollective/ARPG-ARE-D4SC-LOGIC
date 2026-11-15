using System;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fadenwerk.Tests
{
    public class FadenwerkAuthoringSceneSafetyTests
    {
        [Test]
        public void SamplePreservesExistingSceneAndCreatesNoRuntimeAuthority()
        {
            Type sample = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                sample = assembly.GetType("Fadenwerk.EditorTools.FadenwerkSample");
                if (sample != null) break;
            }
            Assert.IsNotNull(sample);
            var create = sample.GetMethod("CreateAuthoringScene", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(create, "An isolated authoring-only sample entry point is required");
            Scene original = SceneManager.GetActiveScene();
            GameObject[] originalRoots = original.GetRootGameObjects();
            string originalPath = original.path;
            bool originalDirty = original.isDirty;
            string fixturePath = AssetDatabase.GenerateUniqueAssetPath("Assets/FadenwerkAuthoringFixture.unity");
            Scene fixture = default(Scene);
            Scene authored = default(Scene);
            FadenwerkDatabase db = null;
            const string sampleFolder = "Assets/FadenwerkSamples";
            bool sampleFolderExisted = AssetDatabase.IsValidFolder(sampleFolder);
            try
            {
                TestContext.WriteLine("Original dirty before fixture: " + original.isDirty);
                Assert.IsTrue(AssetDatabase.CopyAsset("Packages/com.ouroboros.fadenwerk/Editor/FadenwerkAuthoring.unity", fixturePath));
                TestContext.WriteLine("Original dirty after fixture copy: " + original.isDirty);
                fixture = EditorSceneManager.OpenScene(fixturePath, OpenSceneMode.Additive);
                TestContext.WriteLine("Original dirty after fixture open: " + original.isDirty);
                Assert.IsTrue(SceneManager.SetActiveScene(fixture));
                var sentinel = new GameObject("existing-scene-sentinel");
                EditorSceneManager.MarkSceneDirty(fixture);
                GameObject[] before = fixture.GetRootGameObjects();
                Assert.IsTrue(fixture.isDirty, "fixture must contain unsaved changes");
                TestContext.WriteLine("Original dirty immediately before API: " + original.isDirty);
                db = ScriptableObject.CreateInstance<FadenwerkDatabase>();
                var root = (GameObject)create.Invoke(null, new object[] { db });
                Assert.IsNotNull(root);
                authored = root.scene;
                TestContext.WriteLine("Original dirty after API: " + original.isDirty);
                Assert.IsTrue(fixture.isDirty, "unsaved fixture changes must remain unsaved");
                Assert.AreNotEqual(fixture.handle, authored.handle, "sample must use its own scene");
                CollectionAssert.AreEquivalent(before, fixture.GetRootGameObjects());
                Assert.AreEqual(fixture.handle, sentinel.scene.handle);
                foreach (var obj in authored.GetRootGameObjects())
                {
                    Assert.IsEmpty(obj.GetComponentsInChildren<FadenwerkManager>(true));
                    Assert.IsEmpty(obj.GetComponentsInChildren<FadenwerkDebugHud>(true));
                }
                Assert.AreEqual(2, db.quests.Count);
                Assert.AreEqual(3, root.GetComponentsInChildren<FadenwerkNpc>(true).Length);
                Assert.AreEqual(3, root.GetComponentsInChildren<FadenwerkMarker>(true).Length);
                Assert.AreEqual(authored.handle, SceneManager.GetActiveScene().handle);
                Assert.IsTrue(original.IsValid() && original.isLoaded);
                CollectionAssert.AreEquivalent(originalRoots, original.GetRootGameObjects());
                Assert.AreEqual(originalPath, original.path);
                Assert.AreEqual(originalDirty, original.isDirty);

            }
            finally
            {
                if (authored.IsValid() && authored.isLoaded)
                {
                    string ownedPath = authored.path;
                    EditorSceneManager.CloseScene(authored, true);
                    AssetDatabase.DeleteAsset(ownedPath);
                }
                if (original.IsValid() && original.isLoaded)
                {
                    SceneManager.SetActiveScene(original);
                    if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
                }
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(fixturePath);
                if (db != null) UnityEngine.Object.DestroyImmediate(db);
                if (!sampleFolderExisted && AssetDatabase.IsValidFolder(sampleFolder)
                    && Directory.GetFileSystemEntries(sampleFolder).Length == 0)
                    AssetDatabase.DeleteAsset(sampleFolder);
            }
        }
    }
}
