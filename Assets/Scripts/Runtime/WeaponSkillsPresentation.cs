using System.Globalization;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Menu tab "FÄHIGKEITEN": weapon-type skills (RuneScape-style learn by doing) with level, experience and bonus.
    /// Read-only projection of CharacterProgression; the values, points and damage are in the WERTE tab
    /// (StatsPresentation.cs). Shared number formatting lives here.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private static readonly int[] DamageSkillSlots = { 0, 1, 3 };

        /// <summary>Basis points as an exact German percentage with up to two decimals, e.g. 250 -> "2,5 %",
        /// 25 -> "0,25 %" (shield block per level); fixed formatting, independent of the cultures installed on a device.</summary>
        public static string Percent(int basisPoints)
        {
            return (basisPoints < 0 ? "-" : "") + Decimal(System.Math.Abs((long)basisPoints)) + " %";
        }

        /// <summary>Thousands separated by dots, as in German, e.g. 13034431 -> "13.034.431".</summary>
        public static string Thousands(long value)
        {
            string digits = System.Math.Abs(value).ToString(CultureInfo.InvariantCulture);
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) b.Append('.');
                b.Append(digits[i]);
            }
            return (value < 0 ? "-" : "") + b;
        }

        private void BuildSkillsTab(RectTransform body, Vector2 size)
        {
            var p = _game.Progression;
            float width = size.x;
            // One learn-by-doing track per trained weapon type; the values and their effects are in the WERTE tab.
            var skills = AshenUiTheme.Frame("WeaponSkills", body, new Rect(0f, 0f, width, size.y), "Waffenkünste");
            float y = 34f;
            var intro = AshenUiTheme.Label("Intro", skills.transform, 12, AshenUiTheme.Muted, TextAnchor.UpperLeft);
            AshenUiTheme.Place(intro.rectTransform, new Rect(16f, y, width - 32f, 18f));
            intro.text = "Lernen durch Tun · alle 5 Stufen +1 Wertepunkt und +2,5 % Schaden · Schild +0,25 % Block je Stufe";
            Fit(intro);
            y += 24f;
            WeaponFamily? equipped = p.TrainingFamily;
            float rowH = Mathf.Min(64f, (size.y - y - 44f) / WeaponMasteryRules.TrainedFamilies.Count);
            foreach (WeaponFamily family in WeaponMasteryRules.TrainedFamilies)
            {
                int level = p.WeaponMastery.Level(family);
                long xp = p.WeaponMastery.Experience(family);
                bool active = family == WeaponFamily.Shield ? p.Equipment.Shield != null : equipped.HasValue && equipped.Value == family;
                var name = AshenUiTheme.Label("Skill_" + family, skills.transform, 15, active ? AshenUiTheme.Ember : AshenUiTheme.Parchment,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                AshenUiTheme.Place(name.rectTransform, new Rect(16f, y, width * 0.5f, 20f));
                name.text = WeaponMasteryRules.SkillName(family) + (active ? "  · angelegt" : "");
                var lvl = AshenUiTheme.Label("Level_" + family, skills.transform, 15, AshenUiTheme.Parchment, TextAnchor.MiddleRight, FontStyle.Bold);
                AshenUiTheme.Place(lvl.rectTransform, new Rect(width * 0.5f, y, width * 0.5f - 16f, 20f));
                lvl.text = "Stufe " + level + " / " + WeaponMasteryRules.MaxLevel;

                long floor = WeaponMasteryRules.ExperienceForLevel(level);
                long next = level >= WeaponMasteryRules.MaxLevel ? floor : WeaponMasteryRules.ExperienceForLevel(level + 1);
                float fill = next > floor ? Mathf.Clamp01((xp - floor) / (float)(next - floor)) : 1f;
                var track = AshenUiTheme.Image("Track_" + family, skills.transform, AshenUiTheme.Bar, new Color(0.12f, 0.10f, 0.08f, 1f));
                AshenUiTheme.Place(track.rectTransform, new Rect(16f, y + 22f, width - 32f, 8f));
                var bar = AshenUiTheme.Image("Fill_" + family, track.transform, AshenUiTheme.Bar, AshenUiTheme.Experience);
                AshenUiTheme.Place(bar.rectTransform, new Rect(0f, 0f, Mathf.Max(2f, (width - 32f) * fill), 8f));

                var detail = AshenUiTheme.Label("Detail_" + family, skills.transform, 12, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
                AshenUiTheme.Place(detail.rectTransform, new Rect(16f, y + 32f, width - 32f, 18f));
                detail.text = (level >= WeaponMasteryRules.MaxLevel ? Thousands(xp) + " EP · Höchststufe"
                                   : Thousands(xp) + " / " + Thousands(next) + " EP") +
                              "   ·   +" + WeaponMasteryRules.StatPoints(level) + " Wertepunkte   ·   " +
                              (family == WeaponFamily.Shield ? "Block " + Percent(WeaponProfiles.BlockChanceBp(level))
                                  : "+" + Percent(WeaponMasteryRules.DamageBonusBp(level)) + " Schaden") +
                              (family == WeaponFamily.Bow ? "   ·   Treffsicherheit " + Percent(BowAccuracy(level)) : "");
                Fit(detail);
                y += rowH;
            }
            var foot = AshenUiTheme.Label("ShieldNote", skills.transform, 12, AshenUiTheme.Muted, TextAnchor.LowerLeft);
            AshenUiTheme.Place(foot.rectTransform, new Rect(16f, size.y - 34f, width - 32f, 24f));
            foot.text = (equipped.HasValue ? "Trainiert gerade: " + WeaponMasteryRules.SkillName(equipped.Value) + "."
                                           : "Ohne Waffe trainiert keine Waffenkunst.") + "  Werte und Schaden: Tab WERTE.";
            Fit(foot);
        }

        private static int BowAccuracy(int level)
        {
            foreach (var definition in AllWeaponDefinitions())
                if (definition.Family == WeaponFamily.Bow) return WeaponProfiles.For(definition).Accuracy(level);
            return 10000;
        }

        private static System.Collections.Generic.IEnumerable<WeaponDefinition> AllWeaponDefinitions()
        {
            for (int i = 0; i < WeaponCatalog.Count; i++) yield return WeaponCatalog.Get(i);
        }

        /// <summary>Share of damage that armour removes in the Core rule, armour / (1 + armour), rounded to a tenth.</summary>
        private static string ArmorReduction(int enemyKind)
        {
            long armor = CombatRules.ArmorBp(enemyKind);
            long tenths = (armor * 2000 / (10000 + armor) + 1) / 2;
            return Decimal(tenths * 10) + " %";
        }

        /// <summary>Hundredths as a German decimal with at most two places, e.g. 330 -> "3,3".</summary>
        private static string Decimal(long hundredths)
        {
            long whole = hundredths / 100, rest = System.Math.Abs(hundredths % 100);
            string fraction = rest == 0 ? "" : "," + (rest % 10 == 0 ? (rest / 10).ToString(CultureInfo.InvariantCulture) : rest.ToString("00", CultureInfo.InvariantCulture));
            return whole.ToString(CultureInfo.InvariantCulture) + fraction;
        }

        /// <summary>Dense rows shrink to the space a phone or a scaled desktop canvas actually gives them.</summary>
        private static Text Fit(Text text, int minimumSize = 9)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimumSize;
            text.resizeTextMaxSize = text.fontSize;
            return text;
        }

        private static string SkillName(int slot)
        {
            string label = SkillLabels[slot];
            return label.Substring(0, 1) + label.Substring(1).ToLowerInvariant();
        }
    }
}
