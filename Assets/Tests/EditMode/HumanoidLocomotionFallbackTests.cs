using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Owner report 2026-11-17: Mara and Ilyan stood waist-deep in the village ground. Their private
    /// controller existed but its clips were not imported, so the humanoid pose put the hips at the origin.
    /// </summary>
    public sealed class HumanoidLocomotionFallbackTests
    {
        [Test]
        public void ControllerWithoutAnyClipIsNotPlayable()
        {
            var controller = new AnimatorController();
            try
            {
                controller.AddLayer("Base Layer");
                controller.layers[0].stateMachine.AddState("Idle"); // motion missing, as with unimported clips
                Assert.IsFalse(PassiveArtVisuals.HasPlayableClip(controller));
            }
            finally { Object.DestroyImmediate(controller); }
        }

        [Test]
        public void ControllerWithARealClipIsPlayableAndNullIsNot()
        {
            var controller = new AnimatorController();
            var clip = new AnimationClip { name = "Idle" };
            try
            {
                controller.AddLayer("Base Layer");
                controller.layers[0].stateMachine.AddState("Idle").motion = clip;
                Assert.IsTrue(PassiveArtVisuals.HasPlayableClip(controller));
                Assert.IsFalse(PassiveArtVisuals.HasPlayableClip(null));
            }
            finally { Object.DestroyImmediate(clip); Object.DestroyImmediate(controller); }
        }

        [Test]
        public void GenericRigKeepsItsOwnControllerButStillGetsOneWhenMissing()
        {
            var actor = new GameObject("GenericVillager");
            var controller = new AnimatorController();
            try
            {
                controller.AddLayer("Base Layer");
                controller.layers[0].stateMachine.AddState("Idle");
                var animator = actor.AddComponent<Animator>();
                Assert.IsTrue(PassiveArtVisuals.NeedsSharedLocomotion(animator), "A missing controller is replaced as before");
                animator.runtimeAnimatorController = controller;
                Assert.IsFalse(PassiveArtVisuals.NeedsSharedLocomotion(animator),
                    "A generic rig's own clipless controller must not be swapped for the humanoid one");
                Assert.IsFalse(PassiveArtVisuals.NeedsSharedLocomotion(null));
            }
            finally { Object.DestroyImmediate(controller); Object.DestroyImmediate(actor); }
        }

        [Test]
        public void ActorWithoutHumanoidAnimatorIsLeftUntouched()
        {
            var actor = new GameObject("GenericActor");
            try
            {
                actor.AddComponent<Animator>();
                Assert.AreEqual(0, PassiveArtVisuals.EnsurePlayableHumanoidLocomotion(actor),
                    "Generic rigs must not receive the humanoid controller");
                Assert.AreEqual(0, PassiveArtVisuals.EnsurePlayableHumanoidLocomotion(null));
            }
            finally { Object.DestroyImmediate(actor); }
        }
    }
}
