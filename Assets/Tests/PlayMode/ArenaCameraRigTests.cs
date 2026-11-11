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
    }
}
