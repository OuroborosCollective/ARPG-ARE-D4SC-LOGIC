using System;
using System.Numerics;
using AreArpg.Core;

/// <summary>Companion defeat consequences (#76): a badly wounded companion withdraws, can be rescued or treated,
/// stays unavailable for one to two game days and then returns. The confirmed lethal-zero death path is unchanged.
/// The recovery state is its own AC22 section; MemoryForge remembers rescue and loss.</summary>
internal static class CompanionDefeatTests
{
    private static ArenaSimulation Recruited()
    {
        var w = CompanionForgeTests.RescueFixture();
        if (!w.RecruitHunter().Accepted) throw new Exception("RecruitHunter refused");
        return w;
    }

    public static void Run(Action<bool, string> require)
    {
        // A badly wounded (but not lethally hit) companion withdraws instead of dying.
        var w = Recruited();
        var c = w.Progression.Companion;
        c.Status = CompanionStatus.Waiting; c.Health = 180;
        c.Xmm = c.PreviousXmm = 5000; c.Zmm = c.PreviousZmm = 9000;
        w.Monsters.Add(new MonsterState { Id = 92, Kind = 0, Xmm = 6000, Zmm = 9000, Health = 10000, MaxHealth = 10000 });
        w.Step();
        require(c.Status != CompanionStatus.Dead && c.Health > 0, "companion-wound-not-instant-death");
        require(w.Progression.CompanionRecovery.Wounded, "companion-wounded-flag-set");

        // A wounded companion takes no further damage and does not fight.
        int woundedHealth = c.Health;
        w.Step(); w.Step();
        require(c.Health == woundedHealth && w.Progression.CompanionRecovery.Wounded, "companion-wounded-ignores-further-hits");

        // Rescue requires real proximity; far away it is refused.
        var far = Recruited();
        far.Progression.CompanionRecovery.Wound(far.Progression.Companion, 2);
        far.Progression.Companion.Xmm = far.Progression.Companion.PreviousXmm = 25000;
        far.Progression.Companion.Zmm = far.Progression.Companion.PreviousZmm = 25000;
        require(!far.RescueCompanion().Accepted, "companion-rescue-refused-when-far");

        // A real rescue near the companion starts a one-to-two game-day recovery and is remembered.
        var rescue = Recruited();
        var rc = rescue.Progression.Companion;
        int events = rescue.Progression.Social.Forge.Archive.Events.Count;
        rescue.Progression.CompanionRecovery.Wound(rc, 2);
        rc.Xmm = rc.PreviousXmm = rescue.PlayerXmm + 1000; rc.Zmm = rc.PreviousZmm = rescue.PlayerZmm;
        var result = rescue.RescueCompanion();
        require(result.Accepted, "companion-rescue-accepted-nearby");
        int ticks = rescue.Progression.CompanionRecovery.RecoveryTicks;
        require(ticks >= (int)WorldSkyClock.TicksPerDay && ticks <= 2 * (int)WorldSkyClock.TicksPerDay,
            "companion-recovery-one-to-two-days");
        require(rescue.Progression.Social.Forge.Archive.Events.Count == events + 1, "companion-rescue-remembered");

        // Review 2026-11-18 (gitar-bot): rescue is once per wound; a second call adds no memory or reward.
        int afterFirst = rescue.Progression.Social.Forge.Archive.Events.Count;
        require(!rescue.RescueCompanion().Accepted &&
            rescue.Progression.Social.Forge.Archive.Events.Count == afterFirst, "companion-rescue-once-per-wound");

        // Review 2026-11-18 (gitar-bot): a companion dismissed before the wound stays put after recovery.
        var dismissed = Recruited();
        var dc = dismissed.Progression.Companion;
        dc.Status = CompanionStatus.Waiting;
        dismissed.Progression.CompanionRecovery.Wound(dc, 1);
        require(!dismissed.Progression.CompanionRecovery.ResumeFollowing, "companion-dismissed-does-not-resume-following");
        for (int i = 0; i < (int)WorldSkyClock.TicksPerDay; i++) dismissed.Step();
        require(dc.Status != CompanionStatus.Following, "companion-dismissed-stays-dismissed-after-recovery");

        // The recovery countdown runs on real ticks and the companion returns afterwards.
        for (int i = 0; i < ticks; i++) rescue.Step();
        require(!rescue.Progression.CompanionRecovery.Wounded && rescue.Progression.CompanionRecovery.RecoveryTicks == 0,
            "companion-returns-after-recovery");
        require(rescue.Progression.Companion.Health > 0, "companion-returns-with-health");

        // The confirmed lethal-zero path is unchanged: a hit that reaches zero still kills.
        var mortal = Recruited();
        var mc = mortal.Progression.Companion;
        mc.Status = CompanionStatus.Waiting; mc.Health = 40;
        mc.Xmm = mc.PreviousXmm = 5000; mc.Zmm = mc.PreviousZmm = 9000;
        mortal.Monsters.Add(new MonsterState { Id = 92, Kind = 0, Xmm = 6000, Zmm = 9000, Health = 10000, MaxHealth = 10000 });
        mortal.Step();
        require(mc.Status == CompanionStatus.Dead && mc.Health == 0, "companion-lethal-hit-still-kills");

        // Recovery state persists as its own AC22 section; a fresh character writes none.
        require(!new CharacterProgression().SaveString().StartsWith("AC22|"), "companion-fresh-save-stays-legacy");
        var carrier = Recruited();
        carrier.Progression.CompanionRecovery.Wound(carrier.Progression.Companion, 2);
        string saved = carrier.Progression.SaveString();
        require(saved.StartsWith("AC22|"), "companion-recovery-writes-section");
        require(CharacterProgression.TryLoad(saved, out var loaded) && loaded.CompanionRecovery.Wounded &&
            loaded.CompanionRecovery.RecoveryTicks == carrier.Progression.CompanionRecovery.RecoveryTicks,
            "companion-recovery-roundtrips");
        require(loaded.SaveString() == saved, "companion-recovery-canonical");

        // Owner rule 2026-11-18: Orsel revives a fallen companion three times at the mill sickbed; the third death
        // is permanent, and only the holy revive cloth (First-Aid 85+, huge rare resources, level-based roll) revives.
        var revived = Recruited();
        var rv = revived.Progression.Companion;
        require(CompanionRecoveryState.MaxRevivals == 3, "companion-three-revivals");
        for (int i = 0; i < 3; i++)
        {
            rv.Status = CompanionStatus.Dead; rv.Health = 0;
            revived.Progression.CompanionRecovery.MarkDeath();
            require(!revived.Progression.CompanionRecovery.PermanentlyDead, "companion-revival-not-permanent-" + i);
            require(revived.Progression.CompanionRecovery.ReviveAtMill(rv), "companion-orsel-revives-" + i);
            require(rv.Status == CompanionStatus.Waiting && rv.Health > 0, "companion-revived-awaiting-pickup-" + i);
        }
        require(revived.Progression.CompanionRecovery.RevivalsUsed == 3, "companion-three-revivals-used");

        // The fourth death is permanent.
        rv.Status = CompanionStatus.Dead; rv.Health = 0;
        revived.Progression.CompanionRecovery.MarkDeath();
        require(revived.Progression.CompanionRecovery.PermanentlyDead, "companion-fourth-death-permanent");
        require(!revived.Progression.CompanionRecovery.ReviveAtMill(rv), "companion-orsel-cannot-revive-permanent");

        // The holy revive cloth needs First-Aid 85+ and lots of rare resources; the attempt spends them either way.
        var cloth = new CharacterProgression();
        cloth.Crafting.AddMaterial(MaterialKind.Runenerz, 8); cloth.Crafting.AddMaterial(MaterialKind.Glutstein, 6);
        cloth.Crafting.AddMaterial(MaterialKind.Nachtbluete, 6); cloth.Crafting.AddMaterial(MaterialKind.Dornbeere, 4);
        require(!cloth.Crafting.CraftHolyReviveCloth(1UL), "companion-holy-cloth-needs-first-aid-85");
        require(cloth.Crafting.MaterialCount(MaterialKind.Runenerz) == 8, "companion-holy-cloth-unavailable-keeps-materials");
        cloth.Crafting.RecordExperience(CraftSkill.FirstAid, WeaponMasteryRules.ExperienceForLevel(85));
        require(cloth.Crafting.Level(CraftSkill.FirstAid) == 85, "companion-holy-cloth-first-aid-85-reached");
        // Deterministic: find a seed that succeeds and one that fails at 85.
        ulong goodSeed = 0; bool found = false;
        for (ulong s = 0; s < 400 && !found; s++) if (CraftingRules.HolyReviveSucceeds(85, s)) { goodSeed = s; found = true; }
        require(found, "companion-holy-cloth-has-a-success-seed");
        require(cloth.Crafting.CraftHolyReviveCloth(goodSeed), "companion-holy-cloth-succeeds-on-good-seed");
        require(cloth.Crafting.ReviveClothCount == 1, "companion-holy-cloth-crafted");
        require(cloth.Crafting.MaterialCount(MaterialKind.Runenerz) == 0, "companion-holy-cloth-spends-materials");
        // A failed attempt at 85 still spends the resources.
        var failCloth = new CharacterProgression();
        failCloth.Crafting.AddMaterial(MaterialKind.Runenerz, 8); failCloth.Crafting.AddMaterial(MaterialKind.Glutstein, 6);
        failCloth.Crafting.AddMaterial(MaterialKind.Nachtbluete, 6); failCloth.Crafting.AddMaterial(MaterialKind.Dornbeere, 4);
        failCloth.Crafting.RecordExperience(CraftSkill.FirstAid, WeaponMasteryRules.ExperienceForLevel(85));
        ulong badSeed = 0; bool foundBad = false;
        for (ulong s = 0; s < 400 && !foundBad; s++) if (!CraftingRules.HolyReviveSucceeds(85, s)) { badSeed = s; foundBad = true; }
        require(foundBad && !failCloth.Crafting.CraftHolyReviveCloth(badSeed) && failCloth.Crafting.ReviveClothCount == 0 &&
            failCloth.Crafting.MaterialCount(MaterialKind.Runenerz) == 0, "companion-holy-cloth-failed-attempt-spends-resources");

        // A revived companion still loads: the death stays a MemoryForge fact but the companion is alive.
        var reload = Recruited();
        var lc = reload.Progression.Companion;
        lc.Status = CompanionStatus.Dead; lc.Health = 0;
        reload.Progression.CompanionRecovery.MarkDeath();
        reload.Progression.CompanionRecovery.ReviveAtMill(lc);
        string revivedSave = reload.Progression.SaveString();
        require(CharacterProgression.TryLoad(revivedSave, out var revivedLoaded) &&
            revivedLoaded.Companion.Status == CompanionStatus.Waiting &&
            revivedLoaded.CompanionRecovery.RevivalsUsed == 1, "companion-revived-state-roundtrips");
    }
}
