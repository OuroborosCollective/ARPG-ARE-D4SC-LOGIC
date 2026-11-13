using System;
using System.Collections;
using System.IO;
using AshenCrypt.MemoryForge.Samples;
using AshenCrypt.MemoryForge.Integration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshenCrypt.MemoryForge.Tests
{
    public sealed class RememberingVillageTests
    {
        [UnityTest]
        public IEnumerator RenderingCannotAdvanceCanonicalSampleTicks()
        {
            var go=new GameObject("Sample tick boundary under test");
            try
            {
                var sample=go.AddComponent<RememberingVillage>();sample.Running=false;yield return null;
                sample.Execute("approach");sample.Running=true;
                var update=typeof(RememberingVillage).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                string before=sample.World.Save();
                for(int i=0;i<1000;i++)update.Invoke(sample,null);
                Assert.AreEqual(before,sample.World.Save(),"Rendering cannot schedule or drop canonical world steps");
            }
            finally{UnityEngine.Object.Destroy(go);}
            yield return null;
        }
        [UnityTest]
        public IEnumerator ActualExampleRemembersHelpRestoresSaveAndRejectsOccludedWitness()
        {
            bool existed=PlayerPrefs.HasKey(RememberingVillage.SaveKey);string previous=PlayerPrefs.GetString(RememberingVillage.SaveKey,"");
            var go=new GameObject("Actual standalone sample under test");
            try
            {
                var sample=go.AddComponent<RememberingVillage>();sample.Running=false;yield return null;
                sample.Execute("approach");for(int i=0;i<20;i++)sample.Execute("step");
                sample.Execute("food");for(int i=0;i<4;i++)sample.Execute("step");
                Assert.AreEqual(2,sample.World.Provisions);Assert.AreEqual(1200,sample.World.Archive.Get("nara").RelationshipTo("player").Trust);
                Assert.AreEqual(ForgeAction.Observe,sample.World.Archive.Get("nara").DecisionState.Action);
                Assert.AreEqual(0,sample.World.Archive.Get("watch").Known.Count);
                sample.Execute("leave");for(int i=0;i<20;i++)sample.Execute("step");
                sample.Execute("approach");for(int i=0;i<20;i++)sample.Execute("step");
                Assert.That(sample.World.Greet(),Does.Contain("food"));sample.Execute("greet");
                string saved=sample.World.Save();sample.Execute("save");sample.Execute("reset");Assert.AreEqual(0,sample.World.Archive.Get("nara").Known.Count);
                sample.Execute("load");Assert.AreEqual(saved,sample.World.Save());
                Assert.AreSame(sample.World.Archive.Get("nara"),go.transform.Find("Nara").GetComponent<ForgeNpcView>().Mind);
                Assert.AreEqual(sample.World.NaraX/1000f,go.transform.Find("Nara").position.x);
                yield return null;
                string evidence=Environment.GetEnvironmentVariable("FORGE_EVIDENCE_DIR");
                if(!string.IsNullOrEmpty(evidence))
                {
                    Directory.CreateDirectory(evidence);string path=Path.Combine(evidence,"remembering-village-after-load.png");
                    // Batch mode has no end-of-frame screen capture. Render the actual sample camera explicitly.
                    var target=new RenderTexture(1600,900,24);var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
                    var previousTarget=sample.SampleCamera.targetTexture;var active=RenderTexture.active;var viewport=sample.SampleCamera.rect;
                    try
                    {
                        sample.SampleCamera.rect=new Rect(0,0,1,1);sample.SampleCamera.targetTexture=target;sample.SampleCamera.Render();RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
                    }
                    finally{sample.SampleCamera.rect=viewport;sample.SampleCamera.targetTexture=previousTarget;RenderTexture.active=active;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);}
                    Assert.Greater(new FileInfo(path).Length,10000);
                }
                sample.Execute("rob");Assert.AreEqual(12,sample.World.Coins);Assert.AreEqual(0,sample.World.Archive.Get("watch").Known.Count);
                sample.Execute("reset");sample.Execute("approach");sample.Execute("witness");for(int i=0;i<20;i++)sample.Execute("step");sample.Execute("rob");
                Assert.AreEqual(KnowledgeOrigin.Witnessed,sample.World.Archive.Get("watch").Known[0].Origin);
                for(int i=0;i<50;i++)sample.Execute("step");Assert.Less(sample.World.PlayerHealth,1000,"The remembered crime must cause a real host effect");
            }
            finally
            {
                UnityEngine.Object.Destroy(go);
                if(existed)PlayerPrefs.SetString(RememberingVillage.SaveKey,previous);else PlayerPrefs.DeleteKey(RememberingVillage.SaveKey);PlayerPrefs.Save();
            }
            yield return null;
        }
    }
}
