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
            // Review PR #59: armour 1000/2200 removes armour/(1+armour) of the damage, not the raw armour value.
            StringAssert.Contains("Ghul −9,1 %, Ritter −18 %", MenuText("PreviewNote").text);
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

        /// <summary>Own a one-handed catalog weapon of the family through the real receipt path.</summary>
        private LootItem OwnTestWeapon(CharacterProgression p, WeaponFamily family)
        {
            for (ulong seed = 0; seed < 20000; seed++)
            {
                var receipt = (LootItem)typeof(LootCatalog).GetMethod("Generate", Flags).Invoke(LootCatalog.Version1,
                    new object[] { seed, System.Numerics.BigInteger.One, 1UL, 1, 0, p.LifetimeKills + 1 });
                var gear = p.Equipment.ForItem(receipt);
                if (gear == null || gear.Definition.Family != family || gear.Definition.TwoHanded) continue;
                p.RecordKill();
                Assert.IsTrue((bool)typeof(CharacterProgression).GetMethod("CollectItem", Flags).Invoke(p, new object[] { receipt }));
                return receipt;
            }
            Assert.Fail("The catalog has no one-handed " + family);
            return null;
        }

        private static void RecordWeaponExperience(CharacterProgression p, WeaponFamily family, long amount)
        {
            typeof(WeaponMasteryState).GetMethod("Record", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(p.WeaponMastery, new object[] { family, amount });
        }

        /// <summary>Review PR #59: a weapon level-up from a hit that does not kill reaches the save slot at once.</summary>
        [UnityTest]
        public IEnumerator WeaponLevelUpFromANonKillingHitIsSaved()
        {
            _runtime.enabled = false;
            var p = Game.Progression; p.Equipment.Enable(p);
            var sword = OwnTestWeapon(p, WeaponFamily.Sword);
            Assert.IsTrue(Game.TryEquipWeapon(sword.KillOrdinal));
            RecordWeaponExperience(p, WeaponFamily.Sword, WeaponMasteryRules.ExperienceForLevel(2) - 1);
            // Put the Klinge discipline at the start of a rank, so this one hit cannot rank it up (that would save anyway).
            var rankStart = CharacterProgression.RequiredExperienceForLevel(p.PracticeLevel(0) + 1) - p.PracticeExperience(0);
            typeof(CharacterProgression).GetMethod("RecordPractice", Flags).Invoke(p, new object[] { 0, rankStart });
            var target = Game.Monsters[0];
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 20000; enemy.Zmm = 20000; }
            target.Xmm = Game.PlayerXmm + 1000; target.Zmm = Game.PlayerZmm; target.Health = target.MaxHealth = 100000;
            var rank = p.PracticeLevel(0);
            var queue = (Queue<int>)typeof(ArenaRuntime).GetField("_pendingSkills", Flags).GetValue(_runtime);
            queue.Enqueue(0); TickOnce();
            yield return null;
            Assert.Less(target.Health, 100000, "The sword hit lands");
            Assert.IsFalse(target.IsDead, "The hit does not kill");
            Assert.AreEqual(rank, p.PracticeLevel(0), "The discipline did not rank up, so only the weapon level-up can save");
            Assert.AreEqual(2, p.WeaponMastery.Level(WeaponFamily.Sword), "The hit levels the sword skill");
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0], ""), out var saved) &&
                          saved.WeaponMastery.Level(WeaponFamily.Sword) == 2, "The sword level-up is in the save slot without a kill");
        }

        /// <summary>Review PR #59: a shield level-up from an incoming attack reaches the save slot at once.</summary>
        [UnityTest]
        public IEnumerator ShieldLevelUpFromAnIncomingAttackIsSaved()
        {
            _runtime.enabled = false;
            var p = Game.Progression; p.Equipment.Enable(p);
            var shield = OwnTestWeapon(p, WeaponFamily.Shield);
            Assert.IsTrue(Game.TryEquipWeapon(shield.KillOrdinal));
            Assert.IsNotNull(p.Equipment.Shield);
            RecordWeaponExperience(p, WeaponFamily.Shield, WeaponMasteryRules.ExperienceForLevel(2) - 1);
            var attacker = Game.Monsters[0];
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 20000; enemy.Zmm = 20000; }
            attacker.AttackCooldownTicks = 0;
            int attacks = Game.IncomingAttacks;
            for (int i = 0; i < 40 && Game.IncomingAttacks == attacks; i++)
            {
                attacker.Xmm = Game.PlayerXmm + 1000; attacker.Zmm = Game.PlayerZmm;
                TickOnce();
            }
            yield return null;
            Assert.Greater(Game.IncomingAttacks, attacks, "The monster attack reaches the shield bearer");
            Assert.AreEqual(2, p.WeaponMastery.Level(WeaponFamily.Shield), "The attack trains the shield to level 2");
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0], ""), out var saved) &&
                          saved.WeaponMastery.Level(WeaponFamily.Shield) == 2, "The shield level-up is in the save slot at once");
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
