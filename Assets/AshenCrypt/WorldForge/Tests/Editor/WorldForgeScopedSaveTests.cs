using System.IO;
using AshenCrypt.WorldForge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshenCrypt.WorldForge.Tests
{
    public class WorldForgeScopedSaveTests
    {
        [Test]
        public void GenerationDoesNotSaveAnUnrelatedDirtyAssetOrScene()
        {
            const string folder="Assets/WF_ScopedSave_Test";
            Assert.IsFalse(AssetDatabase.IsValidFolder(folder),"Test output must be isolated");
            AssetDatabase.CreateFolder("Assets","WF_ScopedSave_Test");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var p=ScriptableObject.CreateInstance<WorldProfile>();
            try
            {
                SceneManager.SetActiveScene(scene);
                EditorSceneManager.SaveScene(scene,folder+"/isolated.unity");
                var sentinel=ScriptableObject.CreateInstance<WorldProfile>();
                AssetDatabase.CreateAsset(sentinel,folder+"/Unrelated.asset");
                AssetDatabase.SaveAssetIfDirty(sentinel);
                byte[] original=File.ReadAllBytes(folder+"/Unrelated.asset");
                sentinel.seed=999;EditorUtility.SetDirty(sentinel);
                p.tilesX=p.tilesZ=1;p.tileSize=32;p.heightmapResolution=33;
                p.rootObjectName="WF_ScopedSave_Test";p.outputFolder=folder+"/Generated";
                p.heightLayers.Add(new WFNoiseLayer{amplitude=1,baseHeight=2});
                WFTerrainBuilder.GenerateHeightsOnly(p);
                CollectionAssert.AreEqual(original,File.ReadAllBytes(folder+"/Unrelated.asset"),"Generation saved an unrelated dirty asset");
                Assert.IsTrue(EditorUtility.IsDirty(sentinel),"Unrelated edits must remain unsaved");
                Assert.IsTrue(scene.isDirty,"Generated scene remains under its caller's save ownership");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
                Object.DestroyImmediate(p);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
