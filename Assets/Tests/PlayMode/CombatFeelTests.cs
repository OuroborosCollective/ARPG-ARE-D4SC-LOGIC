using System.Collections;
using System.Collections.Generic;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Owner report 2026-11-15: attacks at the start enemies made combat sounds but showed no attack animation.</summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        /// <summary>Every executed ability shows a visible hero action in the real runtime flow (HUD route, fixed ticks, real frames).</summary>
        [UnityTest]
        public IEnumerator EveryExecutedAbilityShowsAVisibleHeroAction()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private character and animation overlay");
            var hero = Find("Wanderer");
            Assert.IsNotNull(hero);
            var animator = hero.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "The private hero rig must be bound");
            Debug.Log("COMBAT_RIG controller=" + (animator.runtimeAnimatorController ? animator.runtimeAnimatorController.name : "none") +
                " culling=" + animator.cullingMode + " enabled=" + animator.enabled);
            var missing = new List<string>();
            for (int slot = 0; slot < 4; slot++)
            {
                var target = Game.Monsters.Find(m => !m.IsDead);
                Assert.IsNotNull(target);
                target.Xmm = Game.PlayerXmm + 900; target.Zmm = Game.PlayerZmm; target.Health = 100000;
                for (int i = 0; i < Game.Cooldowns.Length; i++) Game.Cooldowns[i] = 0;
                typeof(ArenaSimulation).GetProperty("Resource").SetValue(Game, 100);
                if (slot == 2) typeof(ArenaSimulation).GetProperty("PlayerHealth").SetValue(Game, Game.MaxPlayerHealth / 2);
                yield return null;
                int before = Animator.StringToHash("Idle");
                var seen = new List<string>();
                typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags).Invoke(_runtime, new object[] { ArenaHudControl.Skill0 + slot });
                bool action = false;
                float until = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    string name = state.IsName("Melee") ? "Melee" : state.IsName("Cast") ? "Cast" : state.IsName("Guard") ? "Guard" :
                        state.IsName("Move") ? "Move" : state.IsName("Idle") ? "Idle" : "other";
                    if (seen.Count == 0 || seen[seen.Count - 1] != name) seen.Add(name);
                    if (name == "Melee" || name == "Cast" || name == "Guard") action = true;
                }
                Debug.Log("COMBAT_SLOT " + slot + " message=" + Message() + " states=" + string.Join(">", seen));
                if (!action) missing.Add("slot " + slot + " (" + Message() + ")");
                if (slot == 1) yield return Capture("combat-heavy-strike.png", true);
            }
            Assert.IsEmpty(missing, "Executed abilities without a visible hero action: " + string.Join(", ", missing));
        }

        /// <summary>Hits knock the visual back and let it spring onto the Core position; kills topple; level-ups shine.</summary>
        [UnityTest]
        public IEnumerator HitsReactOpticallyKillsToppleAndLevelUpsShine()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private character and animation overlay");
            var target = Game.Monsters.Find(m => !m.IsDead);
            foreach (var other in Game.Monsters) if (other != target) { other.Xmm = 20000; other.Zmm = 20000; }
            target.Xmm = target.PreviousXmm = Game.PlayerXmm + 900; target.Zmm = target.PreviousZmm = Game.PlayerZmm; target.Health = 100000;
            var visuals = (Dictionary<int, Transform>)typeof(ArenaRuntime).GetField("_monsterVisuals", Flags).GetValue(_runtime);
            var visual = visuals[target.Id];
            var rig = Camera.main.GetComponent<ArenaCameraRig>();
            yield return null;
            for (int i = 0; i < Game.Cooldowns.Length; i++) Game.Cooldowns[i] = 0;
            typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags).Invoke(_runtime, new object[] { ArenaHudControl.Skill0 });
            float maxOffset = 0f, finalOffset = 0f; bool shook = false;
            float until = Time.time + .9f;
            while (Time.time < until)
            {
                yield return null;
                var core = new Vector3(target.Xmm / 1000f, 0f, target.Zmm / 1000f);
                var seen = visual.position; seen.y = 0f;
                finalOffset = Vector3.Distance(seen, core);
                maxOffset = Mathf.Max(maxOffset, finalOffset);
                shook |= rig != null && rig.Shaking;
            }
            Debug.Log("COMBAT_HIT maxOffset=" + maxOffset.ToString("0.000") + " finalOffset=" + finalOffset.ToString("0.000") + " shook=" + shook);
            Assert.That(maxOffset, Is.InRange(.15f, .6f), "A hit must visibly knock the enemy back, but only a short distance");
            Assert.Less(finalOffset, .06f, "The knockback is optical and returns onto the Core position");
            Assert.IsTrue(shook, "Accepted hits shake the camera briefly");

            // A kill topples the visual instead of making it vanish at once.
            target.Health = 1; for (int i = 0; i < Game.Cooldowns.Length; i++) Game.Cooldowns[i] = 0;
            typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags).Invoke(_runtime, new object[] { ArenaHudControl.Skill0 });
            float maxTilt = 0f; bool visibleWhileDead = false; bool hiddenLater = false;
            until = Time.time + 2.2f;
            bool captured = false;
            while (Time.time < until)
            {
                yield return null;
                if (!target.IsDead) continue;
                if (visual.gameObject.activeSelf) { visibleWhileDead = true; maxTilt = Mathf.Max(maxTilt, Vector3.Angle(visual.up, Vector3.up)); }
                else hiddenLater = true;
                if (!captured && maxTilt > 40f) { captured = true; yield return Capture("combat-topple.png"); }
            }
            Debug.Log("COMBAT_DEATH tilt=" + maxTilt.ToString("0.0") + " visibleWhileDead=" + visibleWhileDead + " hiddenLater=" + hiddenLater);
            Assert.IsTrue(target.IsDead);
            Assert.IsTrue(visibleWhileDead, "A defeated enemy stays visible while it falls");
            Assert.Greater(maxTilt, 50f, "A defeated enemy topples over");
            Assert.IsTrue(hiddenLater, "The fallen enemy is removed after its fall");

            // Level-up: explicit fixture through the canonical Core experience method used by kills and loot.
            var level = Game.Progression.Level;
            Game.Progression.EarnExperience(CharacterProgression.RequiredExperienceForLevel(level + 1) - Game.Progression.TotalExperience);
            Assert.AreEqual(level + 1, Game.Progression.Level);
            _runtime.enabled = true;
            yield return null; yield return null;
            var light = GameObject.Find("Level-up light");
            var banner = (UnityEngine.UI.Text)typeof(ArenaRuntime).GetField("_hudLevelBanner", Flags).GetValue(_runtime);
            Debug.Log("COMBAT_LEVEL level=" + Game.Progression.Level + " light=" + (light != null && light.GetComponent<Light>().enabled) + " banner=" + (banner != null && banner.gameObject.activeInHierarchy ? banner.text : "hidden"));
            Assert.IsTrue(light != null && light.GetComponent<Light>().enabled, "A level-up flashes a light at the hero");
            Assert.IsTrue(banner != null && banner.gameObject.activeInHierarchy && banner.text.Contains("STUFE"), "A level-up shows the new level");
            yield return Capture("combat-level-up.png", true);
        }
    }
}
