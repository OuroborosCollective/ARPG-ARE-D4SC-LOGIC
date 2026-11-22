using UnityEngine;

namespace AreArpg.UnityClient
{
    // The alternate skin has the original vertex buffers, weights, material and bones.
    // Its index buffer omits only the exact triangles of the purchased left pauldron.
    public sealed class FieldPauldronBodySwap : MonoBehaviour
    {
        private SkinnedMeshRenderer _skin;
        private FieldPauldronBodyFit _fit;

        public void Bind(SkinnedMeshRenderer skin, FieldPauldronBodyFit fit)
        {
            _skin = skin;
            _fit = fit;
            Apply(true);
        }

        private void OnEnable() => Apply(true);
        private void OnDisable() => Apply(false);

        private void Apply(bool equipped)
        {
            if (_skin == null || _fit == null) return;
            var expected = equipped ? _fit.Original : _fit.WithoutLeftCap;
            if (_skin.sharedMesh == expected) _skin.sharedMesh = equipped ? _fit.WithoutLeftCap : _fit.Original;
        }
    }
}
