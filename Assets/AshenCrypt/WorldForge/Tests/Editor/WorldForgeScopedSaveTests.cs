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
        { VerifyScope("heights"); }

        [TestCase("all")]
        [TestCase("repaint")]
        [TestCase("respawn")]
        [TestCase("clear")]
        public void OtherTerrainOperationsRetainUnrelatedUnsavedAssets(string operation)
        { VerifyScope(operation); }

        private static void VerifyScope(string operation)
        {
            const string folder="Assets/WF_ScopedSave_Test";
            Assert.IsFalse(AssetDatabase.IsValidFolder(folder),"Test output must be isolated");
            var previous=SceneManager.GetActiveScene();
            AssetDatabase.CreateFolder("Assets","WF_ScopedSave_Test");
            Scene scene=default;
            var p=ScriptableObject.CreateInstance<WorldProfile>();
            try
            {
                Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/Arena.unity",folder+"/isolated.unity"));
                scene=EditorSceneManager.OpenScene(folder+"/isolated.unity",OpenSceneMode.Additive);
                Assert.AreEqual(0,scene.rootCount,"Generation fixture requires the existing empty arena template");
                SceneManager.SetActiveScene(scene);
                var sentinel=ScriptableObject.CreateInstance<WorldProfile>();
                AssetDatabase.CreateAsset(sentinel,folder+"/Unrelated.asset");
                AssetDatabase.SaveAssetIfDirty(sentinel);
                byte[] original=File.ReadAllBytes(folder+"/Unrelated.asset");
                p.tilesX=p.tilesZ=1;p.tileSize=32;p.heightmapResolution=33;
                p.rootObjectName="WF_ScopedSave_Test";p.outputFolder=folder+"/Generated";
                p.heightLayers.Add(new WFNoiseLayer{amplitude=1,baseHeight=2});
                if(operation!="heights")WFTerrainBuilder.GenerateHeightsOnly(p);
                sentinel.seed=999;EditorUtility.SetDirty(sentinel);
                if(operation=="heights")WFTerrainBuilder.GenerateHeightsOnly(p);
                else if(operation=="all")WFTerrainBuilder.GenerateAll(p);
                else if(operation=="repaint")WFTerrainBuilder.RepaintTextures(p);
                else if(operation=="respawn")WFTerrainBuilder.Respawn(p);
                else WFTerrainBuilder.ClearSpawned(p);
                CollectionAssert.AreEqual(original,File.ReadAllBytes(folder+"/Unrelated.asset"),"Generation saved an unrelated dirty asset");
                Assert.IsTrue(EditorUtility.IsDirty(sentinel),"Unrelated edits must remain unsaved");
                Assert.IsTrue(scene.isDirty,"Generated scene remains under its caller's save ownership");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
                Object.DestroyImmediate(p);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
