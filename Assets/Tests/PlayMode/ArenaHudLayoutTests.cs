using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Native Unity HUD layout checks. No gameplay state or test doubles.</summary>
    public sealed class ArenaHudLayoutTests
    {
        private static ArenaHudLayout FullView(float width, float height)
        {
            return new ArenaHudLayout(width, height, new Rect(0f, 0f, width, height));
        }

        private static void Fits(Rect rect, ArenaHudLayout layout, string name)
        {
            Assert.GreaterOrEqual(rect.xMin, 0f, name + " is left of the safe viewport");
            Assert.GreaterOrEqual(rect.yMin, 0f, name + " is above the safe viewport");
            Assert.LessOrEqual(rect.xMax, layout.Width + 0.01f, name + " exceeds viewport width");
            Assert.LessOrEqual(rect.yMax, layout.Height + 0.01f, name + " exceeds viewport height");
        }

        private static void Disjoint(Rect a, Rect b, string description)
        {
            Assert.IsFalse(a.Overlaps(b), description);
        }

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

        [Test] public void HudFitsCompactGameView_720x405()
        {
            var layout = FullView(720f, 405f);
            Assert.AreEqual(0.75f, layout.Scale, 0.0001f);
            ValidateGameView(layout);
        }

        [Test] public void HudFitsDesktop_960x540()
        {
            ValidateGameView(FullView(960f, 540f));
        }

        [Test] public void HudFitsTablet_1007x601()
        {
            ValidateGameView(FullView(1007f, 601f));
        }

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
    }
}
