using System.Collections;
using System.Linq;
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
        private LootItem OwnWeapon(CharacterProgression p, System.Func<WeaponDefinition, bool> wanted)
        {
            var generate = typeof(LootCatalog).GetMethod("Generate", Flags);
            var collect = typeof(CharacterProgression).GetMethod("CollectItem", Flags);
            for (ulong seed = 0; seed < 20000; seed++)
            {
                var receipt = (LootItem)generate.Invoke(LootCatalog.Version1, new object[] { seed, System.Numerics.BigInteger.One, 1UL, 1, 0, p.LifetimeKills + 1 });
                var item = p.Equipment.ForItem(receipt);
                if (item == null || !wanted(item.Definition)) continue;
                p.RecordKill();
                Assert.IsTrue((bool)collect.Invoke(p, new object[] { receipt }));
                return receipt;
            }
            Assert.Fail("No matching weapon could be generated");
            return null;
        }

        private IEnumerator CaptureMenu(string name, int width, int height)
        {
            string directory = System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            ArenaRuntime.HudScreenOverride = new Vector2(width, height);
            _runtime.OpenMenu(_runtime.MenuTab);
            yield return null;
            var camera = (Camera)typeof(ArenaRuntime).GetField("_camera", Flags).GetValue(_runtime);
            var canvases = _runtime.GetComponentsInChildren<Canvas>();
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + 0.05f; }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name), pixels.EncodeToPNG());
            }
            finally
            {
                foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
                camera.targetTexture = previous; RenderTexture.active = active;
                Object.Destroy(pixels); target.Release(); Object.Destroy(target);
                ArenaRuntime.HudScreenOverride = null;
                _runtime.OpenMenu(_runtime.MenuTab);
            }
        }

        /// <summary>Owner decisions 2026-11-17: shield block, two-handed rule, bow accuracy and weapon profiles in the menu.</summary>
        [UnityTest]
        public IEnumerator SkillsTabShowsShieldBlockBowAccuracyAndWeaponProfile()
        {
            _runtime.enabled = false;
            var p = Game.Progression;
            p.Equipment.Enable(p);
            var sword = OwnWeapon(p, d => d.Family == WeaponFamily.Sword && !d.TwoHanded);
            var shield = OwnWeapon(p, d => d.Family == WeaponFamily.Shield);
            var bow = OwnWeapon(p, d => d.Family == WeaponFamily.Bow);
            Assert.IsTrue(Game.TryEquipWeapon(sword.KillOrdinal) && Game.TryEquipWeapon(shield.KillOrdinal));
            Assert.IsFalse(Game.TryEquipWeapon(bow.KillOrdinal), "With a shield only one-handed weapons can be equipped");

            var record = typeof(WeaponMasteryState).GetMethod("Record", BindingFlags.Instance | BindingFlags.NonPublic);
            record.Invoke(p.WeaponMastery, new object[] { WeaponFamily.Sword, WeaponMasteryRules.ExperienceForLevel(12) });
            record.Invoke(p.WeaponMastery, new object[] { WeaponFamily.Shield, WeaponMasteryRules.ExperienceForLevel(20) });
            record.Invoke(p.WeaponMastery, new object[] { WeaponFamily.Bow, WeaponMasteryRules.ExperienceForLevel(8) });
            _runtime.OpenMenu(ArenaRuntime.MenuSkills);
            yield return null;
            Assert.AreEqual("Stufe 20 / 99", MenuText("Level_Shield").text);
            StringAssert.Contains("Block 5 %", MenuText("Detail_Shield").text, "20 levels x 0.25 %");
            StringAssert.Contains("Treffsicherheit 70 %", MenuText("Detail_Bow").text, "60 % + 8 x 1.25 %");
            StringAssert.Contains("Einhandschwert", MenuText("ProfileHead").text);
            StringAssert.Contains("Bewegung 86 %", MenuText("Profile").text, "98 % sword minus 12 points for the shield");
            StringAssert.Contains("Schild: Block 5 %", MenuText("ProfileExtra").text);
            StringAssert.Contains("nur Einhandwaffen", MenuText("ProfileExtra").text);
            StringAssert.Contains("+2 Wertepunkte", MenuText("Detail_Sword").text, "Sword level 12 grants two points");
            StringAssert.Contains("7 aus Waffenkünsten", MenuText("PointsSource").text, "sword 2 + shield 4 + bow 1, none spent");
            yield return CaptureMenu("ui-menu-faehigkeiten-ausgeruestet.png", 1920, 1080);
            yield return CaptureMenu("ui-menu-faehigkeiten-ausgeruestet-phone.png", 2400, 1080);

            Assert.IsTrue(Game.TryUnequipWeapon(true) && Game.TryEquipWeapon(bow.KillOrdinal));
            _runtime.OpenMenu(ArenaRuntime.MenuSkills);
            yield return null;
            StringAssert.Contains("Bogen", MenuText("ProfileHead").text);
            StringAssert.Contains("Treffsicherheit 70 %", MenuText("ProfileExtra").text);
            StringAssert.Contains("Krit 22 %", MenuText("DamageHead").text);
            yield return CaptureMenu("ui-menu-faehigkeiten-bogen.png", 1920, 1080);
            _runtime.CloseMenu();
        }

        /// <summary>Owner decision 2026-11-18: the staff is the magic focus; the menu shows Ruin's extra share.</summary>
        [UnityTest]
        public IEnumerator SkillsTabShowsTheStaffsMagicFocus()
        {
            _runtime.enabled = false;
            var p = Game.Progression;
            p.Equipment.Enable(p);
            var staff = OwnWeapon(p, d => d.Family == WeaponFamily.Staff);
            Assert.IsTrue(Game.TryEquipWeapon(staff.KillOrdinal));
            _runtime.OpenMenu(ArenaRuntime.MenuSkills);
            yield return null;
            StringAssert.Contains("Stab", MenuText("ProfileHead").text);
            var ruin = new CombatStats(p, CombatStats.RuinSkillSlot);
            StringAssert.Contains("+45 %", MenuText("Damage3").text, "Ruin's share includes the staff's +25 % (base 20 %)");
            StringAssert.Contains("Magiefokus 25 % (Ruin)", MenuText("DamageParts").text, "The shares name the staff's focus");
            Assert.AreEqual(4500, ruin.AdditiveDamageBp);
            StringAssert.Contains("+20 %", MenuText("Damage0").text, "Strikes get no focus");
            StringAssert.Contains("Magiefokus: Ruin +25 %", MenuText("ProfileExtra").text);
            StringAssert.Contains("verfehlt nie", MenuText("ProfileExtra").text);
            yield return CaptureMenu("ui-menu-faehigkeiten-stab.png", 1920, 1080);
            _runtime.CloseMenu();
        }
    }
}
