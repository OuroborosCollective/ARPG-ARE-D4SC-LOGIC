using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    // Anchors come from the source skin's bind poses. The hand/arm can be anywhere in its
    // animation when an item is equipped; the saved appearance never depends on that pose.
    public sealed class RegionalWearableRig
    {
        internal readonly struct Part
        {
            internal readonly string MeshName;
            internal readonly HumanBodyBones Joint;
            internal readonly HumanBodyBones Distal;

            internal Part(string name, HumanBodyBones joint, HumanBodyBones distal = HumanBodyBones.LastBone)
            {
                MeshName = name;
                Joint = joint;
                Distal = distal;
            }
        }

        internal static readonly Part[] Guard = {
            new Part("Garde_Ofenhaube", HumanBodyBones.Head), new Part("Garde_Lamellenwams", HumanBodyBones.Chest),
            new Part("Garde_Zaehlgurt", HumanBodyBones.Hips),
            new Part("Garde_Armaermel_L", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
            new Part("Garde_Armaermel_R", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
            new Part("Garde_Manschette_L", HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            new Part("Garde_Manschette_R", HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            new Part("Garde_Wickel_L", HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            new Part("Garde_Wickel_R", HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            new Part("Garde_Schlackenschild", HumanBodyBones.LeftHand), new Part("Garde_Schuerhaken", HumanBodyBones.RightHand)
        };
        internal static readonly Part[] Bell = {
            new Part("Glocke_Stirnreif", HumanBodyBones.Head), new Part("Glocke_Glockenjoch", HumanBodyBones.Chest),
            new Part("Glocke_Schultertuch", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm), new Part("Glocke_Schurzgurt", HumanBodyBones.Hips),
            new Part("Glocke_Armaermel_L", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
            new Part("Glocke_Armaermel_R", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
            new Part("Glocke_Manschette_L", HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            new Part("Glocke_Manschette_R", HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            new Part("Glocke_Gamasche_L", HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            new Part("Glocke_Gamasche_R", HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            new Part("Glocke_Handglocke", HumanBodyBones.RightHand)
        };
        internal static readonly Part[] Burrower = {
            new Part("R_Wangenmaske", HumanBodyBones.Head), new Part("R_Gratharnisch", HumanBodyBones.Chest),
            new Part("R_Scharrstulpe_L", HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            new Part("R_Scharrstulpe_R", HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            new Part("R_Gratschiene_L", HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            new Part("R_Gratschiene_R", HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot)
        };
        public static IEnumerable<string> RequiredModelNodes(string model)
        {
            if (model=="Brandreif") { yield return "Brandreif"; yield break; }
            Part[] parts=model=="Schlackengarde" ? Guard : model=="Glockentraegerin" ? Bell :
                throw new ArgumentException("Unbekannte Arbeitsmontur",nameof(model));
            foreach(var part in parts) yield return part.MeshName;
        }
        private const string Folder = "Equipment/AshWorks/";
        private readonly Animator _avatar;
        private readonly SkinnedMeshRenderer _skin;
        private readonly Transform[] _joints;
        private readonly Matrix4x4[] _bind;
        private readonly Vector3 _forward, _up;
        private readonly float _size;
        private readonly Vector3 _shoulder;
        private readonly RegionalBodyFit _fit;
        private Mesh _before, _cover;
        private bool _worker;
        private readonly List<GameObject> _mounted = new List<GameObject>();

        private RegionalWearableRig(Animator avatar, SkinnedMeshRenderer skin, Transform[] joints, Matrix4x4[] bind,
            Vector3 forward, Vector3 up, float size, Vector3 shoulder, RegionalBodyFit fit)
        {
            _avatar = avatar; _skin = skin; _joints = joints; _bind = bind;
            _forward = forward; _up = up; _size = size; _shoulder=shoulder; _fit=fit;
        }

        internal static RegionalWearableRig Find(Transform actor)
        {
            if (!actor) return null;
            foreach (var avatar in actor.GetComponentsInChildren<Animator>(true))
            {
                if (!avatar.avatar || !avatar.avatar.isValid || !avatar.isHuman) continue;
                foreach (var skin in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!skin.sharedMesh) continue;
                    var joints = skin.bones;
                    var bind = skin.sharedMesh.bindposes;
                    if (bind.Length == 0 || joints.Length != bind.Length) continue;
                    int Index(HumanBodyBones bone) => Array.IndexOf(joints, avatar.GetBoneTransform(bone));
                    int a = Index(HumanBodyBones.LeftUpperArm), b = Index(HumanBodyBones.LeftLowerArm);
                    int r = Index(HumanBodyBones.RightUpperArm), h = Index(HumanBodyBones.Hips), c = Index(HumanBodyBones.Chest);
                    if (a < 0 || b < 0 || r < 0 || h < 0 || c < 0 || Index(HumanBodyBones.Head) < 0) continue;
                    Vector3 Pivot(int i) => bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
                    var shoulder = (Pivot(r)+Pivot(a))*.5f;
                    var up = (shoulder - Pivot(h)).normalized;
                    var front = Vector3.Cross(Pivot(r) - Pivot(a), up).normalized;
                    var arm = skin.transform.TransformVector(Pivot(b) - Pivot(a)).magnitude;
                    if (front.sqrMagnitude < .8f || up.sqrMagnitude < .8f || arm < .01f) continue;
                    RegionalBodyFit fit = null;
                    foreach (var key in new[]{"EntryHero","EntryNpc1"})
                    {
                        var candidate=Resources.Load<RegionalBodyFit>("ARE/Visuals/RegionalFit_"+key);
                        if(candidate && (candidate.Original==skin.sharedMesh || candidate.OriginalShoulder==skin.sharedMesh))
                        {fit=candidate;break;}
                    }
                    return new RegionalWearableRig(avatar, skin, joints, bind, front, up, arm / .30f, shoulder, fit);
                }
            }
            return null;
        }

        // Records one real mesh socket for this body. Hands on an equipped hero are reserved
        // for the actual combat loadout. The guards at the work stations also carry their tools.
        internal bool Dress(AreArpg.Core.RegionalRelic outfit, bool hands, bool hideHead)
        {
            using (var stage = new MountStage(this))
            {
                _worker = hands;
                var pattern = outfit == AreArpg.Core.RegionalRelic.Schlackengarde ? Guard :
                    outfit == AreArpg.Core.RegionalRelic.Glockentraegerin ? Bell : Burrower;
                foreach (var piece in pattern)
                {
                    if (hideHead && piece.Joint == HumanBodyBones.Head) continue;
                    if (!hands && (piece.Joint == HumanBodyBones.LeftHand || piece.Joint == HumanBodyBones.RightHand)) continue;
                    GameObject model = pattern == Burrower ?
                        Resources.Load<GameObject>("ARE/Visuals/Werkarsenal/" + piece.MeshName) :
                        Resources.Load<GameObject>(Folder + (pattern == Guard ? "Schlackengarde" : "Glockentraegerin"));
                    if (!MountMesh(model, piece, pattern == Burrower)) return false;
                }
                ApplyBodyCover(true, hideHead || pattern==Guard);
                stage.Complete = true;
                return true;
            }
        }

        internal bool Headpiece()
        {
            bool mounted=MountMesh(Resources.Load<GameObject>(Folder + "Brandreif"),
                new Part("Brandreif", HumanBodyBones.Head), false);
            if(mounted && !_cover) ApplyBodyCover(false,true);
            return mounted;
        }

        private void ApplyBodyCover(bool uniform,bool hood)
        {
            if(!_fit)return;
            var replacement=_fit.Select(_skin.sharedMesh,uniform,hood);
            if(!replacement)return;
            _before=_skin.sharedMesh;_cover=replacement;_skin.sharedMesh=replacement;
        }

        internal static MeshFilter Filter(GameObject model, string meshName)
        {
            if (!model) return null;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh && filter.name == meshName) return filter;
            return null;
        }

        private bool MountMesh(GameObject model, Part piece, bool prepared)
        {
            // Werkzeug-Einzelprefabs name their root by the original mesh. Imported outfit
            // files have one named node per part. Preserve the actual FBX local transform.
            var filter = Filter(model, piece.MeshName);
            if (!filter && prepared && model) filter = model.GetComponentInChildren<MeshFilter>(true);
            if (!filter) return false;
            var renderer = filter.GetComponent<MeshRenderer>();
            var material = prepared && renderer ? renderer.sharedMaterial : Resources.Load<Material>(Folder + "Werkstoff");
            if (!material) return false;
            var socket = Socket(piece.Joint, piece.Distal, piece.MeshName, Vector3.zero);
            if (!socket) return false;
            var mesh = new GameObject("Mesh").transform; mesh.SetParent(socket, false);
            var relative = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            mesh.SetLocalPositionAndRotation(relative.MultiplyPoint3x4(Vector3.zero), relative.rotation);
            mesh.localScale = relative.lossyScale;
            mesh.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            mesh.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return true;
        }

        internal bool Equipment(GameObject prefab, string label, HumanBodyBones joint, Vector3 offset)
        {
            if (!prefab) return false;
            int start = _mounted.Count;
            var socket = Socket(joint, HumanBodyBones.LastBone, label, offset);
            if (socket && PassiveArtVisuals.TryBindPrefab(socket, prefab)) return true;
            RemoveFrom(start);
            return false;
        }

        private Transform Socket(HumanBodyBones target, HumanBodyBones distal, string label, Vector3 offset)
        {
            var joint = _avatar.GetBoneTransform(target);
            int i = Array.IndexOf(_joints, joint);
            if (i < 0 || !joint) return null;
            var up = _up;
            if (distal != HumanBodyBones.LastBone)
            {
                int j = Array.IndexOf(_joints, _avatar.GetBoneTransform(distal));
                if (j < 0) return null;
                up = (_bind[i].inverse.MultiplyPoint3x4(Vector3.zero) - _bind[j].inverse.MultiplyPoint3x4(Vector3.zero)).normalized;
            }
            var forward = Vector3.ProjectOnPlane(_forward, up).normalized;
            if (forward.sqrMagnitude < .8f) return null;
            var frame = Quaternion.LookRotation(_bind[i].MultiplyVector(forward), _bind[i].MultiplyVector(up));
            if (_fit && target==HumanBodyBones.LeftHand) frame = _worker ? _fit.StaffLeft : _fit.HeroLeft;
            if (_fit && target==HumanBodyBones.RightHand) frame = _worker ? _fit.StaffRight : _fit.HeroRight;
            var nominal = new Vector3(_size,_size,_size);
            var skinDelta=Vector3.zero;
            if(target==HumanBodyBones.Chest)
            {
                // In the purchased humanoid Chest lies only 8 cm above Hips; the costume's
                // authoring socket sits 10.8 cm below the midpoint of the arm roots instead.
                skinDelta=_shoulder-_bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
                offset+=new Vector3(0,-.108f,0);
                if(_fit)
                {
                    var profile=label=="Garde_Lamellenwams" ? _fit.GuardTorso : label=="Glocke_Glockenjoch" ? _fit.BellTorso : Vector2.zero;
                    skinDelta+=_forward*(profile.y>0 ? profile.x : _fit.TorsoCenterZ);
                    float width=_skin.transform.TransformVector(Vector3.Cross(_up,_forward)*_fit.TorsoWidth).magnitude;
                    nominal.x=Mathf.Max(_size,width/.357f*1.055f);
                    float depth=profile.y>0 ? profile.y : _fit.TorsoDepth/.245f*1.065f;
                    nominal.z=Mathf.Max(_size,_skin.transform.TransformVector(_forward*depth).magnitude);
                }
            }
            if (target==HumanBodyBones.Hips && _fit && (label=="Garde_Zaehlgurt" || label=="Glocke_Schurzgurt"))
            {
                skinDelta+=_forward*_fit.WaistCenterZ;
                float width=_skin.transform.TransformVector(Vector3.Cross(_up,_forward)*_fit.WaistWidth).magnitude;
                float depth=_skin.transform.TransformVector(_forward*_fit.WaistDepth).magnitude;
                nominal.x=Mathf.Max(_size,width/.306f*1.035f);
                nominal.z=Mathf.Max(_size,depth/.218f*1.045f);
            }
            if(target==HumanBodyBones.Head && _fit)
            {
                var right=Vector3.Cross(_up,_forward);
                bool band=label=="Glocke_Stirnreif";
                Vector3 face=band ? _fit.BrowHigh-_fit.BrowLow : _fit.FaceHigh-_fit.FaceLow;
                // Circumference constants are the interior of the measured authored shell,
                // with brow-band sizing evaluated outside the forehead and braided hair.
                float width=_skin.transform.TransformVector(right*face.x).magnitude;
                float depth=_skin.transform.TransformVector(_forward*face.z).magnitude;
                nominal.x=Mathf.Max(_size,width/(band ? .195f : .200f)*1.04f);
                nominal.z=Mathf.Max(_size,depth/(band ? .225f : .230f)*1.035f);
                nominal.y=Mathf.Max(_size,(nominal.x+nominal.z)*.5f);
                var mid=band ? (_fit.BrowHigh+_fit.BrowLow)*.5f : (_fit.FaceHigh+_fit.FaceLow)*.5f;
                float desiredHeight = band ? _fit.EyeHeight + .051f : _fit.EyeHeight;
                float modelHeight = band ? .049f : label=="Brandreif" ? .063f : .077f;
                skinDelta=right*mid.x+_up*desiredHeight+_forward*mid.z;
                offset+=new Vector3(0,-modelHeight,band ? .003f : .010f);
            }
            var axes = _skin.transform.localToWorldMatrix * _bind[i].inverse * Matrix4x4.Rotate(frame);
            var lengths = new Vector3(axes.MultiplyVector(Vector3.right).magnitude,
                axes.MultiplyVector(Vector3.up).magnitude, axes.MultiplyVector(Vector3.forward).magnitude);
            if (Mathf.Min(lengths.x, Mathf.Min(lengths.y, lengths.z)) < .00001f) return null;
            var obj = new GameObject(label);
            obj.transform.SetParent(joint, false);
            obj.transform.localScale = new Vector3(nominal.x / lengths.x, nominal.y / lengths.y, nominal.z / lengths.z);
            obj.transform.SetLocalPositionAndRotation(_bind[i].MultiplyVector(skinDelta)+frame * Vector3.Scale(offset,obj.transform.localScale),frame);
            _mounted.Add(obj);
            return obj.transform;
        }

        internal void Clear()
        {
            if(_skin && _cover && _skin.sharedMesh==_cover) _skin.sharedMesh=_before;
            _before=null;_cover=null;
            RemoveFrom(0);
        }
        private void RemoveFrom(int start)
        {
            for (int i = _mounted.Count - 1; i >= start; i--)
            {
                var part = _mounted[i];
                if (part) { part.SetActive(false); UnityEngine.Object.Destroy(part); }
                _mounted.RemoveAt(i);
            }
        }

        // All visible parts of a new outfit must bind before it replaces the old appearance.
        private sealed class MountStage : IDisposable
        {
            private readonly RegionalWearableRig _rig;
            internal readonly int Start;
            internal bool Complete;
            internal MountStage(RegionalWearableRig rig) { _rig = rig; Start = rig._mounted.Count; }
            public void Dispose() { if (!Complete) _rig.RemoveFrom(Start); }
        }
    }
}
