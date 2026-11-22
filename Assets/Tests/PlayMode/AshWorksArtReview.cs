using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static string AshWorksOutput => Environment.GetEnvironmentVariable("ASHEN_ARTISAN_OUTPUT");
        private static readonly string[] GuardPieces = { "Garde_Ofenhaube", "Garde_Lamellenwams", "Garde_Zaehlgurt", "Garde_Schlackenschild", "Garde_Schuerhaken",
            "Garde_Manschette_L","Garde_Manschette_R","Garde_Wickel_L","Garde_Wickel_R","Garde_Armaermel_L","Garde_Armaermel_R" };
        private static readonly string[] BellPieces = { "Glocke_Stirnreif", "Glocke_Glockenjoch", "Glocke_Schultertuch", "Glocke_Schurzgurt", "Glocke_Handglocke",
            "Glocke_Manschette_L","Glocke_Manschette_R","Glocke_Gamasche_L","Glocke_Gamasche_R","Glocke_Armaermel_L","Glocke_Armaermel_R" };
        private static readonly string[] GuardClothing=GuardPieces.Where(n=>n!="Garde_Schlackenschild"&&n!="Garde_Schuerhaken").ToArray();
        private static readonly string[] BellClothing=BellPieces.Where(n=>n!="Glocke_Handglocke").ToArray();
        [Serializable]
        private sealed class ArtisanFrame
        {
            public string subject, device, unity, chapter, saveHash, avatar, projection;
            public int width,height,changedPixels,repeatPixels,renderers,triangles,centralPixels,changedCentralPixels;
            public Vector3 center,dimensions,eye;
        }
        private IEnumerable<Transform> ArtisanSceneObjects() => _scene.GetRootGameObjects()
            .SelectMany(o=>o.GetComponentsInChildren<Transform>(false));
        private Transform ArtisanOne(string name) => ArtisanSceneObjects().Single(t=>t.name==name);
        private static Mesh ArtisanMesh(Renderer r) => r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
        private static Transform[] MatchingArt(Transform subject,string token) =>
            subject.GetComponentsInChildren<Transform>(true)
                   .Where(t=>t.name.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0).ToArray();

        private static Bounds ArtisanBounds(IEnumerable<Renderer> sources)
        {
            var shapes=sources.Where(r=>r && r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.sqrMagnitude>0).ToArray();
            Assert.IsNotEmpty(shapes,"No visible native geometry");
            Bounds result=shapes[0].bounds;foreach(var r in shapes.Skip(1)) result.Encapsulate(r.bounds);
            return result;
        }

        private static Renderer[] CheckWearables(Transform actor,string[] names,bool humanoid)
        {
            var anim=humanoid?actor.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>
                a.avatar && a.avatar.isValid && a.isHuman && a.runtimeAnimatorController):null;
            if(humanoid)
            {
                Assert.IsNotNull(anim,"Purchasing and posing a real avatar is a prerequisite for this check.");
                Assert.IsTrue(anim.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s=>s.enabled&&s.sharedMesh&&s.bones.Length>15),"No animated native skin");
            }
            var result=new List<Renderer>();
            foreach(var name in names)
            {
                var owner=MatchingArt(actor,name);
                Assert.IsNotEmpty(owner,"Imported module "+name+" under "+actor.name);
                var renderers=owner.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).Distinct()
                    .Where(r=>r.enabled && r.gameObject.activeInHierarchy && ArtisanMesh(r)).ToArray();
                Assert.IsNotEmpty(renderers,"No visible native mesh on "+name);
                foreach(var r in renderers)
                {
                    var mesh=ArtisanMesh(r);
                    Assert.Greater(mesh.vertexCount,12,"placeholder instead of authored "+name);
                    Assert.IsNotEmpty(r.sharedMaterials,name);
                    foreach(var m in r.sharedMaterials)
                    {
                        Assert.IsTrue(m && m.shader && m.shader.isSupported,name+" has a missing or unsupported shader");
                        Assert.AreNotEqual("Hidden/InternalErrorShader",m.shader.name,name+" has an error shader");
                    }
                    if(humanoid)
                    {
                        Assert.IsTrue(r.transform.IsChildOf(anim.transform),"Loose attachment: "+name);
                        bool bone=false;
                        for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
                        {
                            var limb=anim.GetBoneTransform((HumanBodyBones)i);
                            if(limb && r.transform.IsChildOf(limb)){bone=true;break;}
                        }
                        Assert.IsTrue(bone,"No live avatar bone owns "+name);
                        var head=anim.GetBoneTransform(HumanBodyBones.Head);
                        if(name.EndsWith("Ofenhaube",StringComparison.Ordinal) || name.EndsWith("Stirnreif",StringComparison.Ordinal))
                            Assert.Less(Vector3.Distance(head.position,r.bounds.center),.47f,"Headgear fitted away from head: "+name);
                    }
                    result.Add(r);
                }
            }
            return result.Distinct().ToArray();
        }

        private static void ArtisanWrite(string file,byte[] data)
        {
            if(string.IsNullOrEmpty(AshWorksOutput)) return;
            Directory.CreateDirectory(AshWorksOutput);
            File.WriteAllBytes(System.IO.Path.Combine(AshWorksOutput,file),data);
        }
        private static Texture2D ArtisanReadCamera(Camera cam,int w,int h)
        {
            var active=RenderTexture.active;var prior=cam.targetTexture;
            var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            try
            {
                cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
                var image=new Texture2D(w,h,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();
                return image;
            }
            finally {cam.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }

        private void ArtisanFramePair(string name,string kind,Transform subject,Renderer[] pieces,Camera cam,int w,int h,
            Action<bool> setVisible=null,float centralCoverage=0)
        {
            string stem=name+"-"+kind;
            bool[] enabled=pieces.Select(r=>r.enabled).ToArray();
            var takes=new List<Texture2D>(4);
            string state=Game.Progression.SaveString();ulong hash=Game.ComputeWorldHash();
            Bounds bounds=ArtisanBounds(pieces);
            try
            {
                takes.Add(ArtisanReadCamera(cam,w,h));takes.Add(ArtisanReadCamera(cam,w,h));
                takes.Add(ArtisanReadCamera(cam,w,h));
                if(setVisible!=null)setVisible(false);
                else for(int i=0;i<pieces.Length;i++) pieces[i].enabled=false;
                takes.Add(ArtisanReadCamera(cam,w,h));
                Color32[] baseline=takes[1].GetPixels32(),repeat=takes[2].GetPixels32(),off=takes[3].GetPixels32();
                bool Diff(Color32 a,Color32 b) => Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)))>10;
                int changed=0,noise=0,central=0,centralChanged=0;
                for(int i=0;i<baseline.Length;i++)
                {
                    bool differs=Diff(baseline[i],off[i]);
                    if(differs)changed++;
                    if(Diff(baseline[i],repeat[i]))noise++;
                    if(centralCoverage<=0)continue;
                    float x=((i%w)+.5f-w*.5f)/(w*.34f),y=((i/w)+.5f-h*.5f)/(h*.32f);
                    if(x*x+y*y<=1f){central++;if(differs)centralChanged++;}
                }
                var animator=subject.GetComponentInChildren<Animator>(true);
                var note=new ArtisanFrame {subject=name,device=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,
                    chapter=Game.ChapterName,saveHash=hash.ToString("X16"),avatar=animator&&animator.avatar?animator.avatar.name:"",
                    projection=kind,width=w,height=h,changedPixels=changed,repeatPixels=noise,renderers=pieces.Length,
                    triangles=pieces.Sum(r=>(int)Enumerable.Range(0,ArtisanMesh(r).subMeshCount).Sum(i=>(long)ArtisanMesh(r).GetIndexCount(i)/3)),
                    center=bounds.center,dimensions=bounds.size,eye=cam.transform.position,centralPixels=central,changedCentralPixels=centralChanged};
                ArtisanWrite(stem+".png",takes[1].EncodeToPNG());
                ArtisanWrite(stem+"-ohne.png",takes[3].EncodeToPNG());
                ArtisanWrite(stem+".json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(note,true)));
                int min=kind=="Spielkamera"?16:180;
                Assert.Greater(changed,Math.Max(min,2*noise),stem+": "+changed+" isolated pixels vs "+noise+" in repeated frame");
                if(centralCoverage>0)Assert.Greater((float)centralChanged/central,centralCoverage,
                    stem+": original head or chest protrudes through the clothing in the central native view");
            }
            finally
            {
                if(setVisible!=null)setVisible(true);
                for(int i=0;i<pieces.Length;i++) if(pieces[i]) pieces[i].enabled=enabled[i];
                foreach(var tex in takes) if(tex) UnityEngine.Object.DestroyImmediate(tex);
            }
            Assert.AreEqual(hash,Game.ComputeWorldHash());Assert.AreEqual(state,Game.Progression.SaveString());
        }

        private void ArtisanPortrait(string name,Transform subject,Renderer[] pieces,bool detailOnly,
            Vector3? center=null,Vector3? viewDirection=null,float? halfHeight=null,int width=820,int height=960,
            Action<bool> setVisible=null,float centralCoverage=0)
        {
            var props=new GameObject("AshWorks_QC_FixedCamera");var cam=props.AddComponent<Camera>();
            var actorLayers=subject.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.layer);
            const int studioLayer=28;
            try
            {
                foreach(var t in actorLayers.Keys) t.gameObject.layer=studioLayer;
                cam.enabled=false;cam.cullingMask=1<<studioLayer;cam.nearClipPlane=.02f;cam.farClipPlane=20;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.095f,.112f,.115f,1);
                var body=subject.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s=>s.sharedMesh&&s.enabled&&s.gameObject.activeInHierarchy);
                Bounds frame=ArtisanBounds(pieces);if(body&&!detailOnly)frame.Encapsulate(body.bounds);
                Vector3 aim=center??frame.center;
                float span=Mathf.Max(.08f,Mathf.Max(frame.size.y*.5f,Mathf.Max(frame.size.x,frame.size.z)*.43f));
                Quaternion rotation=subject.rotation;
                cam.orthographic=true;cam.orthographicSize=halfHeight??(span*1.27f);
                float distance=viewDirection.HasValue?Mathf.Max(2f,span*3.3f):Mathf.Max(1.3f,span*2.7f);
                cam.transform.position=aim+rotation*((viewDirection??new Vector3(.60f,.23f,1.62f)).normalized*distance);
                cam.transform.LookAt(aim,Vector3.up);
                void Lamp(Vector3 axis,Color tint,float intensity)
                {
                    var o=new GameObject("Gebietslicht_QC");o.transform.SetParent(props.transform,false);var l=o.AddComponent<Light>();
                    l.type=LightType.Directional;l.color=tint;l.intensity=intensity;l.shadows=LightShadows.None;l.cullingMask=1<<studioLayer;
                    o.transform.rotation=Quaternion.LookRotation(-(rotation*axis),Vector3.up);
                }
                Lamp(new Vector3(-1,1.4f,1.1f),new Color(1,.82f,.63f),2.15f);
                Lamp(new Vector3(.6f,.55f,.4f),new Color(.65f,.78f,1),.85f);
                Lamp(new Vector3(.8f,1,-1),new Color(.74f,.86f,1),1.65f);
                ArtisanFramePair(name,"Werkansicht",subject,pieces,cam,width,height,setVisible,centralCoverage);
            }
            finally
            {
                foreach(var it in actorLayers) if(it.Key) it.Key.gameObject.layer=it.Value;
                if(props) UnityEngine.Object.DestroyImmediate(props);
            }
        }

        private void ArtisanMainFrame(string name,Transform subject,Renderer[] pieces,bool wornOnCameraTarget=false)
        {
            ProjectCameraFrame();
            var camera=Camera.main;var rig=camera.GetComponent<ArenaCameraRig>();rig.SnapToTarget();
            var delta=ArtisanBounds(pieces).center-rig.FocusPoint;
            // From a hero the camera looks out toward an NPC. For a worn piece, put the same
            // player-orbit camera on the side of the hero carrying it.
            if(wornOnCameraTarget)delta=-delta;
            if(delta.x*delta.x+delta.z*delta.z>.01f)
            {
                float heading=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                for(int i=0;i<2;i++) rig.Orbit(new Vector2(Mathf.DeltaAngle(rig.Yaw,heading)/.32f,0));
                rig.SnapToTarget();
            }
            ArtisanFramePair(name,"Spielkamera",subject,pieces,camera,1152,720);
        }

        private static void CheckArtMotion(Transform actor,Renderer[] parts)
        {
            var animator=actor.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.isHuman&&a.runtimeAnimatorController);
            Assert.IsNotNull(animator,actor.name);
            var attachments=parts.Select(r=>r.transform.localToWorldMatrix).ToArray();
            var bones=parts.Select(r=>r.transform.parent).ToArray();
            var offsets=parts.Select(r=>r.transform.localPosition).ToArray();
            var clips=animator.runtimeAnimatorController.animationClips;
            Assert.IsNotEmpty(clips);
            var starting=animator.GetCurrentAnimatorStateInfo(0);
            var old=animator.cullingMode;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            try
            {
                int state=new[]{"Base Layer.Move","Move","Base Layer.Walk","Walk","Base Layer.Idle","Idle"}
                    .Select(Animator.StringToHash).FirstOrDefault(h=>animator.HasState(0,h));
                Assert.AreNotEqual(0,state,"Controller has no normal locomotion state");
                animator.Play(state,0,.16f);animator.Update(1f/60f);
                var active=animator.GetCurrentAnimatorClipInfo(0);
                Assert.IsNotEmpty(active,"Native retargeted clip");
                string clip=active[0].clip.name;
                for(int i=0;i<parts.Length;i++) attachments[i]=parts[i].transform.localToWorldMatrix;
                animator.Play(state,0,.64f);animator.Update(1f/60f);
                bool moved=false;
                for(int i=0;i<parts.Length;i++)
                {
                    Assert.AreSame(bones[i],parts[i].transform.parent);
                    Assert.Less(Vector3.Distance(offsets[i],parts[i].transform.localPosition),.001f);
                    for(int c=0;c<3;c++) if(((Vector3)attachments[i].GetColumn(c)-(Vector3)parts[i].transform.localToWorldMatrix.GetColumn(c)).sqrMagnitude>.000004f) moved=true;
                }
                Assert.IsTrue(moved,"No item followed the native clip "+clip);
            }
            finally
            {
                if(starting.fullPathHash!=0){animator.Play(starting.fullPathHash,0,starting.normalizedTime);animator.Update(0);}
                animator.cullingMode=old;
            }
        }

        private IEnumerator ArtisanCarrier(string name,string regionActor,string[] names,long nearX,long nearZ)
        {
            AshWorksJourney.Walk(Game,nearX,nearZ);
            ProjectCameraFrame();yield return null;
            Transform actor=ArtisanOne(regionActor);
            var pieces=CheckWearables(actor,names,true);
            ArtisanMainFrame(name,actor,pieces);ArtisanPortrait(name,actor,pieces,false);
            var detail=CheckWearables(actor,names.Take(2).ToArray(),true);
            ArtisanPortrait(name+"-Detail",actor,detail,true);
            CheckOccupationalFit(actor,name=="Schlackengarde");
            CheckArtMotion(actor,pieces);
        }

        private void ArtisanTravel(int expected,bool north)
        {
            AshWorksJourney.Walk(Game,0,north?24000:-24000);
            TravelStory();
            Assert.AreEqual(expected,Game.Progression.EntryAct);
        }

        private IEnumerator ReachArtisanRoad()
        {
            if (!PassiveArtVisuals.Has("EntryHero") && string.IsNullOrEmpty(AshWorksOutput) &&
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")) &&
                !Directory.Exists(System.IO.Path.Combine(Application.dataPath, "BoZo_StylizedModularCharacters")))
                Assert.Ignore("This rig and animation test requires the installed character pack.");
            _runtime.enabled=false;
            AshWorksJourney.ClearEntry(Game);ArtisanTravel(2,true);
            AshWorksJourney.Meet(Game,0);yield return StoryAction(HungerAction.HearIlyan);
            ArtisanTravel(3,true);
        }

        private IEnumerator ClaimAndEquipArtisan(RegionalRelic id,long x,long z,bool fromJournal)
        {
            var game=Game;
            AshWorksJourney.Walk(game,x,z,820);ProjectCameraFrame();yield return null;
            Assert.IsFalse(game.Progression.Equipment.OwnsRegionalRelic(id));
            Assert.IsTrue(game.CanClaimRegionalRelic,"Available at the actual authored location "+id);
            Assert.IsNotNull(ArtisanOne("Fund_"+id));
            if(fromJournal) yield return StoryAction(HungerAction.ClaimRegionalRelic);
            else
            {
                typeof(ArenaRuntime).GetField("_pendingPickup",Flags).SetValue(_runtime,true);
                _runtime.enabled=true;
                for(int i=0;i<100&&!game.Progression.Equipment.OwnsRegionalRelic(id);i++) yield return null;
                _runtime.enabled=false;
            }
            Assert.IsTrue(game.Progression.Equipment.OwnsRegionalRelic(id),"Inventory source "+id);
            Assert.IsFalse(game.Progression.Equipment.IsRegionalRelicEquipped(id));
            _runtime.OpenMenu(ArenaRuntime.MenuInventory);yield return null;
            var equipment=game.Progression.Equipment;
            int ordinal=Enum.GetValues(typeof(RegionalRelic)).Cast<RegionalRelic>().Where(k=>(int)k>0&&equipment.OwnsRegionalRelic(k)).OrderBy(k=>(int)k).ToList().IndexOf(id);
            Assert.GreaterOrEqual(ordinal,0);
            int index=game.Progression.Items.Count+(equipment.FieldPauldronOwned?1:0)+ordinal;
            ArtisanOne("Cell"+(index%30)).GetComponent<Button>().onClick.Invoke();yield return null;
            var toggle=ArtisanOne("EquipRegionalRelic").GetComponent<Button>();Assert.IsTrue(toggle.interactable);
            toggle.onClick.Invoke();yield return null;
            if(!string.IsNullOrEmpty(AshWorksOutput)) yield return CaptureMenu(id+"-Inventar.png",1280,720);
            _runtime.CloseMenu();yield return null;
            Assert.IsTrue(equipment.IsRegionalRelicEquipped(id));
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),NativeJourneyMaps,out var saved));
            Assert.IsTrue(saved.Equipment.OwnsRegionalRelic(id));Assert.IsTrue(saved.Equipment.IsRegionalRelicEquipped(id));
        }

        [UnityTest, Category("PrivateArt")]
        public IEnumerator AshWardensAndMillRingerAppearWearAndPersistThroughTheRoad()
        {
            yield return ReachArtisanRoad();
            Assert.IsFalse(Game.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.Schlackengarde));
            AshWorksJourney.Walk(Game,0,-2000);yield return StoryAction(HungerAction.InspectTrail);
            yield return ArtisanCarrier("Schlackengarde","ARE_Schlackengarde",GuardPieces,2600,-2600);
            yield return ClaimAndEquipArtisan(RegionalRelic.Schlackengarde,3400,-1400,false);
            var hero=ArtisanOne("Wanderer");var guard=CheckWearables(hero,GuardClothing,true);
            ArtisanPortrait("Feldheilerin-Schlackengarde",hero,guard,false);
            ArtisanTravel(4,true);yield return null;
            Assert.IsFalse(ArtisanSceneObjects().Any(t=>t.name=="ARE_Schlackengarde"));
            AshWorksJourney.Walk(Game,-4000,-2000);yield return StoryAction(HungerAction.InspectShelter);
            yield return ArtisanCarrier("Glockentraegerin","ARE_Glockentraegerin",BellPieces,-5700,1300);
            yield return ClaimAndEquipArtisan(RegionalRelic.Glockentraegerin,-6800,2400,true);
            hero=ArtisanOne("Wanderer");var bell=CheckWearables(hero,BellClothing,true);
            ArtisanPortrait("Feldheilerin-Glockentraegerin",hero,bell,false);
            Assert.IsFalse(Game.Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.Schlackengarde));
            var previous=Game;
            Assert.IsTrue(SceneManager.SetActiveScene(_previous));yield return SceneManager.UnloadSceneAsync(_scene);
            _scene=SceneManager.CreateScene("AshenEntry");Assert.IsTrue(SceneManager.SetActiveScene(_scene));
            _runtime=new GameObject("AshWorks_FreshScene").AddComponent<ArenaRuntime>();yield return null;_runtime.enabled=false;
            Assert.AreNotSame(previous,Game);Assert.AreEqual(4,Game.Progression.EntryAct);
            Assert.IsTrue(Game.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.Schlackengarde));
            Assert.IsTrue(Game.Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.Glockentraegerin));
            CheckWearables(ArtisanOne("Wanderer"),BellClothing,true);
            ArtisanTravel(3,false);ProjectCameraFrame();yield return null;
            Assert.IsNotNull(ArtisanOne("ARE_Schlackengarde"));Assert.IsFalse(ArtisanSceneObjects().Any(t=>t.name=="ARE_Glockentraegerin"));
            AshWorksJourney.Walk(Game,3400,-1400);Assert.IsFalse(Game.ActOnHunger(HungerAction.ClaimRegionalRelic).Accepted,"No duplicate authored drop");
            Assert.IsTrue(Game.TrySetRegionalRelicEquipped(RegionalRelic.Glockentraegerin,false));
            ProjectCameraFrame();yield return null;
            var fit=Resources.Load<RegionalBodyFit>("ARE/Visuals/RegionalFit_EntryHero");
            var unequipped=ArtisanOne("Wanderer");
            var skins=unequipped.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            Assert.IsTrue(skins.Any(s=>s.enabled&&(s.sharedMesh==fit.Original||s.sharedMesh==fit.OriginalShoulder)),
                "Removing clothing must restore the hero's genuine original skin");
            Assert.IsFalse(unequipped.GetComponentsInChildren<Transform>(false).Any(t=>BellClothing.Contains(t.name)),
                "Old costume and sleeves still draw after removing the outfit");
        }

        [UnityTest, Category("PrivateArt")]
        public IEnumerator TiefscharrerKeepsHisBrandAndDropsAnEquipableGrubenlicht()
        {
            yield return ReachArtisanRoad();
            AshWorksJourney.Walk(Game,0,-2000);yield return StoryAction(HungerAction.InspectTrail);ArtisanTravel(4,true);
            AshWorksJourney.Walk(Game,-4000,-2000);yield return StoryAction(HungerAction.InspectShelter);
            ArtisanTravel(3,false);ArtisanTravel(2,false);
            AshWorksJourney.Meet(Game,SocialLedger.MaraNpcId);yield return StoryAction(HungerAction.Replace);
            ArtisanTravel(3,true);ArtisanTravel(4,true);AshWorksJourney.Walk(Game,-4000,5000);
            yield return StoryAction(HungerAction.InspectBeds);ArtisanTravel(5,true);
            AshWorksJourney.Walk(Game,-6500,0);yield return StoryAction(HungerAction.StoreBandage);
            AshWorksJourney.Walk(Game,6500,-3000);yield return StoryAction(HungerAction.StoreTracks);
            AshWorksJourney.Walk(Game,0,18000);yield return StoryAction(HungerAction.InspectSeal);yield return StoryAction(HungerAction.OpenSeal);
            ArtisanTravel(6,true);AshWorksJourney.Walk(Game,0,15000);yield return StoryAction(HungerAction.InspectCryptTrace);
            yield return StoryAction(HungerAction.OpenInnerDoor);ArtisanTravel(7,true);
            yield return StoryAction(HungerAction.ToggleSneak);
            var beast=Game.Progression.Rattler.Creature;
            AshWorksJourney.Walk(Game,beast.Xmm-700,beast.Zmm-2600,720);
            typeof(ArenaRuntime).GetMethod("UpdateRattlerPresentation",Flags).Invoke(_runtime,null);ProjectCameraFrame();yield return null;
            var actor=ArtisanOne("ARE_MaulwurfRattler");
            var projection=typeof(ArenaRuntime).GetProperty("RattlerStatusText",Flags).GetValue(_runtime).ToString();
            StringAssert.Contains("TIEFSCHARR",projection.ToUpperInvariant());
            yield return InspectOriginalTiefscharrer(actor);
            Assert.IsFalse(Game.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.Grubenlicht));
            yield return StoryAction(HungerAction.ToggleSneak);AshWorksJourney.DefeatTiefscharrer(Game);
            AshWorksJourney.Walk(Game,beast.Xmm,beast.Zmm);
            Assert.IsFalse(Game.Progression.Rattler.BrandExamined);
            yield return StoryAction(HungerAction.InspectRoyalBrand); Assert.IsTrue(Game.Progression.Rattler.BrandExamined);
            yield return ClaimAndEquipArtisan(RegionalRelic.Grubenlicht,beast.Xmm,beast.Zmm,false);
            // At the pickup the broad carcass physically hides the character's waist. Walk out
            // from behind it before inspecting the lamp, as an actual player can in the den.
            long nearX=beast.Xmm+(beast.Xmm>0?-4200:4200),nearZ=beast.Zmm+(beast.Zmm>0?-3700:3700);
            AshWorksJourney.Walk(Game,nearX,nearZ,650);ProjectCameraFrame();yield return null;
            long dx=Game.PlayerXmm-beast.Xmm,dz=Game.PlayerZmm-beast.Zmm;
            Assert.Greater(dx*dx+dz*dz,4300L*4300,"Player must have cleared the actual carcass on foot");
            var hero=ArtisanOne("Wanderer");var lamp=CheckWearables(hero,new[]{"Equipped_Grubenlicht"},true);
            var size=ArtisanBounds(lamp).size;
            Assert.That(Mathf.Max(size.x,Mathf.Max(size.y,size.z)),Is.InRange(.22f,.72f),"Lamp scale at the live hand");
            ArtisanMainFrame("Feldheilerin-Grubenlicht",hero,lamp,true);ArtisanPortrait("Grubenlicht",hero,lamp,true);
            ArtisanTravel(6,false);
            yield return ClaimAndEquipArtisan(RegionalRelic.Brandreif,2300,15800,true);
            ArtisanTravel(5,false);
            yield return ClaimAndEquipArtisan(RegionalRelic.Rattlerkluft,4200,10500,false);
            hero=ArtisanOne("Wanderer");
            var graveSet=new[]{"R_Wangenmaske","R_Gratharnisch","R_Scharrstulpe_L","R_Scharrstulpe_R","R_Gratschiene_L","R_Gratschiene_R"};
            Assert.IsFalse(hero.GetComponentsInChildren<Transform>(false).Any(t=>t.name=="R_Wangenmaske"),
                "Single Brandreif must replace the set's face mask");
            var mixed=CheckWearables(hero,graveSet.Skip(1).Concat(new[]{"Brandreif","Equipped_Grubenlicht"}).ToArray(),true);
            ArtisanPortrait("Scharrgraeber-und-Brandreif",hero,mixed,false);
            Assert.IsTrue(Game.TrySetRegionalRelicEquipped(RegionalRelic.Brandreif,false));ProjectCameraFrame();yield return null;
            var complete=CheckWearables(hero,graveSet,true);
            Assert.IsFalse(hero.GetComponentsInChildren<Transform>(false).Any(t=>t.name=="Brandreif"));
            ArtisanPortrait("Feldheilerin-Scharrgraeberkluft",hero,complete,false);
        }
    }
}
