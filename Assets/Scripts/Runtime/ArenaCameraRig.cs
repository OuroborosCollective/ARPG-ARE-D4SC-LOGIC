using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public enum HeroFraming { Travel, Combat, Idle, Dialogue, Manual }

    /// <summary>Cosmetic hero framing only; this component never reads or writes Core state.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraRig : MonoBehaviour
    {
        private Transform _target;
        private float _defaultDistance, _framingDistance, _pitch, _idleSeconds, _dangerHold;
        private bool _entryProfile, _manual, _moving, _danger, _speaking;
        private Vector3 _focus, _speakerPosition;
        public HeroFraming Framing { get; private set; }
        public bool Automatic { get { return !_manual; } }
        private float MinimumDistance { get { return _entryProfile ? 2.8f : 7f; } }
        private struct Finger
        {
            public int Id;
            public Vector2 Position;
            public bool Seen;
        }
        private Finger _first = new Finger { Id = -1 };
        private Finger _second = new Finger { Id = -1 };
        private readonly List<Finger> _started = new List<Finger>(4);
        private float _previousSpan;
        private bool _hasSpan;
        public float Distance { get; private set; }

        /// <summary>Choose the profile's framing and immediately place a newly created camera.</summary>
        public void Configure(Transform target, bool entryProfile)
        {
            _target = target;
            _entryProfile = entryProfile;
            _defaultDistance = entryProfile ? 6.5f : 22f;
            ResetZoom();
            SnapToTarget();
        }

        /// <summary>Restore only camera framing; no character data is involved.</summary>
        public void ResetZoom()
        {
            Distance = _defaultDistance; _manual = false; _idleSeconds = 0;
            ClearTouches();
        }

        /// <summary>Positive button or wheel steps move closer; invalid input cannot poison camera transforms.</summary>
        public void ZoomSteps(float steps)
        {
            if (!Finite(steps) || Mathf.Abs(steps) < .001f) return;
            float from = _manual ? Distance : _framingDistance;
            Distance = Mathf.Clamp(from * Mathf.Pow(.85f, Mathf.Clamp(steps, -32f, 32f)), MinimumDistance, 22f);
            _manual = true;
            ClearTouches();
        }

        /// <summary>Release gestures after a pause, focus loss or region transition, retaining chosen zoom.</summary>
        public void ClearTouches()
        {
            _started.Clear();
            _first.Id = _second.Id = -1;
            _first.Seen = _second.Seen = false;
            _hasSpan = false;
        }

        /// <summary>Every held finger must be observed again; disappearing OS touches cannot remain captured.</summary>
        public void BeginTouchFrame()
        {
            _started.Clear();
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
            // Selection waits until releases and absent contacts have freed their slots.
            _started.Add(new Finger { Id = id, Position = position, Seen = true });
        }

        /// <summary>Apply one pinch ratio after reading both fingers, independent of input enumeration order.</summary>
        public void EndTouchFrame()
        {
            if (!_first.Seen) { _first.Id = -1; _hasSpan = false; }
            if (!_second.Seen) { _second.Id = -1; _hasSpan = false; }
            foreach (Finger finger in _started)
            {
                if (finger.Id == _first.Id || finger.Id == _second.Id) continue;
                if (_first.Id < 0) _first = finger;
                else if (_second.Id < 0) _second = finger;
                else break;
                _hasSpan = false;
            }
            _started.Clear();
            if (_first.Id < 0 || _second.Id < 0) { _hasSpan = false; return; }
            float span = Vector2.Distance(_first.Position, _second.Position);
            if (!Finite(span) || span < 8f) { _hasSpan = false; return; }
            if (_hasSpan && Mathf.Abs(span - _previousSpan) > .01f)
            {
                float from = _manual ? Distance : _framingDistance;
                Distance = Mathf.Clamp(from * (_previousSpan / span), MinimumDistance, 22f);
                _manual = true;
            }
            _previousSpan = span;
            _hasSpan = true;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private void OnDisable() { ClearTouches(); }

        /// <summary>Read-only context supplied by the existing runtime projection.</summary>
        public void SetContext(bool moving, bool danger, bool speaking, Vector3 speakerPosition)
        {
            _moving = moving; _danger = danger; _speaking = speaking;
            _speakerPosition = speakerPosition;
        }

        /// <summary>Travel clears old cinematic context while preserving a deliberate manual zoom.</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            _moving = _danger = _speaking = false; _idleSeconds = _dangerHold = 0;
            _framingDistance = Distance; _pitch = _entryProfile ? 38f : 52f; _focus = _target.position;
            Framing = _manual ? HeroFraming.Manual : HeroFraming.Travel;
            PlaceCamera();
        }

        private void PlaceCamera()
        {
            var rotation = Quaternion.Euler(_pitch, 0, 0);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _framingDistance, rotation);
        }

        private void LateUpdate() { AdvancePresentation(Time.deltaTime); }

        /// <summary>Advance cosmetic interpolation only; explicit delta also permits bounded native checks.</summary>
        public void AdvancePresentation(float seconds)
        {
            if (_target == null || !Finite(seconds) || seconds <= 0) return;
            float dt = Mathf.Min(seconds, .1f);
            _dangerHold = _danger ? 1.5f : Mathf.Max(0, _dangerHold - dt);
            _idleSeconds = _moving || _dangerHold > 0 || _speaking ? 0 : _idleSeconds + dt;
            Framing = _manual ? HeroFraming.Manual : !_entryProfile ? HeroFraming.Travel :
                _dangerHold > 0 ? HeroFraming.Combat : _moving ? HeroFraming.Travel :
                _speaking ? HeroFraming.Dialogue : _idleSeconds >= 1.1f ? HeroFraming.Idle : HeroFraming.Travel;
            float distance = Distance, pitch = _entryProfile ? 38f : 52f;
            Vector3 focus = _target.position;
            if (Framing == HeroFraming.Combat) { distance = 9f; pitch = 48f; }
            if (Framing == HeroFraming.Idle) { distance = 3f; pitch = 20f; }
            if (Framing == HeroFraming.Dialogue)
            {
                distance = 3.5f; pitch = 18f;
                Vector3 offset = _speakerPosition - focus; offset.y = 0;
                if (offset.sqrMagnitude <= 3.5f * 3.5f) focus += Vector3.ClampMagnitude(offset * .25f, .6f);
            }
            if (Framing == HeroFraming.Manual) pitch = Mathf.Lerp(20f, 52f, Mathf.InverseLerp(MinimumDistance, 9f, distance));
            float blend = 1f - Mathf.Exp(-8f * dt);
            _framingDistance = Mathf.Lerp(_framingDistance, distance, blend);
            _pitch = Mathf.Lerp(_pitch, pitch, blend);
            _focus = Vector3.Lerp(_focus, focus, 1f - Mathf.Exp(-10f * dt));
            PlaceCamera();
        }
    }
}
