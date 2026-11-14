using System;
using System.Reflection;
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
            string fixturePath = AssetDatabase.GenerateUniqueAssetPath("Assets/FadenwerkAuthoringFixture.unity");
            Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/Arena.unity", fixturePath));
            Scene fixture = EditorSceneManager.OpenScene(fixturePath, OpenSceneMode.Additive);
            // The batch runner starts with an empty unsaved scene; never discard scene content.
            if (original.IsValid() && original.isLoaded && string.IsNullOrEmpty(original.path))
            {
                Assert.IsEmpty(original.GetRootGameObjects(), "fixture must not discard an existing scene");
                Assert.IsFalse(original.isDirty, "fixture must not discard unsaved edits");
                EditorSceneManager.CloseScene(original, true);
            }
            SceneManager.SetActiveScene(fixture);
            var sentinel = new GameObject("existing-scene-sentinel");
            GameObject[] before = fixture.GetRootGameObjects();
            var db = ScriptableObject.CreateInstance<FadenwerkDatabase>();
            Scene authored = default(Scene);
            try
            {
                var root = (GameObject)create.Invoke(null, new object[] { db });
                Assert.IsNotNull(root);
                authored = root.scene;
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
                string[] databases = AssetDatabase.FindAssets("t:FadenwerkDatabase");
                sample.GetMethod("Create", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                CollectionAssert.AreEquivalent(databases, AssetDatabase.FindAssets("t:FadenwerkDatabase"), "unsaved scene guard must not create a database");
                Assert.AreEqual(authored.handle, SceneManager.GetActiveScene().handle);
                Assert.Throws<TargetInvocationException>(() => create.Invoke(null, new object[] { db }));
            }
            finally
            {
                if (authored.IsValid() && authored.isLoaded) EditorSceneManager.CloseScene(authored, true);
                if (original.IsValid() && original.isLoaded)
                {
                    SceneManager.SetActiveScene(original);
                    if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
                }
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(fixturePath);
                UnityEngine.Object.DestroyImmediate(db);
            }
        }
    }
}
