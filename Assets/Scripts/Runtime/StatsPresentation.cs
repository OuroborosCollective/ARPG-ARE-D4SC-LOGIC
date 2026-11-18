using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.UI;
using BigInteger = System.Numerics.BigInteger;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Menu tab "WERTE" (owner decision 2026-11-18): a compact character sheet in the style of classic MMORPG stat
    /// windows. Rows show a short key and its value only; the explanation of a value appears in one detail line when its
    /// row is tapped. Every number is read from the Core (CharacterProgression, CombatStats, CombatRules). Tapping never
    /// changes the simulation; only the + buttons spend a point, through the same Core call as everywhere else.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string StatDetailHint = "Wert antippen für Details.";
        private static readonly Color SelectedStatRow = new Color(0.98f, 0.50f, 0.16f, 0.16f);
        private static readonly Color StatRule = new Color(0.40f, 0.31f, 0.19f, 0.45f);
        private string _statSelected;

        private void BuildStatsTab(RectTransform body, Vector2 size)
        {
            var p = _game.Progression;
            var profile = p.CarriedProfile;
            var rows = new Dictionary<string, Image>();
            const float gap = 12f, detailH = 50f;
            float frameH = size.y - detailH - 8f;
            float leftW = Mathf.Round((size.x - gap) * 0.38f);
            float rightW = size.x - gap - leftW;

            var box = AshenUiTheme.Frame("StatDetailBox", body, new Rect(0f, size.y - detailH, size.x, detailH));
            var detail = AshenUiTheme.Label("StatDetail", box.transform, 13, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
            AshenUiTheme.Place(detail.rectTransform, new Rect(16f, 6f, size.x - 32f, detailH - 12f));
            Fit(detail, 10);
            System.Action<string> show = id =>
            {
                foreach (var row in rows) row.Value.color = row.Key == id ? SelectedStatRow : Color.clear;
                _statSelected = id;
                detail.color = id == null ? AshenUiTheme.Muted : AshenUiTheme.Parchment;
                detail.text = id == null ? StatDetailHint : StatDetail(id);
            };

            // Left: free points, the three stats with their + buttons, and what they add up to.
            var left = AshenUiTheme.Frame("Values", body, new Rect(0f, 0f, leftW, frameH), "Werte");
            BigInteger free = p.UnspentMasteryPoints;
            bool canSpend = free > BigInteger.Zero;
            float rowH = Mathf.Min(34f, (frameH - 52f) / 7f);
            float rowW = leftW - 24f;
            float y = 36f;
            StatRow(left.transform, rows, show, "Points", new Rect(12f, y, rowW, rowH), "Punkte",
                canSpend ? free + " frei" : "0", canSpend ? AshenUiTheme.Ember : AshenUiTheme.Parchment);
            y += rowH;
            string[] ids = { "Might", "Vitality", "Resolve" };
            BigInteger[] ranks = { p.TotalMightRank, p.TotalVitalityRank, p.TotalResolveRank };
            for (int i = 0; i < 3; i++)
            {
                int path = i;
                string id = ids[i];
                StatRow(left.transform, rows, show, id, new Rect(12f, y, rowW - 44f, rowH), MasteryName((MasteryPath)i), ranks[i].ToString(),
                    AshenUiTheme.Parchment);
                var plus = AshenUiTheme.MenuButton("StatSpend" + i, left.transform, new Rect(leftW - 50f, y + (rowH - 28f) * 0.5f, 36f, 28f), "+",
                    () => { _statSelected = id; SpendMasteryFromMenu((MasteryPath)path); }, new Color(0.58f, 0.34f, 0.12f, 1f), 16);
                plus.interactable = canSpend;
                y += rowH;
            }
            y += 8f;
            StatRow(left.transform, rows, show, "Life", new Rect(12f, y, rowW, rowH), "Leben", _game.PlayerHealth + " / " + _game.MaxPlayerHealth,
                AshenUiTheme.Parchment);
            y += rowH;
            StatRow(left.transform, rows, show, "Healing", new Rect(12f, y, rowW, rowH), "Heilung", GuardHealing(p).ToString(), AshenUiTheme.Parchment);
            y += rowH;
            StatRow(left.transform, rows, show, "Move", new Rect(12f, y, rowW, rowH), "Bewegung", Percent(p.MoveSpeedBp), AshenUiTheme.Parchment);

            // Right: how the carried weapon fights. Damage per hit against a ghoul as "normal / critical".
            var right = AshenUiTheme.Frame("Combat", body, new Rect(leftW + gap, 0f, rightW, frameH));
            var head = AshenUiTheme.Label("CombatHead", right.transform, 15, AshenUiTheme.Bronze, TextAnchor.UpperLeft, FontStyle.Bold);
            AshenUiTheme.Place(head.rectTransform, new Rect(14f, 8f, rightW - 28f, 22f));
            head.text = "KAMPF  ·  " + profile.Name;
            Fit(head, 11);
            var legend = AshenUiTheme.Label("CombatLegend", right.transform, 12, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
            AshenUiTheme.Place(legend.rectTransform, new Rect(20f, 32f, rightW - 40f, 18f));
            legend.text = "Schaden je Treffer gegen einen Ghul: normal / kritisch";
            Fit(legend, 10);
            float colW = (rightW - 36f) / 2f;
            float x0 = 12f, x1 = 24f + colW;
            float top = 54f;
            float combatRowH = Mathf.Min(34f, (frameH - top - 10f) / 5f);
            var strike = new CombatStats(p, 0);
            int cooldown = _game.AttackCooldown(0);

            StatRow(right.transform, rows, show, "Dps", new Rect(x0, top, colW, combatRowH), "DPS", StrikeDamagePerSecond(p, cooldown).ToString(),
                AshenUiTheme.Ember);
            string[] damageIds = { "Blade", "Force", "Ruin" };
            for (int i = 0; i < DamageSkillSlots.Length; i++)
                StatRow(right.transform, rows, show, damageIds[i], new Rect(x0, top + (i + 1) * combatRowH, colW, combatRowH),
                    SkillName(DamageSkillSlots[i]), HitAndCrit(p, DamageSkillSlots[i], 0), AshenUiTheme.Parchment);
            if (profile.RuinFocusBp > 0)
                StatRow(right.transform, rows, show, "Focus", new Rect(x0, top + 4f * combatRowH, colW, combatRowH), "Fokus",
                    "Ruin +" + Percent(profile.RuinFocusBp), AshenUiTheme.Magic);

            StatRow(right.transform, rows, show, "Crit", new Rect(x1, top, colW, combatRowH), "Krit",
                Percent(strike.CritChanceBp) + " ×" + CritMultiplier(), AshenUiTheme.Parchment);
            StatRow(right.transform, rows, show, "Hit", new Rect(x1, top + combatRowH, colW, combatRowH), "Treffer", Percent(p.WeaponAccuracyBp),
                profile.CanMiss && p.WeaponAccuracyBp < 10000 ? AshenUiTheme.Ember : AshenUiTheme.Parchment);
            StatRow(right.transform, rows, show, "Tempo", new Rect(x1, top + 2f * combatRowH, colW, combatRowH), "Tempo",
                Decimal(1000L / cooldown) + "/s", AshenUiTheme.Parchment);
            StatRow(right.transform, rows, show, "Reach", new Rect(x1, top + 3f * combatRowH, colW, combatRowH), "Reichweite",
                Decimal(_game.AttackRange(0) / 10) + " m", AshenUiTheme.Parchment);
            StatRow(right.transform, rows, show, "Block", new Rect(x1, top + 4f * combatRowH, colW, combatRowH), "Block",
                p.Equipment.Shield != null ? Percent(p.BlockChanceBp) : "–", AshenUiTheme.Parchment);

            show(_statSelected != null && rows.ContainsKey(_statSelected) ? _statSelected : null);
        }

        /// <summary>One tappable key/value row: a transparent button that highlights when selected.</summary>
        private static void StatRow(Transform parent, Dictionary<string, Image> rows, System.Action<string> show, string id, Rect rect,
            string key, string value, Color valueColor)
        {
            var hit = AshenUiTheme.Image("StatRow_" + id, parent, null, Color.clear, false);
            hit.raycastTarget = true;
            AshenUiTheme.Place(hit.rectTransform, rect);
            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => show(id));
            rows[id] = hit;
            var rule = AshenUiTheme.Image("Rule", hit.transform, AshenUiTheme.Bar, StatRule, false);
            AshenUiTheme.Place(rule.rectTransform, new Rect(4f, rect.height - 1f, rect.width - 8f, 1f));
            var k = AshenUiTheme.Label("StatKey_" + id, hit.transform, 14, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
            AshenUiTheme.Place(k.rectTransform, new Rect(8f, 0f, rect.width * 0.55f - 8f, rect.height));
            k.text = key;
            Fit(k, 10);
            var v = AshenUiTheme.Label("StatValue_" + id, hit.transform, 15, valueColor, TextAnchor.MiddleRight, FontStyle.Bold);
            AshenUiTheme.Place(v.rectTransform, new Rect(rect.width * 0.35f, 0f, rect.width * 0.65f - 8f, rect.height));
            v.text = value;
            Fit(v, 10);
        }

        /// <summary>The explanation of one value; shown in the detail line when its row is tapped.</summary>
        private string StatDetail(string id)
        {
            var p = _game.Progression;
            var profile = p.CarriedProfile;
            switch (id)
            {
                case "Points":
                    return BigInteger.Max(BigInteger.Zero, p.UnspentLevelPoints) + " aus Stufen · " + p.WeaponMastery.StatPointsUnspent +
                           " aus Waffenkünsten · mit + auf Macht, Vitalität oder Entschlossenheit verteilen";
                case "Might":
                    return "Macht " + p.TotalMightRank + ": +" + Percent(p.MightDamageBonusBp) +
                           " Schaden für alle Angriffe · +1 % je Punkt, höchstens +100 %";
                case "Vitality":
                    return "Vitalität " + p.TotalVitalityRank + ": +" + p.VitalityHealthBonus + " Leben · +20 je Punkt";
                case "Resolve":
                    return "Entschlossenheit " + p.TotalResolveRank + ": +" + p.ResolveHealBonus + " Heilung für " + SkillName(2) + " · +10 je Punkt";
                case "Life":
                    return "Höchstleben " + _game.MaxPlayerHealth + ", davon +" + p.VitalityHealthBonus + " aus Vitalität";
                case "Healing":
                    return SkillName(2) + " heilt " + GuardHealing(p) + ": Basis " + SkillCatalog.Get(2).BaseHealing + " · Entschlossenheit " +
                           p.ResolveHealBonus + " · Disziplin " + p.PracticeHealingBonus;
                case "Move":
                    return profile.Name + " " + Percent(profile.MoveSpeedBp) +
                           (p.Equipment.Shield != null ? " · Schild −" + WeaponProfiles.ShieldMovePenaltyBp / 100 + " Punkte" : "");
                case "Dps":
                {
                    var stats = new CombatStats(p, 0);
                    int power = SkillCatalog.Get(0).PowerBp;
                    return SkillName(0) + " im Mittel gegen einen Ghul, mit Krit" + (profile.CanMiss ? " und Fehlschüssen" : "") + ": " +
                           CombatRules.PreviewDamage(stats, power, 0, false) + " / " + CombatRules.PreviewDamage(stats, power, 0, true) +
                           " je Treffer · " + Decimal(1000L / _game.AttackCooldown(0)) + " Schläge/s";
                }
                case "Blade": return DamageDetail(p, 0);
                case "Force": return DamageDetail(p, 1);
                case "Ruin": return DamageDetail(p, CombatStats.RuinSkillSlot);
                case "Focus":
                    return "Magiefokus: Ruin +" + Percent(profile.RuinFocusBp) + " Schadensanteil, solange der Stab angelegt ist · verfehlt nie";
                case "Crit":
                    return "Kritischer Treffer: " + Percent(new CombatStats(p, 0).CritChanceBp) + " Chance für ×" + CritMultiplier() +
                           " Schaden · " + profile.Name;
                case "Hit":
                    return profile.CanMiss
                        ? "Treffsicherheit " + Percent(profile.Accuracy(0)) + " + 1,25 % je Bogenstufe (Stufe " + p.WeaponMastery.Level(WeaponFamily.Bow) +
                          "), 100 % ab Stufe 32 · Fehlschüsse verursachen nichts und trainieren nichts"
                        : profile.Name + " verfehlt nie";
                case "Tempo":
                {
                    int cooldown = _game.AttackCooldown(0);
                    return "Abklingzeit " + cooldown + " Ticks (" + Decimal(cooldown * 10L) + " s) · " + Decimal(1000L / cooldown) + " Schläge pro Sekunde";
                }
                case "Reach":
                    return SkillName(0) + " trifft bis " + Decimal(_game.AttackRange(0) / 10) + " m · " + profile.Name;
                case "Block":
                    return p.Equipment.Shield != null
                        ? "Schildkunst Stufe " + p.WeaponMastery.Level(WeaponFamily.Shield) + ": Block " + Percent(p.BlockChanceBp) +
                          " (+0,25 % je Stufe) · Schadensminderung " + Percent(p.Equipment.Shield.Definition.BasePower) + " · Bewegung −" +
                          WeaponProfiles.ShieldMovePenaltyBp / 100 + " Punkte · nur Einhandwaffen"
                        : "Kein Schild angelegt · ein Schild blockt +0,25 % je Schildkunst-Stufe und erlaubt nur Einhandwaffen";
                default:
                    return StatDetailHint;
            }
        }

        /// <summary>Additive shares of one damage type, then the knight and the armour rule (Core: armour / (1 + armour)).</summary>
        private static string DamageDetail(CharacterProgression p, int slot)
        {
            var stats = new CombatStats(p, slot);
            int focus = slot == CombatStats.RuinSkillSlot ? p.CarriedProfile.RuinFocusBp : 0;
            return SkillName(slot) + " +" + Percent(stats.AdditiveDamageBp) + ": Basis " + Percent(CombatStats.BaseAdditiveBp) +
                   " · Macht " + Percent(p.MightDamageBonusBp) + " · Waffenaffix " + Percent(p.Equipment.Bonus(WeaponAffix.Power)) +
                   " · Waffenkunst " + Percent(p.WeaponMasteryDamageBonusBp) + " · Disziplin " + Percent(p.PracticeDamageBonusBp(slot)) +
                   (focus > 0 ? " · Magiefokus " + Percent(focus) : "") +
                   "\nRitter " + HitAndCrit(p, slot, 1) + " · Rüstung: Ghul −" + ArmorReduction(0) + ", Ritter −" + ArmorReduction(1);
        }

        /// <summary>Exact preview of one hit without and with a critical, through the Core's own damage rule.</summary>
        private static string HitAndCrit(CharacterProgression p, int slot, int enemyKind)
        {
            var stats = new CombatStats(p, slot);
            int power = SkillCatalog.Get(slot).PowerBp;
            return CombatRules.PreviewDamage(stats, power, enemyKind, false) + " / " + CombatRules.PreviewDamage(stats, power, enemyKind, true);
        }

        /// <summary>Mean strike damage per second against a ghoul, including crits and, for the bow, misses.</summary>
        private static long StrikeDamagePerSecond(CharacterProgression p, int cooldown)
        {
            var stats = new CombatStats(p, 0);
            int power = SkillCatalog.Get(0).PowerBp;
            long normal = CombatRules.PreviewDamage(stats, power, 0, false), crit = CombatRules.PreviewDamage(stats, power, 0, true);
            return (normal * (10000 - stats.CritChanceBp) + crit * stats.CritChanceBp) / 10000L * p.WeaponAccuracyBp / 10000L * 10L / cooldown;
        }

        private static int GuardHealing(CharacterProgression p) => SkillCatalog.Get(2).BaseHealing + p.ResolveHealBonus + p.PracticeHealingBonus;

        private static string CritMultiplier() => Percent(CombatRules.CriticalMultiplierBp / 100).Replace(" %", "");
    }
}
