using AshenCrypt.MemoryForge;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>How the player earned Orsel's lesson (owner's Heilkunst draft of 2026-11-12): supplies, payment or a favour.</summary>
    public enum HealingCoursePath { None = 0, Supplies = 1, Payment = 2, Favour = 3 }

    /// <summary>Who a Wundschluss is for. The Core chooses when the cast starts and shows it during the windup.</summary>
    public enum HealTargetKind { None = 0, Self = 1, Companion = 2, Patient = 3 }

    /// <summary>
    /// Orsel's healing course and the spell Wundschluss (owner decisions 2026-11-23). Each way in costs 2 rations,
    /// 12 coins or 3 Aschenflieder, and all three lead to the same lesson; money buys no higher practice. Wundschluss
    /// heals 180 plus the existing healing bonuses for 15 focus, with an 8 s cooldown, and needs a staff in hand:
    /// one-handed 80 %, two-handed 100 %. The caster's armor lowers it (<see cref="ArmorRules"/>), nothing else does:
    /// no attribute penalty, no miss chance. Integer HP, focus, ticks and basis points, one rounding at the end.
    /// First test values: tune after playtests.
    /// </summary>
    public static class HealingRules
    {
        public const int Revision = 1;
        public const int SupplyRations = 2, PaymentCoins = 12, FavourHerbs = 3;
        public const int WundschlussHealing = 180, WundschlussFocus = 15, WundschlussCooldownTicks = 80;
        /// <summary>A short windup, the heal on the tick after it, a short recovery.</summary>
        public static readonly ActionTiming Wundschluss = new ActionTiming(4, 1, 4, 0);
        public const int OneHandStaffBp = 8000, TwoHandStaffBp = 10000;
        /// <summary>A heal on somebody else reaches this far when it starts; the caster must see them.</summary>
        public const long ReachMm = 4000;
        /// <summary>When it lands the target may have moved a little: half as far again still counts.</summary>
        public const long LandingReachMm = 6000;
        /// <summary>
        /// Where Orsel stands at the old mill: just west of the shelter, at the head of the row of sick beds, clear of its
        /// posts and more than two talking distances away from the beds' clue and from the Glockenträgerin's relic, so no
        /// two offers ever share the button.
        /// </summary>
        public const long OrselMillXmm = -12500, OrselMillZmm = 5000;
        /// <summary>The mill's three patients lie on the beds; each has an authored wound out of 300 health.</summary>
        public const int PatientCount = 3, PatientMaxHealth = 300;
        private static readonly int[] PatientWounds = { 160, 220, 110 };

        public static int PatientWound(int patient) { return PatientWounds[patient]; }
        public static long PatientXmm(int patient) { return -4000L - patient * 2500L; }
        public static long PatientZmm(int patient) { return 5000L; }

        /// <summary>What a way in costs: rations, coins or herbs.</summary>
        public static int Cost(HealingCoursePath path)
        {
            switch (path)
            {
                case HealingCoursePath.Supplies: return SupplyRations;
                case HealingCoursePath.Payment: return PaymentCoins;
                case HealingCoursePath.Favour: return FavourHerbs;
                default: return 0;
            }
        }

        /// <summary>The staff factor of what is in hand; anything but a staff cannot carry the spell.</summary>
        public static int StaffBp(WeaponDefinition hand)
        { return hand == null || hand.Family != WeaponFamily.Staff ? 0 : hand.TwoHanded ? TwoHandStaffBp : OneHandStaffBp; }

        /// <summary>floor(base × staff × armor / 10^8): the owner's draft formula with one rounding at the end.</summary>
        public static int Healing(int baseHealing, int staffBp, int armorBp)
        {
            if (baseHealing <= 0 || staffBp <= 0 || armorBp <= 0) return 0;
            return (int)((long)baseHealing * staffBp * armorBp / 100000000L);
        }
    }

    /// <summary>One Wundschluss that took effect, fizzled or was cut short; presentation only.</summary>
    public sealed class HealResult
    {
        public bool Activated, Interrupted;
        public HealTargetKind Target;
        public int TargetIndex = -1;
        /// <summary>Health actually restored; overhealing is not counted.</summary>
        public int Amount;
        public long TargetXmm, TargetZmm;
        public string Message = "";
    }

    /// <summary>
    /// The course and the mill patients' wounds. Own save section "healing"; absent while nothing happened, so older
    /// saves load unchanged. Learning and practice stay apart: the lesson unlocks the spell once, practice comes only
    /// from health a cast really restored and goes into the healing mastery (the former guard practice).
    /// </summary>
    public sealed class HealingCourseState
    {
        private readonly int[] _wounds = new int[HealingRules.PatientCount];
        public HealingCoursePath Path { get; private set; }
        public bool Learned { get; private set; }
        public int CooldownTicks { get; private set; }
        /// <summary>The first patient was treated with Wundschluss: the practical part of the lesson is done.</summary>
        public bool PatientTreated { get; private set; }

        public HealingCourseState()
        {
            for (int i = 0; i < HealingRules.PatientCount; i++) _wounds[i] = HealingRules.PatientWound(i);
        }

        public int PatientWound(int patient) { return _wounds[patient]; }

        public bool HasProgress
        {
            get
            {
                if (Path != HealingCoursePath.None || Learned || CooldownTicks != 0 || PatientTreated) return true;
                for (int i = 0; i < HealingRules.PatientCount; i++) if (_wounds[i] != HealingRules.PatientWound(i)) return true;
                return false;
            }
        }

        internal bool Help(HealingCoursePath path)
        {
            if (Path != HealingCoursePath.None || path < HealingCoursePath.Supplies || path > HealingCoursePath.Favour) return false;
            Path = path;
            return true;
        }

        internal bool Learn()
        {
            if (Path == HealingCoursePath.None || Learned) return false;
            Learned = true;
            return true;
        }

        internal void StartCooldown() { CooldownTicks = HealingRules.WundschlussCooldownTicks; }
        internal void AdvanceTick() { if (CooldownTicks > 0) CooldownTicks--; }

        /// <summary>Close a patient's wound by up to the amount; returns the health really restored.</summary>
        internal int TreatPatient(int patient, int amount)
        {
            int healed = Math.Max(0, Math.Min(amount, _wounds[patient]));
            _wounds[patient] -= healed;
            if (healed > 0) PatientTreated = true;
            return healed;
        }

        internal ulong StableHash { get { return LootCatalog.HashText(Token()); } }

        /// <summary>"1;path;learned;cooldown;treated;wound0,wound1,wound2".</summary>
        internal string Token()
        {
            var c = CultureInfo.InvariantCulture;
            var b = new StringBuilder("1;");
            b.Append(((int)Path).ToString(c)).Append(';').Append(Learned ? '1' : '0').Append(';')
                .Append(CooldownTicks.ToString(c)).Append(';').Append(PatientTreated ? '1' : '0').Append(';');
            for (int i = 0; i < HealingRules.PatientCount; i++) { if (i > 0) b.Append(','); b.Append(_wounds[i].ToString(c)); }
            return b.ToString();
        }

        private static bool Number(string s, int max, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s) || s.Length > 6 || (s.Length > 1 && s[0] == '0')) return false;
            foreach (char ch in s) if (ch < '0' || ch > '9') return false;
            return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= max;
        }

        internal static bool TryRead(string token, out HealingCourseState state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] f = token.Split(';');
            if (f.Length != 6 || f[0] != "1" || (f[2] != "0" && f[2] != "1") || (f[4] != "0" && f[4] != "1")) return false;
            if (!Number(f[1], 3, out int path) || !Number(f[3], HealingRules.WundschlussCooldownTicks, out int cooldown)) return false;
            string[] w = f[5].Split(',');
            if (w.Length != HealingRules.PatientCount) return false;
            var s = new HealingCourseState { Path = (HealingCoursePath)path, Learned = f[2] == "1", CooldownTicks = cooldown, PatientTreated = f[4] == "1" };
            bool anyHealed = false;
            for (int i = 0; i < HealingRules.PatientCount; i++)
            {
                if (!Number(w[i], HealingRules.PatientWound(i), out int wound)) return false;
                s._wounds[i] = wound;
                anyHealed |= wound != HealingRules.PatientWound(i);
            }
            // Only a learned spell casts, only a cast has a cooldown or treats somebody, and only help opens the lesson.
            if ((s.Learned && s.Path == HealingCoursePath.None) || (!s.Learned && (cooldown != 0 || s.PatientTreated || anyHealed)) ||
                s.PatientTreated != anyHealed) return false;
            state = s;
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        private HealingCourseState _healing = new HealingCourseState();
        public HealingCourseState Healing { get { return _healing; } }
        private const string HealingSectionName = "healing";

        private void SyncHealingSection()
        {
            if (_healing.HasProgress) SetSection(HealingSectionName, _healing.Token());
            else _sections.Remove(HealingSectionName);
        }

        /// <summary>
        /// The lesson and Orsel's staff belong together, and the course only opens after the camp: a save with one
        /// without the other, or help before the camp was examined, is rejected instead of silently repaired.
        /// </summary>
        private bool RestoreHealingSection()
        {
            var state = new HealingCourseState();
            if (_sections.TryGetValue(HealingSectionName, out string token) && !HealingCourseState.TryRead(token, out state)) return false;
            if (state.Learned != Equipment.OwnsRegionalRelic(RegionalRelic.M_Heilerstab)) return false;
            if (state.Path != HealingCoursePath.None && HungerStage < 3) return false;
            _healing = state;
            return true;
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>Heals that took effect, fizzled or were cut short during the last <see cref="Step"/>; presentation only.</summary>
        public readonly List<HealResult> Heals = new List<HealResult>();
        public HealTargetKind HealTarget { get; private set; }
        public int HealTargetIndex { get; private set; } = -1;

        private bool InMillOrVillage(int act)
        { return !GameOver && !InJourney && !InWoodlandRegion && !InDungeon && Progression.EntryAct == act; }

        /// <summary>
        /// Where Orsel teaches: at the old mill while she lives there; once she has moved to the village, in front of the
        /// sick room as soon as it stands and only until the lesson is learned, so a player who passed the mill without
        /// it can still learn it. While she still asks for the sick room's lot, that talk has the place to itself.
        /// </summary>
        public bool TryGetOrselTeachingSpot(out long x, out long z)
        {
            x = z = 0;
            if (Progression.Houses.OrselInVillage)
            {
                if (!InMillOrVillage(2) || SiteOffered(VillagePlace.SickRoom) || Progression.Healing.Learned) return false;
                x = VillageBuildRules.AskerXmm(VillagePlace.SickRoom); z = VillageBuildRules.AskerZmm(VillagePlace.SickRoom);
                return true;
            }
            if (!InMillOrVillage(4)) return false;
            x = HealingRules.OrselMillXmm; z = HealingRules.OrselMillZmm;
            return true;
        }

        public bool OrselInReach
        {
            get
            {
                return TryGetOrselTeachingSpot(out long x, out long z) &&
                    DistanceSquared(PlayerXmm, PlayerZmm, x, z) <= FixedMath.Square(NpcInteractionRadiusMm) && CombatSight(PlayerXmm, PlayerZmm, x, z);
            }
        }

        /// <summary>Orsel teaches once the camp is examined and Ilyan's help is revealed, before the beds (owner 2026-11-23).</summary>
        public bool HealingCourseOpen { get { return Progression.HungerStage >= 3; } }

        /// <summary>What Orsel says when the player comes to her, from the persisted course state only.</summary>
        public string OrselCourseText
        {
            get
            {
                var course = Progression.Healing;
                if (!HealingCourseOpen) return "Orsel: Wenn du helfen willst, halt die Schüssel. Fragen kannst du hinterher. Sieh dir erst an, was hier fehlt.";
                if (course.Learned)
                    return course.PatientTreated ? "Orsel: Du hast es in den Händen. Übe weiter, an dir und an anderen. Und bring mir keine Leute, die du selbst aufgeschlitzt hast." :
                        "Orsel: Hand auf die Wunde, Stab fest, ruhig atmen. Versuch's an einem von denen in den Betten.";
                if (course.Path != HealingCoursePath.None) return "Orsel: Gut. Dann zeig ich dir, wie man eine Wunde schließt. Komm her.";
                return "Orsel: Mir fehlt alles. Brot für die Verletzten, Geld für Verbände, Aschenflieder für Umschläge. " +
                    "Hilf mir mit einem davon, dann zeig ich dir, wie man eine Wunde schließt. Mit einem Stab, nicht mit dem Schwert.";
            }
        }

        /// <summary>Can the player pay this way in right now? Only reads what is carried.</summary>
        public bool CanAffordCoursePath(HealingCoursePath path)
        {
            switch (path)
            {
                case HealingCoursePath.Supplies: return Progression.Social.Provisions >= HealingRules.SupplyRations;
                case HealingCoursePath.Payment: return Progression.Social.Coins >= HealingRules.PaymentCoins;
                case HealingCoursePath.Favour: return Progression.Crafting.VillageGiftCount(VillageGood.Herb) >= HealingRules.FavourHerbs;
                default: return false;
            }
        }

        /// <summary>Help Orsel one of three ways; the help must really be handed over before trust follows.</summary>
        public SocialResult HelpOrsel(HealingCoursePath path)
        {
            var r = new SocialResult { SpeakerId = 3 };
            var course = Progression.Healing;
            if (!OrselInReach) { r.Text = "Orsel ist nicht in Reichweite."; return r; }
            if (!HealingCourseOpen) { r.Text = OrselCourseText; return r; }
            if (course.Path != HealingCoursePath.None) { r.Text = "Orsel: Du hast schon geholfen. Mehr brauche ich nicht von dir."; return r; }
            bool handedOver;
            switch (path)
            {
                case HealingCoursePath.Supplies: handedOver = Progression.Social.SpendProvisions(HealingRules.SupplyRations); break;
                case HealingCoursePath.Payment: handedOver = Progression.Social.SpendCoins(HealingRules.PaymentCoins); break;
                case HealingCoursePath.Favour: handedOver = Progression.Crafting.TakeForVillage(VillageGood.Herb, HealingRules.FavourHerbs); break;
                default: return r;
            }
            if (!handedOver)
            {
                r.Text = path == HealingCoursePath.Supplies ? "Dir fehlen Rationen. Orsel braucht " + HealingRules.SupplyRations + ". Nichts wurde gegeben." :
                    path == HealingCoursePath.Payment ? "Dir fehlen Münzen. Orsel braucht " + HealingRules.PaymentCoins + ". Nichts wurde gegeben." :
                    "Dir fehlt Aschenflieder. Orsel braucht " + HealingRules.FavourHerbs + ". Nichts wurde gegeben.";
                return r;
            }
            course.Help(path);
            RememberService("player", "orsel", "healer.helped", "mill",
                path == HealingCoursePath.Supplies ? "rations" : path == HealingCoursePath.Payment ? "coins" : "herbs");
            r.Accepted = true;
            r.Text = path == HealingCoursePath.Supplies ? "Orsel: Brot. Endlich. Die da hinten haben seit gestern nichts gegessen." :
                path == HealingCoursePath.Payment ? "Orsel: Dafür bekomme ich Leinen für eine Woche. Gut." :
                "Orsel: Frisches Aschenflieder. Daraus mache ich Umschläge. Du hast ein Auge dafür.";
            return r;
        }

        /// <summary>The short practical lesson: unlock Wundschluss once, hand over Orsel's staff and her old robe.</summary>
        public SocialResult LearnWundschluss()
        {
            var r = new SocialResult { SpeakerId = 3 };
            var course = Progression.Healing;
            if (!OrselInReach) { r.Text = "Orsel ist nicht in Reichweite."; return r; }
            if (!course.Learn()) { r.Text = course.Learned ? "Orsel: Das habe ich dir schon gezeigt." : OrselCourseText; return r; }
            var equipment = Progression.Equipment;
            equipment.ClaimRegionalRelic(Progression, RegionalRelic.M_Heilerstab);
            equipment.SetRegionalRelicEquipped(RegionalRelic.M_Heilerstab, true);
            var robe = ArmorCatalog.Find("armor.cloth.robe.healer");
            if (Progression.Armor.Grant(robe) && Progression.Armor.Worn[(int)ArmorSlot.Chest] == null) Progression.Armor.Wear(robe);
            RememberService("orsel", "player", "lesson.taught", "mill", "wundschluss");
            r.Accepted = true;
            r.Text = "Orsel: Hand auf die Wunde, Stab fest, ruhig atmen. So schließt du sie. Nimm meinen alten Stab und die Robe. " +
                "In Eisen geht dir die Hälfte verloren. Versuch's an einem von denen in den Betten.";
            return r;
        }

        // ---------------------------------------------------------------- the spell

        /// <summary>Why Wundschluss cannot start now (null when it can), and whom it would heal.</summary>
        public string WundschlussBlocker(out HealTargetKind target, out int index)
        {
            target = HealTargetKind.None; index = -1;
            if (GameOver) return "Besiegt";
            if (!Progression.Healing.Learned) return "Wundschluss ist noch nicht erlernt";
            if (!CombatV2) return "Wundschluss gibt es nur im neuen Kampfsystem";
            if (ActionKind != CombatActionKind.None && ActionKind != CombatActionKind.Guard) return "Noch mitten in einer Bewegung";
            if (Progression.Healing.CooldownTicks > 0) return "Wundschluss klingt ab";
            if (HealingRules.StaffBp(Progression.Equipment.Weapon?.Definition) == 0) return "Wundschluss braucht einen Stab in der Hand";
            if (Resource < HealingRules.WundschlussFocus) return "Zu wenig Fokus";
            if (!ChooseHealTarget(out target, out index)) return "Niemand in Reichweite ist verletzt";
            return null;
        }

        public bool CanCastWundschluss { get { return WundschlussBlocker(out _, out _) == null; } }

        /// <summary>
        /// The most hurt by share of health: the player, the companion close by, a patient on the mill's beds. Ties go
        /// to the player, then the companion, then the lower bed. Nobody hurt, nobody chosen.
        /// </summary>
        private bool ChooseHealTarget(out HealTargetKind target, out int index)
        {
            target = HealTargetKind.None; index = -1;
            long bestMissing = 0, bestMax = 1;
            int missing = MaxPlayerHealth - PlayerHealth;
            if (PlayerHealth > 0 && missing > 0) { target = HealTargetKind.Self; bestMissing = missing; bestMax = MaxPlayerHealth; }
            var c = Progression.Companion;
            if (CompanionHealable(HealingRules.ReachMm))
            {
                missing = CompanionState.MaximumHealth - c.Health;
                if (target == HealTargetKind.None || (long)missing * bestMax > bestMissing * CompanionState.MaximumHealth)
                { target = HealTargetKind.Companion; bestMissing = missing; bestMax = CompanionState.MaximumHealth; }
            }
            for (int i = 0; i < HealingRules.PatientCount; i++)
            {
                if (!PatientHealable(i, HealingRules.ReachMm)) continue;
                missing = Progression.Healing.PatientWound(i);
                if (target == HealTargetKind.None || (long)missing * bestMax > bestMissing * HealingRules.PatientMaxHealth)
                { target = HealTargetKind.Patient; index = i; bestMissing = missing; bestMax = HealingRules.PatientMaxHealth; }
            }
            return target != HealTargetKind.None;
        }

        private bool CompanionHealable(long reach)
        {
            var c = Progression.Companion;
            return CompanionPresent && c.Status == CompanionStatus.Following && c.Health > 0 && c.Health < CompanionState.MaximumHealth &&
                DistanceSquared(PlayerXmm, PlayerZmm, c.Xmm, c.Zmm) <= FixedMath.Square(reach) && CombatSight(PlayerXmm, PlayerZmm, c.Xmm, c.Zmm);
        }

        /// <summary>A patient still lies at the mill (one bed is empty after the supply decision) and is still hurt.</summary>
        private bool PatientHealable(int patient, long reach)
        {
            if (!InMillOrVillage(4) || patient < HealingRules.PatientCount - HungerShelterPatients || Progression.Healing.PatientWound(patient) <= 0) return false;
            long x = HealingRules.PatientXmm(patient), z = HealingRules.PatientZmm(patient);
            return DistanceSquared(PlayerXmm, PlayerZmm, x, z) <= FixedMath.Square(reach) && CombatSight(PlayerXmm, PlayerZmm, x, z);
        }

        private bool TargetPosition(HealTargetKind target, int index, out long x, out long z)
        {
            x = PlayerXmm; z = PlayerZmm;
            if (target == HealTargetKind.Companion) { x = Progression.Companion.Xmm; z = Progression.Companion.Zmm; }
            else if (target == HealTargetKind.Patient) { x = HealingRules.PatientXmm(index); z = HealingRules.PatientZmm(index); }
            return target != HealTargetKind.None;
        }

        /// <summary>
        /// Start Wundschluss: the focus is paid now, the target is fixed now and shown during the windup; the heal comes
        /// on the tick after the windup, and only then the cooldown starts. A blow during the windup cuts it short
        /// without a refund. Holding the guard lowers it to cast.
        /// </summary>
        public HealResult CastWundschluss()
        {
            var r = new HealResult();
            string blocker = WundschlussBlocker(out var target, out int index);
            if (blocker != null) { r.Message = blocker; return r; }
            if (ActionKind == CombatActionKind.Guard) EndAction();
            Resource -= HealingRules.WundschlussFocus;
            HealTarget = target; HealTargetIndex = index;
            TargetPosition(target, index, out long x, out long z);
            if (target != HealTargetKind.Self) Face(x - PlayerXmm, z - PlayerZmm);
            BeginAction(CombatActionKind.Cast, HealingRules.Wundschluss, -1);
            r.Activated = true; r.Target = target; r.TargetIndex = index; r.TargetXmm = x; r.TargetZmm = z;
            r.Message = "Wundschluss: " + HealTargetName(target, index);
            return r;
        }

        public static string HealTargetName(HealTargetKind target, int index)
        {
            switch (target)
            {
                case HealTargetKind.Self: return "du selbst";
                case HealTargetKind.Companion: return "dein Begleiter";
                case HealTargetKind.Patient: return "Verletzte im Bett " + (index + 1).ToString(CultureInfo.InvariantCulture);
                default: return "niemand";
            }
        }

        /// <summary>The effect tick: the staff and armor in place now decide, overhealing is not counted.</summary>
        private void ResolveWundschluss()
        {
            var target = HealTarget; int index = HealTargetIndex;
            HealTarget = HealTargetKind.None; HealTargetIndex = -1;
            var r = new HealResult { Activated = true, Target = target, TargetIndex = index };
            TargetPosition(target, index, out r.TargetXmm, out r.TargetZmm);
            int staff = HealingRules.StaffBp(Progression.Equipment.Weapon?.Definition);
            bool reachable = target == HealTargetKind.Self ? PlayerHealth > 0 :
                target == HealTargetKind.Companion ? CompanionHealable(HealingRules.LandingReachMm) :
                target == HealTargetKind.Patient && PatientHealable(index, HealingRules.LandingReachMm);
            if (staff == 0 || !reachable)
            {
                r.Message = staff == 0 ? "Wundschluss verpufft: kein Stab in der Hand" : "Wundschluss verpufft: das Ziel ist außer Reichweite";
                Heals.Add(r);
                return;
            }
            int baseHealing = HealingRules.WundschlussHealing + Progression.ResolveHealBonus + Progression.PracticeHealingBonus;
            int amount = HealingRules.Healing(baseHealing, staff, Progression.WornHealingEfficiencyBp);
            int restored = 0;
            if (target == HealTargetKind.Self)
            {
                int before = PlayerHealth;
                PlayerHealth = Math.Min(MaxPlayerHealth, PlayerHealth + amount);
                restored = PlayerHealth - before;
            }
            else if (target == HealTargetKind.Companion)
            {
                var c = Progression.Companion;
                int before = c.Health;
                c.Health = Math.Min(CompanionState.MaximumHealth, c.Health + amount);
                restored = c.Health - before;
            }
            else
            {
                restored = Progression.Healing.TreatPatient(index, amount);
                if (restored > 0) RememberTreatment(index);
            }
            Progression.Healing.StartCooldown();
            // Practice only from health really restored: no practice at full health or for overhealing.
            if (restored > 0) Progression.RecordPractice(2, PracticeRules.ExperienceFromEffectiveAmount(restored));
            r.Amount = restored;
            r.Message = "Wundschluss: +" + restored.ToString(CultureInfo.InvariantCulture) + " für " + HealTargetName(target, index);
            Heals.Add(r);
        }

        /// <summary>A blow before the heal lands cuts the cast short: no heal, no cooldown, the focus is gone.</summary>
        private void InterruptWundschluss()
        {
            var r = new HealResult { Interrupted = true, Target = HealTarget, TargetIndex = HealTargetIndex, Message = "Wundschluss unterbrochen" };
            TargetPosition(HealTarget, HealTargetIndex, out r.TargetXmm, out r.TargetZmm);
            HealTarget = HealTargetKind.None; HealTargetIndex = -1;
            EndAction();
            Heals.Add(r);
        }

        /// <summary>
        /// The treated patient remembers the first treatment personally; Orsel sees it while she lives at the mill.
        /// Nobody else learns of it, and further casts on the same patient add no new memories.
        /// </summary>
        private void RememberTreatment(int patient)
        {
            var forge = Progression.Social.Forge;
            if (forge == null) return;
            string id = "mill.patient." + patient.ToString(CultureInfo.InvariantCulture);
            var mind = forge.Ensure(id, "villager");
            foreach (var known in mind.Known)
                if (known.Event.Kind == MemoryKind.Help && known.Event.Actor == "player") return;
            var ev = forge.Archive.Append(MemoryKind.Help, "player", id, forge.ProcessedTicks, 6000, false, "healing.wundschluss",
                new RelationshipChange(trust: 600, gratitude: 1200, familiarity: 300));
            var words = new EpisodeDetails("healing", "mill", "wundschluss");
            forge.Archive.Perceive(id, ev.Id, KnowledgeOrigin.Self, forge.ProcessedTicks, words);
            if (!Progression.Houses.OrselInVillage)
            {
                forge.Ensure("orsel", "healer");
                forge.Archive.Perceive("orsel", ev.Id, KnowledgeOrigin.Witnessed, forge.ProcessedTicks, words);
            }
        }

        private void MixHealing(ref ulong h)
        {
            if (Progression.Healing.HasProgress) { Mix(ref h, 0x4845414C494E47UL); Mix(ref h, Progression.Healing.StableHash); } // "HEALING"
            if (HealTarget != HealTargetKind.None) { Mix(ref h, (ulong)HealTarget); Mix(ref h, (ulong)(HealTargetIndex + 1)); }
        }
    }
}
