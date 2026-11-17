using System.Globalization;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Menu tab "FÄHIGKEITEN": weapon-type skills (RuneScape-style learn by doing), freely spendable stat points and
    /// what each stat and skill changes. Read-only projection of CharacterProgression; spending goes through the
    /// same Core call as the character tab. All numbers come from the Core rules, nothing is estimated here.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private static readonly int[] DamageSkillSlots = { 0, 1, 3 };

        /// <summary>Basis points as a German percentage with at most one decimal, e.g. 250 -> "2,5 %".</summary>
        /// <summary>Fixed German formatting, independent of the cultures installed on a device.</summary>
        public static string Percent(int basisPoints)
        {
            string sign = basisPoints < 0 ? "-" : "";
            long bp = System.Math.Abs((long)basisPoints);
            long whole = bp / 100, tenth = (bp % 100) / 10;
            return sign + whole.ToString(CultureInfo.InvariantCulture) + (tenth != 0 ? "," + tenth.ToString(CultureInfo.InvariantCulture) : "") + " %";
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
            float gap = 14f;
            float leftW = Mathf.Round((size.x - gap) * 0.47f);
            float rightW = size.x - gap - leftW;

            // Left: one learn-by-doing track per droppable attacking weapon type.
            var skills = AshenUiTheme.Frame("WeaponSkills", body, new Rect(0f, 0f, leftW, size.y), "Waffenkünste");
            float y = 34f;
            var intro = AshenUiTheme.Label("Intro", skills.transform, 12, AshenUiTheme.Muted, TextAnchor.UpperLeft);
            AshenUiTheme.Place(intro.rectTransform, new Rect(16f, y, leftW - 32f, 34f));
            intro.text = "Lernen durch Tun: jeder wirksame Treffer trainiert die angelegte Waffenart. " +
                         "Alle 5 Stufen: +1 freier Wertepunkt und +2,5 % Schaden mit dieser Waffe.";
            Fit(intro);
            y += 38f;
            WeaponFamily? equipped = p.TrainingFamily;
            float rowH = Mathf.Min(64f, (size.y - y - 44f) / WeaponMasteryRules.TrainedFamilies.Count);
            foreach (WeaponFamily family in WeaponMasteryRules.TrainedFamilies)
            {
                int level = p.WeaponMastery.Level(family);
                long xp = p.WeaponMastery.Experience(family);
                bool active = equipped.HasValue && equipped.Value == family;
                var name = AshenUiTheme.Label("Skill_" + family, skills.transform, 14, active ? AshenUiTheme.Ember : AshenUiTheme.Parchment,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                AshenUiTheme.Place(name.rectTransform, new Rect(16f, y, leftW * 0.6f, 20f));
                name.text = WeaponMasteryRules.SkillName(family) + (active ? "  · angelegt" : "");
                var lvl = AshenUiTheme.Label("Level_" + family, skills.transform, 14, AshenUiTheme.Parchment, TextAnchor.MiddleRight, FontStyle.Bold);
                AshenUiTheme.Place(lvl.rectTransform, new Rect(leftW * 0.55f, y, leftW * 0.45f - 16f, 20f));
                lvl.text = "Stufe " + level + " / " + WeaponMasteryRules.MaxLevel;

                long floor = WeaponMasteryRules.ExperienceForLevel(level);
                long next = level >= WeaponMasteryRules.MaxLevel ? floor : WeaponMasteryRules.ExperienceForLevel(level + 1);
                float fill = next > floor ? Mathf.Clamp01((xp - floor) / (float)(next - floor)) : 1f;
                var track = AshenUiTheme.Image("Track_" + family, skills.transform, AshenUiTheme.Bar, new Color(0.12f, 0.10f, 0.08f, 1f));
                AshenUiTheme.Place(track.rectTransform, new Rect(16f, y + 22f, leftW - 32f, 8f));
                var bar = AshenUiTheme.Image("Fill_" + family, track.transform, AshenUiTheme.Bar, AshenUiTheme.Experience);
                AshenUiTheme.Place(bar.rectTransform, new Rect(0f, 0f, Mathf.Max(2f, (leftW - 32f) * fill), 8f));

                var detail = AshenUiTheme.Label("Detail_" + family, skills.transform, 11, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
                AshenUiTheme.Place(detail.rectTransform, new Rect(16f, y + 32f, leftW - 32f, 18f));
                detail.text = (level >= WeaponMasteryRules.MaxLevel ? Thousands(xp) + " EP · Höchststufe"
                                   : Thousands(xp) + " / " + Thousands(next) + " EP") +
                              "   ·   +" + WeaponMasteryRules.StatPoints(level) + " Wertepunkte   ·   +" +
                              Percent(WeaponMasteryRules.DamageBonusBp(level)) + " Schaden";
                Fit(detail);
                y += rowH;
            }
            var foot = AshenUiTheme.Label("ShieldNote", skills.transform, 11, AshenUiTheme.Muted, TextAnchor.LowerLeft);
            AshenUiTheme.Place(foot.rectTransform, new Rect(16f, size.y - 40f, leftW - 32f, 30f));
            foot.text = equipped.HasValue ? "Trainiert gerade: " + WeaponMasteryRules.SkillName(equipped.Value) + "."
                                          : "Ohne Waffe trainiert keine Waffenkunst. Schilde (Nebenhand) trainieren noch nicht.";
            Fit(foot);

            // Right: stats with their effect and every damage type with its numbers.
            var right = AshenUiTheme.Frame("Effects", body, new Rect(leftW + gap, 0f, rightW, size.y), "Werte & Auswirkungen");
            y = 34f;
            System.Numerics.BigInteger fromLevels = System.Numerics.BigInteger.Max(System.Numerics.BigInteger.Zero, p.UnspentLevelPoints);
            int fromWeapons = p.WeaponMastery.StatPointsUnspent;
            bool canSpend = p.UnspentMasteryPoints > System.Numerics.BigInteger.Zero;
            var head = AshenUiTheme.Label("PointsHead", right.transform, 13, AshenUiTheme.Bronze, TextAnchor.MiddleLeft, FontStyle.Bold);
            AshenUiTheme.Place(head.rectTransform, new Rect(16f, y, rightW - 32f, 20f));
            head.text = "WERTE  ·  " + (canSpend ? "<color=#F7A23A>" + p.UnspentMasteryPoints + " Punkte frei</color>" : "keine Punkte frei");
            Fit(head);
            var source = AshenUiTheme.Label("PointsSource", right.transform, 11, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
            AshenUiTheme.Place(source.rectTransform, new Rect(16f, y + 19f, rightW - 32f, 16f));
            source.text = fromLevels + " aus Stufen  ·  " + fromWeapons + " aus Waffenkünsten";
            Fit(source);
            y += 40f;
            string[] stat = { "Macht", "Vitalität", "Entschlossenheit" };
            System.Numerics.BigInteger[] total = { p.TotalMightRank, p.TotalVitalityRank, p.TotalResolveRank };
            string[] effect =
            {
                "+" + Percent(p.MightDamageBonusBp) + " Schaden, alle Angriffe (max. 100 %)",
                "+" + p.VitalityHealthBonus + " Leben  →  " + _game.MaxPlayerHealth + " gesamt",
                "+" + p.ResolveHealBonus + " Heilung je Schutz",
            };
            for (int i = 0; i < 3; i++)
            {
                int path = i;
                var k = AshenUiTheme.Label("Stat" + i, right.transform, 13, AshenUiTheme.Parchment, TextAnchor.MiddleLeft, FontStyle.Bold);
                AshenUiTheme.Place(k.rectTransform, new Rect(16f, y, rightW * 0.38f - 16f, 24f));
                k.text = stat[i] + "  " + total[i];
                Fit(k);
                var v = AshenUiTheme.Label("StatEffect" + i, right.transform, 12, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
                AshenUiTheme.Place(v.rectTransform, new Rect(rightW * 0.38f + 4f, y, rightW * 0.62f - 64f, 24f));
                v.text = effect[i];
                Fit(v);
                var b = AshenUiTheme.MenuButton("SkillSpend" + i, right.transform, new Rect(rightW - 52f, y - 1f, 36f, 26f), "+",
                    () => SpendMasteryFromMenu((MasteryPath)path), new Color(0.58f, 0.34f, 0.12f, 1f), 16);
                b.interactable = canSpend;
                y += 28f;
            }

            y += 6f;
            var dh = AshenUiTheme.Label("DamageHead", right.transform, 13, AshenUiTheme.Bronze, TextAnchor.MiddleLeft, FontStyle.Bold);
            AshenUiTheme.Place(dh.rectTransform, new Rect(16f, y, rightW - 32f, 20f));
            string weapon = p.Equipment.Weapon != null ? p.Equipment.Weapon.Definition.Name : "ohne Waffe";
            dh.text = "SCHADENSARTEN  ·  " + weapon + "  ·  Krit " + Percent(CombatRules.CriticalChanceBp) +
                      " für ×" + Percent(CombatRules.CriticalMultiplierBp / 100).Replace(" %", "");
            Fit(dh);
            y += 24f;
            float lineH = Mathf.Min(44f, (size.y - y - 48f) / (DamageSkillSlots.Length + 1));
            foreach (int slot in DamageSkillSlots)
            {
                var skill = SkillCatalog.Get(slot);
                var stats = new CombatStats(p, slot);
                long ghoul = CombatRules.PreviewDamage(stats, skill.PowerBp, 0, false);
                long ghoulCrit = CombatRules.PreviewDamage(stats, skill.PowerBp, 0, true);
                long knight = CombatRules.PreviewDamage(stats, skill.PowerBp, 1, false);
                long knightCrit = CombatRules.PreviewDamage(stats, skill.PowerBp, 1, true);
                var t = AshenUiTheme.Label("Damage" + slot, right.transform, 13, AshenUiTheme.Parchment, TextAnchor.UpperLeft, FontStyle.Bold);
                AshenUiTheme.Place(t.rectTransform, new Rect(16f, y, rightW * 0.36f - 16f, 20f));
                t.text = SkillName(slot) + "  +" + Percent(stats.AdditiveDamageBp);
                Fit(t);
                var hits = AshenUiTheme.Label("DamageHits" + slot, right.transform, 13, AshenUiTheme.Parchment, TextAnchor.UpperRight, FontStyle.Bold);
                AshenUiTheme.Place(hits.rectTransform, new Rect(rightW * 0.36f, y, rightW * 0.64f - 16f, 20f));
                hits.text = "Ghul " + ghoul + " / Krit " + ghoulCrit + "   ·   Ritter " + knight + " / Krit " + knightCrit;
                Fit(hits);
                var parts = AshenUiTheme.Label("DamageParts" + slot, right.transform, 11, AshenUiTheme.Muted, TextAnchor.UpperLeft);
                AshenUiTheme.Place(parts.rectTransform, new Rect(16f, y + 19f, rightW - 32f, 18f));
                parts.text = "Basis " + Percent(CombatStats.BaseAdditiveBp) + " · Macht " + Percent(p.MightDamageBonusBp) +
                             " · Waffenaffix " + Percent(p.Equipment.Bonus(WeaponAffix.Power)) +
                             " · Disziplin " + Percent(p.PracticeDamageBonusBp(slot)) +
                             " · Waffenkunst " + Percent(p.WeaponMasteryDamageBonusBp);
                Fit(parts);
                y += lineH;
            }
            var heal = AshenUiTheme.Label("Healing", right.transform, 13, AshenUiTheme.Parchment, TextAnchor.UpperLeft, FontStyle.Bold);
            AshenUiTheme.Place(heal.rectTransform, new Rect(16f, y, rightW - 32f, 20f));
            var guard = SkillCatalog.Get(2);
            heal.text = SkillName(2) + "  heilt " + (guard.BaseHealing + p.ResolveHealBonus + p.PracticeHealingBonus) +
                        " Leben  (Basis " + guard.BaseHealing + " · Entschlossenheit " + p.ResolveHealBonus +
                        " · Disziplin " + p.PracticeHealingBonus + ")";
            Fit(heal);
            var note = AshenUiTheme.Label("PreviewNote", right.transform, 11, AshenUiTheme.Muted, TextAnchor.LowerLeft);
            AshenUiTheme.Place(note.rectTransform, new Rect(16f, size.y - 40f, rightW - 32f, 30f));
            note.text = "Werte je Treffer nach Rüstung (Ghul 10 %, Ritter 22 %), exakt nach den Kampfregeln berechnet.";
            Fit(note);
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
