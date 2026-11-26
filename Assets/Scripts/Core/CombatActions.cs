using System;
using System.Collections.Generic;

namespace AreArpg.Core
{
    public enum CombatActionKind { None = 0, Strike = 1, HeavyStrike = 2, Ruin = 3, Dodge = 4, Guard = 5, Drink = 6, Cast = 7 }
    public enum CombatPhase { Idle = 0, Windup = 1, Active = 2, Recovery = 3 }

    /// <summary>Ticks of one action: windup, active (the impact or the invulnerable dodge) and recovery, plus stamina.</summary>
    public readonly struct ActionTiming
    {
        public readonly int Startup, Active, Recovery, Stamina;
        public ActionTiming(int startup, int active, int recovery, int stamina)
        { Startup = startup; Active = active; Recovery = recovery; Stamina = stamina; }
        public int Total { get { return Startup + Active + Recovery; } }
    }

    /// <summary>
    /// Combat v2 rule table (issue #127, first revision): the start values of the issue's tables, as integer ticks of
    /// 100 ms. Weapon archetypes come from <see cref="WeaponDefinition.Family"/> and <see cref="WeaponDefinition.TwoHanded"/>;
    /// damage still comes from the existing weapon profiles and skills. Values are calibration candidates, not tested balance.
    /// </summary>
    public static class CombatV2Rules
    {
        public const int Revision = 1;
        public const int StaminaMax = 100, StaminaRegenPerTick = 6, StaminaRegenDelayTicks = 5;
        public static readonly ActionTiming Dodge = new ActionTiming(1, 3, 4, 25);
        public const long DodgeSpeedMmPerTick = 350;
        public static readonly ActionTiming Drink = new ActionTiming(5, 1, 3, 0);
        public static readonly ActionTiming Ruin = new ActionTiming(6, 1, 8, 20);
        public const int GuardRaiseTicks = 1, GuardHoldStaminaPerTick = 3, GuardLowerTicks = 1, GuardBreakTicks = 6;
        /// <summary>Exploration keeps 6.2 m/s; while fighting the base is 4.6 m/s, both scaled by the carried weapon.</summary>
        public const long CombatSpeedMmPerTick = 460;
        public const long CombatRadiusMm = 12000;
        /// <summary>Steering during windup, recovery, drinking and a raised guard; none during an attack's impact tick.</summary>
        public const int SlowSteeringBp = 4000;
        /// <summary>Half-angle of the guard: a shield covers 120 degrees in front, a parrying weapon 90 degrees.</summary>
        public const int ShieldGuardCosBp = 5000, WeaponGuardCosBp = 7071;
        /// <summary>Without a shield a deflected hit still lets 30 % through; a broken guard lets 50 % through.</summary>
        public const int DeflectChipBp = 3000, GuardBreakChipBp = 5000;

        /// <summary>
        /// Enemies that used to hit at once (issue #127 phase-4 table): ghouls and plain melee 5/1/11, knights 6/1/10, the
        /// heavy crypt boss 9/1/6. The blow lands only within reach of the swing, so a step back or a dodge avoids it.
        /// </summary>
        public static int EnemyWindupTicks(MonsterRole role, int kind)
        { return role != MonsterRole.None ? 5 : kind == 1 ? 6 : kind >= 2 ? 9 : 5; }
        public static int EnemyRecoveryTicks(MonsterRole role, int kind)
        { return role != MonsterRole.None ? 11 : kind == 1 ? 10 : kind >= 2 ? 6 : 11; }
        public const long EnemyImpactReachMm = 2100;

