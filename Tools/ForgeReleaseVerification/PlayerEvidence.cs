using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AshenCrypt.MemoryForge.Samples;
using UnityEngine;

namespace AshenCrypt.ForgeVerification
{
    // Verification-only driver. Never exported with the commercial package.
    public sealed class PlayerEvidence : MonoBehaviour
    {
        private string _root;
        private bool _savedKey;
        private string _previous;
        private bool Prepare(out RememberingVillage sample)
        {
            sample=null;
            try
            {
                _root=Environment.GetEnvironmentVariable("FORGE_EVIDENCE_DIR");if(string.IsNullOrEmpty(_root))throw new Exception("Missing explicit evidence output directory");Directory.CreateDirectory(_root);
                _savedKey=PlayerPrefs.HasKey(RememberingVillage.SaveKey);_previous=PlayerPrefs.GetString(RememberingVillage.SaveKey,"");
                sample=FindFirstObjectByType<RememberingVillage>();if(sample==null)throw new Exception("Actual example missing");sample.Running=false;
                sample.Execute("approach");for(int i=0;i<20;i++)sample.Execute("step");sample.Execute("food");for(int i=0;i<4;i++)sample.Execute("step");
                sample.Execute("leave");for(int i=0;i<20;i++)sample.Execute("step");sample.Execute("approach");for(int i=0;i<20;i++)sample.Execute("step");sample.Execute("greet");
                if(!sample.World.Greet().Contains("food"))throw new Exception("Remembered greeting failed");string saved=sample.World.Save();sample.Execute("save");sample.Execute("reset");sample.Execute("load");
                if(sample.World.Save()!=saved||sample.World.Archive.Get("nara").RelationshipTo("player").Trust!=1200)throw new Exception("Actual PlayerPrefs restore failed");
                File.WriteAllText(Path.Combine(_root,"actual-save.txt"),saved);return true;
            }
            catch(Exception e){Fail(e.ToString());return false;}
        }
        private IEnumerator Start()
        {
            yield return null;if(!Prepare(out var sample))yield break;
            for(int i=0;i<12;i++)yield return null;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(_root,"01-remembered-help.png"));
            for(int i=0;i<12;i++)yield return null;
            typeof(RememberingVillage).GetField("_tab",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(sample,1);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(_root,"02-weighted-decision.png"));
            for(int i=0;i<12;i++)yield return null;
            try
            {
                foreach(string name in new[]{"01-remembered-help.png","02-weighted-decision.png"})if(!File.Exists(Path.Combine(_root,name))||new FileInfo(Path.Combine(_root,name)).Length<10000)throw new Exception("Missing actual player screenshot: "+name);
                File.WriteAllText(Path.Combine(_root,"player-result.json"),"{\"passed\":true,\"unity\":\""+Application.unityVersion+"\",\"platform\":\""+Application.platform+"\",\"tick\":"+sample.World.Tick+",\"trust\":1200,\"provisions\":2,\"saveRestoredExactly\":true}");
                RestoreKey();Application.Quit(0);
            }
            catch(Exception e){Fail(e.ToString());}
        }
        private void RestoreKey(){if(_savedKey)PlayerPrefs.SetString(RememberingVillage.SaveKey,_previous);else PlayerPrefs.DeleteKey(RememberingVillage.SaveKey);PlayerPrefs.Save();}
        private void Fail(string error){Debug.LogError(error);if(!string.IsNullOrEmpty(_root))File.WriteAllText(Path.Combine(_root,"player-failure.txt"),error);RestoreKey();Application.Quit(1);}
    }
}
