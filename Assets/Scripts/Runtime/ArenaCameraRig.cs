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
        private float _defaultDistance;
        private struct Finger
        {
            public int Id;
            public Vector2 Position;
            public bool Seen;
        }
        private Finger _first = new Finger { Id = -1 };
        private Finger _second = new Finger { Id = -1 };
        private float _previousSpan;
        private bool _hasSpan;
        public float Distance { get; private set; }

        /// <summary>Choose the profile's framing and immediately place a newly created camera.</summary>
        public void Configure(Transform target, bool entryProfile)
        {
            _target = target;
            _defaultDistance = entryProfile ? 11.5f : 22f;
            ResetZoom();
            SnapToTarget();
        }

        /// <summary>Restore only camera framing; no character data is involved.</summary>
        public void ResetZoom()
        {
            Distance = _defaultDistance;
            ClearTouches();
        }

        /// <summary>Positive button or wheel steps move closer; invalid input cannot poison camera transforms.</summary>
        public void ZoomSteps(float steps)
        {
            if (!Finite(steps)) return;
            Distance = Mathf.Clamp(Distance * Mathf.Pow(.85f, Mathf.Clamp(steps, -32f, 32f)), 7f, 22f);
            ClearTouches();
        }

        /// <summary>Release gestures after a pause, focus loss or region transition, retaining chosen zoom.</summary>
        public void ClearTouches()
        {
            _first.Id = _second.Id = -1;
            _first.Seen = _second.Seen = false;
            _hasSpan = false;
        }

        /// <summary>Every held finger must be observed again; disappearing OS touches cannot remain captured.</summary>
        public void BeginTouchFrame()
        {
            _first.Seen = _second.Seen = false;
        }

        /// <summary>Capture only a new touch that began on free world space, keyed by stable finger ID.</summary>
        public void ObserveTouch(int id, TouchPhase phase, Vector2 position, bool canStart)
        {
            if (id < 0 || !Finite(position.x) || !Finite(position.y)) return;
            if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
            {
                if (_first.Id == id) { _first.Id = -1; _hasSpan = false; }
                if (_second.Id == id) { _second.Id = -1; _hasSpan = false; }
                return;
            }
            if (_first.Id == id) { _first.Position = position; _first.Seen = true; return; }
            if (_second.Id == id) { _second.Position = position; _second.Seen = true; return; }
            if (phase != TouchPhase.Began || !canStart) return;
            if (_first.Id < 0) _first = new Finger { Id = id, Position = position, Seen = true };
            else if (_second.Id < 0) _second = new Finger { Id = id, Position = position, Seen = true };
            else return;
            _hasSpan = false;
        }

        /// <summary>Apply one pinch ratio after reading both fingers, independent of input enumeration order.</summary>
        public void EndTouchFrame()
        {
            if (!_first.Seen) { _first.Id = -1; _hasSpan = false; }
            if (!_second.Seen) { _second.Id = -1; _hasSpan = false; }
            if (_first.Id < 0 || _second.Id < 0) { _hasSpan = false; return; }
            float span = Vector2.Distance(_first.Position, _second.Position);
            if (!Finite(span) || span < 8f) { _hasSpan = false; return; }
            if (_hasSpan) Distance = Mathf.Clamp(Distance * (_previousSpan / span), 7f, 22f);
            _previousSpan = span;
            _hasSpan = true;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private void OnDisable() { ClearTouches(); }

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
