using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// The crypt's enemy figures follow the Core (owner task 2026-11-24): the attack clip starts with the Core's windup
    /// and is at its blow when the windup ends, a blow that gets through makes a free enemy flinch but never one that
    /// winds up, and a defeated enemy plays its death clip upright in place, lies still and is removed. The Core's
    /// monster state is set directly; the presentation is asked once per frame, as the running game does. Needs the
    /// private enemy figures built from the monster packs; without them the test is ignored.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static bool EnemyShows(Animator enemy, string state)
        {
            return enemy.GetCurrentAnimatorStateInfo(0).IsName(state) || (enemy.IsInTransition(0) && enemy.GetNextAnimatorStateInfo(0).IsName(state));
        }

        private IEnumerator EnemyFrames(int frames)
        {
            var update = typeof(ArenaRuntime).GetMethod("UpdatePresentation", Flags);
            for (int frame = 0; frame < frames; frame++) { update.Invoke(_runtime, new object[] { 1f }); yield return null; }
        }

        private static Animator ClipEnemy(GameObject visual)
        {
            if (visual == null) return null;
            foreach (Animator animator in visual.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null && animator.HasState(0, Animator.StringToHash("Attack")) &&
                    animator.HasState(0, Animator.StringToHash("Death")) && animator.parameters.Any(p => p.name == "AttackSpeed")) return animator;
            return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EnemyClipsFollowTheCoresWindupHitsAndDefeat()
        {
            _runtime.enabled = false;
            var figures = new List<MonsterState>();
            foreach (MonsterState monster in Game.Monsters)
                if (!monster.IsDead && monster.Kind <= 2 && figures.All(known => known.Kind != monster.Kind) && ClipEnemy(Find("Enemy_" + monster.Id)) != null)
                    figures.Add(monster);
            if (figures.Count == 0) Assert.Ignore("Requires the private enemy figures with attack, hit and death clips");
            Assert.IsTrue(Game.CombatV2, "A new character fights with combat v2");

            float before = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = .05f;
                foreach (MonsterState enemy in figures)
                {
                    // Everybody else waits far away; this one stands in front of the hero and the Core leaves it alone.
                    foreach (MonsterState other in Game.Monsters)
                        if (other != enemy && !other.IsDead) { other.Xmm = other.PreviousXmm = 26000; other.Zmm = other.PreviousZmm = 26000 - 1500L * other.Id; }
                    enemy.Xmm = enemy.PreviousXmm = Game.PlayerXmm + 1700; enemy.Zmm = enemy.PreviousZmm = Game.PlayerZmm;
                    enemy.Health = enemy.MaxHealth; enemy.BossWindupTicks = 0; enemy.AttackCooldownTicks = 0;
                    Transform visual = Find("Enemy_" + enemy.Id).transform;
                    Animator animator = ClipEnemy(visual.gameObject);
                    string label = "Kind " + enemy.Kind + ": ";
                    var clips = animator.runtimeAnimatorController.animationClips.GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
                    AnimationClip attack = clips.First(pair => pair.Key.EndsWith("Attack")).Value, death = clips.First(pair => pair.Key.EndsWith("Death")).Value;
                    Assert.IsFalse(attack.isLooping || death.isLooping, label + "attack and death play once");
                    yield return EnemyFrames(6);
                    Assert.IsTrue(EnemyShows(animator, "Idle"), label + "a standing enemy idles");
                    SaveReviewFrame("enemy-clip-" + enemy.Kind + "-idle.png");

                    // The Core announces the blow: the clip starts now and is at its blow when the windup ends.
                    int windup = CombatV2Rules.EnemyWindupTicks(enemy.Role, enemy.Kind);
                    enemy.BossWindupTicks = windup; enemy.AttackCooldownTicks = windup + 1 + CombatV2Rules.EnemyRecoveryTicks(enemy.Role, enemy.Kind);
                    yield return EnemyFrames(1);
                    Assert.IsTrue(EnemyShows(animator, "Attack"), label + "the attack clip starts with the Core's windup");
                    Assert.AreEqual(1f / (windup * .1f), animator.GetFloat("AttackSpeed"), .01f, label + "the clip is paced by the Core's windup");
                    Vector3 toHero = Find("Wanderer").transform.position - visual.position; toHero.y = 0f;
                    Assert.Greater(Vector3.Dot(visual.forward, toHero.normalized), .95f, label + "the enemy turns to the hero as it winds up");
                    yield return EnemyFrames(windup); // half of the windup: two frames of 0.05 s are one tick
                    SaveReviewFrame("enemy-clip-" + enemy.Kind + "-windup.png");
                    yield return EnemyFrames(windup - 1);
                    AnimatorStateInfo swing = animator.GetCurrentAnimatorStateInfo(0);
                    Assert.IsTrue(swing.IsName("Attack"), label + "the attack still plays when the windup ends");
                    float clipSeconds = swing.normalizedTime * attack.length;
                    Debug.Log("ENEMY_CLIP kind=" + enemy.Kind + " windupTicks=" + windup + " stateSpeed=" + swing.speed.ToString("0.000") +
                        " multiplier=" + swing.speedMultiplier.ToString("0.000") + " clipSecondsAtWindupEnd=" + clipSeconds.ToString("0.000") +
                        " attackLength=" + attack.length.ToString("0.00") + " deathLength=" + death.length.ToString("0.00"));
                    // By the controller's contract the state's own speed is the second of the clip at which the blow lands.
                    Assert.AreEqual(swing.speed, clipSeconds, .1f, label + "when the Core's windup ends the clip is at its blow");
                    enemy.BossWindupTicks = 0;
                    SaveReviewFrame("enemy-clip-" + enemy.Kind + "-blow.png");

                    // A blow that lands while the enemy winds up again does not break its announcement.
                    yield return EnemyFrames(4);
                    enemy.BossWindupTicks = windup;
                    yield return EnemyFrames(1);
                    enemy.Health -= 5;
                    yield return EnemyFrames(2);
                    Assert.IsTrue(EnemyShows(animator, "Attack") && !EnemyShows(animator, "Hit"), label + "an enemy that winds up does not flinch");
                    enemy.BossWindupTicks = 0;
                    yield return EnemyFrames(2);
                    enemy.Health -= 5;
                    yield return EnemyFrames(2);
                    Assert.IsTrue(EnemyShows(animator, "Hit"), label + "a free enemy flinches when a blow gets through");
                    SaveReviewFrame("enemy-clip-" + enemy.Kind + "-hit.png");
                    yield return EnemyFrames(30);
                    Assert.IsFalse(EnemyShows(animator, "Hit"), label + "the flinch passes");

                    // Defeat: the death clip plays upright where the enemy stood; then it lies still and is removed.
                    enemy.Health = 0;
                    yield return EnemyFrames(3);
                    Assert.IsTrue(EnemyShows(animator, "Death") && animator.GetBool("Dead"), label + "a defeated enemy plays its death clip");
                    Assert.Greater(animator.speed, 0f, label + "the death clip is not frozen");
                    int fallFrames = Mathf.CeilToInt(death.length / .05f);
                    yield return EnemyFrames(fallFrames);
                    Assert.IsTrue(visual.gameObject.activeSelf, label + "the fallen enemy is still there when its clip ends");
                    Assert.Less(Vector3.Angle(visual.up, Vector3.up), 1f, label + "the clip does the falling, not an optical topple");
                    SaveReviewFrame("enemy-clip-" + enemy.Kind + "-death.png");
                    bool removed = false;
                    for (int frame = 0; frame < 60 && !removed; frame++) { yield return EnemyFrames(1); removed = !visual.gameObject.activeSelf; }
                    Assert.IsTrue(removed, label + "the fallen enemy is removed after it lay still");
                }
            }
            finally { Time.captureDeltaTime = before; }
        }

        /// <summary>The old combat lands its blow without a windup: the enemy still shows its attack, right away.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator EnemyAttackShowsUnderTheOldCombatToo()
        {
            _runtime.enabled = false;
            MonsterState enemy = Game.Monsters.FirstOrDefault(monster => !monster.IsDead && monster.Kind <= 2 && ClipEnemy(Find("Enemy_" + monster.Id)) != null);
            if (enemy == null) Assert.Ignore("Requires the private enemy figures with attack, hit and death clips");
            typeof(CharacterProgression).GetProperty("CombatVersion").SetValue(Game.Progression, 1);
            Assert.IsFalse(Game.CombatV2, "This run fights with the old combat");
            Animator animator = ClipEnemy(Find("Enemy_" + enemy.Id));
            enemy.Xmm = enemy.PreviousXmm = Game.PlayerXmm + 1700; enemy.Zmm = enemy.PreviousZmm = Game.PlayerZmm;
            enemy.BossWindupTicks = 0; enemy.AttackCooldownTicks = 0;
            float before = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = .05f;
                yield return EnemyFrames(4);
                Assert.IsFalse(EnemyShows(animator, "Attack"), "No attack before the Core lands one");
                enemy.AttackCooldownTicks = 17; // the Core's blow: no windup, only the cooldown starts anew
                yield return EnemyFrames(1);
                Assert.IsTrue(EnemyShows(animator, "Attack"), "The unannounced blow shows the attack at once");
                Assert.AreEqual(4f, animator.GetFloat("AttackSpeed"), .01f, "The clip hurries to its blow");
                for (int tick = 16; tick > 0; tick--) { enemy.AttackCooldownTicks = tick; yield return EnemyFrames(2); }
                Assert.IsFalse(EnemyShows(animator, "Attack"), "A cooldown that only counts down starts no new attack");
            }
            finally { Time.captureDeltaTime = before; }
        }
    }
}
