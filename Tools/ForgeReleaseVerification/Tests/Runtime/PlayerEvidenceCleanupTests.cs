using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AshenCrypt.MemoryForge.Samples;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshenCrypt.ForgeVerification.Tests
{
    public sealed class PlayerEvidenceCleanupTests
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Restore(bool existed,string previous)
        {if(existed)PlayerPrefs.SetString(RememberingVillage.SaveKey,previous);else PlayerPrefs.DeleteKey(RememberingVillage.SaveKey);PlayerPrefs.Save();}

        [UnityTest]
        public IEnumerator CleanupBeforeSnapshotCannotDeleteAnExistingSave()
        {
            bool existed=PlayerPrefs.HasKey(RememberingVillage.SaveKey);string previous=PlayerPrefs.GetString(RememberingVillage.SaveKey,"");
            var go=new GameObject("Unprepared verification driver");
            try
            {
                PlayerPrefs.SetString(RememberingVillage.SaveKey,"owner-save-before-verification");
                var driver=go.AddComponent<PlayerEvidence>();driver.enabled=false;
                typeof(PlayerEvidence).GetMethod("RestoreKey",Private).Invoke(driver,null);
                Assert.AreEqual("owner-save-before-verification",PlayerPrefs.GetString(RememberingVillage.SaveKey,""),"No snapshot means no permission to restore or delete the existing save");
            }
            finally{Restore(existed,previous);UnityEngine.Object.Destroy(go);}
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedEvidenceWritesStillRestoreTheActualOriginalSave()
        {
            bool existed=PlayerPrefs.HasKey(RememberingVillage.SaveKey);string previous=PlayerPrefs.GetString(RememberingVillage.SaveKey,"");
            string previousRoot=Environment.GetEnvironmentVariable("FORGE_EVIDENCE_DIR");bool oldIgnore=LogAssert.ignoreFailingMessages;
            string root=Path.Combine(Application.temporaryCachePath,"forge-evidence-failure-"+Guid.NewGuid().ToString("N"));
            var sampleObject=new GameObject("Actual sample under failing evidence output");var driverObject=new GameObject("Failure verification driver");
            try
            {
                Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(root,"actual-save.txt"));Directory.CreateDirectory(Path.Combine(root,"player-failure.txt"));
                Environment.SetEnvironmentVariable("FORGE_EVIDENCE_DIR",root);LogAssert.ignoreFailingMessages=true;
                PlayerPrefs.SetString(RememberingVillage.SaveKey,"owner-save-before-failed-output");
                var sample=sampleObject.AddComponent<RememberingVillage>();sample.Running=false;
                var driver=driverObject.AddComponent<PlayerEvidence>();driver.enabled=false;
                object[] arguments={null};bool prepared=true;
                Assert.DoesNotThrow(()=>prepared=(bool)typeof(PlayerEvidence).GetMethod("Prepare",Private).Invoke(driver,arguments),"Failure logging cannot prevent preference cleanup");
                Assert.IsFalse(prepared);Assert.AreEqual(2,sample.World.Provisions,"The actual sample must have executed its help and save/load path before the output failed");
                Assert.AreEqual("owner-save-before-failed-output",PlayerPrefs.GetString(RememberingVillage.SaveKey,""));
                Assert.IsFalse(File.Exists(Path.Combine(root,"player-result.json")),"A failed evidence run cannot report success");
            }
            finally
            {
                LogAssert.ignoreFailingMessages=oldIgnore;Environment.SetEnvironmentVariable("FORGE_EVIDENCE_DIR",previousRoot);Restore(existed,previous);
                UnityEngine.Object.Destroy(sampleObject);UnityEngine.Object.Destroy(driverObject);if(Directory.Exists(root))Directory.Delete(root,true);
            }
            yield return null;
        }
    }
}
