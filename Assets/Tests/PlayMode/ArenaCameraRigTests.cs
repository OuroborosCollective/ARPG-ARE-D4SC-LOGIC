using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Real Unity camera input boundary; touch samples are explicit test inputs, not device evidence.</summary>
    public sealed class ArenaCameraRigTests
    {
        private GameObject _hero, _camera;
        private ArenaCameraRig _rig;

        [SetUp] public void Setup()
        {
            _hero = new GameObject("Camera test hero");
            _camera = new GameObject("Camera test projection");
            _rig = _camera.AddComponent<ArenaCameraRig>();
            _rig.Configure(_hero.transform, true);
        }

        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(_camera);
            Object.DestroyImmediate(_hero);
        }

        /// <summary>A 200-to-300-unit spread magnifies the hero; touch enumeration order is irrelevant.</summary>
        [Test] public void PinchTracksFingerIdsAndCanResetItsFraming()
        {
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(4, TouchPhase.Began, new Vector2(400f, 200f), true);
            _rig.ObserveTouch(9, TouchPhase.Began, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(11.5f, _rig.Distance);
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(9, TouchPhase.Moved, new Vector2(650f, 200f), true);
            _rig.ObserveTouch(4, TouchPhase.Moved, new Vector2(350f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(7.666667f, _rig.Distance, .001f);
            _rig.ResetZoom();
            Assert.AreEqual(11.5f, _rig.Distance);
        }

        /// <summary>Manual buttons cannot leave the reviewed range, and legacy framing keeps its prior default.</summary>
        [Test] public void StepZoomClampsAndResetsPerProfile()
        {
            _rig.ZoomSteps(1f);
            Assert.AreEqual(9.775f, _rig.Distance, .001f);
            _rig.ZoomSteps(float.NaN);
            _rig.ZoomSteps(float.PositiveInfinity);
            Assert.AreEqual(9.775f, _rig.Distance, .001f);
            _rig.ZoomSteps(100f);
            Assert.AreEqual(7f, _rig.Distance);
            _rig.ZoomSteps(-100f);
            Assert.AreEqual(22f, _rig.Distance);
            _rig.ResetZoom();
            Assert.AreEqual(11.5f, _rig.Distance);
            _rig.Configure(_hero.transform, false);
            Assert.AreEqual(22f, _rig.Distance);
        }

        /// <summary>A finger that started on a HUD or MOVE target is never adopted after dragging into the world.</summary>
        [Test] public void HudStartedTouchCannotBecomeAPinchFinger()
        {
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(1, TouchPhase.Began, new Vector2(400f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Began, new Vector2(600f, 200f), false);
            _rig.EndTouchFrame();
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(1, TouchPhase.Moved, new Vector2(300f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Moved, new Vector2(700f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(11.5f, _rig.Distance);
        }

        /// <summary>Lost, canceled and replacement fingers rebase the gesture rather than reusing stale spans.</summary>
        [Test] public void MissingOrCanceledFingerCannotJumpTheCamera()
        {
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(1, TouchPhase.Began, new Vector2(400f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Began, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(2, TouchPhase.Stationary, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(3, TouchPhase.Began, new Vector2(100f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Stationary, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(11.5f, _rig.Distance);
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(3, TouchPhase.Canceled, new Vector2(100f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Moved, new Vector2(900f, 200f), true);
            _rig.EndTouchFrame();
            _rig.ClearTouches();
            Assert.AreEqual(11.5f, _rig.Distance);
        }

        /// <summary>Coincident fingers do not divide by zero or reuse an invalid pinch baseline.</summary>
        [Test] public void CollapsedPinchRebasesWithoutInvalidDistance()
        {
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(1, TouchPhase.Began, new Vector2(500f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Began, new Vector2(500f, 200f), true);
            _rig.EndTouchFrame();
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(1, TouchPhase.Moved, new Vector2(400f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Moved, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(11.5f, _rig.Distance);
        }

        /// <summary>Zoom uses the same safe-area rectangles for drawing and touch; other HUD regions block pinch starts.</summary>
        [TestCase(720f, 405f)]
        [TestCase(1340f, 800f)]
        [TestCase(1007f, 601f)]
        public void ZoomTargetsFitAndExcludeGameplayTouches(float width, float height)
        {
            var layout = new ArenaHudLayout(width, height, new Rect(12f, 8f, width - 24f, height - 16f));
            for (int i = 0; i < 3; i++)
            {
                Rect target = layout.CameraZoomRect(i);
                Assert.IsFalse(target.Overlaps(layout.StatusPanelRect));
                Assert.IsFalse(target.Overlaps(layout.NpcPanelRect));
                Assert.GreaterOrEqual(target.xMin, 0f);
                Assert.LessOrEqual(target.xMax, layout.Width);
                Assert.GreaterOrEqual(target.yMin, 0f);
                Assert.LessOrEqual(target.yMax, layout.Height);
                Assert.AreEqual((ArenaHudControl)((int)ArenaHudControl.CameraCloser + i),
                    layout.HitTest(target.center, false, true, true, true, true));
                Assert.IsTrue(layout.BlocksCameraGesture(target.center, false, true, true, true, true));
            }
            foreach (Rect panel in new[] { layout.StatusPanelRect, layout.NpcPanelRect, layout.CampPanelRect,
                layout.MoveRect, layout.SkillRect(0), layout.PickupRect, layout.TakeRationRect, layout.MasteryRect(0) })
                Assert.IsTrue(layout.BlocksCameraGesture(panel.center, false, true, true, true, true));
            Assert.IsFalse(layout.BlocksCameraGesture(new Vector2(layout.Width * .55f, 340f), false, false, false, false));
        }
    }
}