        /// <summary>Primary strike of the carried weapon (issue #127 table; one-handed mace like the one-handed axe).</summary>
        public static ActionTiming Primary(WeaponDefinition weapon)
        {
            if (weapon == null) return new ActionTiming(2, 1, 3, 12);
            switch (weapon.Family)
            {
                case WeaponFamily.Dagger: return new ActionTiming(2, 1, 2, 12);
                case WeaponFamily.Sword: return weapon.TwoHanded ? new ActionTiming(4, 1, 5, 27) : new ActionTiming(2, 1, 3, 17);
                case WeaponFamily.Axe: return weapon.TwoHanded ? new ActionTiming(5, 1, 6, 34) : new ActionTiming(3, 1, 4, 20);
                case WeaponFamily.Mace: return weapon.TwoHanded ? new ActionTiming(4, 1, 6, 31) : new ActionTiming(3, 1, 4, 20);
                case WeaponFamily.Spear: return new ActionTiming(3, 1, 5, 22);
                case WeaponFamily.Bow: return new ActionTiming(4, 1, 5, 20);
                case WeaponFamily.Staff: return new ActionTiming(3, 1, 5, 24);
                default: return new ActionTiming(2, 1, 3, 12);
            }
        }

        /// <summary>Heavy strike: two ticks longer windup and recovery and half again the stamina of the primary strike.</summary>
        public static ActionTiming Heavy(WeaponDefinition weapon)
        {
            var p = Primary(weapon);
            return new ActionTiming(p.Startup + 2, p.Active, p.Recovery + 2, (p.Stamina * 3 + 1) / 2);
        }

        /// <summary>Stamina a blocked hit costs: a quarter of its damage, eased by the shield level like the old block curve.</summary>
        public static int GuardCost(int damage, bool shield, int shieldLevel)
        {
            int baseCost = Math.Max(1, (damage + 3) / 4);
            if (!shield) return (baseCost * 3 + 1) / 2;
            int ease = Math.Min(2475, WeaponProfiles.BlockChancePerLevelBp * Math.Max(1, shieldLevel));
            return Math.Max(1, (int)(((long)baseCost * (10000 - ease) + 9999) / 10000));
        }
    }

    /// <summary>
    /// Combat v2 for the player (issue #127, phase 1): one action at a time, each with windup, active and recovery ticks,
    /// stamina, dodging with invulnerable ticks, a held guard and drinking that only takes effect on its effect tick.
    /// Owner decision 2026-11-23: skill slot 2 no longer heals for free; healing comes from potions (and later Orsel's
    /// Wundschluss). Only a progression switched to combat version 2 uses these rules; every other world keeps the
    /// legacy instant combat and its golden hashes.
    /// </summary>
    public sealed partial class ArenaSimulation
    {
        public bool CombatV2 { get { return Progression.CombatVersion >= 2; } }
        public CombatActionKind ActionKind { get; private set; }
        public int ActionSlot { get; private set; } = -1;
        /// <summary>1-based tick of the running action; 0 while idle.</summary>
        public int ActionTick { get; private set; }
        public ulong ActionSequence { get; private set; }
        public int Stamina { get; private set; } = CombatV2Rules.StaminaMax;
        /// <summary>Ticks until stamina refills again: five full ticks pass after the last spending (issue #127).</summary>
        public int StaminaDelayTicks
        { get { return !_staminaSpent ? 0 : (int)Math.Max(0L, (long)_staminaSpentTick + CombatV2Rules.StaminaRegenDelayTicks - (long)CurrentTick); } }
        public bool GuardHeld { get; private set; }
        /// <summary>The way the player faces, as a direction of length 10000.</summary>
        public long FacingXbp { get; private set; }
        public long FacingZbp { get; private set; } = 10000;
        public int DodgedAttacks { get; private set; }
        /// <summary>Strike results that landed during the last <see cref="Step"/>; the presentation reads them after the tick.</summary>
        public readonly List<SkillResult> Impacts = new List<SkillResult>();
        private ActionTiming _timing;
        private long _dodgeXbp, _dodgeZbp;
        private PotionType _drinkType;
        private bool _guardReleased, _staminaSpent;
        private ulong _staminaSpentTick;

        public CombatPhase ActionPhase
        {
            get
            {
                if (ActionKind == CombatActionKind.None) return CombatPhase.Idle;
                if (ActionKind == CombatActionKind.Guard)
                    return ActionTick <= CombatV2Rules.GuardRaiseTicks && !_guardReleased ? CombatPhase.Windup : _guardReleased ? CombatPhase.Recovery : CombatPhase.Active;
                if (ActionTick <= _timing.Startup) return CombatPhase.Windup;
                return ActionTick <= _timing.Startup + _timing.Active ? CombatPhase.Active : CombatPhase.Recovery;
            }
        }

