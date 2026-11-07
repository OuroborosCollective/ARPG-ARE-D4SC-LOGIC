using System;
using System.Collections.Generic;

namespace AreArpg.Core
{
    // This file has zero Unity dependencies so the same implementation is exercised in CI.
    public sealed class DeterministicRng
    {
        public ulong S0 { get; private set; }
        public ulong S1 { get; private set; }

        public DeterministicRng(ulong seed)
        {
            ulong x = seed;
            S0 = SplitMix64(ref x);
            S1 = SplitMix64(ref x);
            if (S0 == 0 && S1 == 0) S0 = 1;
        }

        private static ulong SplitMix64(ref ulong x)
        {
            unchecked
            {
                ulong z = (x += 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public ulong Next64()
        {
            unchecked
            {
                ulong a = S0;
                ulong b = S1;
                S0 = b;
                a ^= a << 23;
                S1 = a ^ b ^ (a >> 18) ^ (b >> 5);
                return S1 + b;
            }
        }

        public int NextBasisPoints() { return (int)(Next64() % 10000UL); }
    }

    public static class FixedMath
    {
        public const long BasisPoints = 10000L;
        public static long Clamp(long n, long min, long max) { return n < min ? min : (n > max ? max : n); }
        public static int ClampInt(int n, int min, int max) { return n < min ? min : (n > max ? max : n); }
        public static long Square(long n) { return checked(n * n); }

        // Binary search avoids Newton overflow at long.MaxValue.
        public static long IntegerSqrt(long n)
        {
            if (n <= 0) return 0;
            long lo = 1;
            long hi = Math.Min(n, 3037000499L);
            long answer = 0;
            while (lo <= hi)
            {
                long mid = lo + (hi - lo) / 2;
                if (mid <= n / mid) { answer = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return answer;
        }

        public static long Damage(long weapon, int powerBp, long mainStat, int additiveBp,
            long armor, int critChanceBp, DeterministicRng rng, out bool crit)
        {
            crit = rng.NextBasisPoints() < ClampInt(critChanceBp, 0, 10000);
            long damage = checked(weapon * powerBp) / BasisPoints;
            damage = checked(damage * (BasisPoints + mainStat * 10)) / BasisPoints;
            damage = checked(damage * (BasisPoints + additiveBp)) / BasisPoints;
            if (crit) damage = checked(damage * 15000) / BasisPoints;
            damage = checked(damage * BasisPoints) / (BasisPoints + Math.Max(0, armor));
            return Math.Max(1, damage);
        }
    }

    public sealed class MonsterState
    {
        public int Id;
        public int Kind; // 0: ghoul, 1: knight, 2: boss
        public long Xmm;
        public long Zmm;
        public long PreviousXmm;
        public long PreviousZmm;
        public int Health;
        public int MaxHealth;
        public int AttackCooldownTicks;
        public bool LootGranted;
        // The ember-warden's countdown is part of the authoritative replay state.
        public int BossWindupTicks;
        public int BossPulseCooldownTicks;
        public bool IsDead { get { return Health <= 0; } }
    }

    public sealed class LootState
    {
        public int Id;
        public long Xmm;
        public long Zmm;
        public int WeaponBonus;
    }

    public sealed class SkillResult
    {
        public bool Activated;
        public int TotalDamage;
        public int Hits;
        public int Kills;
        public string Message = "";
    }

    /// <summary>Standalone single-player simulation. Not a network server or a clone of any proprietary engine.</summary>
    public sealed class ArenaSimulation
    {
        public const int TickMs = 100;
        public const long ArenaLimitMm = 27000L;
        public const long PlayerSpeedMmPerTick = 620L;
        public const long MonsterSpeedMmPerTick = 260L;
        public const long PlayerRadiusMm = 450L;
        public const long PickupRadiusMm = 1600L;

        private readonly DeterministicRng _rng;
        private readonly ulong _runSeed;
        public System.Numerics.BigInteger CryptDepth { get; private set; }
        public int ChapterId { get { return CryptDepth.IsOne ? 1 : 2; } }
        public string ChapterName { get { return ChapterId == 1 ? "Ashen Crypt" : "Ember Rift"; } }
        public bool CryptCleared { get { return !GameOver && Monsters.TrueForAll(m => m.IsDead); } }
        private long _directionXbp;
        private long _directionZbp;
        private int _nextLootId = 1;
        public ulong CurrentTick { get; private set; }
        public long PlayerXmm { get; private set; }
        public long PlayerZmm { get; private set; }
        public long PrevPlayerXmm { get; private set; }
        public long PrevPlayerZmm { get; private set; }
        public int PlayerHealth { get; private set; }
        public int MaxPlayerHealth { get { return 1200 + Progression.VitalityHealthBonus; } }
        public int Resource { get; private set; } = 100;
        public CharacterProgression Progression { get; private set; }
        public int WeaponBonus { get { return Progression.WeaponBonus; } }
        public int Kills { get; private set; }
        public System.Numerics.BigInteger LootCollected { get { return Progression.LootCollected; } }
        public bool GameOver { get { return PlayerHealth == 0; } }
        public readonly List<MonsterState> Monsters = new List<MonsterState>();
        public readonly List<LootState> GroundLoot = new List<LootState>();
        public readonly int[] Cooldowns = new int[4];

        public ArenaSimulation(ulong seed, CharacterProgression progression = null)
            : this(seed, progression, System.Numerics.BigInteger.One) { }

        public ArenaSimulation(ulong seed, CharacterProgression progression,
            System.Numerics.BigInteger cryptDepth)
        {
            if (cryptDepth < System.Numerics.BigInteger.One)
                throw new ArgumentOutOfRangeException(nameof(cryptDepth));
            _runSeed = seed;
            CryptDepth = cryptDepth;
            Progression = progression ?? new CharacterProgression();
            PlayerHealth = MaxPlayerHealth;
            _rng = new DeterministicRng(seed);
            // Fixed bounds preserve finite combat arithmetic even for astronomical depth.
            int depthScale = (int)System.Numerics.BigInteger.Min(cryptDepth - 1, 10000);
            if (ChapterId == 1)
            {
                Monsters.Add(NewMonster(1, 0, 3600, 2400, 320));
                Monsters.Add(NewMonster(2, 1, -4800, 2400, 650));
                Monsters.Add(NewMonster(3, 2, 0, 8000, 2400));
            }
            else
            {
                // New chapter: ash hunter, ember knight and a distinct channeling boss.
                Monsters.Add(NewMonster(1, 0, -5000, 5500, 420 + depthScale * 25));
                Monsters.Add(NewMonster(2, 1, 6000, 4500, 770 + depthScale * 55));
                Monsters.Add(NewMonster(3, 3, 0, 12000, 2800 + depthScale * 200));
            }
        }

        /// <summary>Advance only after victory and all loot pickup; permanent character is reused.</summary>
        public ArenaSimulation TryAdvanceCrypt()
        {
            if (!CryptCleared || GroundLoot.Count != 0 ||
                CryptDepth != Progression.UnlockedCryptDepth) return null;
            CharacterProgression nextProgress;
            if (!CharacterProgression.TryLoad(Progression.SaveString(), out nextProgress) ||
                !nextProgress.TryUnlockNextCryptDepth(CryptDepth)) return null;
            return new ArenaSimulation(_runSeed, nextProgress,
                nextProgress.UnlockedCryptDepth);
        }

        private static MonsterState NewMonster(int id, int kind, long x, long z, int health)
        {
            return new MonsterState { Id = id, Kind = kind, Xmm = x, Zmm = z,
                PreviousXmm = x, PreviousZmm = z, Health = health, MaxHealth = health };
        }

        public void SetMoveInput(long xbp, long zbp)
        {
            xbp = FixedMath.Clamp(xbp, -10000, 10000);
            zbp = FixedMath.Clamp(zbp, -10000, 10000);
            long squared = xbp * xbp + zbp * zbp;
            if (squared < 40000) { _directionXbp = 0; _directionZbp = 0; return; }
            long magnitude = FixedMath.IntegerSqrt(squared);
            _directionXbp = xbp * 10000 / magnitude;
            _directionZbp = zbp * 10000 / magnitude;
        }

        private static long DistanceSquared(long ax, long az, long bx, long bz)
        {
            return FixedMath.Square(ax - bx) + FixedMath.Square(az - bz);
        }

        // The same obstacle specification is used by player and monster movement.
        public bool CanStand(long x, long z)
        {
            if (x < -ArenaLimitMm || x > ArenaLimitMm || z < -ArenaLimitMm || z > ArenaLimitMm) return false;
            long[] coordinates = { -7000, 7000 };
            foreach (long px in coordinates)
                foreach (long pz in coordinates)
                    if (DistanceSquared(x, z, px, pz) < FixedMath.Square(1200 + PlayerRadiusMm))
                        return false;
            return true;
        }

        private void MovePlayer()
        {
            long dx = _directionXbp * PlayerSpeedMmPerTick / 10000L;
            long dz = _directionZbp * PlayerSpeedMmPerTick / 10000L;
            if (CanStand(PlayerXmm + dx, PlayerZmm + dz)) { PlayerXmm += dx; PlayerZmm += dz; }
            else if (CanStand(PlayerXmm + dx, PlayerZmm)) PlayerXmm += dx;
            else if (CanStand(PlayerXmm, PlayerZmm + dz)) PlayerZmm += dz;
        }

        public void Step()
        {
            CurrentTick++;
            PrevPlayerXmm = PlayerXmm;
            PrevPlayerZmm = PlayerZmm;
            foreach (MonsterState monster in Monsters)
            {
                monster.PreviousXmm = monster.Xmm;
                monster.PreviousZmm = monster.Zmm;
            }
            for (int i = 0; i < Cooldowns.Length; i++)
                if (Cooldowns[i] > 0) Cooldowns[i]--;
            if (GameOver) return;
            MovePlayer();

            foreach (MonsterState enemy in Monsters)
            {
                if (enemy.IsDead) continue;
                if (enemy.AttackCooldownTicks > 0) enemy.AttackCooldownTicks--;
                if (enemy.BossPulseCooldownTicks > 0) enemy.BossPulseCooldownTicks--;
                long distance2 = DistanceSquared(enemy.Xmm, enemy.Zmm, PlayerXmm, PlayerZmm);
                if (distance2 > FixedMath.Square(14000)) continue;
                if (ChapterId == 2 && enemy.Kind == 3)
                {
                    // Explicit 8-tick warning (800 ms) before an avoidable fire shockwave.
                    if (enemy.BossWindupTicks > 0)
                    {
                        enemy.BossWindupTicks--;
                        if (enemy.BossWindupTicks == 0 &&
                            DistanceSquared(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm) <= FixedMath.Square(4000))
                            PlayerHealth = Math.Max(0, PlayerHealth - 190);
                        continue;
                    }
                    if (enemy.BossPulseCooldownTicks == 0 && distance2 <= FixedMath.Square(10000))
                    {
                        enemy.BossWindupTicks = 8;
                        enemy.BossPulseCooldownTicks = 55;
                        continue;
                    }
                }
                if (distance2 > FixedMath.Square(1650))
                {
                    long distance = Math.Max(1, FixedMath.IntegerSqrt(distance2));
                    long speed = enemy.Kind >= 2 ? 190 : MonsterSpeedMmPerTick;
                    long nx = enemy.Xmm + (PlayerXmm - enemy.Xmm) * speed / distance;
                    long nz = enemy.Zmm + (PlayerZmm - enemy.Zmm) * speed / distance;
                    if (CanStand(nx, nz)) { enemy.Xmm = nx; enemy.Zmm = nz; }
                    else if (CanStand(nx, enemy.Zmm)) enemy.Xmm = nx;
                    else if (CanStand(enemy.Xmm, nz)) enemy.Zmm = nz;
                }
                else if (enemy.AttackCooldownTicks == 0)
                {
                    enemy.AttackCooldownTicks = enemy.Kind >= 2 ? 12 : 17;
                    PlayerHealth = Math.Max(0, PlayerHealth - (enemy.Kind >= 2 ? 105 : (enemy.Kind == 1 ? 60 : 40)));
                    if (GameOver) break;
                }
            }
        }

        private MonsterState NearestMonster(long maxDistanceMm)
        {
            long bestDistance2 = FixedMath.Square(maxDistanceMm);
            MonsterState target = null;
            foreach (MonsterState enemy in Monsters)
            {
                if (enemy.IsDead) continue;
                long d2 = DistanceSquared(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm);
                if (d2 <= bestDistance2) { bestDistance2 = d2; target = enemy; }
            }
            return target;
        }

        private void DealDamage(MonsterState enemy, int powerBp, SkillResult result)
        {
            bool crit;
            long damage = FixedMath.Damage(95 + WeaponBonus, powerBp, 190,
                2000 + Progression.MightDamageBonusBp,
                enemy.Kind >= 2 ? 4000 : (enemy.Kind == 1 ? 2200 : 1000), 1900, _rng, out crit);
            enemy.Health = (int)Math.Max(0, enemy.Health - damage);
            result.TotalDamage += (int)damage;
            result.Hits++;
            if (enemy.IsDead && !enemy.LootGranted)
            {
                enemy.LootGranted = true;
                result.Kills++;
                Kills++;
                Progression.RecordKill();
                Progression.EarnExperience(new System.Numerics.BigInteger(
                    enemy.Kind >= 2 ? 1200 : (enemy.Kind == 1 ? 410 : 160)));
                GroundLoot.Add(new LootState { Id = _nextLootId++, Xmm = enemy.Xmm, Zmm = enemy.Zmm,
                    WeaponBonus = enemy.Kind >= 2 ? 34 : (enemy.Kind == 1 ? 17 : 8) });
            }
        }

        public SkillResult ActivateSkill(int slot)
        {
            var result = new SkillResult();
            if (GameOver) { result.Message = "Defeated - restart (R)"; return result; }
            if (slot < 0 || slot >= 4) { result.Message = "Unknown ability"; return result; }
            if (Cooldowns[slot] > 0) { result.Message = "Ability cooling down"; return result; }
            if (slot == 1 && Resource < 25) { result.Message = "Insufficient focus"; return result; }
            if (slot == 2)
            {
                PlayerHealth = Math.Min(MaxPlayerHealth, PlayerHealth + 170 + Progression.ResolveHealBonus);
                Cooldowns[2] = 50;
                result.Activated = true;
                result.Message = "Guard: restored 170 HP";
                return result;
            }
            long range = slot == 0 ? 3100 : (slot == 1 ? 4300 : 6500);
            MonsterState nearest = NearestMonster(range);
            if (nearest == null) { result.Message = "No enemy within reach"; return result; }
            if (slot == 0) { Resource = Math.Min(100, Resource + 12); Cooldowns[0] = 2; }
            if (slot == 1) { Resource -= 25; Cooldowns[1] = 4; }
            if (slot == 3) Cooldowns[3] = 100;
            result.Activated = true;
            if (slot == 3)
            {
                foreach (MonsterState enemy in Monsters)
                    if (!enemy.IsDead && DistanceSquared(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm) <= FixedMath.Square(range))
                        DealDamage(enemy, 30000, result);
            }
            else DealDamage(nearest, slot == 1 ? 16000 : 10000, result);
            result.Message = (slot == 3 ? "Ruin Storm" : (slot == 1 ? "Heavy Strike" : "Strike")) +
                ": " + result.TotalDamage + " damage / " + result.Kills + " kills";
            return result;
        }

        public bool TryCollectLoot()
        {
            if (GameOver) return false;
            for (int i = 0; i < GroundLoot.Count; i++)
            {
                LootState item = GroundLoot[i];
                if (DistanceSquared(PlayerXmm, PlayerZmm, item.Xmm, item.Zmm) > FixedMath.Square(PickupRadiusMm)) continue;
                Progression.AddWeaponUpgrade(item.WeaponBonus);
                GroundLoot.RemoveAt(i);
                return true;
            }
            return false;
        }

        // Stable FNV-1a over every field that currently affects future simulation outcomes.
        public ulong ComputeWorldHash()
        {
            ulong h = 14695981039346656037UL;
            Mix(ref h, CurrentTick); Mix(ref h, _rng.S0); Mix(ref h, _rng.S1);
            byte[] depthBytes = CryptDepth.ToByteArray();
            Mix(ref h, (ulong)depthBytes.Length);
            foreach (byte depthByte in depthBytes) Mix(ref h, depthByte);
            Mix(ref h, (ulong)PlayerXmm); Mix(ref h, (ulong)PlayerZmm);
            Mix(ref h, (ulong)PrevPlayerXmm); Mix(ref h, (ulong)PrevPlayerZmm);
            Mix(ref h, (ulong)_directionXbp); Mix(ref h, (ulong)_directionZbp);
            Mix(ref h, (ulong)PlayerHealth); Mix(ref h, (ulong)Resource);
            Mix(ref h, Progression.StableHash); Mix(ref h, (ulong)Kills);
            Mix(ref h, (ulong)_nextLootId);
            foreach (int cooldown in Cooldowns) Mix(ref h, (ulong)cooldown);
            Mix(ref h, (ulong)Monsters.Count);
            foreach (MonsterState enemy in Monsters)
            {
                Mix(ref h, (ulong)enemy.Id); Mix(ref h, (ulong)enemy.Kind);
                Mix(ref h, (ulong)enemy.Xmm); Mix(ref h, (ulong)enemy.Zmm);
                Mix(ref h, (ulong)enemy.PreviousXmm); Mix(ref h, (ulong)enemy.PreviousZmm);
                Mix(ref h, (ulong)enemy.Health); Mix(ref h, (ulong)enemy.MaxHealth);
                Mix(ref h, (ulong)enemy.AttackCooldownTicks); Mix(ref h, enemy.LootGranted ? 1UL : 0UL);
                Mix(ref h, (ulong)enemy.BossWindupTicks); Mix(ref h, (ulong)enemy.BossPulseCooldownTicks);
            }
            Mix(ref h, (ulong)GroundLoot.Count);
            foreach (LootState loot in GroundLoot)
            {
                Mix(ref h, (ulong)loot.Id); Mix(ref h, (ulong)loot.Xmm);
                Mix(ref h, (ulong)loot.Zmm); Mix(ref h, (ulong)loot.WeaponBonus);
            }
            return h;
        }

        private static void Mix(ref ulong h, ulong n)
        {
            unchecked
            {
                for (int i = 0; i < 8; i++)
                {
                    h ^= (byte)(n >> (i * 8));
                    h *= 1099511628211UL;
                }
            }
        }
    }
}
