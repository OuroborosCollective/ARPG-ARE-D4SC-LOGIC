using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public enum HeroFraming { Travel, Combat, Idle, Dialogue, Manual }

    /// <summary>
    /// Cosmetic hero framing only; this component never reads or writes Core state.
    /// The entry profile is a close third-person camera behind the hero: one finger on free
    /// world space turns it, two fingers zoom, and it drifts back behind a walking hero.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraRig : MonoBehaviour
    {
        /// <summary>The entry camera looks at the hero's head, this far above the body pivot.</summary>
        public const float HeadHeight = 0.6f;
        private const float YawPerUnit = 0.32f, PitchPerUnit = 0.2f, FollowDegreesPerSecond = 36f;

        private Transform _target;
        private float _defaultDistance, _framingDistance, _pitch, _idleSeconds, _dangerHold;
        private float _yaw, _pitchOffset, _followHold, _pullIn;
        private bool _entryProfile, _manual, _moving, _danger, _speaking;
        private bool _lateralInput;
        private Vector3 _focus, _speakerPosition;
        private Bounds[] _occluders = new Bounds[0];
        public HeroFraming Framing { get; private set; }
        public bool Automatic { get { return !_manual; } }
        /// <summary>Heading the camera looks along, in degrees clockwise from world north (+Z).</summary>
        public float Yaw { get { return _yaw; } }
        /// <summary>The smoothed world point the camera looks at.</summary>
        public Vector3 FocusPoint { get { return _focus; } }
        private float MinimumDistance { get { return _entryProfile ? 2.2f : 7f; } }
        private float MaximumDistance { get { return _entryProfile ? 9f : 22f; } }
        private float RestPitch { get { return _entryProfile ? 12f : 52f; } }
        private Vector3 TargetFocus { get { return _target.position + Vector3.up * (_entryProfile ? HeadHeight : 0f); } }
        private struct Finger
        {
            public int Id;
            public Vector2 Position, Last;
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
            _defaultDistance = entryProfile ? 4.2f : 22f;
            ResetZoom();
            SnapToTarget();
        }

        /// <summary>Restore only camera framing: default zoom, default height, back behind the hero.</summary>
        public void ResetZoom()
        {
            Distance = _defaultDistance; _manual = false; _idleSeconds = 0;
            _pitchOffset = 0; _followHold = 0;
            _yaw = _target != null && _entryProfile ? _target.eulerAngles.y : 0f;
            ClearTouches();
        }

        /// <summary>Positive button or wheel steps move closer; invalid input cannot poison camera transforms.</summary>
        public void ZoomSteps(float steps)
        {
            if (!Finite(steps) || Mathf.Abs(steps) < .001f) return;
            float from = _manual ? Distance : _framingDistance;
            Distance = Mathf.Clamp(from * Mathf.Pow(.85f, Mathf.Clamp(steps, -32f, 32f)), MinimumDistance, MaximumDistance);
            _manual = true;
            ClearTouches();
        }

        /// <summary>Turn the view by a drag in layout units: right looks right, down raises the camera.</summary>
        public void Orbit(Vector2 drag)
        {
            if (!Finite(drag.x) || !Finite(drag.y) || drag.sqrMagnitude < .0001f) return;
            _yaw = Mathf.Repeat(_yaw + Mathf.Clamp(drag.x, -400f, 400f) * YawPerUnit, 360f);
            float before = _pitchOffset;
            _pitchOffset = Mathf.Clamp(_pitchOffset + Mathf.Clamp(drag.y, -400f, 400f) * PitchPerUnit, -25f, 55f);
            _pitch = ClampPitch(_pitch + _pitchOffset - before);
            _followHold = 2.5f;
        }

        /// <summary>Tall scenery the camera must not sit behind; boxes are world-space and presentation-only.</summary>
        public void SetOccluders(Bounds[] boxes) { _occluders = boxes ?? new Bounds[0]; }

        /// <summary>Camera-relative lateral input must not turn its own coordinate frame through follow.</summary>
        public void SetMovementIntent(long lateral) { _lateralInput = lateral != 0; }

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
            _started.Add(new Finger { Id = id, Position = position, Last = position, Seen = true });
        }

        /// <summary>
        /// One captured finger turns the camera; two apply a single pinch ratio after both were
        /// read, independent of input enumeration order.
        /// </summary>
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
            bool first = _first.Id >= 0, second = _second.Id >= 0;
            if (first != second)
            {
                // A lone finger drags the view; the pinch baseline is rebuilt when a partner arrives.
                _hasSpan = false;
                Orbit(first ? _first.Position - _first.Last : _second.Position - _second.Last);
            }
            else if (first)
            {
                float span = Vector2.Distance(_first.Position, _second.Position);
                if (!Finite(span) || span < 8f) _hasSpan = false;
                else
                {
                    if (_hasSpan && Mathf.Abs(span - _previousSpan) > .01f)
                    {
                        float from = _manual ? Distance : _framingDistance;
                        Distance = Mathf.Clamp(from * (_previousSpan / span), MinimumDistance, MaximumDistance);
                        _manual = true;
                    }
                    _previousSpan = span;
                    _hasSpan = true;
                }
            }
            else _hasSpan = false;
            _first.Last = _first.Position;
            _second.Last = _second.Position;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static float ClampPitch(float pitch) { return Mathf.Clamp(pitch, -5f, 70f); }
        private void OnDisable() { ClearTouches(); }

        /// <summary>Read-only context supplied by the existing runtime projection.</summary>
        public void SetContext(bool moving, bool danger, bool speaking, Vector3 speakerPosition)
        {
            _moving = moving; _danger = danger; _speaking = speaking;
            _speakerPosition = speakerPosition;
        }

        /// <summary>Travel clears old cinematic context while preserving a deliberate manual zoom and heading.</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            _moving = _danger = _speaking = false; _idleSeconds = _dangerHold = 0;
            _framingDistance = Distance; _pitch = ClampPitch(RestPitch + _pitchOffset); _focus = TargetFocus;
            Framing = _manual ? HeroFraming.Manual : HeroFraming.Travel;
            _pullIn = _framingDistance - ClearDistance();
            PlaceCamera();
        }

        /// <summary>How far the camera may sit behind the focus before tall scenery would hide the hero.</summary>
        private float ClearDistance()
        {
            float clear = _framingDistance;
            if (_occluders.Length == 0) return clear;
            var ray = new Ray(_focus, Quaternion.Euler(_pitch, _yaw, 0) * Vector3.back);
            foreach (Bounds box in _occluders)
            {
                if (box.Contains(_focus)) continue; // The hero stands inside it, e.g. a doorway or canopy.
                float hit;
                if (box.IntersectRay(ray, out hit) && hit < clear) clear = hit;
            }
            return clear < _framingDistance ? Mathf.Max(clear - .25f, clear * .5f) : clear;
        }

        private void PlaceCamera()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * (_framingDistance - _pullIn), rotation);
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
            float distance = Distance, pitch = RestPitch;
            Vector3 focus = TargetFocus;
            if (_entryProfile)
            {
                // Fights step back and up a little so nearby enemies stay in view.
                if (Framing == HeroFraming.Combat) { distance = 5.4f; pitch = 20f; }
                if (Framing == HeroFraming.Idle) { distance = 3.4f; pitch = 9f; }
                if (Framing == HeroFraming.Dialogue)
                {
                    distance = 3.2f; pitch = 8f;
                    Vector3 offset = _speakerPosition - _target.position; offset.y = 0;
                    if (offset.sqrMagnitude <= 3.5f * 3.5f) focus += Vector3.ClampMagnitude(offset * .25f, .6f);
                }
                // Zooming out by hand also rises towards an overview.
                if (Framing == HeroFraming.Manual)
                    pitch = Mathf.Lerp(8f, 30f, Mathf.InverseLerp(MinimumDistance, MaximumDistance, distance));
                // A walking hero pulls the view back behind them. The pull fades out towards the
                // opposite heading, so walking at the camera does not spin it around.
                _followHold = Mathf.Max(0, _followHold - dt);
                if (_moving && !_lateralInput && _followHold <= 0)
                {
                    float away = Mathf.DeltaAngle(_yaw, _target.eulerAngles.y) * Mathf.Deg2Rad;
                    _yaw = Mathf.Repeat(_yaw + Mathf.Sin(away) * Mathf.Cos(away * .5f) * FollowDegreesPerSecond * dt, 360f);
                }
            }
            else if (Framing == HeroFraming.Manual)
                pitch = Mathf.Lerp(20f, 52f, Mathf.InverseLerp(MinimumDistance, 9f, distance));
            float blend = 1f - Mathf.Exp(-8f * dt);
            _framingDistance = Mathf.Lerp(_framingDistance, distance, blend);
            _pitch = Mathf.Lerp(_pitch, ClampPitch(pitch + _pitchOffset), blend);
            _focus = Vector3.Lerp(_focus, focus, 1f - Mathf.Exp(-10f * dt));
            // Scenery pushes the camera in at once and releases it gently.
            float blocked = _framingDistance - ClearDistance();
            _pullIn = blocked > _pullIn ? blocked : Mathf.Lerp(_pullIn, blocked, 1f - Mathf.Exp(-5f * dt));
            if (_pullIn < .001f) _pullIn = 0;
            PlaceCamera();
        }
    }
}
