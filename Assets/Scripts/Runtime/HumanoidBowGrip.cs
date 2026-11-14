using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Map the authored bow frame to a humanoid palm without writing IK, gameplay or source assets.</summary>
    [DisallowMultipleComponent]
    public sealed class HumanoidBowGrip : MonoBehaviour
    {
        // Inverse bone-to-palm frame measured on the reviewed player rig in Unity's IK callback.
        // Keeping that existing bow orientation lets other avatars use their own wrist bone axes.
        private static readonly Quaternion PalmToBow = new Quaternion(.54204f,-.50064f,.45147f,.50173f).normalized;
        private Animator _animator;
        private Transform _hand, _bow;
        private bool _calibrated;

        public static void Bind(Animator animator, Transform bow)
        {
            if(animator==null)return;
            var binding=animator.GetComponent<HumanoidBowGrip>();
            if(binding!=null)binding._bow=null;
            if(bow==null||animator.avatar==null||!animator.avatar.isValid||!animator.isHuman)return;
            var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if(hand==null||bow.parent!=hand)return;
            if(binding==null)binding=animator.gameObject.AddComponent<HumanoidBowGrip>();
            binding._animator=animator;binding._hand=hand;binding._bow=bow;binding._calibrated=false;
        }

        private void OnAnimatorIK(int layer)
        {
            if(layer!=0||_calibrated||_bow==null||_hand==null||_animator==null)return;
            var boneToPalm=Quaternion.Inverse(_hand.rotation)*_animator.GetIKRotation(AvatarIKGoal.LeftHand);
            _bow.localRotation=boneToPalm*PalmToBow;
            _calibrated=true;
        }
    }
}