        /// <summary>The dodge's active ticks make the player untouchable.</summary>
        public bool Invulnerable { get { return ActionKind == CombatActionKind.Dodge && ActionPhase == CombatPhase.Active; } }
        public bool GuardRaised { get { return ActionKind == CombatActionKind.Guard && ActionPhase == CombatPhase.Active; } }

        /// <summary>Is a living enemy close enough to count as a fight (slower combat movement)?</summary>
        public bool InCombat
        {
            get
            {
                if (ActionKind != CombatActionKind.None) return true;
                foreach (var m in Monsters)
                    if (!m.IsDead && (m.Kind != 4 || Progression.Road.Started) &&
                        DistanceSquared(m.Xmm, m.Zmm, PlayerXmm, PlayerZmm) <= FixedMath.Square(CombatV2Rules.CombatRadiusMm)) return true;
                return false;
            }
        }

        private SkillResult StartSkillAction(int slot)
        {
            var result = new SkillResult();
            var skill = SkillCatalog.Get(slot);
            if (slot == 2) { result.Message = "Kein freies Heilen mehr: Halte den Schutz-Knopf, um zu blocken."; return result; }
            if (ActionKind != CombatActionKind.None) { result.Message = "Noch mitten in einer Bewegung"; return result; }
            if (Cooldowns[slot] > 0) { result.Message = "Ability cooling down"; return result; }
            if (Resource < skill.FocusCost) { result.Message = "Insufficient focus"; return result; }
            if (Stamina <= 0) { result.Message = "Keine Ausdauer"; return result; }
            var weapon = Progression.Equipment.Weapon?.Definition;
            var timing = slot == 0 ? CombatV2Rules.Primary(weapon) : slot == 1 ? CombatV2Rules.Heavy(weapon) : CombatV2Rules.Ruin;
            Resource -= skill.FocusCost;
            Cooldowns[slot] = AttackCooldown(slot);
            SpendStamina(timing.Stamina);
            MonsterState target = NearestMonster(AttackRange(slot));
            if (target != null)
            {
                Face(target.Xmm - PlayerXmm, target.Zmm - PlayerZmm);
                result.HasTarget = true; result.TargetXmm = target.Xmm; result.TargetZmm = target.Zmm; // presentation facing only
            }
            else if (AschbeterInReach(AttackRange(slot)))
            {
                Face(Aschbeter.Xmm - PlayerXmm, Aschbeter.Zmm - PlayerZmm);
                result.HasTarget = true; result.TargetXmm = Aschbeter.Xmm; result.TargetZmm = Aschbeter.Zmm;
            }
            BeginAction(slot == 0 ? CombatActionKind.Strike : slot == 1 ? CombatActionKind.HeavyStrike : CombatActionKind.Ruin, timing, slot);
            result.Activated = true;
            result.Message = skill.DisplayName + ": holt aus";
            return result;
        }

        /// <summary>Dodge toward a direction (or step back without one): 2.8 m over 8 ticks, ticks 2–4 untouchable.</summary>
        public bool StartDodge(long directionXbp, long directionZbp)
        {
            if (!CombatV2 || GameOver || Stamina < CombatV2Rules.Dodge.Stamina) return false;
            // A dodge may cut an attack's or a spell's recovery short, never its windup or impact.
            if (ActionKind != CombatActionKind.None &&
                !((IsAttack(ActionKind) || ActionKind == CombatActionKind.Cast) && ActionPhase == CombatPhase.Recovery)) return false;
            if (directionXbp == 0 && directionZbp == 0) { directionXbp = -FacingXbp; directionZbp = -FacingZbp; }
            long length = Math.Max(1, FixedMath.IntegerSqrt(FixedMath.Square(directionXbp) + FixedMath.Square(directionZbp)));
            _dodgeXbp = directionXbp * 10000 / length; _dodgeZbp = directionZbp * 10000 / length;
            SpendStamina(CombatV2Rules.Dodge.Stamina);
            BeginAction(CombatActionKind.Dodge, CombatV2Rules.Dodge, -1);
            return true;
        }

