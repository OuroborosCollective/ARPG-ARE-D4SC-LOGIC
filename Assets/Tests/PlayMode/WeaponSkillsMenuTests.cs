using System.Collections;
using System.Collections.Generic;
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

        /// <summary>Owner decision 2026-11-18: the values have their own tab WERTE, compact and with details on tap.</summary>
        private int ValuesTab()
        {
            int tab = System.Array.IndexOf(ArenaRuntime.MenuTitles, "WERTE");
            Assert.GreaterOrEqual(tab, 0, "The menu has a WERTE tab");
            return tab;
        }

        /// <summary>Tap a value row and return the detail line it shows.</summary>
        private string TapValue(string id)
        {
            FindButton("StatRow_" + id).onClick.Invoke();
            return MenuText("StatDetail").text;
        }

        /// <summary>Owner order 2026-11-17 and decision 2026-11-18: weapon skills in FÄHIGKEITEN, stats and their
        /// effect on every damage type (with crit) compact in WERTE.</summary>
        [UnityTest]
        public IEnumerator SkillsAndValuesTabsShowLevelsPointsAndExactDamage()
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
            yield return Capture("ui-menu-faehigkeiten.png");

            // WERTE: compact values, every number from the Core rules; details only on tap.
            _runtime.OpenMenu(ValuesTab());
            yield return null;
            StringAssert.Contains("1 aus Waffenkünsten", TapValue("Points"));
            var stats = new CombatStats(p, 0);
            long ghoul = CombatRules.PreviewDamage(stats, SkillCatalog.Get(0).PowerBp, 0, false);
            long ghoulCrit = CombatRules.PreviewDamage(stats, SkillCatalog.Get(0).PowerBp, 0, true);
            long knight = CombatRules.PreviewDamage(stats, SkillCatalog.Get(0).PowerBp, 1, false);
            long knightCrit = CombatRules.PreviewDamage(stats, SkillCatalog.Get(0).PowerBp, 1, true);
            Assert.AreEqual(ghoul + " / " + ghoulCrit, MenuText("StatValue_Blade").text, "Klinge: hit / crit against a ghoul");
            string blade = TapValue("Blade");
            StringAssert.Contains("Ritter " + knight + " / " + knightCrit, blade);
            // Review PR #59: armour 1000/2200 removes armour/(1+armour) of the damage, not the raw armour value.
            StringAssert.Contains("Ghul −9,1 %, Ritter −18 %", blade);
            Assert.AreEqual("19 % ×1,5", MenuText("StatValue_Crit").text);
            StringAssert.StartsWith("DPS", MenuText("StatKey_Dps").text);
            Assert.AreEqual(hash, Game.ComputeWorldHash(), "Opening the menu and tapping values never changes the simulation");
            yield return Capture("ui-menu-werte-punkt-frei.png"); // ui-menu-werte.png belongs to the all-tabs capture

            // Spending the weapon point through the + button raises Might and persists.
            var before = p.TotalMightRank;
            BigIntegerSpend(p);
            _runtime.OpenMenu(ValuesTab());
            yield return null;
            var button = FindButton("StatSpend0");
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
