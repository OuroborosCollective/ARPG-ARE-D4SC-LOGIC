using System.Collections.Generic;
using AreArpg.UnityClient;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// A phone screen played by a test: one finger on the floating stick, one finger for taps. The game reads it
    /// through <see cref="PlayerDevice"/> exactly as it reads a real touch screen, so every tap still has to land on
    /// a visible button and the stick still has to start inside MOVE. Keys and mouse stay silent.
    /// </summary>
    internal sealed class ScriptedPhone : IPlayerDevice
    {
        private const int StickFinger = 0, TapFinger = 1;
        private readonly List<Touch> _screen = new List<Touch>();
        private readonly Queue<Vector2> _taps = new Queue<Vector2>();
        private bool _stickWanted, _stickDown;
        private Vector2 _stickOrigin, _stickAt;
        private Vector2? _tapDown;

        /// <summary>The stick finger rests on the glass.</summary>
        public bool StickDown { get { return _stickDown; } }
        /// <summary>All taps that reached the screen so far.</summary>
        public int TapCount { get; private set; }

        /// <summary>Push the stick toward a direction on screen (x right, y up; length 1 is full deflection), or let go.</summary>
        public void Stick(ArenaHudLayout layout, Vector2? direction)
        {
            if (!direction.HasValue) { _stickWanted = false; return; }
            if (!_stickWanted && !_stickDown)
                _stickOrigin = layout.ToScreenTouch(layout.MoveRect.center);
            _stickWanted = true;
            Vector2 d = Vector2.ClampMagnitude(direction.Value, 1f) * ArenaHudLayout.StickRadius;
            // Screen y grows upward, GUI y downward: the layout maps the offset back the same way the game reads it.
            _stickAt = _stickOrigin + new Vector2(d.x, d.y) * layout.Scale;
        }

        /// <summary>Tap one point of the logical 960×540 HUD canvas.</summary>
        public void Tap(ArenaHudLayout layout, Vector2 guiPoint) { _taps.Enqueue(layout.ToScreenTouch(guiPoint)); }

        /// <summary>Lift every finger, the way a player puts the phone down.</summary>
        public void LiftAll() { _stickWanted = false; _taps.Clear(); }

        /// <summary>True while a tap is still on its way to the screen.</summary>
        public bool Busy { get { return _taps.Count > 0 || _tapDown.HasValue; } }

        /// <summary>Freeze what the screen reports during the next frame.</summary>
        public void Commit()
        {
            _screen.Clear();
            if (_stickWanted && !_stickDown) { _screen.Add(At(StickFinger, TouchPhase.Began, _stickOrigin)); _stickDown = true; }
            else if (_stickWanted) _screen.Add(At(StickFinger, TouchPhase.Moved, _stickAt));
            else if (_stickDown) { _screen.Add(At(StickFinger, TouchPhase.Ended, _stickAt)); _stickDown = false; }
            // One finger cannot press again in the frame it lifts: a queued tap waits one frame.
            if (_tapDown.HasValue) { _screen.Add(At(TapFinger, TouchPhase.Ended, _tapDown.Value)); _tapDown = null; }
            else if (_taps.Count > 0) { _tapDown = _taps.Dequeue(); _screen.Add(At(TapFinger, TouchPhase.Began, _tapDown.Value)); TapCount++; }
        }

        /// <summary>The game dropped the stick finger (a modal panel, travel): lift it so the next push starts fresh.</summary>
        public void ForgetStick() { _stickWanted = false; }

        private static Touch At(int finger, TouchPhase phase, Vector2 position)
        {
            return new Touch { fingerId = finger, phase = phase, position = position, rawPosition = position, tapCount = 1,
                pressure = 1f, maximumPossiblePressure = 1f, type = TouchType.Direct };
        }

        public bool GetKey(KeyCode key) { return false; }
        public bool GetKeyDown(KeyCode key) { return false; }
        public bool GetMouseButton(int button) { return false; }
        public bool GetMouseButtonDown(int button) { return false; }
        public Vector3 MousePosition { get { return Vector3.zero; } }
        public Vector2 MouseScrollDelta { get { return Vector2.zero; } }
        public int TouchCount { get { return _screen.Count; } }
        public Touch GetTouch(int index) { return _screen[index]; }
    }
}
