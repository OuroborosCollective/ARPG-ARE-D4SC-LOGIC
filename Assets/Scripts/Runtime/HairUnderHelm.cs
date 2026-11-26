using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Keeps the hero's hair hidden under a worn helm (AschenwachtGearPresentation) by holding his hair bones at a tiny
    /// size. His animations may write those bones every frame, so the size is set again after the Animator has run;
    /// releasing the hair gives it back its own size.
    /// </summary>
    public sealed class HairUnderHelm : MonoBehaviour
    {
        private readonly List<Transform> _bones = new List<Transform>();
        private readonly List<Vector3> _sizes = new List<Vector3>();
        private float _factor = 1f;

        public int Count => _bones.Count;

        public void Tuck(Transform bone, float factor)
        {
            if (!bone || _bones.Contains(bone)) return;
            _bones.Add(bone);
            _sizes.Add(bone.localScale);
            _factor = factor;
            bone.localScale *= factor;
        }

        public void Release()
        {
            for (int i = 0; i < _bones.Count; i++) if (_bones[i]) _bones[i].localScale = _sizes[i];
            _bones.Clear();
            _sizes.Clear();
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _bones.Count; i++) if (_bones[i]) _bones[i].localScale = _sizes[i] * _factor;
        }

        private void OnDestroy() { Release(); }
    }
}
