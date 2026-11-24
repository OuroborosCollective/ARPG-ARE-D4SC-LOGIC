using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// The hero's real combat clips follow what the Core accepted (issue #48, block 3): a dodge rolls, a flask is drunk,
    /// a spell is cast, the guard stays up while the Core holds it, a blow that gets through makes the hero flinch, and a
    /// defeat lets him fall. Roll, drink and cast play at the speed that makes the clip span the Core's action.
    /// Presentation only: nothing here changes the Core or waits for an animation. A hero controller without these
    /// states (no private animation overlay) keeps its earlier behaviour.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private static readonly int GuardingParameter = Animator.StringToHash("Guarding"), DeadParameter = Animator.StringToHash("Dead"),
            ActionSpeedParameter = Animator.StringToHash("ActionSpeed"), GuardTrigger = Animator.StringToHash("Guard"),
            CastTrigger = Animator.StringToHash("Cast"), HitTrigger = Animator.StringToHash("Hit"),
            DodgeTrigger = Animator.StringToHash("Dodge"), DrinkTrigger = Animator.StringToHash("Drink");
        private const float LegacyGuardSeconds = .6f, SlowestClip = .6f, FastestClip = 2.2f, FlinchPauseSeconds = .35f;

        private readonly Dictionary<Animator, HashSet<int>> _heroParameters = new Dictionary<Animator, HashSet<int>>();
        private ulong _clipSequence;
        private int _clipHealth = int.MinValue;
        private bool _clipGuarding, _clipDead;
        private float _clipGuardUntil;

        /// <summary>Whether this animator's controller has the parameter; looked up once per animator.</summary>
        private bool HeroHas(Animator animator, int parameter)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (!_heroParameters.TryGetValue(animator, out HashSet<int> known))
            {
                known = new HashSet<int>();
                foreach (AnimatorControllerParameter each in animator.parameters) known.Add(each.nameHash);
                _heroParameters[animator] = known;
            }
            return known.Contains(parameter);
        }

        /// <summary>A guard asked for by the action queue. Controllers with a held guard take it from here.</summary>
        private bool HeldGuardRequested(Animator animator)
        {
            if (!HeroHas(animator, GuardingParameter)) return false;
            if (!_game.CombatV2) _clipGuardUntil = Time.time + LegacyGuardSeconds; // the old guard is one moment, not a hold
            return true;
        }

        /// <summary>Once per frame, after the ticks: bring the hero's clips in line with the Core.</summary>
        private void UpdateHeroClips()
        {
            if (!_entryProfile || _hero == null || _heroMeleeAnimations.Length == 0) return;
            if (_game.CombatV2 && _game.ActionSequence != _clipSequence)
            {
                _clipSequence = _game.ActionSequence;
                switch (_game.ActionKind)
                {
                    case CombatActionKind.Dodge:
                        _faceTargetUntil = 0f; // the hero turns into the roll instead of holding his last target
                        PlayHeroClip(DodgeTrigger, "EntryDodge", CombatV2Rules.Dodge.Total); break;
                    case CombatActionKind.Drink: PlayHeroClip(DrinkTrigger, "EntryDrink", CombatV2Rules.Drink.Total); break;
                    case CombatActionKind.Cast: PlayHeroClip(CastTrigger, "EntryCast", HealingRules.Wundschluss.Total); break;
                    case CombatActionKind.Ruin: SetHeroActionSpeed("EntryCast", CombatV2Rules.Ruin.Total); break; // its trigger comes from the action queue
                }
            }
            bool guarding = (_game.CombatV2 && _game.ActionKind == CombatActionKind.Guard) || Time.time < _clipGuardUntil;
            if (guarding != _clipGuarding)
            {
                _clipGuarding = guarding;
                foreach (Animator animator in _heroMeleeAnimations)
                {
                    if (!HeroHas(animator, GuardingParameter)) continue;
                    animator.SetBool(GuardingParameter, guarding);
                    animator.ResetTrigger(GuardTrigger);
                    if (guarding) animator.SetTrigger(GuardTrigger);
                }
            }
            int health = _game.PlayerHealth;
            bool free = _game.CombatV2 ? _game.ActionKind == CombatActionKind.None : Time.time - _heroActionAt > FlinchPauseSeconds;
            if (_clipHealth != int.MinValue && health < _clipHealth && !_game.GameOver && free)
                foreach (Animator animator in _heroMeleeAnimations)
                    if (HeroHas(animator, HitTrigger)) { animator.ResetTrigger(HitTrigger); animator.SetTrigger(HitTrigger); }
            _clipHealth = health;
            bool dead = _game.GameOver;
            if (dead != _clipDead)
            {
                _clipDead = dead;
                foreach (Animator animator in _heroMeleeAnimations)
                    if (HeroHas(animator, DeadParameter)) animator.SetBool(DeadParameter, dead);
            }
        }

        /// <summary>Start a clip so that it spans the Core's action of that many ticks.</summary>
        private void PlayHeroClip(int trigger, string clip, int actionTicks)
        {
            SetHeroActionSpeed(clip, actionTicks);
            foreach (Animator animator in _heroMeleeAnimations)
                if (HeroHas(animator, trigger)) { animator.ResetTrigger(trigger); animator.SetTrigger(trigger); }
        }

        private void SetHeroActionSpeed(string clip, int actionTicks)
        {
            foreach (Animator animator in _heroMeleeAnimations)
            {
                if (!HeroHas(animator, ActionSpeedParameter)) continue;
                float length = 0f;
                foreach (AnimationClip each in animator.runtimeAnimatorController.animationClips) if (each.name == clip) { length = each.length; break; }
                float seconds = actionTicks * ArenaSimulation.TickMs / 1000f;
                animator.SetFloat(ActionSpeedParameter, length > 0f && seconds > 0f ? Mathf.Clamp(length / seconds, SlowestClip, FastestClip) : 1f);
            }
        }
    }
}
