using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AreArpg.Core
{
    /// <summary>
    /// Immutable definition of a named enemy: its family, role and base health. Damage, speed, armour, reach and
    /// telegraph come from the role, so every member of a family inherits consistent behaviour. Loot keeps the
    /// existing Kind contract, so old receipts and the shipped loot catalog are untouched.
    /// </summary>
    public sealed class MonsterDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public MonsterFamily Family { get; }
        public MonsterRole Role { get; }
        public int BaseHealth { get; }
        /// <summary>Loot uses the existing Kind 0..3 so the shipped catalog and old receipts stay valid.</summary>
        public int LootKind { get; }

        public MonsterDefinition(string id, string displayName, MonsterFamily family, MonsterRole role, int baseHealth, int lootKind)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(displayName)) throw new ArgumentException("Monster id and name are required.");
            if (family == MonsterFamily.Legacy || role == MonsterRole.None) throw new ArgumentException("Named enemies need an explicit family and role.");
            if (baseHealth <= 0 || lootKind < 0 || lootKind > 3) throw new ArgumentException("Invalid monster health or loot kind.");
            Id = id; DisplayName = displayName; Family = family; Role = role; BaseHealth = baseHealth; LootKind = lootKind;
        }

        public bool Telegraphs { get { return MonsterRoles.TelegraphTicks(Role) > 0; } }
        public long MoveSpeedMmPerTick { get { return MonsterRoles.SpeedMmPerTick(Role, Family); } }
        public int AttackDamage { get { return MonsterRoles.AttackDamage(Role); } }
        public int ArmorBp { get { return MonsterRoles.ArmorBp(Role); } }
        public long AttackRangeMm { get { return MonsterRoles.AttackRangeMm(Role); } }
    }

    /// <summary>Immutable shipped enemy families. Publishing a changed definition requires a new revision.</summary>
    public static class MonsterCatalog
    {
        public const int Revision = 1;
        private static readonly MonsterDefinition[] Definitions =
        {
            // Orks: brute melee, a shaman that keeps its distance, an elite warrior.
            new MonsterDefinition("ork.grunt", "Ork", MonsterFamily.Ork, MonsterRole.Melee, 320, 0),
            new MonsterDefinition("ork.shaman", "Ork-Schamane", MonsterFamily.Ork, MonsterRole.Ranged, 260, 0),
            new MonsterDefinition("ork.warrior", "Ork-Krieger", MonsterFamily.Ork, MonsterRole.Elite, 520, 1),
            // Centaurs: a fast ranged skirmisher and an elite charger.
            new MonsterDefinition("centaur.archer", "Zentaur-Schütze", MonsterFamily.Centaur, MonsterRole.Ranged, 300, 0),
            new MonsterDefinition("centaur.charger", "Zentaur-Stürmer", MonsterFamily.Centaur, MonsterRole.Elite, 480, 1)
        };

        public static IReadOnlyList<MonsterDefinition> All { get { return Definitions; } }

        public static MonsterDefinition Definition(string id)
        {
            for (int i = 0; i < Definitions.Length; i++) if (Definitions[i].Id == id) return Definitions[i];
            return null;
        }

        /// <summary>Spawn one monster of a named family with an explicit id, position and depth-scaled health.</summary>
        public static MonsterState Spawn(string id, int monsterId, long x, long z, int depth)
        {
            var def = Definition(id);
            if (def == null) throw new ArgumentException("Unknown monster id: " + id, nameof(id));
            int scale = Math.Max(0, depth - 1) * 40;
            int health = def.BaseHealth + scale;
            return new MonsterState
            {
                Id = monsterId, Kind = def.LootKind, Family = def.Family, Role = def.Role,
                Xmm = x, Zmm = z, PreviousXmm = x, PreviousZmm = z, Health = health, MaxHealth = health
            };
        }

        /// <summary>Deterministic canonical signature of the shipped families (revision-bound).</summary>
        public static string Signature()
        {
            return string.Join(";", Definitions.Select(d => string.Join(",",
                d.Id, d.DisplayName, ((int)d.Family).ToString(CultureInfo.InvariantCulture),
                ((int)d.Role).ToString(CultureInfo.InvariantCulture), d.BaseHealth.ToString(CultureInfo.InvariantCulture),
                d.LootKind.ToString(CultureInfo.InvariantCulture))));
        }
    }
}
