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
    }
}
