using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Enemy figures with real clips follow the Core (owner task 2026-11-24): the attack clip starts when the Core's
    /// windup starts and its blow lands when the windup ends, a blow that gets through makes the enemy flinch unless it
    /// is winding up, and a defeated enemy plays its death clip and lies still before it sinks. Presentation only:
    /// nothing here changes the Core or waits for an animation. A figure whose controller has no such states keeps the
    /// earlier behaviour (run cycle, optical knockback, topple).
    ///
    /// Contract with the figure's controller: states Attack, Hit and Death on layer 0, the float AttackSpeed as the
    /// attack state's speed multiplier and the bool Dead. The attack state's own speed is set so that, at multiplier 1,
    /// the blow lands one second after the state starts; the game then only divides by the Core's windup.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private static readonly int EnemyAttackState = Animator.StringToHash("Attack"), EnemyHitState = Animator.StringToHash("Hit"),
            EnemyDeathState = Animator.StringToHash("Death"), EnemyDeadParameter = Animator.StringToHash("Dead"),
            EnemyAttackSpeedParameter = Animator.StringToHash("AttackSpeed");
        private const float SlowestEnemyAttack = .5f, FastestEnemyAttack = 4f, LegacyBlowSeconds = .25f;
        private const float EnemyLieSeconds = .8f, FallbackDeathSeconds = 1.5f;

        private sealed class EnemyClips
        {
            public Animator[] Controllers;
            public float MeasuredDeathSeconds;
            public int Windup, Cooldown, Health = int.MinValue;
            public bool Dead;
        }

        private readonly Dictionary<int, EnemyClips> _enemyClips = new Dictionary<int, EnemyClips>();

        /// <summary>What this figure's controllers can show; looked up once per enemy.</summary>
        private EnemyClips EnemyClipsOf(int id, Transform visual)
        {
            if (_enemyClips.TryGetValue(id, out EnemyClips clips)) return clips;
            var controllers = new List<Animator>();
            foreach (Animator animator in AnimatorsOf(visual))
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                bool speed = false, dead = false;
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    speed |= parameter.nameHash == EnemyAttackSpeedParameter && parameter.type == AnimatorControllerParameterType.Float;
                    dead |= parameter.nameHash == EnemyDeadParameter && parameter.type == AnimatorControllerParameterType.Bool;
                }
                if (speed && dead && animator.HasState(0, EnemyAttackState) && animator.HasState(0, EnemyHitState) &&
                    animator.HasState(0, EnemyDeathState))
                    controllers.Add(animator);
            }
            clips = new EnemyClips { Controllers = controllers.ToArray() };
            _enemyClips[id] = clips;
            return clips;
        }

        /// <summary>Once per frame for a living enemy, after its position was projected.</summary>
        private void UpdateEnemyClips(MonsterState enemy, Transform visual)
        {
            EnemyClips clips = EnemyClipsOf(enemy.Id, visual);
            if (clips.Dead)
            {
                // A new run reuses the enemy's id: it stands again.
                clips.Dead = false;
                clips.MeasuredDeathSeconds = 0f;
                _deathTime.Remove(enemy.Id);
                foreach (Animator animator in clips.Controllers) if (animator != null) animator.SetBool(EnemyDeadParameter, false);
            }
            int windup = enemy.BossWindupTicks, cooldown = enemy.AttackCooldownTicks;
            if (clips.Controllers.Length > 0)
            {
                bool announced = windup > 0 && clips.Windup == 0;
                // The old combat lands a blow without a windup; the cooldown starting anew is the only sign of it.
                bool unannounced = windup == 0 && clips.Windup == 0 && cooldown > clips.Cooldown && clips.Health != int.MinValue;
                if (announced || unannounced)
                {
                    float seconds = announced ? windup * ArenaSimulation.TickMs / 1000f : LegacyBlowSeconds;
                    float speed = Mathf.Clamp(1f / seconds, SlowestEnemyAttack, FastestEnemyAttack);
                    Vector3 target = _hero != null ? _hero.position : visual.position;
                    if (unannounced && enemy.LastImmediateStrikeTarget is { } strike)
                        target = new Vector3(strike.Xmm * .001f, 0f, strike.Zmm * .001f);
                    Vector3 facing = target - visual.position;
                    facing.y = 0f;
                    if (facing.sqrMagnitude > .0001f) visual.rotation = Quaternion.LookRotation(facing);
                    foreach (Animator animator in clips.Controllers)
                    {
                        if (animator == null || !animator.isActiveAndEnabled) continue;
                        animator.SetFloat(EnemyAttackSpeedParameter, speed);
                        animator.CrossFadeInFixedTime(EnemyAttackState, .08f, 0, 0f);
                    }
                }
                else if (clips.Health != int.MinValue && enemy.Health < clips.Health && windup == 0)
                {
                    // While it winds up the body keeps announcing the blow the Core is going to land.
                    foreach (Animator animator in clips.Controllers)
                        if (animator != null && animator.isActiveAndEnabled) animator.CrossFadeInFixedTime(EnemyHitState, .04f, 0, 0f);
                }
            }
            clips.Windup = windup; clips.Cooldown = cooldown; clips.Health = enemy.Health;
        }

        /// <summary>Whether this enemy falls with its own clip instead of the optical topple.</summary>
        private bool EnemyDiesWithClip(int id, Transform visual) { return EnemyClipsOf(id, visual).Controllers.Length > 0; }

        /// <summary>The death clip plays where the enemy fell; it lies for a moment, then sinks. False once nothing is left to show.</summary>
        private bool PresentEnemyDeathClip(int id, Transform visual)
        {
            EnemyClips clips = EnemyClipsOf(id, visual);
            if (!_deathTime.TryGetValue(id, out float start))
            {
                if (!visual.gameObject.activeSelf) return false;
                start = Time.time; _deathTime[id] = start;
                _deathPosition[id] = visual.position;
                _deathRotation[id] = _hitBaseRotation.TryGetValue(id, out Quaternion upright) ? upright : visual.rotation;
                clips.Dead = true; clips.Windup = 0;
                foreach (Animator animator in clips.Controllers) if (animator != null) animator.SetBool(EnemyDeadParameter, true);
            }
            // Unity resolves the motion (including overrides) after it evaluates the Dead transition.
            foreach (Animator animator in clips.Controllers)
            {
                if (animator == null || !animator.isActiveAndEnabled) continue;
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash != EnemyDeathState && animator.IsInTransition(0))
                    state = animator.GetNextAnimatorStateInfo(0);
                // During hit-stop Unity can report an infinite state length until the animator resumes.
                float length = state.length;
                if (state.shortNameHash == EnemyDeathState && length > 0f && !float.IsInfinity(length))
                    clips.MeasuredDeathSeconds = Mathf.Max(clips.MeasuredDeathSeconds, length);
            }
            float duration = clips.MeasuredDeathSeconds > 0f ? clips.MeasuredDeathSeconds : FallbackDeathSeconds;
            float t = Time.time - start, down = duration + EnemyLieSeconds;
            _deathSeconds[id] = down + SinkSeconds;
            if (t > down + SinkSeconds) return false;
            Vector3 origin = _deathPosition[id];
            float sink = t > down ? (t - down) / SinkSeconds * .7f : 0f;
            visual.SetPositionAndRotation(origin - Vector3.up * sink, _deathRotation[id]);
            visual.gameObject.SetActive(true);
            return true;
        }
    }
}
