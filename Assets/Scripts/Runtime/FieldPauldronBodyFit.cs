using UnityEngine;

namespace AreArpg.UnityClient
{
    // Created only in the owner project from its installed and exactly matched modular shoulder.
    public sealed class FieldPauldronBodyFit : ScriptableObject
    {
        public Mesh Original;
        public Mesh WithoutLeftCap;
        public Vector3 CapCenterInUpperArm;
        public Vector2 CapWidthDepthInSkin;
    }
}
