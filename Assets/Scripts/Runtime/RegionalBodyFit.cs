using UnityEngine;

namespace AreArpg.UnityClient
{
    // Generated only from the owner's installed modular character parts and its verified original atlas mesh.
    public sealed class RegionalBodyFit : ScriptableObject
    {
        public Mesh Original, Uniform, HoodedUniform, HoodOnly, ShoulderWithoutHair, OriginalShoulder;
        public Vector3 FaceLow, FaceHigh, BrowLow, BrowHigh;
        public float TorsoWidth, TorsoDepth, TorsoCenterZ, EyeHeight;
        public float WaistWidth, WaistDepth, WaistCenterZ;
        public Vector2 GuardTorso, BellTorso;
        public Quaternion StaffLeft, StaffRight, HeroLeft, HeroRight;

        public Mesh Select(Mesh current, bool uniform, bool hood)
        {
            if (current == OriginalShoulder && hood && !uniform) return ShoulderWithoutHair;
            if (current != Original) return null;
            return uniform ? (hood ? HoodedUniform : Uniform) : hood ? HoodOnly : null;
        }
    }
}