        /// <summary>The guard follows the held button: raised after one tick, drains stamina while held.</summary>
        public void SetGuard(bool held)
        {
            if (!CombatV2) return;
            GuardHeld = held && !GameOver;
        }

        private bool StartDrink(PotionType type)
        {
            if (ActionKind != CombatActionKind.None || Progression.Crafting.PotionCount(type) == 0) return false;
            _drinkType = type;
            BeginAction(CombatActionKind.Drink, CombatV2Rules.Drink, -1);
            return true;
        }

        private static bool IsAttack(CombatActionKind kind)
        { return kind == CombatActionKind.Strike || kind == CombatActionKind.HeavyStrike || kind == CombatActionKind.Ruin; }

        private void BeginAction(CombatActionKind kind, ActionTiming timing, int slot)
        {
            ActionKind = kind; _timing = timing; ActionSlot = slot; ActionTick = 0; _guardReleased = false;
            ActionSequence++;
        }

        private void EndAction()
        {
            ActionKind = CombatActionKind.None; ActionSlot = -1; ActionTick = 0; _guardReleased = false;
            HealTarget = HealTargetKind.None; HealTargetIndex = -1;
        }

        private void SpendStamina(int amount)
        {
            Stamina = Math.Max(0, Stamina - amount);
            _staminaSpent = true; _staminaSpentTick = CurrentTick;
        }

        private void Face(long x, long z)
        {
            if (x == 0 && z == 0) return;
            long length = Math.Max(1, FixedMath.IntegerSqrt(FixedMath.Square(x) + FixedMath.Square(z)));
            FacingXbp = x * 10000 / length; FacingZbp = z * 10000 / length;
        }

        /// <summary>First part of a v2 tick, before the player moves: advance the action and resolve its effect tick.</summary>
        private void AdvanceCombatBeforeMove()
        {
            Impacts.Clear();
            Heals.Clear();
            if (GameOver) { EndAction(); return; }
            if (ActionKind == CombatActionKind.None && GuardHeld && Stamina > 0)
                BeginAction(CombatActionKind.Guard, new ActionTiming(CombatV2Rules.GuardRaiseTicks, 0, CombatV2Rules.GuardLowerTicks, 0), -1);
            if (ActionKind == CombatActionKind.None) return;
            ActionTick++;
            if (ActionKind == CombatActionKind.Guard)
            {
                if (!_guardReleased && (!GuardHeld || Stamina <= 0) && ActionTick > CombatV2Rules.GuardRaiseTicks)
                { _guardReleased = true; ActionTick = 1; _timing = new ActionTiming(0, 0, CombatV2Rules.GuardLowerTicks, 0); }
                if (GuardRaised) SpendStamina(CombatV2Rules.GuardHoldStaminaPerTick);
                return;
            }
            if (ActionTick == _timing.Startup + 1)
            {
                if (IsAttack(ActionKind)) ResolveSkillImpact(ActionSlot);
                else if (ActionKind == CombatActionKind.Drink) DrinkNow(_drinkType);
                else if (ActionKind == CombatActionKind.Cast) ResolveWundschluss();
            }
        }

        /// <summary>Last part of a v2 tick: finish a completed action and refill stamina after its delay.</summary>
        private void AdvanceCombatAfterTick()
        {
            if (ActionKind == CombatActionKind.Guard && _guardReleased && ActionTick >= _timing.Recovery) EndAction();
            else if (ActionKind != CombatActionKind.None && ActionKind != CombatActionKind.Guard && ActionTick >= _timing.Total) EndAction();
            bool rested = !_staminaSpent || CurrentTick > _staminaSpentTick + CombatV2Rules.StaminaRegenDelayTicks;
            if (rested && Stamina < MaxStamina && !GuardRaised) Stamina = Math.Min(MaxStamina, Stamina + CombatV2Rules.StaminaRegenPerTick);
        }

