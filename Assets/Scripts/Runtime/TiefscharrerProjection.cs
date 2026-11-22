using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Project the live encounter's four existing clips onto the low digging animal.</summary>
    [DisallowMultipleComponent]
    public sealed class TiefscharrerProjection : MonoBehaviour
    {
        private struct Joint
        {
            public Transform driver, driven;
            public Quaternion inverseDriverRest, drivenRest;
            public float influence, limit;
        }

        private Transform sourceFrame, creatureFrame, sourceTrunk, rootJoint;
        private Transform[] soleContacts;
        private Animator animationSource;
        private Vector3 sourceTrunkRest, rootJointRest;
        private Quaternion rootRestRotation;
        private Joint[] joints;

        private static Dictionary<string,Transform> Nodes(Transform root)
        {
            var found=new Dictionary<string,Transform>(StringComparer.Ordinal);
            foreach(var node in root.GetComponentsInChildren<Transform>(true))
                if(!found.ContainsKey(node.name)) found.Add(node.name,node);
            return found;
        }

        private static int Depth(Transform node)
        {
            int depth=0;
            for(var p=node.parent;p;p=p.parent) depth++;
            return depth;
        }

        public static bool Attach(Transform actor)
        {
            if(!actor) return false;
            if(actor.GetComponent<TiefscharrerProjection>()) return true;
            var original=Resources.Load<GameObject>("ARE/Visuals/CryptRattler");
            var model=Resources.Load<GameObject>("Equipment/Tiefscharrer/Tiefscharrer_World");
            var driver=actor.Find("Art_CryptRattler");
            if(!original || !model || !driver) return false;
            var component=actor.gameObject.AddComponent<TiefscharrerProjection>();
            var instance=Instantiate(model,actor,false);
            try {
                component.Initialize(original.transform,driver,instance);
                return true;
            }
            catch(Exception error) {
                instance.SetActive(false);
                Destroy(instance);component.enabled=false;Destroy(component);
                Debug.LogWarning("Tiefscharrer skin binding failed: "+error.Message);
                return false;
            }
        }

        private void Initialize(Transform reference,Transform driver,GameObject visual)
        {
            visual.name="ARE_Tiefscharrer_Form";
            creatureFrame=visual.transform;sourceFrame=driver;
            animationSource=driver.GetComponentInChildren<Animator>(true);
            if(!animationSource || !animationSource.runtimeAnimatorController) throw new InvalidOperationException("Encounter controller");
            var originalSkin=driver.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if(!originalSkin || !originalSkin.sharedMesh) throw new InvalidOperationException("Encounter skin");
            var form=Nodes(creatureFrame);var live=Nodes(driver);var rest=Nodes(reference);
            // Validate all targets and source bones before retiring any visible piece.
            rootJoint=form["TS_Root"];var tail=form["TS_Tail"];
            var brandSocket=form["TS_Brand"];var nose=form["TS_Nose"];
            var royalMark=live["Immergruen_Royal_Brand"];
            if(!royalMark.GetComponent<Renderer>())throw new InvalidOperationException("Original physical brand");
            var formLods=visual.GetComponent<LODGroup>();
            if(!formLods || formLods.lodCount!=3)throw new InvalidOperationException("Creature LODGroup");
            if(creatureFrame.InverseTransformPoint(nose.position).z<creatureFrame.InverseTransformPoint(tail.position).z)
                creatureFrame.localRotation=Quaternion.Euler(0,180,0)*creatureFrame.localRotation;

            var bind=new List<Joint>();
            void Drive(string dst,string src,float influence,float degrees)
            {
                var target=form["TS_"+dst];
                bind.Add(new Joint {driver=live[src],driven=target,
                    inverseDriverRest=Quaternion.Inverse(Quaternion.Inverse(reference.rotation)*rest[src].rotation),
                    drivenRest=Quaternion.Inverse(creatureFrame.rotation)*target.rotation,influence=influence,limit=degrees});
            }
            Drive("Pelvis","Torso",.21f,15);Drive("Spine","Torso",.27f,17);Drive("Chest","Chest",.28f,21);
            Drive("Neck","Neck",.48f,31);Drive("Head","Head",.62f,40);Drive("Jaw","Jaw1",.56f,34);
            Drive("Tail","Torso",.36f,23);
            // Resolve anatomical sides by their physical rest pose even if the old FBX labels use
            // the opposing modelling convention. A mirrored shoulder never drives the near paw.
            float sourceLeft=reference.InverseTransformPoint(rest["Shoulder.L"].position).x;
            float targetLeft=transform.InverseTransformPoint(form["TS_ForeArm_L"].position).x;
            bool flipped=sourceLeft*targetLeft<0f;
            foreach(bool left in new[]{true,false}) {
                string targetSide=left?"_L":"_R", sourceSide=(left!=flipped)?".L":".R";
                Drive("ForeArm"+targetSide,"Arm1"+sourceSide,.43f,35);
                Drive("ForeElbow"+targetSide,"Arm2"+sourceSide,.51f,43);
                Drive("ForePaw"+targetSide,"Hand"+sourceSide,.59f,46);
                Drive("HindHip"+targetSide,"Leg1"+sourceSide,.52f,38);
                Drive("HindHock"+targetSide,"Leg2"+sourceSide,.55f,43);
                Drive("HindPaw"+targetSide,"Foot"+sourceSide,.57f,38);
            }
            bind.Sort((a,b)=>Depth(a.driven).CompareTo(Depth(b.driven)));joints=bind.ToArray();
            var contacts = new List<Transform>(16);
            foreach (string paw in new[] { "ForePaw_L", "ForePaw_R", "HindPaw_L", "HindPaw_R" })
                for (int i = 0; i < 4; i++) contacts.Add(form["TS_Sole_" + paw + "_" + i]);
            soleContacts = contacts.ToArray();
            sourceTrunk=live["Chest"];sourceTrunkRest=reference.InverseTransformPoint(rest["Chest"].position);
            rootJointRest=rootJoint.localPosition;rootRestRotation=rootJoint.localRotation;
            foreach(var a in visual.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var originalBrand=rest["Immergruen_Royal_Brand"];
            var skinScale=royalMark.lossyScale;
            var brandOrientation=Quaternion.Inverse(reference.rotation)*originalBrand.rotation;
            royalMark.SetParent(brandSocket,false);royalMark.localScale=Vector3.one;
            var surfaceNormal=new Vector3(.93471f,.35428f,-.02854f).normalized;
            royalMark.position=brandSocket.position + transform.TransformDirection(surfaceNormal)*.003f;
            royalMark.rotation=transform.rotation*Quaternion.FromToRotation(Vector3.right,surfaceNormal)*brandOrientation;
            var basis=royalMark.lossyScale;
            royalMark.localScale=new Vector3(skinScale.x/basis.x,skinScale.y/basis.y,skinScale.z/basis.z);
            originalSkin.enabled=false;
            SamplePose();
        }

        private void LateUpdate() { if(joints!=null)SamplePose(); }

        private void SamplePose()
        {
            var frameRotation=sourceFrame.rotation;var targetRotation=creatureFrame.rotation;
            rootJoint.localPosition=rootJointRest;rootJoint.localRotation=rootRestRotation;
            foreach(var bone in joints) {
                var delta=Quaternion.Inverse(frameRotation)*bone.driver.rotation*bone.inverseDriverRest;
                var angle=Quaternion.Angle(Quaternion.identity,delta);
                if(angle<.001f) delta=Quaternion.identity;
                else delta=Quaternion.SlerpUnclamped(Quaternion.identity,delta,Mathf.Min(bone.influence,bone.limit/angle));
                bone.driven.rotation=targetRotation*delta*bone.drivenRest;
            }
            var trunk=sourceFrame.InverseTransformPoint(sourceTrunk.position)-sourceTrunkRest;
            float bob=Mathf.Clamp(trunk.y*.22f,-.10f,.09f);
            var state=animationSource.GetCurrentAnimatorStateInfo(0);
            float death=state.IsName("Death")?Mathf.SmoothStep(0,1,Mathf.Clamp01(state.normalizedTime)) : 0;
            if (death>0) {
                var lateral=Quaternion.AngleAxis(43f*death,creatureFrame.forward);
                rootJoint.rotation=lateral*rootJoint.rotation;
            }
            float floor=transform.position.y, footY=float.PositiveInfinity;
            foreach(var sole in soleContacts) footY=Mathf.Min(footY,sole.position.y);
            float shift=bob-Mathf.Clamp(footY-floor-.004f+bob,-.48f,.48f);
            rootJoint.position+=transform.up*(shift-.065f*death);
        }
    }
}
