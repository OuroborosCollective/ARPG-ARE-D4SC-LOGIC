using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Native Unity HUD layout checks. No gameplay state or test doubles.</summary>
    public sealed class ArenaHudLayoutTests
    {
        /// <summary>The entry action has its own visible touch target and is absent from legacy scenes.</summary>
        [Test] public void EntryRationControlIsExplicitAndDoesNotOverlapMovement()
        {
            var layout = FullView(720f, 405f);
            Fits(layout.TakeRationRect, layout, "ration theft");
            Disjoint(layout.TakeRationRect, layout.MoveRect, "theft/movement");
            Disjoint(layout.TakeRationRect, layout.DonationRect, "theft/donation");
            Assert.AreEqual(ArenaHudControl.TakeRation, layout.HitTest(layout.TakeRationRect.center, false, true, true, true, true));
            Assert.AreEqual(ArenaHudControl.None, layout.HitTest(layout.TakeRationRect.center, false, true, true, true));
        }
        /// <summary>Construct a viewport without insets for consistent geometry assertions.</summary>
        private static ArenaHudLayout FullView(float width, float height)
        {
            return new ArenaHudLayout(width, height, new Rect(0f, 0f, width, height));
        }

        /// <summary>Require a visible control to remain inside the safe viewport.</summary>
        private static void Fits(Rect rect, ArenaHudLayout layout, string name)
        {
            Assert.GreaterOrEqual(rect.xMin, 0f, name + " is left of the safe viewport");
            Assert.GreaterOrEqual(rect.yMin, 0f, name + " is above the safe viewport");
            Assert.LessOrEqual(rect.xMax, layout.Width + 0.01f, name + " exceeds viewport width");
            Assert.LessOrEqual(rect.yMax, layout.Height + 0.01f, name + " exceeds viewport height");
        }

        /// <summary>Reject overlapping interactive control rectangles.</summary>
        private static void Disjoint(Rect a, Rect b, string description)
        {
            Assert.IsFalse(a.Overlaps(b), description);
        }

        /// <summary>Check the shared layout across status, movement, NPC and skill panels.</summary>
        private static void ValidateGameView(ArenaHudLayout layout)
        {
            Fits(layout.StatusPanelRect, layout, "status");
            Fits(layout.CampPanelRect, layout, "camp");
            Fits(layout.NpcPanelRect, layout, "NPC");
            Fits(layout.DonationRect, layout, "donation");
            Fits(layout.MoveRect, layout, "movement");
            Fits(layout.PickupRect, layout, "pickup");

            Disjoint(layout.StatusPanelRect, layout.CampPanelRect, "status/camp");
            Disjoint(layout.StatusPanelRect, layout.NpcPanelRect, "status/NPC");
            Disjoint(layout.CampPanelRect, layout.NpcPanelRect, "camp/NPC");
            Disjoint(layout.MoveRect, layout.CampPanelRect, "movement/camp");
            Disjoint(layout.MoveRect, layout.PickupRect, "movement/pickup");

            for (int i = 0; i < 3; i++)
            {
                Rect mastery = layout.MasteryRect(i);
                Fits(mastery, layout, "mastery " + i);
                Disjoint(mastery, layout.CampPanelRect, "mastery must never cover the camp");
                Disjoint(mastery, layout.NpcPanelRect, "mastery must never cover the NPC");
                for (int j = 0; j < i; j++)
                    Disjoint(mastery, layout.MasteryRect(j), "mastery controls must be disjoint");
            }

            for (int i = 0; i < 4; i++)
            {
                Rect skill = layout.SkillRect(i);
                Rect npc = layout.NpcActionRect(i);
                Fits(skill, layout, "skill " + i);
                Fits(npc, layout, "NPC action " + i);
                Disjoint(skill, layout.NpcPanelRect, "skill must never cover NPC panel");
                Disjoint(skill, layout.CampPanelRect, "skill must never cover camp");
                for (int j = 0; j < i; j++)
                {
                    Disjoint(skill, layout.SkillRect(j), "skill controls must be disjoint");
                    Disjoint(npc, layout.NpcActionRect(j), "NPC actions must be disjoint");
                }
            }
        }

        /// <summary>Verify scaling in a compact editor Game View.</summary>
        [Test] public void HudFitsCompactGameView_720x405()
        {
            var layout = FullView(720f, 405f);
            Assert.AreEqual(0.75f, layout.Scale, 0.0001f);
            ValidateGameView(layout);
        }

        /// <summary>Verify the baseline landscape viewport.</summary>
        [Test] public void HudFitsDesktop_960x540()
        {
            ValidateGameView(FullView(960f, 540f));
        }

        /// <summary>Verify the known tablet viewport dimensions.</summary>
        [Test] public void HudFitsTablet_1007x601()
        {
            ValidateGameView(FullView(1007f, 601f));
        }

        /// <summary>Require drawing and touch coordinates to use the same safe-area transform.</summary>
        [Test] public void SafeAreaInsetsAreAppliedToVisualsAndTouch()
        {
            var layout = new ArenaHudLayout(1007f, 601f, new Rect(23f, 16f, 964f, 573f));
            Assert.AreEqual(1f, layout.Scale, 0.0001f);
            Assert.AreEqual(964f, layout.Width, 0.01f);
            ValidateGameView(layout);
            Vector2 center = layout.SkillRect(0).center;
            Vector2 screen = layout.ToScreenTouch(center);
            Assert.IsTrue(layout.IsWithinSafeArea(screen));
            Assert.Less(Vector2.Distance(center, layout.ToGuiTouch(screen)), 0.01f);
            Assert.IsFalse(layout.IsWithinSafeArea(new Vector2(5f, 5f)));
        }

        /// <summary>Round-trip physical touch coordinates to each displayed action.</summary>
        [Test] public void TouchHitTestMatchesRenderedButton()
        {
            var layout = FullView(720f, 405f);
            ArenaHudControl[] controls =
            {
                ArenaHudControl.NpcGreet, ArenaHudControl.NpcHelp,
                ArenaHudControl.NpcAttack, ArenaHudControl.NpcRob,
                ArenaHudControl.Skill0, ArenaHudControl.Skill1,
                ArenaHudControl.Skill2, ArenaHudControl.Skill3,
                ArenaHudControl.MasteryMight, ArenaHudControl.MasteryVitality,
                ArenaHudControl.MasteryResolve, ArenaHudControl.Donate,
                ArenaHudControl.Pickup, ArenaHudControl.Move
            };
            Rect[] rectangles =
            {
                layout.NpcActionRect(0), layout.NpcActionRect(1),
                layout.NpcActionRect(2), layout.NpcActionRect(3),
                layout.SkillRect(0), layout.SkillRect(1),
                layout.SkillRect(2), layout.SkillRect(3),
                layout.MasteryRect(0), layout.MasteryRect(1),
                layout.MasteryRect(2), layout.DonationRect,
                layout.PickupRect, layout.MoveRect
            };
            for (int i = 0; i < controls.Length; i++)
            {
                Vector2 touched = layout.ToScreenTouch(rectangles[i].center);
                Vector2 projected = layout.ToGuiTouch(touched);
                Assert.Less(Vector2.Distance(rectangles[i].center, projected), 0.01f);
                Assert.AreEqual(controls[i], layout.HitTest(projected, false, true, true, true),
                    "Visual button and real touch must choose the same action " + controls[i]);
            }
        }

        /// <summary>Prevent gameplay controls from accepting touches after defeat.</summary>
        [Test] public void GameOverHidesGameplayActions()
        {
            var layout = FullView(720f, 405f);
            Assert.AreEqual(ArenaHudControl.None,
                layout.HitTest(layout.NpcActionRect(2).center, true, true, true, true));
            Assert.AreEqual(ArenaHudControl.None,
                layout.HitTest(layout.SkillRect(0).center, true, true, true, true));
            Assert.AreEqual(ArenaHudControl.Restart,
                layout.HitTest(layout.RestartRect.center, true, true, true, true));
            Assert.IsFalse(ArenaHudLayout.ShowGameplayControls(true));
            Assert.IsTrue(ArenaHudLayout.ShowGameplayControls(false));
        }

        /// <summary>Retain movement ownership until the original finger ends or cancels.</summary>
        [Test] public void CapturedMoveContinuesOutsideVisibleRectUntilRelease()
        {
            ArenaHudLayout layout = FullView(960f, 540f);
            var move = new ArenaHudTouchCapture();
            Vector2 direction;
            Assert.IsTrue(move.TryMove(17, TouchPhase.Began, layout.MoveRect.center,
                layout, ArenaHudControl.Move, out direction));
            Assert.AreEqual(0f, direction.magnitude, 0.00001f,
                "A finger starting at the visual center must produce no movement.");

            Vector2 outside = new Vector2(layout.MoveRect.xMax + 85f, layout.MoveRect.center.y);
            Assert.AreEqual(ArenaHudControl.None,
                layout.HitTest(outside, false, false, false, false));
            Assert.IsTrue(move.TryMove(17, TouchPhase.Moved, outside,
                layout, ArenaHudControl.None, out direction),
                "A captured finger must keep driving movement outside MOVE.");
            Assert.AreEqual(1f, direction.x, 0.0001f,
                "Dragging outside the box must clamp to the joystick maximum.");
            Assert.AreEqual(0f, direction.y, 0.0001f);
            Assert.IsTrue(move.IsCaptured);

            Assert.IsFalse(move.TryMove(17, TouchPhase.Ended, outside,
                layout, ArenaHudControl.None, out direction));
            Assert.IsFalse(move.IsCaptured);
            Assert.IsFalse(move.TryMove(17, TouchPhase.Moved, outside,
                layout, ArenaHudControl.None, out direction),
                "Once ended, movement must not resume without a new MOVE start.");
        }

        [Test] public void MoveCaptureIgnoresSecondFingerAndCanceledTouchReleases()
        {
            ArenaHudLayout layout = FullView(1007f, 601f);
            var move = new ArenaHudTouchCapture();
            Vector2 direction;
            Assert.IsFalse(move.TryMove(14, TouchPhase.Began, layout.NpcActionRect(0).center,
                layout, ArenaHudControl.NpcGreet, out direction));
            Assert.IsFalse(move.IsCaptured);
            Assert.IsTrue(move.TryMove(4, TouchPhase.Began, layout.MoveRect.center,
                layout, ArenaHudControl.Move, out direction));
            Assert.IsFalse(move.TryMove(6, TouchPhase.Began, layout.MoveRect.center,
                layout, ArenaHudControl.Move, out direction));
            Assert.IsFalse(move.TryMove(6, TouchPhase.Moved, layout.MoveRect.center,
                layout, ArenaHudControl.None, out direction));
            Assert.IsTrue(move.TryMove(4, TouchPhase.Stationary, layout.MoveRect.center,
                layout, ArenaHudControl.None, out direction));
            Assert.IsFalse(move.TryMove(4, TouchPhase.Canceled, layout.MoveRect.center,
                layout, ArenaHudControl.None, out direction));
            Assert.IsFalse(move.IsCaptured);
            Assert.IsFalse(move.TryMove(4, TouchPhase.Stationary, layout.MoveRect.center,
                layout, ArenaHudControl.None, out direction));
        }

        [Test] public void MobileDefeatInstructionsMatchTouchRestartAction()
        {
            Assert.AreEqual("DEFEATED\nTap RESTART", ArenaRuntime.DefeatPrompt(true));
            Assert.AreEqual("DEFEATED\nPress R to restart", ArenaRuntime.DefeatPrompt(false));
        }

    }
}
