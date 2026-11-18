using System;

namespace AreArpg.Core
{
    /// <summary>Enemy family (content grouping). Legacy is every monster spawned before roles existed.</summary>
    public enum MonsterFamily
    {
        Legacy = 0, Human = 1, Centaur = 2, Ork = 3, Undead = 4,
        BullRider = 5, Animal = 6, AshBrut = 7, Plague = 8
    }

    /// <summary>
    /// Combat role of a monster. None is the legacy behaviour (existing monsters keep their exact rules and world
    /// hash). The other roles are the new, explicit rules from #74.
    /// </summary>
    public enum MonsterRole
    {
        None = 0, Melee = 1, Ranged = 2, Shield = 3, Swarm = 4, Elite = 5
    }

    /// <summary>
    /// Deterministic role rules. Kept separate from MonsterState.Kind so legacy monsters (Role None) are byte- and
    /// hash-identical, while new roles get their own speed, damage, armour, reach and telegraph.
    /// </summary>
    public static class MonsterRoles
    {
        public static long SpeedMmPerTick(MonsterRole role)
        {
            switch (role)
            {
                case MonsterRole.Swarm: return 340L;
                case MonsterRole.Ranged: return 240L;
                case MonsterRole.Shield: return 220L;
                case MonsterRole.Elite: return 190L;
                default: return 260L;
            }
        }

        public static int AttackDamage(MonsterRole role)
        {
            switch (role)
            {
                case MonsterRole.Swarm: return 20;
                case MonsterRole.Ranged: return 45;
                case MonsterRole.Shield: return 50;
                case MonsterRole.Elite: return 105;
                default: return 40;
            }
        }

        /// <summary>Damage reduction in basis points, used when the player attacks this role.</summary>
        public static int ArmorBp(MonsterRole role)
        {
            switch (role)
            {
                case MonsterRole.Swarm: return 500;
                case MonsterRole.Ranged: return 1500;
                case MonsterRole.Shield: return 6000;
                case MonsterRole.Elite: return 4000;
                default: return 1000;
            }
        }

        /// <summary>Reach at which the role may start its attack.</summary>
        public static long AttackRangeMm(MonsterRole role) { return role == MonsterRole.Ranged ? 7000L : 1650L; }

        /// <summary>Windup ticks before the hit lands; 0 means an immediate hit (legacy melee).</summary>
        public static int TelegraphTicks(MonsterRole role)
        {
            switch (role)
            {
                case MonsterRole.Ranged: return 6;
                case MonsterRole.Shield: return 4;
                case MonsterRole.Elite: return 8;
                default: return 0;
            }
        }

        /// <summary>Radius the telegraph freezes; leaving it avoids the hit. A ranged shot covers its whole reach.</summary>
        public static long ImpactRadiusMm(MonsterRole role)
        {
            switch (role)
            {
                case MonsterRole.Elite: return 2600L;
                case MonsterRole.Ranged: return 7000L;
                default: return 1650L;
            }
        }

        public static int AttackCooldownTicks(MonsterRole role)
        {
            switch (role)
            {
                case MonsterRole.Swarm: return 12;
                case MonsterRole.Shield: return 20;
                case MonsterRole.Elite: return 18;
                case MonsterRole.Ranged: return 22;
                default: return 17;
            }
        }

        /// <summary>Ranged enemies hold their reach instead of closing to melee.</summary>
        public static bool KeepsDistance(MonsterRole role) { return role == MonsterRole.Ranged; }
    }
}
