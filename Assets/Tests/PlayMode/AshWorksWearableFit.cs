using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [Serializable]
        private sealed class WearableFitting
        {
            public string actor,skin,originalSkin,headgear;
            public int baselineTriangles,wornTriangles,originalFaceTriangles,remainingFaceTriangles;
            public float eyeToAuthoredOpeningM,torsoTopOverShoulderM,shieldVerticalDot,shieldFrontDot;
        }

        private void CheckOccupationalFit(Transform actor,bool guard)
        {
            var avatar=actor.GetComponentsInChildren<Animator>(true).Single(a=>a.avatar&&a.avatar.isHuman&&a.runtimeAnimatorController);
            var fit=Resources.Load<RegionalBodyFit>("ARE/Visuals/RegionalFit_"+(guard?"EntryHero":"EntryNpc1"));
            Assert.IsTrue(fit && fit.Original,"Private body fitting "+actor.name);
            var mesh=guard?fit.HoodedUniform:fit.Uniform;
            var skin=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).SingleOrDefault(s=>s.sharedMesh==mesh&&s.enabled);
            Assert.IsTrue(skin,"The actual entry avatar must use its reversible authored clothing mask on "+actor.name);
            var head=avatar.GetBoneTransform(HumanBodyBones.Head);
            var left=avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var right=avatar.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var hips=avatar.GetBoneTransform(HumanBodyBones.Hips);
            var shoulder=(left.position+right.position)/2;
            var up=(shoulder-hips.position).normalized;var side=(right.position-left.position).normalized;
            var front=Vector3.Cross(side,up).normalized;
            var all=avatar.GetComponentsInChildren<Transform>(true);
            var leftEye=all.Single(t=>string.Equals(t.name,"eyeRoot_l",StringComparison.OrdinalIgnoreCase));
            var rightEye=all.Single(t=>string.Equals(t.name,"eyeRoot_r",StringComparison.OrdinalIgnoreCase));
            var eye=(leftEye.position+rightEye.position)/2;
            var item=guard?"Garde_Ofenhaube":"Glocke_Stirnreif";
            var headMount=all.Single(t=>t.name==item);
            var note=new WearableFitting{actor=actor.name,skin=mesh.name,originalSkin=fit.Original.name,headgear=item};

            int CountCentralFace(SkinSample sample)
            {
                int count=0;var tri=sample.triangles;
                for(int i=0;i<tri.Length;i+=3)
                {
                    var world=actor.TransformPoint((sample.points[tri[i]]+sample.points[tri[i+1]]+sample.points[tri[i+2]])/3f);
                    var delta=world-eye;
                    if(Mathf.Abs(Vector3.Dot(delta,side))<.079f && Vector3.Dot(delta,up)>-.095f && Vector3.Dot(delta,up)<.045f &&
                           Vector3.Dot(delta,front)>-.082f && Vector3.Dot(delta,front)<.080f)count++;
                }
                return count;
            }
            try
            {
                var dressed=ReadBeastSkin(actor,skin,true);
                note.wornTriangles=dressed.triangles.Length/3;note.remainingFaceTriangles=CountCentralFace(dressed);
                skin.sharedMesh=fit.Original;
                var original=ReadBeastSkin(actor,skin,true);
                note.baselineTriangles=original.triangles.Length/3;note.originalFaceTriangles=CountCentralFace(original);
            }
            finally {skin.sharedMesh=mesh;}
            Assert.Less(note.wornTriangles,note.baselineTriangles,"Original intersecting uniform still renders");
            Assert.Greater(note.originalFaceTriangles,15,"Native eyes and nose were not represented");
            Assert.Greater(note.remainingFaceTriangles,note.originalFaceTriangles*.25f,"Wrong body mask cut out the central face");
            // The visor seam and the forehead band are measured from their different original authoring sockets.
            var authorPoint=headMount.TransformPoint(new Vector3(0,guard ? .077f : .049f,.110f));
            note.eyeToAuthoredOpeningM=Vector3.Dot(authorPoint-eye,up);
            Assert.That(note.eyeToAuthoredOpeningM,guard?Is.InRange(-.067f,.067f):Is.InRange(.004f,.17f),
                        "Authored eye slit or forehead band no longer tracks the character's actual eye centres");
            var torso=all.Single(t=>t.name==(guard?"Garde_Lamellenwams":"Glocke_Glockenjoch"));
            var top=torso.TransformPoint(new Vector3(0,guard ? .18f : .207f,.11f));
            note.torsoTopOverShoulderM=Vector3.Dot(top-shoulder,up);
            Assert.That(note.torsoTopOverShoulderM,Is.InRange(-.13f,.24f),"Chest protection has fallen to the sternum");
            if(guard)
            {
                var shield=all.Single(t=>t.name=="Garde_Schlackenschild");
                note.shieldVerticalDot=Vector3.Dot(shield.TransformVector(Vector3.up).normalized,up);
                note.shieldFrontDot=Vector3.Dot(shield.TransformVector(Vector3.forward).normalized,front);
                Assert.Greater(note.shieldVerticalDot,.40f,"Brick shield is a horizontal tray at idle");
                Assert.Greater(note.shieldFrontDot,.27f,"Brick shield front faces the torso");
            }
            ArtisanWrite((guard?"Schlackengarde":"Glockentraegerin")+"-Passform.json",
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(note,true)));
            var coat=torso.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled&&ArtisanMesh(r)).ToArray();
            var chest=shoulder-up*.168f+front*.10f;
            ArtisanPortrait((guard?"Schlackengarde":"Glockentraegerin")+"-Brustsitz",actor,coat,true,
                chest,actor.InverseTransformDirection(front+up*.035f),.100f,450,370,null,.48f);
            if(guard)
            {
                var hood=headMount.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled&&ArtisanMesh(r)).ToArray();
                var crown=eye+up*.106f-front*.067f;
                ArtisanPortrait("Schlackengarde-Helmkuppe",actor,hood,true,crown,
                    actor.InverseTransformDirection(up+front*.16f),.105f,450,370,null,.65f);
            }
        }
    }
}
