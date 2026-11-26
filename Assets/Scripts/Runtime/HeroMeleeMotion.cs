using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    // Hand paths in the hero's facing frame, measured from the hips. The humanoid solver
    // retargets the two grips and keeps the animated feet planted during weight transfer.
    internal sealed class HeroMeleeMotion
    {
        private readonly struct Pose
        {
            internal readonly Vector3 Right, Left;
            internal readonly float BladeYaw, BladePitch, Turn, Lean;
            internal Pose(Vector3 right, Vector3 left, float yaw, float pitch, float turn, float lean)
            { Right = right; Left = left; BladeYaw = yaw; BladePitch = pitch; Turn = turn; Lean = lean; }

            internal static Pose Control(Pose a, Pose through, Pose b)
            {
                // The quadratic at t=.5 is .25*a + .5*control + .25*b.
                return new Pose(2*through.Right - (a.Right+b.Right)*.5f, 2*through.Left - (a.Left+b.Left)*.5f,
                    through.BladeYaw - .5f*(Mathf.DeltaAngle(through.BladeYaw,a.BladeYaw)+Mathf.DeltaAngle(through.BladeYaw,b.BladeYaw)),
                    through.BladePitch - .5f*(Mathf.DeltaAngle(through.BladePitch,a.BladePitch)+Mathf.DeltaAngle(through.BladePitch,b.BladePitch)),
                    2*through.Turn-(a.Turn+b.Turn)*.5f, 2*through.Lean-(a.Lean+b.Lean)*.5f);
            }

            internal static Pose Blend(Pose a, Pose b, float t) => new Pose(
                Vector3.Lerp(a.Right,b.Right,t), Vector3.Lerp(a.Left,b.Left,t),
                Mathf.LerpAngle(a.BladeYaw,b.BladeYaw,t), Mathf.LerpAngle(a.BladePitch,b.BladePitch,t),
                Mathf.Lerp(a.Turn,b.Turn,t), Mathf.Lerp(a.Lean,b.Lean,t));
        }

        private static readonly Vector3 Free = new Vector3(-.29f,-.15f,.06f);
        private static readonly Vector3 Buckler = new Vector3(-.26f,.23f,.24f);
        private static Pose Sword(float x,float y,float z,float yaw,float pitch,float turn,float lean) =>
            new Pose(new Vector3(x,y,z), Free + new Vector3(-lean*.003f,.08f,-turn*.002f), yaw,pitch,turn,lean);
        private static Pose Shield(float x,float y,float z,float yaw,float pitch,float turn,float lean,Vector3 left) =>
            new Pose(new Vector3(x,y,z),left,yaw,pitch,turn,lean);

        private static readonly Pose Rest = new Pose(new Vector3(.28f,-.12f,.09f),Free,24,59,0,0);
        // Boundaries are shared: low left -> high right -> low left -> right-side guard.
        private static readonly Pose[] SwordEdges = {
            Sword(.42f,.25f,.05f,95,-16,27,-3), Sword(-.23f,.10f,.32f,-68,18,-33,8),
            Sword(.36f,.49f,.22f,53,-55,23,-4), Sword(-.25f,.02f,.28f,-57,35,-35,13)
        };
        private static readonly Pose[] SwordPass = {
            Sword(.09f,.26f,.56f,0,3,-5,9), Sword(.01f,.30f,.52f,5,-26,0,7),
            Sword(.08f,.28f,.55f,-5,7,-4,12), Sword(.04f,.15f,.54f,0,12,0,8)
        };
        private static readonly Pose[] ShieldEdges = {
            Shield(.37f,.23f,.06f,80,-20,22,-3,Buckler),
            Shield(-.13f,.12f,.34f,-58,15,-24,6,Buckler),
            Shield(-.17f,.14f,.22f,-70,-8,25,9,new Vector3(-.16f,.30f,.43f))
        };
        private static readonly Pose[] ShieldPass = {
            Shield(.12f,.25f,.51f,3,5,-6,9,Buckler),
            Shield(-.16f,.17f,.18f,-68,0,22,14,new Vector3(-.08f,.35f,.58f)),
            Shield(.08f,.24f,.50f,4,0,-4,7,Buckler)
        };
        private static readonly Pose[] AxeEdges = {
            Sword(.32f,.26f,.04f,78,-40,37,-8), Sword(-.14f,.13f,.23f,-68,8,-37,12),
            Sword(.17f,.55f,.12f,17,-78,12,-11)
        };
        private static readonly Pose[] AxePass = {
            Sword(.07f,.22f,.46f,0,-8,-7,12), Sword(.03f,.22f,.43f,0,-3,1,14),
            Sword(.08f,.12f,.45f,5,29,-8,19)
        };

        private readonly Animator _rig;
        private readonly Transform _hips, _socket;
        private readonly float _scale;
        private Pose _pose = Rest, _from = Rest;
        private MeleeForm _form;
        private int _stage;
        private ulong _sequence, _endTick;
        private bool _swing;
        private float _weight;

        internal HeroMeleeMotion(Animator rig, Transform socket)
        {
            _rig = rig; _socket = socket; _hips = rig.GetBoneTransform(HumanBodyBones.Hips);
            var upper = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var elbow = rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand = rig.GetBoneTransform(HumanBodyBones.RightHand);
            _scale = (Vector3.Distance(upper.position,elbow.position) + Vector3.Distance(elbow.position,hand.position)) / .58f;
        }

        internal void Begin(ArenaSimulation w, int stage)
        {
            _from = _pose; _form = StandingMeleeChain.Form(w.Progression.Equipment); _stage = stage;
            _sequence = w.ActionSequence; _endTick = w.CurrentTick + (ulong)w.CurrentActionTiming.Total;
            _swing = true;
        }

        private void Track(out Pose[] edges, out Pose[] pass)
        {
            if (_form == MeleeForm.SwordShield) { edges = ShieldEdges; pass = ShieldPass; }
            else if (_form == MeleeForm.GreatAxe) { edges = AxeEdges; pass = AxePass; }
            else { edges = SwordEdges; pass = SwordPass; }
        }

        internal void Project(ArenaSimulation w, float fraction, bool moving, bool paused)
        {
            var form = StandingMeleeChain.Form(w.Progression.Equipment);
            if (!w.CombatV2 || w.GameOver || form == MeleeForm.None) { _weight = 0; _swing = false; return; }
            if (paused) return;
            if (_sequence != w.ActionSequence || form != _form) _swing = false;
            var state = _rig.GetCurrentAnimatorStateInfo(0);
            bool idle = !moving && w.ActionKind == CombatActionKind.None && state.IsName("Idle");
            bool striking = _swing && (w.ActionKind == CombatActionKind.Strike || w.ActionKind == CombatActionKind.HeavyStrike);
            bool hold = _swing && !moving && w.ActionKind == CombatActionKind.None && w.CurrentTick >= _endTick &&
                w.CurrentTick - _endTick < StandingMeleeChain.HoldTicks;
            _weight = Mathf.MoveTowards(_weight, striking || hold || idle ? 1f : 0f, Time.deltaTime * 12f);
            _form = form;
            if (striking || hold)
            {
                Track(out var edges,out var pass);
                var timing = w.CurrentActionTiming;
                float tick = hold ? timing.Total : w.ActionTick + fraction;
                float contact = timing.Startup + 1f, start = timing.Startup * .65f;
                float finish = contact + timing.Recovery * .62f;
                var end = edges[(_stage+1) % edges.Length];
                if (tick < start)
                    _pose = Pose.Blend(_from,edges[_stage],Mathf.SmoothStep(0,1,tick/start));
                else if (tick < finish)
                {
                    // Quadratic swing passes through the authored contact posture without stopping at impact.
                    float t = tick < contact ? .5f * Mathf.InverseLerp(start,contact,tick) :
                        .5f + .5f * Mathf.InverseLerp(contact,finish,tick);
                    var middle = Pose.Control(edges[_stage],pass[_stage],end);
                    _pose = Pose.Blend(Pose.Blend(edges[_stage],middle,t),Pose.Blend(middle,end,t),t);
                }
                else _pose = end;
            }
            else if (idle)
            {
                var rest = form == MeleeForm.SwordShield ? new Pose(Rest.Right,new Vector3(-.30f,.02f,.13f),24,59,0,0) :
                    form == MeleeForm.GreatAxe ? Sword(.22f,.04f,.15f,-26,-47,5,0) : Rest;
                _pose = Pose.Blend(_pose,rest,1f-Mathf.Exp(-10f*Time.deltaTime));
            }
        }

        internal void Solve(Quaternion boneToGoal)
        {
            if (_weight <= 0f) return;
            Transform frame = _rig.transform;
            Vector3 origin = _hips.position;
            Quaternion facing = frame.rotation;
            Quaternion blade = facing * Quaternion.Euler(_pose.BladePitch,_pose.BladeYaw,0);
            Vector3 right = origin + facing * (_scale * _pose.Right);
            Vector3 left = _form == MeleeForm.GreatAxe ? right + blade * Vector3.forward * (.20f*_scale) :
                origin + facing * (_scale * _pose.Left);
            foreach (var foot in Feet)
            {
                var point = _rig.GetIKPosition(foot); var rotation = _rig.GetIKRotation(foot);
                _rig.SetIKPositionWeight(foot,_weight); _rig.SetIKRotationWeight(foot,_weight);
                _rig.SetIKPosition(foot,point); _rig.SetIKRotation(foot,rotation);
            }
            _rig.bodyRotation = Quaternion.AngleAxis(_pose.Turn*_weight,frame.up) *
                Quaternion.AngleAxis(_pose.Lean*_weight,frame.right) * _rig.bodyRotation;
            _rig.bodyPosition += facing * new Vector3(-_pose.Turn*.001f,-Mathf.Abs(_pose.Lean)*.0018f,_pose.Lean*.0025f) * (_scale*_weight);
            _rig.SetIKPositionWeight(AvatarIKGoal.RightHand,_weight);
            _rig.SetIKPositionWeight(AvatarIKGoal.LeftHand,_weight);
            _rig.SetIKPosition(AvatarIKGoal.RightHand,right);
            _rig.SetIKPosition(AvatarIKGoal.LeftHand,left);
            _rig.SetIKRotationWeight(AvatarIKGoal.RightHand,_weight);
            // The baked weapon socket's local Y is the length of the blade.
            _rig.SetIKRotation(AvatarIKGoal.RightHand,blade * Quaternion.Euler(90,0,0) * Quaternion.Inverse(_socket.localRotation) * boneToGoal);
        }
        private static readonly AvatarIKGoal[] Feet = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
    }
}