        /// <summary>The player's movement this tick under v2: slower in a fight, guided by the action phase.</summary>
        private bool MovePlayerV2()
        {
            if (ActionKind == CombatActionKind.Dodge)
            {
                long sx = _dodgeXbp * CombatV2Rules.DodgeSpeedMmPerTick / 10000, sz = _dodgeZbp * CombatV2Rules.DodgeSpeedMmPerTick / 10000;
                if (CanStand(PlayerXmm + sx, PlayerZmm + sz)) { PlayerXmm += sx; PlayerZmm += sz; }
                else if (CanStand(PlayerXmm + sx, PlayerZmm)) PlayerXmm += sx;
                else if (CanStand(PlayerXmm, PlayerZmm + sz)) PlayerZmm += sz;
                return true;
            }
            var phase = ActionPhase;
            if (IsAttack(ActionKind) && phase == CombatPhase.Active) return true; // the impact tick holds its ground
            // Sneaking in the Rattler nest keeps its own slow pace, as in the legacy movement.
            long baseSpeed = Progression.EntryAct == 7 && Progression.Rattler.Sneaking ? 240 :
                InCombat ? CombatV2Rules.CombatSpeedMmPerTick : PlayerSpeedMmPerTick;
            long speed = baseSpeed * Progression.MoveSpeedBp / 10000L;
            if (ActionKind != CombatActionKind.None) speed = speed * CombatV2Rules.SlowSteeringBp / 10000L;
            long dx = _directionXbp * speed / 10000L, dz = _directionZbp * speed / 10000L;
            if (dx != 0 || dz != 0) { if (!IsAttack(ActionKind)) Face(_directionXbp, _directionZbp); }
            if (CanStand(PlayerXmm + dx, PlayerZmm + dz)) { PlayerXmm += dx; PlayerZmm += dz; }
            else if (CanStand(PlayerXmm + dx, PlayerZmm)) PlayerXmm += dx;
            else if (CanStand(PlayerXmm, PlayerZmm + dz)) PlayerZmm += dz;
            return true;
        }

        /// <summary>
        /// A hit on the player under v2: the dodge's untouchable ticks avoid it, a raised guard facing the attacker blocks it
        /// for stamina (a shield fully, a weapon with chip damage), a broken guard lets half through. Returns the damage that
        /// still reaches the armor, or -1 when the legacy rules decide.
        /// </summary>
        private int GuardOrDodge(int amount, bool hasSource, long fromX, long fromZ)
        {
            if (!CombatV2) return -1;
            if (Invulnerable) { DodgedAttacks++; return 0; }
            int damage = amount;
            if (GuardRaised && hasSource)
            {
                bool shield = Progression.Equipment.Shield != null;
                long dx = fromX - PlayerXmm, dz = fromZ - PlayerZmm;
                long distance = Math.Max(1, FixedMath.IntegerSqrt(FixedMath.Square(dx) + FixedMath.Square(dz)));
                // Facing has length 10000, so dot = 10000 · distance · cos(angle); compare against the guard's half-angle.
                long dot = FacingXbp * dx + FacingZbp * dz;
                int cosBp = shield ? CombatV2Rules.ShieldGuardCosBp : CombatV2Rules.WeaponGuardCosBp;
                if (dot >= distance * cosBp)
                {
                    int cost = CombatV2Rules.GuardCost(amount, shield, Progression.WeaponMastery.Level(WeaponFamily.Shield));
                    if (Stamina >= cost)
                    {
                        SpendStamina(cost); BlockedAttacks++;
                        if (shield) { Progression.RecordShieldPractice(amount); return 0; }
                        return Math.Max(1, amount * CombatV2Rules.DeflectChipBp / 10000);
                    }
                    // Guard break: stamina is gone, the guard drops and half the blow gets through.
                    SpendStamina(Stamina);
                    _guardReleased = true; ActionTick = 0; _timing = new ActionTiming(0, 0, CombatV2Rules.GuardBreakTicks, 0);
                    damage = Math.Max(1, amount * CombatV2Rules.GuardBreakChipBp / 10000);
                }
            }
            // A blow before the potion's effect tick spills it: nothing is drunk, nothing is used up.
            if (ActionKind == CombatActionKind.Drink && ActionTick <= _timing.Startup) EndAction();
            // A blow before Wundschluss lands cuts it short: no heal, no cooldown, the focus is gone.
            else if (ActionKind == CombatActionKind.Cast && ActionTick <= _timing.Startup && damage > 0) InterruptWundschluss();
            return damage;
        }

