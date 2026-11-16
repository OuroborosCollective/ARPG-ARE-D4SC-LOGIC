using System;
using AreArpg.Core;

internal static class WorldSkyClockTests
{
    internal static void Run(Action<bool, string> require)
    {
        require(WorldSkyClock.TicksPerDay == 14400UL, "world-sky-24-minutes-100ms");
        require(WorldSkyClock.PhaseTicks(0UL) == 0UL &&
                WorldSkyClock.PhaseTicks(14399UL) == 14399UL &&
                WorldSkyClock.PhaseTicks(14400UL) == 0UL &&
                WorldSkyClock.PhaseTicks(ulong.MaxValue) < WorldSkyClock.TicksPerDay,
                "world-sky-cycle-wrap-overflow-safe");
        var progress = CharacterProgression.CreateEntryProfile();
        progress.Social.EnableForge();
        var game = new ArenaSimulation(0xA11CE2026UL, progress);
        require(WorldSkyClock.CurrentPhaseTicks(game) == 0UL, "world-sky-fresh-no-wallclock");
        for (int i = 0; i < 23; i++) game.Step();
        require(WorldSkyClock.CurrentPhaseTicks(game) == 23UL, "world-sky-advances-only-with-core-tick");
        string saved = progress.SaveString();
        require(CharacterProgression.TryLoad(saved, out var loaded) && loaded.SaveString() == saved,
                "world-sky-persisted-checkpoint-canonical");
        var resumed = new ArenaSimulation(0xA11CE2026UL, loaded);
        require(WorldSkyClock.CurrentPhaseTicks(resumed) == 23UL,
                "world-sky-saved-ticks-restored");
        resumed.Step();
        require(WorldSkyClock.CurrentPhaseTicks(resumed) == 24UL, "world-sky-resumes-no-reset");
        var legacy = new ArenaSimulation(0xABCDEFUL);
        legacy.Step();
        require(WorldSkyClock.CurrentPhaseTicks(legacy) == 1UL,
                "world-sky-legacy-projection-without-mutating-save");
    }
}
