using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Cosmetic hero framing only; this component never reads or writes Core state.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraRig : MonoBehaviour
    {
        private Transform _target;
        private readonly Quaternion _rotation = Quaternion.Euler(52f, 0f, 0f);
        public float Distance { get; private set; }

        /// <summary>Choose the profile's framing and immediately place a newly created camera.</summary>
        public void Configure(Transform target, bool entryProfile)
        {
            _target = target;
            Distance = entryProfile ? 11.5f : 22f;
            SnapToTarget();
        }

        /// <summary>Follow a canonical region transition without an old-distance camera jump.</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            transform.SetPositionAndRotation(_target.position - _rotation * Vector3.forward * Distance, _rotation);
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            Vector3 desired = _target.position - _rotation * Vector3.forward * Distance;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.rotation = _rotation;
        }
    }
}