        /// <summary>Test and event hook: a blow from a point, decided by dodge, guard and armor like a creature's hit.</summary>
        internal void ReceiveBlowFrom(int amount, long fromX, long fromZ) { ReceiveHit(amount, true, fromX, fromZ); }

        private void ResolveSkillImpact(int slot)
        {
            var skill = SkillCatalog.Get(slot);
            var result = new SkillResult { Activated = true, Slot = slot };
            long range = AttackRange(slot);
            MonsterState nearest = NearestMonster(range);
            if (nearest == null && AschbeterInReach(range))
            {
                // His own rules decide the swing on his tick, later in this one (Spielprobe order: decide, then receive).
                SwingAtAschbeter(range);
                Resource = Math.Min(100, Resource + skill.FocusGain);
                result.HasTarget = true; result.TargetXmm = Aschbeter.Xmm; result.TargetZmm = Aschbeter.Zmm;
                result.Message = skill.DisplayName + ": gegen den Aschbeter";
                Impacts.Add(result);
                return;
            }
            if (nearest == null) { result.Message = skill.DisplayName + ": ins Leere"; Impacts.Add(result); return; }
            Resource = Math.Min(100, Resource + skill.FocusGain);
            ApplySkillHits(skill, slot, nearest, range, result);
            Impacts.Add(result);
        }

        private void MixCombatV2(ref ulong h)
        {
            if (Progression.Armor.HasAny) { Mix(ref h, 0x41524D4F52UL); Mix(ref h, Progression.Armor.StableHash); }
            if (!CombatV2) return;
            Mix(ref h, 0x434F4D42415432UL); // "COMBAT2"
            Mix(ref h, (ulong)ActionKind); Mix(ref h, (ulong)(ActionSlot + 1)); Mix(ref h, (ulong)ActionTick); Mix(ref h, ActionSequence);
            Mix(ref h, (ulong)Stamina); Mix(ref h, _staminaSpent ? _staminaSpentTick + 1 : 0UL); Mix(ref h, GuardHeld ? 1UL : 0UL); Mix(ref h, _guardReleased ? 1UL : 0UL);
            Mix(ref h, (ulong)FacingXbp); Mix(ref h, (ulong)FacingZbp); Mix(ref h, (ulong)_dodgeXbp); Mix(ref h, (ulong)_dodgeZbp);
            Mix(ref h, (ulong)_drinkType); Mix(ref h, (ulong)DodgedAttacks);
        }
    }

    public sealed partial class CharacterProgression
    {
        /// <summary>1 = legacy instant combat; 2 = combat v2 (issue #127). Own save section "combat" once switched.</summary>
        public int CombatVersion { get; private set; } = 1;
        private const string CombatSectionName = "combat";

        /// <summary>Switch this character to combat v2, once and for good: new games and the explicit save migration.</summary>
        public bool UseCombatV2()
        {
            if (CombatVersion >= 2) return false;
            CombatVersion = 2;
            return true;
        }

        private void SyncCombatSection()
        {
            if (CombatVersion >= 2) SetSection(CombatSectionName, "2");
            else _sections.Remove(CombatSectionName);
        }

        /// <summary>Only the known version is accepted; anything else rejects the save.</summary>
        private bool RestoreCombatSection()
        {
            if (!_sections.TryGetValue(CombatSectionName, out string token)) return true;
            if (token != "2") return false;
            CombatVersion = 2;
            return true;
        }
    }
}
