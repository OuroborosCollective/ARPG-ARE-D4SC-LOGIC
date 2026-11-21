using System;

namespace AreArpg.Core
{
    /// <summary>
    /// A presentation-only 24-minute solar cycle projected from the existing 100-ms Core tick.
    /// Normal player profiles persist Forge.ProcessedTicks in their existing AC17+ checkpoint:
    /// no wall clock, Unity frame count, new save owner, or extra simulation clock is introduced.
    /// Legacy Core-only profiles without Forge use their current run tick, without save migration.
    /// </summary>
    public static class WorldSkyClock
    {
        public const ulong TicksPerDay = AshenCrypt.MemoryForge.PersonalCognition.TicksPerDay;

        public static ulong PhaseTicks(ulong sourceTicks)
        {
            return sourceTicks % TicksPerDay;
        }

        public static ulong CurrentPhaseTicks(ArenaSimulation simulation)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            var forge = simulation.Progression.Social.Forge;
            return PhaseTicks(forge != null ? forge.ProcessedTicks : simulation.CurrentTick);
        }
    }
}
