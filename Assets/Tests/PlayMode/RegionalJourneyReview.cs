using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [Serializable] private class FrameNote
        {
            public string area, device, core, placement;
            public int changed, repeated, pixels, placed, vertexMeshes;
            public float x, y, z;
        }

        private static Texture2D RenderArea(Camera camera, string path)
        {
            const int width=960, height=600;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var old=RenderTexture.active; var previous=camera.targetTexture;
            try
            {
                camera.targetTexture=target; camera.Render();RenderTexture.active=target;
                var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
                if(path!=null) File.WriteAllBytes(path,tex.EncodeToPNG());
                return tex;
            }
            finally {camera.targetTexture=previous;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);}
        }

        private IEnumerator ChapterFrame(string area, string key, Vector3 eyeOffset, float height)
        {
            // Let deferred destruction of the preceding chapter finish, then freeze only camera presentation.
            yield return null;
            var layer=Find("ARE_BuildLayer_"+area);
            Assert.IsNotNull(layer,area);
            var anchor=layer.transform.Find(key);Assert.IsNotNull(anchor,key);
            var detail=layer.GetComponentsInChildren<Transform>().Where(t=>t.name=="AschepassDetail").ToArray();
            Assert.IsNotEmpty(detail,area);
            var camera=Camera.main;Assert.IsNotNull(camera);
            var rig=camera.GetComponent<ArenaCameraRig>();rig.SnapToTarget();
            var prevPosition=camera.transform.position;var prevRotation=camera.transform.rotation;float oldFov=camera.fieldOfView;
            var output=Environment.GetEnvironmentVariable("ASHEN_REGION_OUTPUT");
            Directory.CreateDirectory(output);
            string Prefix(string name) => System.IO.Path.Combine(output,area+"-"+name+".png");
            var checksum=Game.ComputeWorldHash();var serialized=Game.Progression.SaveString();
            var skins=Find("Wanderer").GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.IsTrue(skins.Any(s=>s.sharedMesh && s.sharedMaterials.All(m=>m && m.mainTexture)),area+" character intake");
            var oldLods=QualitySettings.lodBias;
            try
            {
                var images=new List<Texture2D>();
                try
                {
                    images.Add(RenderArea(camera,null));
                    images.Add(RenderArea(camera,Prefix("Spielkamera")));
                    Vector3 focus=anchor.position+Vector3.up*height;
                    camera.transform.position=focus+eyeOffset;
                    camera.transform.LookAt(focus);camera.fieldOfView=53;
                    images.Add(RenderArea(camera,null));
                    var first=RenderArea(camera,null);images.Add(first);
                    var on=RenderArea(camera,Prefix("Detail"));images.Add(on);
                    // Hide exactly the new geometry in this fixed camera. Imported native geometry stays visible.
                    foreach(var child in detail) child.gameObject.SetActive(false);
                    var off=RenderArea(camera,Prefix("Bestand"));images.Add(off);
                    var a=on.GetPixels32();var b=off.GetPixels32();var c=first.GetPixels32();
                    int changed=0,repeated=0;
                    bool Different(Color32 p, Color32 q) => Math.Max(Math.Abs(p.r-q.r),Math.Max(Math.Abs(p.b-q.b),Math.Abs(p.g-q.g)))>10;
                    for(int i=0;i<a.Length;i++) {if(Different(a[i],b[i])) changed++;if(Different(a[i],c[i])) repeated++;}
                    var desc=new FrameNote {area=area,device=SystemInfo.graphicsDeviceName,core=checksum.ToString("X16"),placement=key,changed=changed,repeated=repeated,pixels=a.Length,placed=detail.Length,vertexMeshes=detail.Sum(t=>t.GetComponentsInChildren<MeshFilter>(true).Length),x=focus.x,y=focus.y,z=focus.z};
                    File.WriteAllText(System.IO.Path.Combine(output,area+".json"),JsonUtility.ToJson(desc,true));
                    Assert.IsTrue(changed>100 && changed>repeated*2,area+" isolated pixels "+changed+" control "+repeated);
                }
                finally { foreach(var img in images) if(img) UnityEngine.Object.Destroy(img); }
            }
            finally
            {
                foreach(var child in detail) if(child) child.gameObject.SetActive(true);
                camera.fieldOfView=oldFov;camera.transform.SetPositionAndRotation(prevPosition,prevRotation);
                Assert.AreEqual(oldLods,QualitySettings.lodBias);
            }
            Assert.AreEqual(checksum,Game.ComputeWorldHash());Assert.AreEqual(serialized,Game.Progression.SaveString());
        }

        [UnityTest, Category("PrivateArt")]
        public IEnumerator AschepassLandmarksFollowTheRealChapterJourney()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASHEN_REGION_OUTPUT")))
                Assert.Ignore("Run the licensed character and camera review with ASHEN_REGION_OUTPUT.");
            _runtime.enabled=false;
            Assert.AreEqual(1,Game.Progression.EntryAct);
            yield return ChapterFrame("krypta","Bau_b0202_Brazier",new Vector3(2.35f,1.4f,-3.3f),.42f);
            foreach(var enemy in Game.Monsters){enemy.Xmm=1000;enemy.Zmm=0;enemy.Health=1;}
            Game.ActivateSkill(3);
            for(int k=0;k<3;k++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0,24000);TravelStory();
            yield return ChapterFrame("aschengrenze","Bau_b0202_BloodcapBank",new Vector3(-2.02f,1.12f,-3.15f),.40f);
            var ilyan=Game.Npcs.Find(n=>n.Id==0);ilyan.Xmm=Game.PlayerXmm+1000;ilyan.Zmm=Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan);
            MoveStory(0,24000);TravelStory();
            yield return ChapterFrame("aschestrasse","Bau_b0002_BloodcapBank",new Vector3(2.95f,1.35f,-3.95f),.6f);
            MoveStory(0,-2000);yield return StoryAction(HungerAction.InspectTrail);
            MoveStory(0,24000);TravelStory();
            yield return ChapterFrame("muehle","Bau_b0201_StoneOutcrop",new Vector3(-3.5f,1.65f,-3.1f),.74f);
        }

        [UnityTest, Category("PrivateArt")]
        public IEnumerator AschepassStoreAndCryptFollowTheSavedInvestigation()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASHEN_REGION_OUTPUT")))
                Assert.Ignore("Run the licensed character and camera review with ASHEN_REGION_OUTPUT.");
            yield return CompleteSupplyJourney();
            MoveStory(0,24000);TravelStory();
            yield return ChapterFrame("speicher","Bau_b0004_Brazier",new Vector3(-3.65f,1.65f,-3.1f),.65f);
            MoveStory(0,0);MoveStory(-6500,0);yield return StoryAction(HungerAction.StoreBandage);
            MoveStory(0,0);MoveStory(0,-3000);MoveStory(6500,-3000);yield return StoryAction(HungerAction.StoreTracks);
            MoveStory(0,-3000);MoveStory(0,18000);yield return StoryAction(HungerAction.InspectSeal);
            yield return StoryAction(HungerAction.OpenSeal);MoveStory(0,24000);TravelStory();
            yield return ChapterFrame("verborgene-krypta","Bau_b0003_CryptArch",new Vector3(1.9f,1.15f,-6.9f),1.8f);
        }
    }
}
