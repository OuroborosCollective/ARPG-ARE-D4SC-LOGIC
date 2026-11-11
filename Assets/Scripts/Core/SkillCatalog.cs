using System;

namespace AreArpg.Core
{
    /// <summary>Target selection is performed by ArenaSimulation, never Unity physics.</summary>
    public enum SkillTarget { Nearest, Self, Area }

    /// <summary>Read-only definition for an existing skill slot; units are ticks, millimetres and basis points.</summary>
    public sealed class SkillDefinition
    {
        public int Revision { get { return 1; } }
        public string Id { get; }
        public string DisplayName { get; }
        public int Slot { get; }
        public SkillTarget Target { get; }
        public int CooldownTicks { get; }
        public long RangeMm { get; }
        public int PowerBp { get; }
        public int FocusCost { get; }
        public int FocusGain { get; }
        public int BaseHealing { get; }

        /// <summary>Construct only shipped definitions; validate all finite gameplay units.</summary>
        internal SkillDefinition(string id, string name, int slot, SkillTarget target, int cooldown,
            long range, int power, int cost, int gain, int healing)
        {
            if (!LootCatalog.ValidId(id) || string.IsNullOrEmpty(name) || slot < 0 || slot >= 4 ||
                cooldown < 1 || range < 0 || power < 0 || cost < 0 || gain < 0 || healing < 0 ||
                target < SkillTarget.Nearest || target > SkillTarget.Area)
                throw new ArgumentException("Invalid skill definition");
            Id = id; DisplayName = name; Slot = slot; Target = target; CooldownTicks = cooldown;
            RangeMm = range; PowerBp = power; FocusCost = cost; FocusGain = gain; BaseHealing = healing;
        }
    }

    /// <summary>Version-one contract for the four existing abilities, preserving save slots and balance.</summary>
    public static class SkillCatalog
    {
        public const int Revision = 1;
        public const int Count = 4;
        private static readonly SkillDefinition[] Definitions = {
            new SkillDefinition("skill.strike", "Strike", 0, SkillTarget.Nearest, 2, 3100, 10000, 0, 12, 0),
            new SkillDefinition("skill.heavy.strike", "Heavy Strike", 1, SkillTarget.Nearest, 4, 4300, 16000, 25, 0, 0),
            new SkillDefinition("skill.guard", "Guard", 2, SkillTarget.Self, 50, 0, 0, 0, 0, 170),
            new SkillDefinition("skill.ruin.storm", "Ruin Storm", 3, SkillTarget.Area, 100, 6500, 30000, 0, 0, 0)
        };

        /// <summary>Return an immutable shipped definition, rejecting unsupported save slots.</summary>
        public static SkillDefinition Get(int slot)
        {
            if (slot < 0 || slot >= Count) throw new ArgumentOutOfRangeException(nameof(slot));
            return Definitions[slot];
        }
    }
}
