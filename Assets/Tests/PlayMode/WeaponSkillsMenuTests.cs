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
            // Combat v2 adds its own save section, so the checkpoint may be the AC22 container around the AC21 chain.
            Assert.IsTrue((saved.StartsWith("AC21|") || saved.StartsWith("AC22|")) && CharacterProgression.TryLoad(saved, out var loaded) &&
                loaded.TotalMightRank == p.TotalMightRank, "The spent weapon point survives save and load");
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
            // Combat v2: the swing lands on its impact tick, after the windup.
            for (int i = 0; i < 12 && target.Health == 100000; i++) TickOnce();
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
            // Combat v2: the shield trains only on blows it really takes, so SCHUTZ is held (key 3) toward the attacker,
            // who stands in front of the hero (north, the starting facing).
            if (Game.CombatV2) PlayerDevice.Scripted = new HeldGuardKey();
            try
            {
                for (int i = 0; i < 40 && Game.IncomingAttacks == attacks; i++)
                {
                    attacker.Xmm = Game.PlayerXmm + (Game.CombatV2 ? 0 : 1000); attacker.Zmm = Game.PlayerZmm + (Game.CombatV2 ? 1000 : 0);
                    TickOnce();
                }
            }
            finally { PlayerDevice.Scripted = null; }
            yield return null;
            Assert.Greater(Game.IncomingAttacks, attacks, "The monster attack reaches the shield bearer");
            Assert.AreEqual(2, p.WeaponMastery.Level(WeaponFamily.Shield), "The attack trains the shield to level 2");
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0], ""), out var saved) &&
                          saved.WeaponMastery.Level(WeaponFamily.Shield) == 2, "The shield level-up is in the save slot at once");
        }

        /// <summary>A desktop keyboard holding key 3 (SCHUTZ) and nothing else.</summary>
        private sealed class HeldGuardKey : IPlayerDevice
        {
            public bool GetKey(KeyCode key) { return key == KeyCode.Alpha3; }
            public bool GetKeyDown(KeyCode key) { return false; }
            public bool GetMouseButton(int button) { return false; }
            public bool GetMouseButtonDown(int button) { return false; }
            public Vector3 MousePosition { get { return Vector3.zero; } }
            public Vector2 MouseScrollDelta { get { return Vector2.zero; } }
            public int TouchCount { get { return 0; } }
            public Touch GetTouch(int index) { return default(Touch); }
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
