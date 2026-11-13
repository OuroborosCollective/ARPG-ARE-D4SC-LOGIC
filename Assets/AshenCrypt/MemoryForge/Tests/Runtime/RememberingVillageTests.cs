using System;
using System.Collections;
using System.IO;
using AshenCrypt.MemoryForge.Samples;
using AshenCrypt.MemoryForge.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshenCrypt.MemoryForge.Tests
{
    public sealed class RememberingVillageTests
    {
        [UnityTest]
        public IEnumerator ActualExampleRemembersHelpRestoresSaveAndRejectsOccludedWitness()
        {
            bool existed=PlayerPrefs.HasKey(RememberingVillage.SaveKey);string previous=PlayerPrefs.GetString(RememberingVillage.SaveKey,"");
            var go=new GameObject("Actual standalone sample under test");
            try
            {
                var sample=go.AddComponent<RememberingVillage>();sample.Running=false;yield return null;
                sample.Execute("approach");for(int i=0;i<20;i++)sample.Execute("step");
                sample.Execute("food");sample.Execute("step");
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
                    ScreenCapture.CaptureScreenshot(path);
                    for(int i=0;i<20&&!File.Exists(path);i++)yield return null;
                    Assert.IsTrue(File.Exists(path),"The actual Unity screenshot must exist");Assert.Greater(new FileInfo(path).Length,10000);
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
