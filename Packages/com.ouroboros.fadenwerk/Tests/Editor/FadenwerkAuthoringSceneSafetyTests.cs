using System;
using System.Reflection;
using NUnit.Framework;
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
            Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
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
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (authored.IsValid() && authored.isLoaded) EditorSceneManager.CloseScene(authored, true);
                if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
                UnityEngine.Object.DestroyImmediate(db);
            }
        }
    }
}
