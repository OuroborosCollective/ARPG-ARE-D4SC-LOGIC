using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [Serializable]
        private sealed class BeastMeasurement
        {
            public string unity,worldHash,sourceController;
            public Vector3 min,max,nose,brand;
            public Vector3[] sensoryCenters,footPositions;
            public int[] lodTriangles,sensoryTriangles;
            public float[] groundClearanceByFoot;
            public List<BeastMotion> poses=new List<BeastMotion>();
        }

        [Serializable]
        private sealed class BeastMotion
        {
            public string state,clip;
            public float bodyRmsM,bodyMaxM;
            public float[] pawTranslationM,pawRotationDegrees;
        }

        private class SkinSample
        {
            public Vector3[] points;
            public int[] triangles;
            public Vector2[] tags;
        }

        // Read the runtime's skinned position with its imported weights, retargeting and live world transforms.
        private static SkinSample ReadBeastSkin(Transform root,SkinnedMeshRenderer skin,bool topology)
        {
            var baked=new Mesh();skin.BakeMesh(baked);
            try
            {
                var points=baked.vertices;
                var toActor=root.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                for(int i=0;i<points.Length;i++) points[i]=toActor.MultiplyPoint3x4(points[i]);
                // Vertex ordering is identical. Some editor importers omit unskinned channels on the
                // baked copy even though the authored, readable source keeps its second UV stream.
                var tags=topology?baked.uv2:null;
                if(topology && tags.Length!=points.Length && skin.sharedMesh.isReadable)
                    tags=skin.sharedMesh.uv2;
                return new SkinSample{points=points,triangles=topology?baked.triangles:null,tags=tags};
            }
            finally {UnityEngine.Object.DestroyImmediate(baked);}
        }

        private static Bounds BeastBounds(IEnumerable<Vector3> vertices)
        {
            using(var it=vertices.GetEnumerator())
            {
                Assert.IsTrue(it.MoveNext());var b=new Bounds(it.Current,Vector3.zero);
                while(it.MoveNext())b.Encapsulate(it.Current);
                return b;
            }
        }

        private static Transform BeastBone(Transform form,string name)
        {
            var found=form.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name).ToArray();
            Assert.AreEqual(1,found.Length,"Original creature bone or socket "+name);
            return found[0];
        }

        private static int SensoryGroup(Vector2 uv)
        {
            if(uv.y<.95f || uv.x<0 || uv.x>=1)return -1;
            int tag=Mathf.FloorToInt(uv.x*8);
            return Mathf.Abs(uv.x-(tag+.5f)/8)<.004f ? tag : -1;
        }

        private static Vector3[] RealSensoryPatches(SkinSample mesh,Vector3 nose,Bounds body,out int[] counts)
        {
            Assert.AreEqual(mesh.points.Length,mesh.tags.Length,"Authored anatomy UV2 tags on the actual native skin");
            counts=new int[8];var areas=new float[8];var centers=new Vector3[8];
            for(int i=0;i<mesh.triangles.Length;i+=3)
            {
                int a=mesh.triangles[i],b=mesh.triangles[i+1],c=mesh.triangles[i+2],g=SensoryGroup(mesh.tags[a]);
                if(g<0 || SensoryGroup(mesh.tags[b])!=g || SensoryGroup(mesh.tags[c])!=g)continue;
                var p=mesh.points[a];var q=mesh.points[b];var r=mesh.points[c];
                float area=.5f*Vector3.Cross(q-p,r-p).magnitude;if(area<1e-11f)continue;
                counts[g]++;areas[g]+=area;centers[g]+=(p+q+r)*(area/3);
            }
            for(int g=0;g<8;g++)
            {
                Assert.GreaterOrEqual(counts[g],3,"Missing triangulated sensory organ "+g);
                Assert.Greater(areas[g],.000012f,"Nonphysical surface area for sensory organ "+g);
                centers[g]/=areas[g];
                Assert.Less(Vector3.Distance(centers[g],nose),Mathf.Max(.30f,body.size.z*.105f),"Detached nose surface "+g);
            }
            var mid=centers.Aggregate(Vector3.zero,(sum,p)=>sum+p)/8f;
            var ring=BeastBounds(centers);
            Assert.Greater(ring.size.x,.075f,"Nose organs lack lateral spacing");
            Assert.Greater(ring.size.y,.065f,"Nose organs form a horizontal crown on the scalp");
            Assert.Greater(mid.z,body.min.z+body.size.z*.74f,"Nose patches do not reach the forward muzzle");
            var angles=centers.Select(p=>Mathf.Atan2(p.y-mid.y,p.x-mid.x)*Mathf.Rad2Deg).OrderBy(v=>v).ToArray();
            for(int g=0;g<8;g++)
            {
                float gap=Mathf.Repeat(angles[(g+1)%8]-angles[g],360f);
                Assert.That(gap,Is.InRange(8f,119f),"Coincident or missing sector around tactile nose "+g);
            }
            return centers;
        }

        private static float FrameHeight(Vector3[] points,Vector3 center,Vector3 eye,int w,int h)
        {
            var camera=Quaternion.Inverse(Quaternion.LookRotation(-eye,Vector3.up));
            float bound=.08f;
            foreach(var p in points)
            {
                var projected=camera*(p-center);
                bound=Mathf.Max(bound,Mathf.Abs(projected.y),Mathf.Abs(projected.x)*h/w);
            }
            return bound*1.12f;
        }

        private static int Indices(Renderer r)
        {
            Mesh mesh=ArtisanMesh(r);return (int)Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i)/3);
        }

        private static bool Renders(Renderer r) => r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy;

        // First exposure is the normal runtime skin. The paired exposure omits only triangles
        // on the eight labelled organs, so a hidden muzzle cannot pass by drawing nearby skin.
        private void SensoryVisibility(Transform actor,Transform nose,List<SkinnedMeshRenderer> skins)
        {
            var originals=skins.Select(s=>s.sharedMesh).ToArray();
            var withoutPads=new Mesh[skins.Count];
            try
            {
                for(int i=0;i<skins.Count;i++)
                {
                    var source=originals[i];Assert.IsTrue(source.isReadable,source.name+" sensory surface readback");
                    var tags=source.uv2;
                    if(tags.Length!=source.vertexCount){Assert.AreEqual(2,i);continue;}
                    var excluded=UnityEngine.Object.Instantiate(source);withoutPads[i]=excluded;
                    for(int sub=0;sub<source.subMeshCount;sub++)
                    {
                        var tri=source.GetTriangles(sub);var kept=new List<int>(tri.Length);
                        for(int t=0;t<tri.Length;t+=3)
                        {
                            int tag=SensoryGroup(tags[tri[t]]);
                            if(tag>=0&&SensoryGroup(tags[tri[t+1]])==tag&&SensoryGroup(tags[tri[t+2]])==tag)continue;
                            kept.Add(tri[t]);kept.Add(tri[t+1]);kept.Add(tri[t+2]);
                        }
                        excluded.SetTriangles(kept,sub,false);
                    }
                    excluded.bounds=source.bounds;
                }
                void ChangePads(bool visible)
                {
                    for(int i=0;i<skins.Count;i++)skins[i].sharedMesh=visible||!withoutPads[i]?originals[i]:withoutPads[i];
                }
                ArtisanPortrait("Tiefscharrer-Tastlappen",actor,skins.Cast<Renderer>().ToArray(),true,
                    nose.position,new Vector3(.16f,.06f,1),.23f,820,660,ChangePads);
            }
            finally
            {
                for(int i=0;i<skins.Count;i++)
                {
                    skins[i].sharedMesh=originals[i];
                    if(withoutPads[i])UnityEngine.Object.DestroyImmediate(withoutPads[i]);
                }
            }
        }

        private IEnumerator InspectOriginalTiefscharrer(Transform actor)
        {
            Assert.IsFalse(Game.Progression.Rattler.Creature.IsDead);
            Assert.AreEqual(5200,Game.Progression.Rattler.Creature.MaxHealth);
            Assert.AreEqual(1,ArtisanSceneObjects().Count(t=>t.name=="ARE_MaulwurfRattler"),"Exactly one canonical boss");
            var form=BeastBone(actor,"ARE_Tiefscharrer_Form");
            var lod=form.GetComponentsInChildren<LODGroup>(true).Single();var levels=lod.GetLODs();
            Assert.AreEqual(3,levels.Length,"Native LOD group");
            var skins=new List<SkinnedMeshRenderer>();
            foreach(var level in levels.Select((value,i)=>new {value,i}))
            {
                var visual=level.value.renderers.OfType<SkinnedMeshRenderer>().Where(s=>s.sharedMesh).ToArray();
                Assert.AreEqual(1,visual.Length,"One weighted native body at each LOD");
                var skin=visual[0];Assert.AreEqual("Tiefscharrer_LOD"+level.i,skin.name);
                Assert.IsTrue(skin.transform.IsChildOf(form));
                Assert.IsTrue(Renders(skin),"LOD skin must be available in the live hierarchy");
                Assert.GreaterOrEqual(skin.bones.Count(b=>b&&b.IsChildOf(form)),16,"Native deformation bones "+skin.name);
                foreach(var m in skin.sharedMaterials)Assert.IsTrue(m && m.shader && m.shader.isSupported &&
                        m.shader.name!="Hidden/InternalErrorShader" && m.mainTexture,"Native textured body "+skin.name);
                Assert.IsNotEmpty(skin.sharedMaterials);
                skins.Add(skin);
            }
            Assert.IsFalse(actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s=>Renders(s)&&!s.transform.IsChildOf(form)),
                "An old source body still draws through the authored quadruped");
            Assert.IsFalse(MatchingArt(actor,"Tastkranz_Innenring").Where(t=>!t.IsChildOf(form))
                .SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).Any(Renders),"Old overlay still draws");
            var nose=BeastBone(form,"TS_Nose");var shoulder=BeastBone(form,"TS_Brand");
            string[] pawNames={"TS_ForePaw_L","TS_ForePaw_R","TS_HindPaw_L","TS_HindPaw_R"};
            var paws=pawNames.Select(n=>BeastBone(form,n)).ToArray();
            foreach(string name in new[]{"TS_Pelvis","TS_Spine","TS_Chest","TS_Neck","TS_Head","TS_Jaw","TS_Tail",
                                         "TS_ForeArm_L","TS_ForeArm_R","TS_ForeElbow_L","TS_ForeElbow_R",
                                         "TS_HindHip_L","TS_HindHip_R","TS_HindHock_L","TS_HindHock_R"})
                Assert.IsTrue(skins[0].bones.Contains(BeastBone(form,name)),"Missing from the deforming LOD0 bone palette "+name);

            var source=Resources.Load<GameObject>("ARE/Visuals/CryptRattler");
            Assert.IsNotNull(source);
            var controller=source.GetComponentsInChildren<Animator>(true).Single(a=>a.runtimeAnimatorController).runtimeAnimatorController;
            var driver=actor.GetComponentsInChildren<Animator>(true).Single(a=>a.runtimeAnimatorController==controller&&a.isActiveAndEnabled);
            Assert.IsFalse(driver.applyRootMotion);
            var note=new BeastMeasurement{unity=Application.unityVersion,worldHash=Game.ComputeWorldHash().ToString("X16"),
                sourceController=controller.name,lodTriangles=skins.Select(Indices).ToArray()};
            Assert.Greater(note.lodTriangles[0],note.lodTriangles[1]);Assert.Greater(note.lodTriangles[1],note.lodTriangles[2]);
            yield return null;
            var sampled=ReadBeastSkin(actor,skins[0],true);Bounds body=BeastBounds(sampled.points);
            note.min=body.min;note.max=body.max;note.nose=actor.InverseTransformPoint(nose.position);note.brand=actor.InverseTransformPoint(shoulder.position);
            Assert.Greater(body.size.z,1.35f*body.size.y,"An elongated quadrupedal silhouette");
            Assert.Greater(body.size.x,.7f);Assert.Greater(body.size.z,1.6f);
            Assert.That(body.min.y,Is.InRange(-.054f,.08f),"Claws meet the ground without disappearing under the encounter floor");
            Assert.Less(note.nose.y,body.min.y+body.size.y*.72f,"Front nose too high");
            Assert.Greater(note.nose.z,body.min.z+.75f*body.size.z,"Nose is not on anterior muzzle");
            Assert.Greater(note.brand.x,body.center.x+.12f,"Readable right shoulder brand");
            Assert.Greater(note.brand.z,body.center.z-.08f,"Brand stays on forward half of the animal");
            note.sensoryCenters=RealSensoryPatches(sampled,note.nose,body,out var sensoryTriangles);
            note.sensoryTriangles=sensoryTriangles;
            RealSensoryPatches(ReadBeastSkin(actor,skins[1],true),note.nose,body,out _);

            note.footPositions=paws.Select(p=>actor.InverseTransformPoint(p.position)).ToArray();
            note.groundClearanceByFoot=new float[paws.Length];
            for(int i=0;i<paws.Length;i++)
            {
                var foot=note.footPositions[i];
                var footSkin=sampled.points.Where(p=>Mathf.Abs(p.x-foot.x)<.27f && Mathf.Abs(p.z-foot.z)<.32f).ToArray();
                Assert.IsNotEmpty(footSkin,"No skin surrounds the "+pawNames[i]+" bone");
                note.groundClearanceByFoot[i]=footSkin.Min(p=>p.y);
                Assert.Less(foot.y,body.min.y+body.size.y*.59f,"Insufficiently grounded "+pawNames[i]);
            }
            Assert.Greater(note.footPositions[1].x-note.footPositions[0].x,.27f,"Broad left and right excavating forepaws");
            Assert.Greater(note.footPositions[3].x-note.footPositions[2].x,.20f,"Independent hindpaws");
            Assert.Greater(note.footPositions.Take(2).Min(p=>p.z)-note.footPositions.Skip(2).Max(p=>p.z),.4f,"Actual quadrupedal stance");
            Assert.GreaterOrEqual(note.groundClearanceByFoot.Count(y=>y<body.min.y+.18f),3,"Three separated contacts must support the low resting pose");
            Assert.IsTrue(note.groundClearanceByFoot.All(y=>y>-.056f),"A native paw sinks more than 5.6 cm under the encounter ground");

            var marks=actor.GetComponentsInChildren<Renderer>(true).Where(r=>Renders(r)&&
                r.name=="Immergruen_Royal_Brand").ToArray();
            Assert.AreEqual(1,marks.Length,"One visible canonical shoulder mark");
            var mark=marks[0];
            Assert.Less(Vector3.Distance(mark.bounds.center,shoulder.position),.2f,"Brand attached to released original shoulder");
            var renderers=skins.Cast<Renderer>().Concat(marks).ToArray();
            ArtisanMainFrame("Tiefscharrer",actor,renderers);
            // Exercise only the player's orbit and zoom controls. Compare projected skin, hero and
            // nose at each pose so the foreground sword and its owner do not obscure the subject.
            var gameCam=Camera.main;var orbit=gameCam.GetComponent<AreArpg.UnityClient.ArenaCameraRig>();
            var player=ArtisanOne("Wanderer");
            var head=player.GetComponentsInChildren<Animator>(true).First(a=>a.avatar&&a.isHuman).GetBoneTransform(HumanBodyBones.Head);
            var forwardNose=nose.position;
            var outline=sampled.points.Where((v,i)=>i%32==0).Select(v=>actor.TransformPoint(v)).ToArray();
            float bestScore=float.NegativeInfinity,chosenYaw=0,chosenPitch=0,chosenSteps=0;
            void SetOrbit(float yaw,float pitch,float zoom)
            {
                orbit.ResetZoom();orbit.SnapToTarget();orbit.ZoomSteps(zoom);
                for(int i=0;i<2;i++) orbit.Orbit(new Vector2(Mathf.DeltaAngle(orbit.Yaw,yaw)/.32f,i==0?pitch/.2f:0));
                orbit.SnapToTarget();
            }
            for(int y=0;y<360;y+=9) foreach(float pitch in new[]{0f,9f,19f})
            foreach(float zoom in new[]{0f,2f,4f})
            {
                SetOrbit(y,pitch,zoom);
                var a=gameCam.WorldToViewportPoint(head.position);
                var b=gameCam.WorldToViewportPoint(player.position);
                var near=gameCam.WorldToViewportPoint(forwardNose);
                var lo=Vector2.one*20;var hi=-lo;int visible=0,obscured=0;
                foreach(var point in outline) {
                    var uv=gameCam.WorldToViewportPoint(point);
                    if(uv.z<=gameCam.nearClipPlane) continue;
                    lo=Vector2.Min(lo,(Vector2)uv);hi=Vector2.Max(hi,(Vector2)uv);
                    if(uv.x<.025f || uv.x>.975f || uv.y<.03f || uv.y>.97f)continue;
                    visible++;
                    if(uv.z>a.z && Mathf.Abs(uv.x-a.x)<.10f && uv.y>b.y && uv.y<a.y+.08f)obscured++;
                }
                if(visible==0 || near.z<=0)continue;
                float screenWidth=hi.x-lo.x;
                float score=6f*visible/outline.Length-3f*obscured/outline.Length-2.5f*Mathf.Abs(screenWidth-.62f)
                    -Mathf.Abs((lo.x+hi.x)*.5f-.55f)-Mathf.Abs((lo.y+hi.y)*.5f-.51f)
                    +.85f*Mathf.Clamp01(Mathf.Abs(near.x-a.x)/.28f);
                if(score>bestScore){bestScore=score;chosenYaw=y;chosenPitch=pitch;chosenSteps=zoom;}
            }
            Assert.IsFalse(float.IsInfinity(bestScore));
            SetOrbit(chosenYaw,chosenPitch,chosenSteps);
            ArtisanFramePair("Tiefscharrer-Profil","Spielkamera",actor,renderers,gameCam,1152,720);

            var side=new Vector3(1,.16f,.52f);const int width=1136,height=760;
            ArtisanPortrait("Tiefscharrer",actor,renderers,true,actor.TransformPoint(body.center),side,FrameHeight(sampled.points,body.center,side,width,height),width,height);
            ArtisanPortrait("Tiefscharrer-Tastnase",actor,renderers,true,nose.position,new Vector3(.22f,.08f,1),.26f,820,660);
            SensoryVisibility(actor,nose,skins);
            ArtisanPortrait("Tiefscharrer-Schulterbrand",actor,marks,true,shoulder.position,new Vector3(1,.16f,.22f),.34f,820,660);

            var start=driver.GetCurrentAnimatorStateInfo(0);float speed=driver.speed;var cull=driver.cullingMode;
            var sourceClips=new HashSet<AnimationClip>(controller.animationClips);
            try
            {
                driver.cullingMode=AnimatorCullingMode.AlwaysAnimate;driver.speed=0;
                foreach(string state in new[]{"Idle","Run","Attack","Death"})
                {
                    int stateId=Animator.StringToHash("Base Layer."+state);
                    Assert.IsTrue(driver.HasState(0,stateId),"Existing private clip state "+state);
                    driver.Play(stateId,0,.16f);driver.Update(0);yield return null;yield return null;
                    var info=driver.GetCurrentAnimatorClipInfo(0);
                    Assert.IsNotEmpty(info,"Existing source clip "+state);Assert.IsTrue(sourceClips.Contains(info[0].clip));
                    var startSkin=ReadBeastSkin(actor,skins[0],false).points;
                    var before=paws.Select(p=>actor.InverseTransformPoint(p.position)).ToArray();
                    var angles=paws.Select(p=>Quaternion.Inverse(actor.rotation)*p.rotation).ToArray();
                    driver.Play(stateId,0,.62f);driver.Update(0);yield return null;yield return null;
                    var endSkin=ReadBeastSkin(actor,skins[0],false).points;double sum=0;float max=0;
                    for(int i=0;i<startSkin.Length;i++){float sq=(endSkin[i]-startSkin[i]).sqrMagnitude;max=Mathf.Max(max,sq);sum+=sq;}
                    var pose=new BeastMotion{state=state,clip=info[0].clip.name,bodyRmsM=(float)Math.Sqrt(sum/startSkin.Length),
                        bodyMaxM=Mathf.Sqrt(max),pawTranslationM=paws.Select((p,i)=>Vector3.Distance(before[i],actor.InverseTransformPoint(p.position))).ToArray(),
                        pawRotationDegrees=paws.Select((p,i)=>Quaternion.Angle(angles[i],Quaternion.Inverse(actor.rotation)*p.rotation)).ToArray()};
                    note.poses.Add(pose);
                    Assert.Greater(pose.bodyMaxM,.0015f,state+" does not deform the original quadrupedal skin");
                    if(state=="Run")
                    {
                        Assert.Greater(pose.bodyRmsM,.002f,"Run displaces the actual skin");
                        for(int i=0;i<paws.Length;i++)Assert.IsTrue(pose.pawTranslationM[i]>.002f||pose.pawRotationDegrees[i]>.75f,
                            "Original locomotion did not reach "+pawNames[i]);
                    }
                    Assert.Less(Vector3.Distance(mark.bounds.center,shoulder.position),.27f,"Brand slips in "+state);
                }
            }
            finally
            {
                driver.speed=speed;driver.cullingMode=cull;
                if(start.fullPathHash!=0){driver.Play(start.fullPathHash,0,start.normalizedTime);driver.Update(0);}
                ArtisanWrite("Tiefscharrer-Anatomie.json",Encoding.UTF8.GetBytes(JsonUtility.ToJson(note,true)));
            }
            Assert.AreEqual(note.worldHash,Game.ComputeWorldHash().ToString("X16"),"Rendering the existing four clips must not change canonical combat state");
        }
    }
}
