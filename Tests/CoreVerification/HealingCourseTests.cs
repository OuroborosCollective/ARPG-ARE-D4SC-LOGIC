using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>
/// Orsel's healing course (owner's Heilkunst draft, decisions of 2026-11-23): three ways in that cost 2 rations,
/// 12 coins or 3 Aschenflieder and lead to the same lesson, the course open after the camp, Wundschluss with 180 HP,
/// 15 focus and an 8 s cooldown, staff factor 80/100 %, armor factor, healing patients and practice only from health
/// really restored, and the course with Orsel's staff in the save.
/// </summary>
public static class HealingCourseTests
{
    public static void Run(Action<bool, string> check)
    {
        TheCourseOpensAfterTheCamp(check);
        EveryWayInCostsItsPriceAndLeadsToTheSameLesson(check);
        NothingIsTakenWhenSomethingIsMissing(check);
        WundschlussNeedsLessonStaffFocusAndSomebodyHurt(check);
        WundschlussHealsOnItsEffectTickAndThenCoolsDown(check);
        StaffAndArmorScaleTheHeal(check);
        FieldSuture(check);
        TheMostHurtIsHealedFirst(check);
        PatientsHealOnlyTheirRealWound(check);
        ABlowDuringTheWindupCutsItShort(check);
        TheCourseSurvivesTheSaveAndRejectsTampering(check);
        TheHealerStaffIsNeverLoot(check);
        SameInputsGiveTheSameWorld(check);
        OrselTeachesInTheVillageOnceSheHasMoved(check);
    }

