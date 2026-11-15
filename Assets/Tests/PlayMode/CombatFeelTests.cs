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

        /// <summary>Review fix: the hero keeps facing its target during the action even while movement continues.</summary>
        [UnityTest]
        public IEnumerator TargetFacingHoldsWhileRunningDuringTheAction()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private character and animation overlay");
            var target = Game.Monsters.Find(m => !m.IsDead);
            foreach (var other in Game.Monsters) if (other != target) { other.Xmm = 20000; other.Zmm = 20000; }
            target.Xmm = target.PreviousXmm = Game.PlayerXmm; target.Zmm = target.PreviousZmm = Game.PlayerZmm + 900; target.Health = 100000;
            var hero = Find("Wanderer").transform;
            var previousX = typeof(ArenaSimulation).GetProperty("PrevPlayerXmm");
            yield return null;
            for (int i = 0; i < Game.Cooldowns.Length; i++) Game.Cooldowns[i] = 0;
            typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags).Invoke(_runtime, new object[] { ArenaHudControl.Skill0 });
            float start = Time.time; float duringAction = -1f, afterAction = -1f;
            while (Time.time - start < .8f)
            {
                previousX.SetValue(Game, Game.PlayerXmm - 600); // keep presenting an eastward run
                yield return null;
                float north = Vector3.Dot(hero.forward, Vector3.forward), east = Vector3.Dot(hero.forward, Vector3.right);
                if (Time.time - start > .15f && Time.time - start < .35f) duringAction = Mathf.Max(duringAction, north);
                if (Time.time - start > .6f) afterAction = Mathf.Max(afterAction, east);
            }
            Debug.Log("COMBAT_FACING duringAction=" + duringAction.ToString("0.00") + " afterAction=" + afterAction.ToString("0.00"));
            Assert.Greater(duringAction, .8f, "While its action plays the running hero faces the accepted target");
            Assert.Greater(afterAction, .8f, "After the action the hero faces its movement again");
        }

        /// <summary>Review fix: two abilities resolved in the same tick each show their own hero action.</summary>
        [UnityTest]
        public IEnumerator SameTickAbilitiesEachShowAnAction()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private character and animation overlay");
            var target = Game.Monsters.Find(m => !m.IsDead);
            foreach (var other in Game.Monsters) if (other != target) { other.Xmm = 20000; other.Zmm = 20000; }
            target.Xmm = target.PreviousXmm = Game.PlayerXmm + 900; target.Zmm = target.PreviousZmm = Game.PlayerZmm; target.Health = 100000;
            var animator = Find("Wanderer").GetComponentInChildren<Animator>();
            yield return null;
            for (int i = 0; i < Game.Cooldowns.Length; i++) Game.Cooldowns[i] = 0;
            typeof(ArenaSimulation).GetProperty("Resource").SetValue(Game, 100);
            var route = typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags);
            route.Invoke(_runtime, new object[] { ArenaHudControl.Skill0 });
            route.Invoke(_runtime, new object[] { ArenaHudControl.Skill1 });
            int starts = 0; bool inMelee = false; float lastTime = 0f;
            float until = Time.time + 1.6f;
            while (Time.time < until)
            {
                yield return null;
                var state = animator.GetCurrentAnimatorStateInfo(0);
                bool melee = state.IsName("Melee");
                if (melee && (!inMelee || state.normalizedTime + .05f < lastTime)) starts++;
                inMelee = melee; lastTime = melee ? state.normalizedTime : 0f;
            }
            Debug.Log("COMBAT_SAME_TICK starts=" + starts + " message=" + Message());
            Assert.GreaterOrEqual(starts, 2, "Strike and Heavy Strike in one tick must both be visible");
        }

        /// <summary>Review fix: a defeated enemy's skeleton stays frozen after the brief hit-stop ends.</summary>
        [UnityTest]
        public IEnumerator DefeatedEnemiesStayFrozenAfterHitStop()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private character and animation overlay");
            var target = Game.Monsters.Find(m => !m.IsDead);
            foreach (var other in Game.Monsters) if (other != target) { other.Xmm = 20000; other.Zmm = 20000; }
            target.Xmm = target.PreviousXmm = Game.PlayerXmm + 900; target.Zmm = target.PreviousZmm = Game.PlayerZmm; target.Health = 1;
            var visuals = (Dictionary<int, Transform>)typeof(ArenaRuntime).GetField("_monsterVisuals", Flags).GetValue(_runtime);
            var dying = visuals[target.Id];
            yield return null;
            for (int i = 0; i < Game.Cooldowns.Length; i++) Game.Cooldowns[i] = 0;
            typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags).Invoke(_runtime, new object[] { ArenaHudControl.Skill0 });
            float start = Time.time; bool checkedFrozen = false;
            while (Time.time - start < .6f)
            {
                yield return null;
                if (target.IsDead && Time.time - start > .25f && dying.gameObject.activeSelf && !checkedFrozen)
                {
                    checkedFrozen = true;
                    foreach (var animator in dying.GetComponentsInChildren<Animator>())
                        Assert.AreEqual(0f, animator.speed, "The falling enemy's skeleton must stay frozen after hit-stop");
                }
            }
            Assert.IsTrue(target.IsDead && checkedFrozen, "The frozen check must run during the fall");
        }

        /// <summary>Review fix: hits on the road bear reach its dedicated visual (bosses are not pooled enemies).</summary>
        [UnityTest]
        public IEnumerator RoadBearReactsToHitsOptically()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private character and animation overlay");
            _runtime.enabled = false;
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3); for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            var ilyan = Game.Npcs.Find(n => n.Id == 0); ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan);
            MoveStory(0, 24000); TravelStory(); MoveStory(0, 3000);
            Assert.IsTrue(Game.Progression.Road.Started, "The road encounter must be running");
            var bear = Game.Progression.Road.Bear;
            bear.Xmm = bear.PreviousXmm = Game.PlayerXmm + 900; bear.Zmm = bear.PreviousZmm = Game.PlayerZmm; bear.Health = bear.MaxHealth; bear.BossWindupTicks = 0;
            _runtime.enabled = true;
            yield return null;
            var bearVisual = Find("ARE_Road_Bear").transform;
            for (int i = 0; i < Game.Cooldowns.Length; i++) Game.Cooldowns[i] = 0;
            typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags).Invoke(_runtime, new object[] { ArenaHudControl.Skill0 });
            float maxOffset = 0f, finalOffset = 0f; float start = Time.time;
            while (Time.time - start < .7f)
            {
                yield return null;
                var core = new Vector3(bear.Xmm / 1000f, 0f, bear.Zmm / 1000f);
                var seen = bearVisual.position; seen.y = 0f;
                finalOffset = Vector3.Distance(seen, core); maxOffset = Mathf.Max(maxOffset, finalOffset);
            }
            Debug.Log("COMBAT_BOSS maxOffset=" + maxOffset.ToString("0.000") + " finalOffset=" + finalOffset.ToString("0.000") + " message=" + Message());
            Assert.That(maxOffset, Is.InRange(.15f, .6f), "A hit on the road bear knocks its own visual back");
            Assert.Less(finalOffset, .06f, "The bear springs back onto its Core position");
        }
    }
}
