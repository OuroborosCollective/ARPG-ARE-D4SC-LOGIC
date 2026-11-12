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

        [Test] public void ContextFramesIdleMotionAndDangerWithoutOverridingManualZoom()
        {
            _rig.SetContext(false, false, false, Vector3.zero);
            for (int i = 0; i < 35; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(HeroFraming.Idle, _rig.Framing);
            Assert.AreEqual(3.4f, Vector3.Distance(_camera.transform.position, _rig.FocusPoint), .01f);
            _rig.SetContext(false, true, true, Vector3.right);
            for (int i = 0; i < 15; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(HeroFraming.Combat, _rig.Framing, "Danger takes priority over dialogue and stillness.");
            Assert.AreEqual(5.4f, Vector3.Distance(_camera.transform.position, _rig.FocusPoint), .01f);
            _rig.ZoomSteps(1); Assert.AreEqual(4.59f, _rig.Distance, .01f, "Manual input starts from the displayed framing.");
            _rig.SetContext(false, false, true, Vector3.right);
            for (int i = 0; i < 35; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(HeroFraming.Manual, _rig.Framing); Assert.IsFalse(_rig.Automatic);
            _rig.SnapToTarget(); Assert.AreEqual(4.59f, _rig.Distance, .01f);
            _rig.ResetZoom(); _rig.SetContext(true, false, true, Vector3.right);
            for (int i = 0; i < 15; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(HeroFraming.Travel, _rig.Framing, "Walking exits the cinematic even while speech continues.");
            Assert.AreEqual(4.2f, Vector3.Distance(_camera.transform.position, _rig.FocusPoint), .01f);
        }

        /// <summary>The entry camera stands low behind the hero and looks at the head, not down from above.</summary>
        [Test] public void EntryFramingIsACloseViewFromBehindTheHero()
        {
            _hero.transform.position = new Vector3(3f, 1.05f, -2f);
            _rig.Configure(_hero.transform, true);
            Vector3 head = _hero.transform.position + Vector3.up * ArenaCameraRig.HeadHeight;
            Assert.AreEqual(0f, Vector3.Distance(head, _rig.FocusPoint), .001f);
            Assert.AreEqual(4.2f, Vector3.Distance(_camera.transform.position, head), .001f);
            Assert.AreEqual(12f, _camera.transform.eulerAngles.x, .01f);
            Assert.Less(_camera.transform.position.z, _hero.transform.position.z - 4f, "The camera starts behind a north-facing hero.");
            Assert.AreEqual(_hero.transform.position.x, _camera.transform.position.x, .001f);
            Assert.Less(_camera.transform.position.y, 3f, "A third-person camera stays near head height.");
        }

        /// <summary>One finger on free world space turns and tilts the view without touching the zoom.</summary>
        [Test] public void OneFingerDragTurnsAndTiltsTheView()
        {
            _rig.SetContext(true, false, false, Vector3.zero);
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(5, TouchPhase.Began, new Vector2(400f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(0f, _rig.Yaw, "Touching down alone must not move the view.");
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(5, TouchPhase.Moved, new Vector2(500f, 250f), true);
            _rig.EndTouchFrame();
            _rig.AdvancePresentation(.02f);
            Assert.AreEqual(32f, _rig.Yaw, .01f, "Dragging right looks right.");
            Assert.AreEqual(22f, _camera.transform.eulerAngles.x, .5f, "Dragging down raises the camera at once.");
            Assert.AreEqual(4.2f, _rig.Distance); Assert.IsTrue(_rig.Automatic);
            Vector3 forward = _camera.transform.forward; forward.y = 0f; forward.Normalize();
            Assert.AreEqual(Mathf.Sin(32f * Mathf.Deg2Rad), forward.x, .001f);
            Assert.AreEqual(Mathf.Cos(32f * Mathf.Deg2Rad), forward.z, .001f);
            Assert.AreEqual(4.2f, Vector3.Distance(_camera.transform.position, _rig.FocusPoint), .01f);
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(5, TouchPhase.Moved, new Vector2(500f, 5000f), true);
            _rig.EndTouchFrame();
            for (int i = 0; i < 20; i++) _rig.AdvancePresentation(.1f);
            Assert.Greater(_camera.transform.eulerAngles.x, 60f);
            Assert.LessOrEqual(_camera.transform.eulerAngles.x, 70.01f, "The view cannot flip over the hero.");
            _rig.ResetZoom();
            for (int i = 0; i < 20; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(0f, _rig.Yaw, .001f); Assert.AreEqual(12f, _camera.transform.eulerAngles.x, .05f);
        }

        /// <summary>A finger that began on the HUD or the move stick never turns the view.</summary>
        [Test] public void HudStartedTouchCannotTurnTheView()
        {
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(2, TouchPhase.Began, new Vector2(100f, 400f), false);
            _rig.EndTouchFrame();
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(2, TouchPhase.Moved, new Vector2(600f, 100f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(0f, _rig.Yaw);
            _rig.Orbit(new Vector2(float.NaN, 10f)); _rig.Orbit(new Vector2(10f, float.PositiveInfinity));
            Assert.AreEqual(0f, _rig.Yaw, "Invalid drags cannot poison the camera.");
        }

        /// <summary>Walking sideways swings the view behind the hero; a manual turn is respected for a moment.</summary>
        [Test] public void WalkingHeroPullsTheViewBehindThem()
        {
            _hero.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            _rig.SetContext(true, false, false, Vector3.zero);
            for (int i = 0; i < 50; i++) _rig.AdvancePresentation(.1f);
            Assert.Greater(_rig.Yaw, 78f); Assert.Less(_rig.Yaw, 90.01f);
            _rig.ResetZoom(); Assert.AreEqual(90f, _rig.Yaw, .001f, "Reset places the camera behind the hero.");
            _hero.transform.rotation = Quaternion.Euler(0f, 260f, 0f);
            for (int i = 0; i < 30; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(90f, _rig.Yaw, 2f, "A hero walking towards the camera must not spin it around.");
            _rig.ResetZoom(); _hero.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _rig.Orbit(new Vector2(-100f, 0f));
            float chosen = _rig.Yaw;
            Assert.AreEqual(228f, chosen, .01f);
            for (int i = 0; i < 20; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(chosen, _rig.Yaw, "A fresh manual turn holds against the follow drift.");
            for (int i = 0; i < 30; i++) _rig.AdvancePresentation(.1f);
            Assert.Less(_rig.Yaw, chosen - 10f, "Afterwards the view follows the walking hero again.");
            _rig.SetContext(false, false, false, Vector3.zero);
            float resting = _rig.Yaw;
            for (int i = 0; i < 20; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(resting, _rig.Yaw, "A standing hero never turns the camera.");
        }

        /// <summary>Tall scenery between camera and hero pushes the camera in front of it; standing inside is ignored.</summary>
        [Test] public void SceneryBehindTheHeroPullsTheCameraIn()
        {
            _rig.SetContext(true, false, false, Vector3.zero);
            _rig.SetOccluders(new[] { new Bounds(new Vector3(0f, 1f, -2.5f), new Vector3(2f, 6f, 1f)) });
            _rig.AdvancePresentation(.02f);
            float blocked = Vector3.Distance(_camera.transform.position, _rig.FocusPoint);
            Assert.AreEqual(2f / Mathf.Cos(12f * Mathf.Deg2Rad) - .25f, blocked, .02f, "The camera stops just in front of the wall.");
            Assert.Greater(_camera.transform.position.z, -2f);
            Assert.AreEqual(4.2f, _rig.Distance, "Scenery never rewrites the chosen zoom.");
            _rig.SetOccluders(new[] { new Bounds(new Vector3(0f, 1f, 0f), new Vector3(3f, 6f, 9f)) });
            _rig.AdvancePresentation(.1f);
            float easing = Vector3.Distance(_camera.transform.position, _rig.FocusPoint);
            Assert.Greater(easing, blocked); Assert.Less(easing, 4f, "The camera eases back out instead of jumping.");
            for (int i = 0; i < 40; i++) _rig.AdvancePresentation(.1f);
            Assert.AreEqual(4.2f, Vector3.Distance(_camera.transform.position, _rig.FocusPoint), .01f,
                "Scenery around the hero's own head is ignored.");
            _rig.SetOccluders(new[] { new Bounds(new Vector3(0f, 1f, -2.5f), new Vector3(2f, 6f, 1f)) });
            _rig.SnapToTarget();
            Assert.AreEqual(blocked, Vector3.Distance(_camera.transform.position, _rig.FocusPoint), .02f,
                "A region change places the camera clear of scenery at once.");
            _rig.SetOccluders(null);
            _rig.SnapToTarget();
            Assert.AreEqual(4.2f, Vector3.Distance(_camera.transform.position, _rig.FocusPoint), .01f);
        }

        /// <summary>A 200-to-300-unit spread magnifies the hero; touch enumeration order is irrelevant.</summary>
        [Test] public void PinchTracksFingerIdsAndCanResetItsFraming()
        {
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(4, TouchPhase.Began, new Vector2(400f, 200f), true);
            _rig.ObserveTouch(9, TouchPhase.Began, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(4.2f, _rig.Distance);
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(9, TouchPhase.Moved, new Vector2(650f, 200f), true);
            _rig.ObserveTouch(4, TouchPhase.Moved, new Vector2(350f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(2.8f, _rig.Distance, .001f);
            _rig.ResetZoom();
            Assert.AreEqual(4.2f, _rig.Distance);
        }

        /// <summary>Manual buttons cannot leave the reviewed range, and legacy framing keeps its prior default.</summary>
        [Test] public void StepZoomClampsAndResetsPerProfile()
        {
            _rig.ZoomSteps(1f);
            Assert.AreEqual(3.57f, _rig.Distance, .001f);
            _rig.ZoomSteps(float.NaN);
            _rig.ZoomSteps(float.PositiveInfinity);
            Assert.AreEqual(3.57f, _rig.Distance, .001f);
            _rig.ZoomSteps(100f);
            Assert.AreEqual(2.2f, _rig.Distance);
            _rig.ZoomSteps(-100f);
            Assert.AreEqual(9f, _rig.Distance, "The entry camera never returns to the far overview.");
            _rig.ResetZoom();
            Assert.AreEqual(4.2f, _rig.Distance);
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
            Assert.AreEqual(4.2f, _rig.Distance);
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
            Assert.AreEqual(4.2f, _rig.Distance);
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(3, TouchPhase.Canceled, new Vector2(100f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Moved, new Vector2(900f, 200f), true);
            _rig.EndTouchFrame();
            _rig.ClearTouches();
            Assert.AreEqual(4.2f, _rig.Distance);
        }

        /// <summary>A replacement starts after release processing regardless of the OS contact order.</summary>
        [TestCase(true, TouchPhase.Ended)]
        [TestCase(false, TouchPhase.Ended)]
        [TestCase(true, TouchPhase.Canceled)]
        [TestCase(false, TouchPhase.Canceled)]
        public void ReplacementFingerCanContinuePinching(bool replacementFirst, TouchPhase release)
        {
            CheckReplacementFinger(replacementFirst, release, true);
        }

        /// <summary>An omitted OS contact also frees its slot before new contacts are selected.</summary>
        [Test] public void ReplacementFingerCanReplaceAMissingContact()
        {
            CheckReplacementFinger(true, TouchPhase.Ended, false);
        }

        /// <summary>Exercise a live camera component over an initial, replacement and spread frame.</summary>
        private void CheckReplacementFinger(bool replacementFirst, TouchPhase release, bool reportRelease)
        {
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(1, TouchPhase.Began, new Vector2(400f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Began, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            _rig.BeginTouchFrame();
            if (replacementFirst) _rig.ObserveTouch(3, TouchPhase.Began, new Vector2(300f, 200f), true);
            if (reportRelease) _rig.ObserveTouch(1, release, new Vector2(400f, 200f), true);
            if (!replacementFirst) _rig.ObserveTouch(3, TouchPhase.Began, new Vector2(300f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Stationary, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(4.2f, _rig.Distance, "Replacement must establish a fresh span without jumping.");
            _rig.BeginTouchFrame();
            _rig.ObserveTouch(3, TouchPhase.Moved, new Vector2(150f, 200f), true);
            _rig.ObserveTouch(2, TouchPhase.Stationary, new Vector2(600f, 200f), true);
            _rig.EndTouchFrame();
            Assert.AreEqual(2.8f, _rig.Distance, .001f, "Replacement must be adopted on its Began frame.");
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
            Assert.AreEqual(4.2f, _rig.Distance);
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
