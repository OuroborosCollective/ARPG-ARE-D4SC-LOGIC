using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// Orsel's healing course in the running game (owner decisions of 2026-11-23): at the mill, once the camp is examined,
    /// the player walks to Orsel, opens her panel with a real tap, helps with two rations, learns Wundschluss, receives her
    /// one-handed staff and her robe, and heals the patient on the westmost bed with the WUNDSCHLUSS button. Every tap goes
    /// through the phone's touch routing; the save keeps the course, the staff, the robe and the closed wound.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        private IEnumerator PhoneTap(ScriptedPhone phone, ArenaHudLayout layout, Rect target, int frames = 12)
        {
            phone.Tap(layout, target.center);
            _runtime.enabled = true;
            for (int i = 0; i < frames || phone.Busy; i++)
            {
                phone.Commit();
                yield return null;
                if (i > 240) break;
            }
            _runtime.enabled = false;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator OrselTeachesWundschlussAndThePlayerHealsAPatient()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            var ilyan = Game.Npcs.Find(n => n.Id == 0);
            ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan);
            MoveStory(0, 24000); TravelStory();
            MoveStory(0, -2000);
            yield return StoryAction(HungerAction.InspectTrail);
            MoveStory(0, 24000); TravelStory();
            Assert.AreEqual(4, Game.Progression.EntryAct);
            Assert.IsTrue(Game.CombatV2, "A new game plays combat version 2");
            yield return RunFrames(1);
            var orsel = Find(ArenaRuntime.OrselFigureName);
            Assert.IsNotNull(orsel, "Orsel stands at the mill");
            Assert.Less(Vector2.Distance(new Vector2(orsel.transform.position.x, orsel.transform.position.z),
                new Vector2(HealingRules.OrselMillXmm / 1000f, HealingRules.OrselMillZmm / 1000f)), 0.2f, "She stands where the Core has her");

            // The camp first: Orsel teaches once Ilyan's help is revealed, before the beds.
            MoveStory(0, -2000); MoveStory(-4000, -2000);
            yield return StoryAction(HungerAction.InspectShelter);
            Assert.AreEqual(3, Game.Progression.HungerStage);
            MoveStory(-4000, HealingRules.OrselMillZmm); MoveStory(HealingRules.OrselMillXmm + 900, HealingRules.OrselMillZmm);
            Assert.IsTrue(Game.OrselInReach);
            Assert.AreEqual("ORSEL ANSPRECHEN", _runtime.OrselHint);

            Vector2? hud = ArenaRuntime.HudScreenOverride;
            var phone = new ScriptedPhone();
            try
            {
                ArenaRuntime.HudScreenOverride = new Vector2(1920f, 1080f);
                Time.captureDeltaTime = 0.1f; // one game tick per frame
                PlayerDevice.Scripted = phone;
                var layout = new ArenaHudLayout(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                System.Numerics.BigInteger rations = Game.Progression.Social.Provisions;

                yield return PhoneTap(phone, layout, ArenaRuntime.OrselToggleRect(layout));
                Assert.IsTrue((bool)typeof(ArenaRuntime).GetField("_orselPanelOpen", Flags).GetValue(_runtime), "A tap opens her panel");
                yield return PhoneTap(phone, layout, ArenaRuntime.OrselChoiceRect(layout, (int)HealingCoursePath.Supplies));
                Assert.AreEqual(HealingCoursePath.Supplies, Game.Progression.Healing.Path, "Two rations are her way in");
                Assert.AreEqual(rations - 2, Game.Progression.Social.Provisions, "Exactly two rations were handed over");
                yield return PhoneTap(phone, layout, ArenaRuntime.OrselChoiceRect(layout, ArenaRuntime.OrselLessonChoice));
                Assert.IsTrue(Game.Progression.Healing.Learned, "The lesson unlocks Wundschluss");
                Assert.IsTrue(Game.Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.M_Heilerstab), "Her staff is in hand");
                Assert.AreEqual("armor.cloth.robe.healer", Game.Progression.Armor.Worn[(int)ArmorSlot.Chest].Id, "Her robe is worn");
                yield return PhoneTap(phone, layout, ArenaRuntime.OrselCloseRect(layout));
                Assert.IsFalse((bool)typeof(ArenaRuntime).GetField("_orselPanelOpen", Flags).GetValue(_runtime));

                // From here only the westmost bed is within reach: the WUNDSCHLUSS button heals that patient.
                int wound = Game.Progression.Healing.PatientWound(2);
                Assert.IsTrue(Game.CanCastWundschluss && Game.WundschlussBlocker(out var target, out int bed) == null &&
                    target == HealTargetKind.Patient && bed == 2, "The westmost patient is the one in reach");
                yield return PhoneTap(phone, layout, layout.HealRect, 24);
                Assert.AreEqual(0, Game.Progression.Healing.PatientWound(2), "Wundschluss closed the wound (" + wound + " of 300)");
                Assert.Greater(Game.Progression.Healing.CooldownTicks, 0, "The cooldown runs");
                Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), NativeJourneyMaps, out var saved), "The save loads");
                Assert.IsTrue(saved.Healing.Learned && saved.Healing.PatientWound(2) == 0 && saved.Equipment.IsRegionalRelicEquipped(RegionalRelic.M_Heilerstab),
                    "The course, the staff and the closed wound are saved");
            }
            finally
            {
                PlayerDevice.Scripted = null;
                ArenaRuntime.HudScreenOverride = hud;
                Time.captureDeltaTime = 0f;
            }
            yield return Capture("orsel-course-mill.png");
        }
    }
}
