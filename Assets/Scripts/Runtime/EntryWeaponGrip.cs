using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Cosmetic humanoid hand constraint. It has no simulation, input or persistence access.</summary>
    [DisallowMultipleComponent]
    public sealed class EntryWeaponGrip : MonoBehaviour
    {
        public const string WeightParameter = "EntryGripWeight";
        private static readonly int Weight = Animator.StringToHash(WeightParameter);
        private Animator _animator;
        private Transform _grip, _elbow, _hand;
        private Quaternion _boneToGoal, _leftBoneToGoal;
        private Transform _leftHand;
        private bool _calibrated;
        private HeroMeleeMotion _chain;
        internal void FitShield(Transform model) => _chain?.FitShield(model);

        internal void BeginChain(AreArpg.Core.ArenaSimulation world, int stage)
        {
            _chain?.Begin(world, stage);
        }
        internal void ProjectChain(AreArpg.Core.ArenaSimulation world, float fraction, bool moving, bool paused)
            => _chain?.Project(world,fraction,moving,paused);

        /// <summary>Attach only after the passive prefab has passed validation and exposes the opt-in rig contract.</summary>
        public static bool TryAttach(Animator animator)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                return false;
            bool parameter = false;
            foreach (var p in animator.parameters)
                if (p.nameHash == Weight && p.type == AnimatorControllerParameterType.Float) parameter = true;
            if (!parameter) return false;
            var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            var arm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (chest == null || arm == null || hand == null) return false;
            Transform grip = null, elbow = null;
            foreach (var t in chest.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "EntryReadyGrip") { if (grip != null) return false; grip = t; }
                if (t.name == "EntryReadyElbow") { if (elbow != null) return false; elbow = t; }
            }
            // The targets must be independent of the arm being solved; otherwise the solver feeds itself.
            if (grip == null || elbow == null || grip.IsChildOf(arm) || elbow.IsChildOf(arm)) return false;
            var projection = animator.GetComponent<EntryWeaponGrip>() ?? animator.gameObject.AddComponent<EntryWeaponGrip>();
            // The player can enter the camera frame or be sampled before its first renderer
            // visibility update. Hand IK must run on that frame as well.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            projection._animator = animator; projection._grip = grip; projection._elbow = elbow;
            projection._hand = hand;
            projection._leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            projection._calibrated = false;
            var socket = hand.Find("EntryWeaponSocket");
            if (socket != null) projection._chain = new HeroMeleeMotion(animator,socket);
            return true;
        }

        /// <summary>Clip curves blend the hold in locomotion and release it during the existing strike.</summary>
        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null) return;
            // Unity's IK goal uses palm axes, which need not match this avatar's wrist bone axes.
            // Calibrate from the real animation pose before this component writes its first IK goal.
            if (!_calibrated)
            {
                _boneToGoal = Quaternion.Inverse(_hand.rotation) * _animator.GetIKRotation(AvatarIKGoal.RightHand);
                if (_leftHand != null)
                    _leftBoneToGoal = Quaternion.Inverse(_leftHand.rotation) * _animator.GetIKRotation(AvatarIKGoal.LeftHand);
                _calibrated = true;
            }
            float weight = _grip != null && _elbow != null ? Mathf.Clamp01(_animator.GetFloat(Weight)) : 0f;
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);
            if (weight > 0f)
            {
                _animator.SetIKPosition(AvatarIKGoal.RightHand, _grip.position);
                _animator.SetIKRotation(AvatarIKGoal.RightHand, _grip.rotation * _boneToGoal);
                _animator.SetIKHintPosition(AvatarIKHint.RightElbow, _elbow.position);
            }
            _chain?.Solve(_boneToGoal, _leftBoneToGoal);
        }
    }
}
