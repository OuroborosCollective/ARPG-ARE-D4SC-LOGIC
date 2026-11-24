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
            public Animator[] Attackers = System.Array.Empty<Animator>(), Flinchers = System.Array.Empty<Animator>(),
                Mortals = System.Array.Empty<Animator>();
            public float DeathSeconds = FallbackDeathSeconds;
            public int Windup, Cooldown, Health = int.MinValue;
            public bool Dead;
        }

        private readonly Dictionary<int, EnemyClips> _enemyClips = new Dictionary<int, EnemyClips>();

        /// <summary>What this figure's controllers can show; looked up once per enemy.</summary>
        private EnemyClips EnemyClipsOf(int id, Transform visual)
        {
            if (_enemyClips.TryGetValue(id, out EnemyClips clips)) return clips;
            clips = new EnemyClips();
            var attackers = new List<Animator>(); var flinchers = new List<Animator>(); var mortals = new List<Animator>();
            foreach (Animator animator in AnimatorsOf(visual))
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                bool speed = false, dead = false;
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    speed |= parameter.nameHash == EnemyAttackSpeedParameter && parameter.type == AnimatorControllerParameterType.Float;
                    dead |= parameter.nameHash == EnemyDeadParameter && parameter.type == AnimatorControllerParameterType.Bool;
                }
                if (speed && animator.HasState(0, EnemyAttackState)) attackers.Add(animator);
                if (animator.HasState(0, EnemyHitState)) flinchers.Add(animator);
                if (dead && animator.HasState(0, EnemyDeathState))
                {
                    mortals.Add(animator);
                    foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                        if (clip != null && clip.name.EndsWith("Death", System.StringComparison.Ordinal)) clips.DeathSeconds = clip.length;
                }
            }
            // The flinch belongs to figures built for this contract, not to any controller that happens to have a "Hit".
            clips.Attackers = attackers.ToArray(); clips.Mortals = mortals.ToArray();
            clips.Flinchers = attackers.Count > 0 ? flinchers.ToArray() : System.Array.Empty<Animator>();
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
                foreach (Animator animator in clips.Mortals) if (animator != null) animator.SetBool(EnemyDeadParameter, false);
            }
            int windup = enemy.BossWindupTicks, cooldown = enemy.AttackCooldownTicks;
            if (clips.Attackers.Length > 0)
            {
                bool announced = windup > 0 && clips.Windup == 0;
                // The old combat lands a blow without a windup; the cooldown starting anew is the only sign of it.
                bool unannounced = windup == 0 && clips.Windup == 0 && cooldown > clips.Cooldown && clips.Health != int.MinValue;
                if (announced || unannounced)
                {
                    float seconds = announced ? windup * ArenaSimulation.TickMs / 1000f : LegacyBlowSeconds;
                    float speed = Mathf.Clamp(1f / seconds, SlowestEnemyAttack, FastestEnemyAttack);
                    if (_hero != null)
                    {
                        Vector3 toHero = _hero.position - visual.position; toHero.y = 0f;
                        if (toHero.sqrMagnitude > .0001f) visual.rotation = Quaternion.LookRotation(toHero);
                    }
                    foreach (Animator animator in clips.Attackers)
                    {
                        if (animator == null || !animator.isActiveAndEnabled) continue;
                        animator.SetFloat(EnemyAttackSpeedParameter, speed);
                        animator.CrossFadeInFixedTime(EnemyAttackState, .08f, 0, 0f);
                    }
                }
                else if (clips.Health != int.MinValue && enemy.Health < clips.Health && windup == 0)
                {
                    // While it winds up the body keeps announcing the blow the Core is going to land.
                    foreach (Animator animator in clips.Flinchers)
                        if (animator != null && animator.isActiveAndEnabled) animator.CrossFadeInFixedTime(EnemyHitState, .04f, 0, 0f);
                }
            }
            clips.Windup = windup; clips.Cooldown = cooldown; clips.Health = enemy.Health;
        }

        /// <summary>Whether this enemy falls with its own clip instead of the optical topple.</summary>
        private bool EnemyDiesWithClip(int id, Transform visual) { return EnemyClipsOf(id, visual).Mortals.Length > 0; }

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
                _deathSeconds[id] = clips.DeathSeconds + EnemyLieSeconds + SinkSeconds;
                clips.Dead = true; clips.Windup = 0;
                foreach (Animator animator in clips.Mortals) if (animator != null) animator.SetBool(EnemyDeadParameter, true);
            }
            float t = Time.time - start, down = clips.DeathSeconds + EnemyLieSeconds;
            if (t > down + SinkSeconds) return false;
            Vector3 origin = _deathPosition[id];
            float sink = t > down ? (t - down) / SinkSeconds * .7f : 0f;
            visual.SetPositionAndRotation(origin - Vector3.up * sink, _deathRotation[id]);
            visual.gameObject.SetActive(true);
            return true;
        }
    }
}
