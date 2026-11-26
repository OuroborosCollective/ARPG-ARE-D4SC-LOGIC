using System.Collections;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        /// <summary>
        /// Owner request 2026-11-26: recorded sand and wood steps (Footsteps - Essentials) for the ash road and the mill, and
        /// sword sounds (SwordSoundPack) to compare with the staffs' existing swing. Every take is in the library; a sword's
        /// swing and hit come from the sword takes, any other weapon keeps the existing swing and hit.
        /// </summary>
        [UnityTest]
        public IEnumerator SwordsSoundLikeSwordsAndEveryStepTakeIsThere()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private audio overlay; native acceptance must run with evidence enabled.");
            _runtime.enabled = false;
            var audio = _runtime.GetComponentInChildren<AshenAtmosphere>();
            var library = Resources.Load<AtmosphereLibrary>("AshenAtmosphere");
            Assert.IsNotNull(library);
            for (int i = 0; i < 10; i++)
            {
                Assert.IsNotNull(library.Find("step_sand_" + i), "recorded sand step " + i);
                Assert.IsNotNull(library.Find("step_wood_" + i), "recorded wood step " + i);
            }
            for (int i = 0; i < 6; i++)
            {
                Assert.IsNotNull(library.Find("swing_sword_" + i), "sword swing " + i);
                Assert.IsNotNull(library.Find("impact_sword_" + i), "sword hit " + i);
            }

            var p = Game.Progression;
            const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            typeof(WeaponEquipment).GetMethod("Enable", Hidden).Invoke(p.Equipment, new object[] { p });
            typeof(WeaponEquipment).GetMethod("ClaimRegionalRelic", Hidden).Invoke(p.Equipment, new object[] { p, RegionalRelic.M_Lotklinge });
            Assert.IsTrue(Game.TrySetRegionalRelicEquipped(RegionalRelic.M_Lotklinge, true), "the sword is in hand");
            Assert.AreEqual(WeaponFamily.Sword, p.Equipment.Weapon.Definition.Family);
            var sword = Game.ActivateSkill(0);
            Assert.IsTrue(sword.Activated);
            audio.Skill(sword, 0, Game);
            StringAssert.Contains("_sword_", audio.LastEffectKey, "a sword sounds like a sword");

            Assert.IsTrue(Game.TrySetRegionalRelicEquipped(RegionalRelic.M_Lotklinge, false));
            for (int i = 0; i < 20; i++) Game.Step();
            var bare = Game.ActivateSkill(0);
            Assert.IsTrue(bare.Activated);
            audio.Skill(bare, 0, Game);
            Assert.AreEqual(bare.Hits > 0 ? "impact" : "swing", audio.LastEffectKey, "other weapons keep the existing swing and hit");
            yield return null;
        }
    }
}