    /// <summary>The mill after the camp was examined: stage 3, act 4, combat v2, creatures far away.</summary>
    private static ArenaSimulation MillAfterCamp()
    {
        var p = CharacterProgression.CreateEntryProfile();
        p.UseCombatV2();
        p.Social.EnableForge(); // the running game always has the NPC memory (ArenaRuntime)
        for (int i = 0; i < 3; i++) p.DiscoverHungerClue();
        p.TravelHunger(4, false);
        return Calm(new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1));
    }

    private static ArenaSimulation Calm(ArenaSimulation w)
    {
        foreach (var m in w.Monsters) { m.Xmm = 26000; m.Zmm = 26000; m.AttackCooldownTicks = 100000; }
        return w;
    }

    private static void ToOrsel(ArenaSimulation w)
    {
        HunterEncounterTests.Move(w, w.PlayerXmm, HealingRules.OrselMillZmm);
        HunterEncounterTests.Move(w, HealingRules.OrselMillXmm, HealingRules.OrselMillZmm);
    }

    /// <summary>Beside a bed, along the shelter's open middle (its posts stand at the corners).</summary>
    private static void ToBed(ArenaSimulation w, int bed) { HunterEncounterTests.Move(w, HealingRules.PatientXmm(bed), 4000); }

    private static ArenaSimulation Learned(HealingCoursePath path = HealingCoursePath.Supplies)
    {
        var w = MillAfterCamp();
        ToOrsel(w);
        if (!w.HelpOrsel(path).Accepted || !w.LearnWundschluss().Accepted) throw new Exception("Course fixture failed");
        return w;
    }

    private static ArenaSimulation Reload(ArenaSimulation w)
    {
        if (!CharacterProgression.TryLoad(w.Progression.SaveString(), out var p)) throw new Exception("Invalid healing save");
        return Calm(new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1));
    }

    private static void Hurt(ArenaSimulation w, int amount) { w.ReceiveBlowFrom(amount, w.PlayerXmm + 1000, w.PlayerZmm); }

    private static void Run(ArenaSimulation w, int ticks) { for (int i = 0; i < ticks; i++) w.Step(); }

    private static void TheCourseOpensAfterTheCamp(Action<bool, string> check)
    {
        var p = CharacterProgression.CreateEntryProfile();
        p.UseCombatV2();
        p.Social.EnableForge(); // the running game always has the NPC memory (ArenaRuntime)
        for (int i = 0; i < 2; i++) p.DiscoverHungerClue();
        p.TravelHunger(4, false);
        var w = Calm(new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1));
        ToOrsel(w);
        string before = w.Progression.SaveString();
        check(w.OrselInReach && !w.HealingCourseOpen && !w.HelpOrsel(HealingCoursePath.Supplies).Accepted &&
            w.Progression.SaveString() == before && w.OrselCourseText.Contains("was hier fehlt"), "healing-closed-before-the-camp");
        HunterEncounterTests.Move(w, -4000, HealingRules.OrselMillZmm); HunterEncounterTests.Move(w, -4000, -2000);
        check(w.ActOnHunger(HungerAction.InspectShelter).Accepted && w.HealingCourseOpen, "healing-camp-examined");
        check(!w.HelpOrsel(HealingCoursePath.Supplies).Accepted, "healing-only-in-reach");
        ToOrsel(w);
        check(w.HelpOrsel(HealingCoursePath.Supplies).Accepted && w.Progression.Healing.Path == HealingCoursePath.Supplies, "healing-open-after-the-camp-before-the-beds");
        check(w.Progression.HungerStage == 3, "healing-before-the-beds");
    }

    private static void EveryWayInCostsItsPriceAndLeadsToTheSameLesson(Action<bool, string> check)
    {
        string lesson = null, gear = null, armor = null;
        foreach (var path in new[] { HealingCoursePath.Supplies, HealingCoursePath.Payment, HealingCoursePath.Favour })
        {
            var w = MillAfterCamp(); var p = w.Progression;
            p.Social.ReceiveHunterCoins(p.Road); p.Crafting.AddMaterial(MaterialKind.Ashenflieder, 3);
            ToOrsel(w);
            BigInteger rations = p.Social.Provisions, coins = p.Social.Coins; int herbs = p.Crafting.MaterialCount(MaterialKind.Ashenflieder);
            check(!w.LearnWundschluss().Accepted && !p.Healing.Learned, "healing-no-lesson-before-help-" + path);
            check(w.HelpOrsel(path).Accepted, "healing-help-accepted-" + path);
            check(p.Social.Provisions == rations - (path == HealingCoursePath.Supplies ? 2 : 0) &&
                p.Social.Coins == coins - (path == HealingCoursePath.Payment ? 12 : 0) &&
                p.Crafting.MaterialCount(MaterialKind.Ashenflieder) == herbs - (path == HealingCoursePath.Favour ? 3 : 0), "healing-exact-price-" + path);
            string paid = p.SaveString();
            check(!w.HelpOrsel(HealingCoursePath.Supplies).Accepted && p.SaveString() == paid, "healing-help-only-once-" + path);
            check(w.LearnWundschluss().Accepted && p.Healing.Learned, "healing-lesson-" + path);
            var robe = ArmorCatalog.Find("armor.cloth.robe.healer");
            check(p.Equipment.OwnsRegionalRelic(RegionalRelic.M_Heilerstab) && p.Equipment.IsRegionalRelicEquipped(RegionalRelic.M_Heilerstab) &&
                p.Equipment.Weapon != null && p.Equipment.Weapon.Definition.Family == WeaponFamily.Staff && !p.Equipment.Weapon.Definition.TwoHanded &&
                p.Armor.Owned.Contains(robe) && p.Armor.Worn[(int)ArmorSlot.Chest] == robe, "healing-staff-and-robe-" + path);
            check(!w.LearnWundschluss().Accepted, "healing-lesson-only-once-" + path);
            string thisLesson = p.Healing.Token().Substring(4), thisGear = p.Equipment.Token(), thisArmor = p.Armor.Token();
            check(lesson == null || (lesson == thisLesson && gear == thisGear && armor == thisArmor), "healing-all-ways-lead-to-the-same-lesson-" + path);
            lesson = thisLesson; gear = thisGear; armor = thisArmor;
        }
    }

    private static void NothingIsTakenWhenSomethingIsMissing(Action<bool, string> check)
    {
        var w = MillAfterCamp(); var p = w.Progression;
        ToOrsel(w);
        p.Social.SpendProvisions((int)p.Social.Provisions - 1);
        string before = p.SaveString();
        check(p.Social.Provisions == 1 && !w.HelpOrsel(HealingCoursePath.Supplies).Accepted && !w.HelpOrsel(HealingCoursePath.Payment).Accepted &&
            !w.HelpOrsel(HealingCoursePath.Favour).Accepted && p.SaveString() == before && p.Healing.Path == HealingCoursePath.None, "healing-missing-price-changes-nothing");
        check(!w.CanAffordCoursePath(HealingCoursePath.Supplies) && !w.CanAffordCoursePath(HealingCoursePath.Payment), "healing-affordability-read-only");
    }

    private static void WundschlussNeedsLessonStaffFocusAndSomebodyHurt(Action<bool, string> check)
    {
        var w = MillAfterCamp();
        Hurt(w, 200);
        check(!w.CastWundschluss().Activated && w.WundschlussBlocker(out _, out _).Contains("nicht erlernt"), "healing-needs-the-lesson");
        w = Learned(); var p = w.Progression;
        HunterEncounterTests.Move(w, HealingRules.OrselMillXmm, -6000); // far from the beds
        check(w.WundschlussBlocker(out _, out _).Contains("Niemand"), "healing-needs-somebody-hurt");
        Hurt(w, 200);
        p.Equipment.SetRegionalRelicEquipped(RegionalRelic.M_Heilerstab, false);
        int focus = w.Resource;
        check(!w.CastWundschluss().Activated && w.Resource == focus && w.WundschlussBlocker(out _, out _).Contains("Stab"), "healing-needs-a-staff");
        p.Equipment.SetRegionalRelicEquipped(RegionalRelic.M_Heilerstab, true);
        check(w.CanCastWundschluss, "healing-castable-with-staff");
        Run(w, 1);
        // Heavy swings at the air pay their focus at the start: four of them empty the bar.
        for (int i = 0; i < 6 && w.Resource >= HealingRules.WundschlussFocus; i++) { w.ActivateSkill(1); Run(w, 14); }
        check(!w.CanCastWundschluss && w.WundschlussBlocker(out _, out _).Contains("Fokus"), "healing-needs-focus");
        check(HealingRules.StaffBp(null) == 0, "healing-no-staff-no-factor");
    }

    private static void WundschlussHealsOnItsEffectTickAndThenCoolsDown(Action<bool, string> check)
    {
        var w = Learned(); var p = w.Progression;
        HunterEncounterTests.Move(w, HealingRules.OrselMillXmm, -6000);
        Hurt(w, 400);
        int hp = w.PlayerHealth, focus = w.Resource;
        BigInteger practice = p.PracticeExperience(2);
        var start = w.CastWundschluss();
        check(start.Activated && start.Target == HealTargetKind.Self && w.Resource == focus - 15 && w.ActionKind == CombatActionKind.Cast, "healing-cast-starts-and-pays-focus");
        Run(w, 4);
        check(w.PlayerHealth == hp && w.Heals.Count == 0, "healing-nothing-during-the-windup");
        w.Step();
        int expected = HealingRules.Healing(HealingRules.WundschlussHealing + p.ResolveHealBonus + p.PracticeHealingBonus, 8000, 10000);
        check(w.Heals.Count == 1 && w.Heals[0].Amount == expected && w.PlayerHealth == hp + expected, "healing-lands-on-the-effect-tick");
        check(p.Healing.CooldownTicks == 80 && p.PracticeExperience(2) == practice + PracticeRules.ExperienceFromEffectiveAmount(expected), "healing-cooldown-and-practice");
        Run(w, 4);
        check(w.ActionKind == CombatActionKind.None && !w.CastWundschluss().Activated, "healing-cooldown-blocks");
        Run(w, 80);
        check(p.Healing.CooldownTicks == 0 && w.CanCastWundschluss, "healing-ready-after-eight-seconds");
    }

    private static void FieldSuture(Action<bool, string> check)
    {
        foreach (var staff in new[] { RegionalRelic.K_Kurzstab, RegionalRelic.K_Langstab })
        foreach (int worn in new[] { 3, 4, 7, 8 })
        {
            var w = Learned();
            var p = w.Progression;
            foreach (var id in RusskantorSet.Pieces) p.Equipment.ClaimRegionalRelic(p, id);
            for (int i = 0; i < worn; i++) w.TrySetRegionalRelicEquipped(RusskantorSet.Pieces[i], true);
            w.TrySetRegionalRelicEquipped(staff, true);
            Hurt(w, 500);
            int hp = w.PlayerHealth;
            int bonus = worn == 8 ? 20 : worn >= 4 ? 10 : 0;
            int expected = HealingRules.Healing(180 + p.ResolveHealBonus + p.PracticeHealingBonus + bonus,
                staff == RegionalRelic.K_Kurzstab ? 8000 : 10000, p.WornHealingEfficiencyBp);
            check(w.CastWundschluss().Target == HealTargetKind.Self, "field-suture-self-target");
            Run(w, 5);
            check(w.PlayerHealth == hp + expected && w.Heals[0].Amount == expected, "field-suture-impact-" + staff + "-" + worn);
        }

        var swap = Learned();
        var gear = swap.Progression.Equipment;
        foreach (var id in RusskantorSet.Pieces)
        {
            gear.ClaimRegionalRelic(swap.Progression, id);
            if (id != RegionalRelic.K_Langstab) swap.TrySetRegionalRelicEquipped(id, true);
        }
        Hurt(swap, 500);
        check(swap.CastWundschluss().Activated, "field-suture-start-before-swap");
        Run(swap, 4);
        swap.TrySetRegionalRelicEquipped(RegionalRelic.M_Heilerstab, true);
        swap.Step();
        int ordinary = HealingRules.Healing(180 + swap.Progression.ResolveHealBonus, 8000, swap.Progression.WornHealingEfficiencyBp);
        check(swap.Heals[0].Amount == ordinary, "field-suture-checks-staff-on-impact");

        var patientWorld = Learned();
        var healer = patientWorld.Progression;
        foreach (var id in RusskantorSet.Pieces)
        {
            healer.Equipment.ClaimRegionalRelic(healer, id);
            if (id != RegionalRelic.K_Langstab) patientWorld.TrySetRegionalRelicEquipped(id, true);
        }
        ToBed(patientWorld, 0);
        HunterEncounterTests.Move(patientWorld, HealingRules.PatientXmm(0) + 2000, 4000);
        check(patientWorld.CastWundschluss().Target == HealTargetKind.Patient, "field-suture-patient-target");
        Run(patientWorld, 5);
        check(patientWorld.Heals[0].Amount == 160 && healer.Healing.PatientWound(0) == 0, "field-suture-closes-first-wound");
    }

    private static void StaffAndArmorScaleTheHeal(Action<bool, string> check)
    {
        check(HealingRules.StaffBp(RegionalRelics.HandDefinition(RegionalRelic.M_Heilerstab)) == 8000, "healing-one-hand-staff-80");
        var twoHand = Enumerable.Range(0, WeaponCatalog.Count).Select(WeaponCatalog.Get).First(d => d.Family == WeaponFamily.Staff);
        check(twoHand.TwoHanded && HealingRules.StaffBp(twoHand) == 10000, "healing-loot-staffs-are-two-handed-100");
        check(HealingRules.Healing(180, 8000, 10000) == 144 && HealingRules.Healing(180, 10000, 2500) == 45 &&
            HealingRules.Healing(180, 8000, 7000) == 100, "healing-formula-rounds-once");
        var w = Learned(); var p = w.Progression;
        var plate = ArmorCatalog.Find("armor.plate.cuirass");
        p.Armor.Grant(plate); p.Armor.Wear(plate);
        check(p.Armor.HealingEfficiencyBp == 7000, "healing-plate-chest-weighs-forty-percent");
        HunterEncounterTests.Move(w, HealingRules.OrselMillXmm, -6000);
        Hurt(w, 500);
        int hp = w.PlayerHealth;
        w.CastWundschluss(); Run(w, 5);
        int expected = HealingRules.Healing(HealingRules.WundschlussHealing + p.ResolveHealBonus + p.PracticeHealingBonus, 8000, 7000);
        check(w.PlayerHealth - hp == expected, "healing-armor-lowers-the-heal");
    }

    private static void TheMostHurtIsHealedFirst(Action<bool, string> check)
    {
        var w = Learned();
        // Next to the second bed (wound 220 of 300) and at full health: the patient.
        ToBed(w, 1);
        check(w.WundschlussBlocker(out var target, out int index) == null && target == HealTargetKind.Patient && index == 1, "healing-patient-when-unhurt");
        Hurt(w, w.MaxPlayerHealth * 9 / 10);
        check(w.WundschlussBlocker(out target, out _) == null && target == HealTargetKind.Self, "healing-self-when-more-hurt");
    }

    private static void PatientsHealOnlyTheirRealWound(Action<bool, string> check)
    {
        // From Orsel's place only the westmost bed (wound 110 of 300) is within reach; the second bed is more hurt.
        var w = Learned(); var p = w.Progression;
        BigInteger practice = p.PracticeExperience(2);
        int wound = p.Healing.PatientWound(2);
        var start = w.CastWundschluss();
        check(start.Activated && start.Target == HealTargetKind.Patient && start.TargetIndex == 2, "healing-targets-the-patient");
        Run(w, 5);
        check(p.Healing.PatientWound(2) == 0 && w.Heals[0].Amount == wound && p.Healing.PatientTreated &&
            p.PracticeExperience(2) == practice + PracticeRules.ExperienceFromEffectiveAmount(wound), "healing-patient-no-overheal-practice");
        var patient = p.Social.Forge.Archive.Get("mill.patient.2");
        check(patient != null && patient.Known.Any(k => k.Event.Actor == "player" && k.Event.Kind == AshenCrypt.MemoryForge.MemoryKind.Help), "healing-patient-remembers");
        Run(w, 90);
        check(!w.WundschlussBlocker(out var target, out int index).Contains("Abkling") && !(target == HealTargetKind.Patient && index == 2), "healing-healed-wound-stays-closed");
    }

    private static void ABlowDuringTheWindupCutsItShort(Action<bool, string> check)
    {
        var w = Learned(); var p = w.Progression;
        HunterEncounterTests.Move(w, HealingRules.OrselMillXmm, -6000);
        Hurt(w, 300);
        int focus = w.Resource;
        w.CastWundschluss(); w.Step();
        int hp = w.PlayerHealth;
        Hurt(w, 50);
        check(w.ActionKind == CombatActionKind.None && w.Heals.Any(h => h.Interrupted) && w.Resource == focus - 15 && p.Healing.CooldownTicks == 0, "healing-interrupted-no-refund-no-cooldown");
        Run(w, 6);
        check(w.PlayerHealth <= hp, "healing-interrupted-heals-nothing");
    }

    private static void TheCourseSurvivesTheSaveAndRejectsTampering(Action<bool, string> check)
    {
        var fresh = MillAfterCamp();
        check(!fresh.Progression.SaveString().Contains("healing="), "healing-no-section-without-progress");
        var w = Learned(HealingCoursePath.Favour.Equals(HealingCoursePath.Favour) ? HealingCoursePath.Supplies : HealingCoursePath.Supplies);
        ToBed(w, 2);
        w.CastWundschluss(); Run(w, 5);
        var back = Reload(w);
        check(back.Progression.Healing.Token() == w.Progression.Healing.Token() && back.Progression.SaveString() == w.Progression.SaveString() &&
            back.Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.M_Heilerstab), "healing-save-roundtrip");
        check(HealingCourseState.TryRead("1;0;1;0;0;160,220,110", out _) == false, "healing-reject-lesson-without-help");
        check(HealingCourseState.TryRead("1;1;0;5;0;160,220,110", out _) == false, "healing-reject-cooldown-without-lesson");
        check(HealingCourseState.TryRead("1;1;1;0;0;160,220,999", out _) == false, "healing-reject-wound-above-authored");
        check(HealingCourseState.TryRead("1;1;1;0;1;160,220,0", out _) && HealingCourseState.TryRead("1;2;0;0;0;160,220,110", out _), "healing-accept-valid-tokens");
    }

    private static void TheHealerStaffIsNeverLoot(Action<bool, string> check)
    {
        check(WeaponCatalog.Count == 36 && WeaponCatalog.Revision == 1 && Enumerable.Range(0, WeaponCatalog.Count).All(i => WeaponCatalog.Get(i).Id != "M_Heilerstab"), "healing-staff-outside-the-loot-catalog");
        var w = MillAfterCamp();
        check(RegionalRelics.GrantedOnly(RegionalRelic.M_Heilerstab) && !w.TryGetRegionalRelicSource(RegionalRelic.M_Heilerstab, out _, out _), "healing-staff-not-lying-in-the-world");
        ToOrsel(w);
        check(!w.ActOnHunger(HungerAction.ClaimRegionalRelic).Accepted || !w.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.M_Heilerstab), "healing-staff-not-claimable");
    }

    /// <summary>A player who passed the mill without the lesson finds Orsel in front of the finished sick room.</summary>
    private static void OrselTeachesInTheVillageOnceSheHasMoved(Action<bool, string> check)
    {
        var p = CharacterProgression.CreateEntryProfile();
        p.UseCombatV2();
        for (int i = 0; i < 3; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect);
        p.TravelHunger(4, false);
        p.DiscoverHungerClue();
        if (!p.Village.UnlockOrselTeaches(p)) throw new Exception("Mill quest fixture");
        p.Houses.Complete(VillagePlace.SickRoom);
        p.TravelHunger(2, false);
        p.Social.EnableForge();
        var w = Calm(new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1));
        if (w.ArrivalScenePending) w.CompleteArrivalScene();
        long x = VillageBuildRules.AskerXmm(VillagePlace.SickRoom), z = VillageBuildRules.AskerZmm(VillagePlace.SickRoom);
        check(!w.SiteOffered(VillagePlace.SickRoom) && w.TryGetOrselTeachingSpot(out long sx, out long sz) && sx == x && sz == z,
            "healing-village-spot-before-the-sick-room");
        VillagePlaceTests.Walk(w, x + 900, z + 500);
        check(w.OrselInReach && w.HelpOrsel(HealingCoursePath.Supplies).Accepted && w.LearnWundschluss().Accepted &&
            w.Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.M_Heilerstab), "healing-village-lesson");
        check(!w.OrselInReach && !w.TryGetOrselTeachingSpot(out _, out _), "healing-village-spot-gone-after-the-lesson");
        var back = Reload(w);
        check(back.Progression.Healing.Learned && back.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.M_Heilerstab), "healing-village-lesson-saved");
    }

    private static void SameInputsGiveTheSameWorld(Action<bool, string> check)
    {
        ulong Play()
        {
            var w = Learned();
            ToBed(w, 1);
            w.CastWundschluss(); Run(w, 3);
            return w.ComputeWorldHash();
        }
        check(Play() == Play(), "healing-deterministic-world-hash");
    }
}
