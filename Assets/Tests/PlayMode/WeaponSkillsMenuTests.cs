using System.Collections;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        private Text MenuText(string name)
        {
            foreach (var text in _runtime.GetComponentsInChildren<Text>(true))
                if (text.name == name) return text;
            Assert.Fail("Menu label " + name + " is missing");
            return null;
        }

        /// <summary>Owner order 2026-11-17: weapon skills, stats and their effects are visible in the HUD menu.</summary>
        [UnityTest]
        public IEnumerator SkillsTabShowsWeaponLevelsStatPointsAndExactDamage()
        {
            _runtime.enabled = false;
            var p = Game.Progression;
            _runtime.OpenMenu(ArenaRuntime.MenuSkills);
            yield return null;
            Assert.AreEqual(ArenaRuntime.MenuSkills, _runtime.MenuTab);
            Assert.AreEqual("FÄHIGKEITEN", ArenaRuntime.MenuTitles[ArenaRuntime.MenuSkills]);
            foreach (WeaponFamily family in WeaponMasteryRules.TrainedFamilies)
                StringAssert.StartsWith("Stufe 1 / 99", MenuText("Level_" + family).text, family + " starts at level 1");

            // Train the sword to level 5 through the Core owner (internal API, reached by reflection only in tests).
            var record = typeof(WeaponMasteryState).GetMethod("Record", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(record);
            Assert.AreEqual(4, record.Invoke(p.WeaponMastery, new object[] { WeaponFamily.Sword, WeaponMasteryRules.ExperienceForLevel(5) }));
            ulong hash = Game.ComputeWorldHash();
            _runtime.OpenMenu(ArenaRuntime.MenuSkills);
            yield return null;
            Assert.AreEqual("Stufe 5 / 99", MenuText("Level_Sword").text);
            StringAssert.Contains("+1 Wertepunkte", MenuText("Detail_Sword").text);
            StringAssert.Contains("+2,5 % Schaden", MenuText("Detail_Sword").text);
            StringAssert.Contains("1 aus Waffenkünsten", MenuText("PointsSource").text);
            Assert.AreEqual(hash, Game.ComputeWorldHash(), "Opening the menu never changes the simulation");

            // The preview text uses the exact Core damage rule.
            var stats = new CombatStats(p, 0);
            long ghoul = CombatRules.PreviewDamage(stats, SkillCatalog.Get(0).PowerBp, 0, false);
            long ghoulCrit = CombatRules.PreviewDamage(stats, SkillCatalog.Get(0).PowerBp, 0, true);
            StringAssert.Contains("Ghul " + ghoul + " / Krit " + ghoulCrit, MenuText("DamageHits0").text);
            StringAssert.Contains("Krit 19 % für ×1,5", MenuText("DamageHead").text);
            yield return Capture("ui-menu-faehigkeiten.png");

            // Spending the weapon point through the + button raises Might and persists.
            var before = p.TotalMightRank;
            BigIntegerSpend(p);
            var button = FindButton("SkillSpend0");
            Assert.IsTrue(button.interactable, "A free point can be spent");
            button.onClick.Invoke();
            yield return null;
            Assert.AreEqual(before + 1, p.TotalMightRank, "The + button spends one point on Might");
            string saved = p.SaveString();
            Assert.IsTrue(saved.StartsWith("AC21|") && CharacterProgression.TryLoad(saved, out var loaded) && loaded.TotalMightRank == p.TotalMightRank,
                "The spent weapon point survives save and load");
            _runtime.CloseMenu();
        }

        private static void BigIntegerSpend(CharacterProgression p)
        {
            // Spend any character-level points first so the button below must use the weapon point.
            while (p.UnspentLevelPoints > System.Numerics.BigInteger.Zero) Assert.IsTrue(p.TrySpendMastery(MasteryPath.Vitality));
        }

        private Button FindButton(string name)
        {
            foreach (var button in _runtime.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            Assert.Fail("Menu button " + name + " is missing");
            return null;
        }
    }
}
