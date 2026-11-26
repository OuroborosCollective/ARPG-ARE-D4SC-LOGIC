using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Owner decisions 2026-11-15: every executed ability shows a hero action; hits knock enemies back only optically,
    /// with a short hit-stop on the struck enemy, camera shake and sparks; defeated enemies topple; level-ups and spells get light and
    /// particles. Presentation only: nothing here writes Core state or delays a simulation tick.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private const float HitReactionSeconds = .28f, HitReactionMetres = .34f, HitTiltDegrees = 14f, HitStopSeconds = .06f;
        private const float ToppleSeconds = .45f, SinkSeconds = .9f, ToppleDegrees = 84f, LevelBannerSeconds = 3f;
        private const float FaceTargetSeconds = .45f, HeroActionSpacing = .35f;
        private readonly Queue<string> _heroActions = new Queue<string>();
        private float _heroActionAt = -10f, _faceTargetUntil = -1f;
        private Vector3 _faceTarget;
        private readonly Dictionary<int, Vector3> _hitDirection = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, float> _hitTime = new Dictionary<int, float>();
        private readonly Dictionary<int, Quaternion> _hitBaseRotation = new Dictionary<int, Quaternion>();
        private readonly Dictionary<int, Quaternion> _hitAppliedRotation = new Dictionary<int, Quaternion>();
        private readonly Dictionary<int, float> _deathTime = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _deathSeconds = new Dictionary<int, float>();
        private readonly Dictionary<int, Vector3> _deathPosition = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Quaternion> _deathRotation = new Dictionary<int, Quaternion>();
        private readonly HashSet<int> _seenAlive = new HashSet<int>();
        private readonly List<KeyValuePair<Animator, float>> _hitStopped = new List<KeyValuePair<Animator, float>>();
        private float _hitStopUntil = -1f, _levelBannerUntil = -1f, _levelLightStart = -1f;
        private string _levelBannerText = "";
        private System.Numerics.BigInteger _shownLevel = System.Numerics.BigInteger.MinusOne;
        private ParticleSystem _levelBurst, _shockwave, _guardShimmer;
        private Light _levelLight;
        private Text _hudLevelBanner;

        /// <summary>True while a defeated enemy is still toppling; visible for checks, never read by the Core.</summary>
        public bool IsPresentingDeath(int monsterId)
        {
            return _deathTime.ContainsKey(monsterId) && Time.time - _deathTime[monsterId] <=
                (_deathSeconds.TryGetValue(monsterId, out float seconds) ? seconds : ToppleSeconds + SinkSeconds);
        }

        /// <summary>Called right after the Core executed an ability from the shared touch/keyboard queue.</summary>
        /// <summary>Legacy combat: the accepted ability starts and lands in the same tick.</summary>
        private void PresentAbility(SkillResult outcome, int slot)
        {
            PresentActionStart(outcome, slot);
            PresentImpact(outcome, slot);
        }

        /// <summary>The hero turns to the accepted target and starts the action's animation.</summary>
        private void PresentActionStart(SkillResult outcome, int slot)
        {
            if (!_entryProfile || outcome == null || !outcome.Activated || _hero == null) return;
            if (outcome.HasTarget)
            {
                var toward = new Vector3(outcome.TargetXmm / 1000f - _hero.position.x, 0f, outcome.TargetZmm / 1000f - _hero.position.z);
                if (toward.sqrMagnitude > .0001f)
                {
                    // Movement facing is projected later in the frame; hold the target facing for the action.
                    _faceTarget = toward.normalized; _faceTargetUntil = Time.time + FaceTargetSeconds;
                    _hero.rotation = Quaternion.LookRotation(_faceTarget);
                }
            }
            // One visible action per executed ability, even when two abilities resolve in the same tick.
            int stage = _meleeChain.Accept(_game, slot, _moveWorldX != 0 || _moveWorldZ != 0);
            if (stage >= 0 && _chainGrip != null) _chainGrip.BeginChain(_game, stage);
            _heroActions.Enqueue(slot == 2 ? "Guard" : slot == 3 ? "Cast" : "Melee");
            PumpHeroActions();
        }

        /// <summary>What a landed ability shows: hit reactions, a short hit stop, camera shake and the spell's effects.</summary>
        private void PresentImpact(SkillResult outcome, int slot)
        {
            if (!_entryProfile || outcome == null || !outcome.Activated || _hero == null) return;
            foreach (int id in outcome.HitMonsterIds)
            {
                var visual = HitVisual(id);
                if (visual == null) continue;
                var away = visual.position - _hero.position; away.y = 0f;
                _hitDirection[id] = away.sqrMagnitude > .0001f ? away.normalized : _hero.forward;
                _hitTime[id] = Time.time;
            }
            if (outcome.Hits > 0)
            {
                _hitStopUntil = Time.unscaledTime + HitStopSeconds;
                if (_cameraRig != null) _cameraRig.Shake(slot == 3 ? .14f : slot == 1 ? .09f : .05f, slot == 3 ? .3f : .18f);
            }
            EnsureCombatEffects();
            bool reduced = _atmosphere != null && _atmosphere.ReducedEffects;
            if (slot == 3 && _shockwave != null)
            {
                var shape = _shockwave.shape; shape.radius = Mathf.Max(.5f, _game.AttackRange(3) / 1000f * .8f);
                _shockwave.transform.position = _hero.position + Vector3.up * .15f;
                _shockwave.Emit(reduced ? 24 : 80);
            }
            if (slot == 2 && _guardShimmer != null)
            {
                _guardShimmer.transform.position = _hero.position + Vector3.up * .9f;
                _guardShimmer.Emit(reduced ? 12 : 40);
            }
        }

        /// <summary>Start the next queued hero action once the previous one had a moment to show.</summary>
        private void PumpHeroActions()
        {
            if (_heroActions.Count == 0 || Time.time - _heroActionAt < HeroActionSpacing) return;
            string trigger = _heroActions.Dequeue();
            _heroActionAt = Time.time;
            if (trigger == "Cast" && !_game.CombatV2) SetHeroActionSpeed("EntryCast", 0); // the old ruin storm has no windup to span
            foreach (var animator in _heroMeleeAnimations)
            {
                if (animator == null) continue;
                // A controller with real clips holds the guard as long as the Core does (HeroClipPresentation).
                if (trigger == "Guard" && HeldGuardRequested(animator)) continue;
                string use = HasTrigger(animator, trigger) ? trigger : "Melee";
                animator.ResetTrigger(use); animator.SetTrigger(use);
            }
        }

        /// <summary>Keep the hero turned to the accepted target while its action plays, after movement facing was applied.</summary>
        private void HoldTargetFacing()
        {
            if (_hero != null && Time.time < _faceTargetUntil && _faceTarget.sqrMagnitude > .0001f)
                _hero.rotation = Quaternion.LookRotation(_faceTarget);
        }

        /// <summary>Regular enemies use their pooled visual; the road bear and the MaulwurfRattler have dedicated ones.</summary>
        private Transform HitVisual(int id)
        {
            if (_monsterVisuals.TryGetValue(id, out var visual) && visual != null) return visual;
            var monster = _game.Monsters.Find(m => m.Id == id);
            if (monster == null) return null;
            if (monster.Kind == 4) return _roadBear;
            if (monster.Kind == 5) return _rattler;
            return null;
        }

        private readonly Dictionary<Transform, Animator[]> _visualAnimators = new Dictionary<Transform, Animator[]>();
        /// <summary>Every animator of a visual, not only those registered for locomotion; cached per visual.</summary>
        private Animator[] AnimatorsOf(Transform visual)
        {
            if (visual == null) return System.Array.Empty<Animator>();
            if (!_visualAnimators.TryGetValue(visual, out var animators)) _visualAnimators[visual] = animators = visual.GetComponentsInChildren<Animator>(true);
            return animators;
        }

        private static bool HasTrigger(Animator animator, string name)
        {
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name) return true;
            return false;
        }

        /// <summary>Optical knockback: the visual springs away from the hero and back onto the Core-projected position.</summary>
        private void ApplyHitReaction(int id, Transform visual)
        {
            if (!_hitTime.TryGetValue(id, out float start)) return;
            float t = (Time.time - start) / HitReactionSeconds;
            if (!_hitBaseRotation.ContainsKey(id) || _hitAppliedRotation[id] != visual.rotation) _hitBaseRotation[id] = visual.rotation;
            if (t >= 1f || t < 0f)
            {
                visual.rotation = _hitBaseRotation[id];
                _hitTime.Remove(id); _hitBaseRotation.Remove(id); _hitAppliedRotation.Remove(id);
                return;
            }
            float envelope = t < .2f ? Mathf.SmoothStep(0f, 1f, t / .2f) : 1f - Mathf.SmoothStep(0f, 1f, (t - .2f) / .8f);
            Vector3 direction = _hitDirection[id];
            visual.position += direction * (HitReactionMetres * envelope);
            visual.rotation = Quaternion.AngleAxis(HitTiltDegrees * envelope, Vector3.Cross(Vector3.up, direction)) * _hitBaseRotation[id];
            _hitAppliedRotation[id] = visual.rotation;
        }

        /// <summary>A defeated enemy topples away from the hero and sinks; false once nothing remains to show.</summary>
        private bool PresentDeath(int id, Transform visual)
        {
            if (!_entryProfile || !_seenAlive.Contains(id)) return false;
            if (EnemyDiesWithClip(id, visual)) return PresentEnemyDeathClip(id, visual); // a figure with a death clip falls by itself
            if (!_deathTime.TryGetValue(id, out float start))
            {
                if (!visual.gameObject.activeSelf) return false;
                start = Time.time; _deathTime[id] = start;
                _deathPosition[id] = visual.position; _deathRotation[id] = _hitBaseRotation.TryGetValue(id, out var r) ? r : visual.rotation;
            }
            // A defeated enemy's skeleton stays frozen while it falls, also after a brief hit-stop ends.
            foreach (var animator in AnimatorsOf(visual)) if (animator != null) animator.speed = 0f;
            float t = Time.time - start;
            if (t > ToppleSeconds + SinkSeconds) return false;
            Vector3 direction = _hitDirection.TryGetValue(id, out var d) ? d : -visual.forward;
            Vector3 origin = _deathPosition[id];
            var foot = new Vector3(origin.x, GroundHeight(origin.x, origin.z), origin.z);
            float fall = Mathf.Clamp01(t / ToppleSeconds); fall *= fall;
            var tilt = Quaternion.AngleAxis(ToppleDegrees * fall, Vector3.Cross(Vector3.up, direction));
            float sink = t > ToppleSeconds ? (t - ToppleSeconds) / SinkSeconds * .7f : 0f;
            visual.SetPositionAndRotation(foot + tilt * (origin - foot) - Vector3.up * sink, tilt * _deathRotation[id]);
            visual.gameObject.SetActive(true);
            return true;
        }

        /// <summary>Per-frame cosmetic state: hit-stop, level-up detection, light fade and banner.</summary>
        private int _shownBlocks;

        private void UpdateCombatFeel()
        {
            if (_game != null && _game.BlockedAttacks != _shownBlocks)
            {
                if (_game.BlockedAttacks > _shownBlocks) _message = "Geblockt! Der Schild fängt den Angriff ab.";
                _shownBlocks = _game.BlockedAttacks;
            }
            bool stop = Time.unscaledTime < _hitStopUntil;
            if (stop && _hitStopped.Count == 0)
            {
                // Only the struck enemy stalls: the Core resolves hits at activation, so freezing the hero would delay its swing.
                foreach (int id in _hitTime.Keys)
                    foreach (var animator in AnimatorsOf(HitVisual(id))) Freeze(animator);
            }
            else if (!stop && _hitStopped.Count > 0)
            {
                foreach (var pair in _hitStopped) if (pair.Key != null && pair.Key.speed == 0f) pair.Key.speed = pair.Value;
                _hitStopped.Clear();
            }
            PumpHeroActions();
            var level = _game.Progression.Level;
            if (_shownLevel < 0 || level < _shownLevel) _shownLevel = level;
            else if (level > _shownLevel) { _shownLevel = level; PresentLevelUp(level); }
            if (_levelLight != null)
            {
                float t = Time.unscaledTime - _levelLightStart;
                _levelLight.enabled = t >= 0f && t < 1.4f;
                if (_levelLight.enabled) { _levelLight.intensity = 6f * (1f - t / 1.4f); if (_hero != null) _levelLight.transform.position = _hero.position + Vector3.up * 1.6f; }
            }
            if (_hudLevelBanner != null)
            {
                bool show = Time.unscaledTime < _levelBannerUntil;
                if (_hudLevelBanner.gameObject.activeSelf != show) _hudLevelBanner.gameObject.SetActive(show);
                if (show) _hudLevelBanner.text = _levelBannerText;
            }
        }

        private void Freeze(Animator animator)
        {
            if (animator == null || animator.speed == 0f) return;
            _hitStopped.Add(new KeyValuePair<Animator, float>(animator, animator.speed)); animator.speed = 0f;
        }

        private void PresentLevelUp(System.Numerics.BigInteger level)
        {
            EnsureCombatEffects();
            bool reduced = _atmosphere != null && _atmosphere.ReducedEffects;
            if (_hero != null && _levelBurst != null) { _levelBurst.transform.position = _hero.position; _levelBurst.Emit(reduced ? 24 : 90); }
            _levelLightStart = Time.unscaledTime;
            _levelBannerText = "STUFE " + level + " ERREICHT";
            _levelBannerUntil = Time.unscaledTime + LevelBannerSeconds;
            if (_atmosphere != null && _hero != null) _atmosphere.Effect("spell", _hero.position, 1f);
        }

        private void EnsureCombatEffects()
        {
            if (_atmosphere == null) return;
            if (_levelBurst == null)
            {
                _levelBurst = _atmosphere.CreateEffect("Level-up burst", 120, .16f, new Color(1f, .82f, .36f, .9f), 1.6f);
                var shape = _levelBurst.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .7f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                var main = _levelBurst.main; main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f); main.gravityModifier = -.15f;
            }
            if (_shockwave == null)
            {
                _shockwave = _atmosphere.CreateEffect("Ruin storm shockwave", 120, .28f, new Color(1f, .45f, .16f, .85f), .6f);
                var shape = _shockwave.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radiusThickness = 0f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                var main = _shockwave.main; main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.4f);
            }
            if (_guardShimmer == null)
            {
                _guardShimmer = _atmosphere.CreateEffect("Guard shimmer", 60, .12f, new Color(.9f, .95f, 1f, .8f), .9f);
                var shape = _guardShimmer.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .75f;
                var main = _guardShimmer.main; main.startSpeed = .3f; main.gravityModifier = -.25f;
            }
            if (_levelLight == null)
            {
                var light = new GameObject("Level-up light").AddComponent<Light>();
                light.type = LightType.Point; light.range = 7f; light.color = new Color(1f, .84f, .45f); light.enabled = false;
                light.transform.SetParent(transform, false);
                _levelLight = light;
            }
        }

        /// <summary>Forget per-visual cosmetic state when the projections are rebuilt.</summary>
        private void ResetCombatFeel()
        {
            _hitDirection.Clear(); _hitTime.Clear(); _hitBaseRotation.Clear(); _hitAppliedRotation.Clear();
            _deathTime.Clear(); _deathSeconds.Clear(); _deathPosition.Clear(); _deathRotation.Clear(); _seenAlive.Clear(); _hitStopped.Clear();
            _enemyClips.Clear();
            _hitStopUntil = -1f; _heroActions.Clear(); _faceTargetUntil = -1f; _visualAnimators.Clear();
        }
    }
}
