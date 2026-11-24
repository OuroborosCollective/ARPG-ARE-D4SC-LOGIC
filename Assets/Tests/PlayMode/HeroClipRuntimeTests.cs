using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// The hero's real combat clips follow the Core (issue #48, block 3): a dodge rolls for as long as the Core's dodge
    /// lasts, a blow that gets through makes him flinch, a flask is drunk, the guard stays up while the Core holds it,
    /// and a defeat lets him fall and stay down. The Core is driven directly; the presentation is asked once per frame,
    /// as the running game does. Needs the private animation overlay; without it the test is ignored.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static bool HeroShows(Animator hero, string state)
        {
            return hero.GetCurrentAnimatorStateInfo(0).IsName(state) || (hero.IsInTransition(0) && hero.GetNextAnimatorStateInfo(0).IsName(state));
        }

        private IEnumerator HeroFrames(int frames)
        {
            var update = typeof(ArenaRuntime).GetMethod("UpdateHeroClips", Flags);
            for (int frame = 0; frame < frames; frame++) { update.Invoke(_runtime, null); yield return null; }
        }

        private void FinishHeroAction()
        {
            for (int tick = 0; tick < 40 && Game.ActionKind != CombatActionKind.None; tick++) Game.Step();
            Assert.AreEqual(CombatActionKind.None, Game.ActionKind, "The Core's action ends");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator HeroClipsFollowTheCoresActions()
        {
            _runtime.enabled = false;
            var animators = (Animator[])typeof(ArenaRuntime).GetField("_heroMeleeAnimations", Flags).GetValue(_runtime);
            Animator hero = animators.Length > 0 ? animators[0] : null;
            if (hero == null || !hero.HasState(0, Animator.StringToHash("Dodge")))
                Assert.Ignore("Requires the private animation overlay with the licensed combat clips");
            Assert.IsTrue(Game.CombatV2, "A new character fights with combat v2");
            ArrangeCryptKill(); // nobody is left to interfere
            FinishHeroAction();

            var clips = hero.runtimeAnimatorController.animationClips.GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
            foreach (var (name, seconds) in new[] { ("EntryGuard", .67f), ("EntryCast", 2.2f), ("EntryHit", .67f), ("EntryDeath", 1.67f), ("EntryDodge", 1.53f), ("EntryDrink", 1.53f) })
            {
                Assert.IsTrue(clips.ContainsKey(name), name + " is part of the hero's controller");
                Assert.IsTrue(clips[name].humanMotion, name + " is a humanoid take");
                Assert.AreEqual(seconds, clips[name].length, .06f, name + " is the licensed take, not a generated pose");
            }
            Assert.IsTrue(clips["EntryGuard"].isLooping, "The guard is held as a loop");

            float before = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = .05f;
                yield return HeroFrames(20); // the ruin storm's clip plays out

                Assert.IsTrue(Game.StartDodge(0, 10000), "The Core accepts the dodge");
                yield return HeroFrames(3);
                Assert.IsTrue(HeroShows(hero, "Dodge"), "The hero rolls when the Core starts a dodge");
                Assert.AreEqual(clips["EntryDodge"].length / (CombatV2Rules.Dodge.Total * .1f), hero.GetFloat("ActionSpeed"), .02f, "The roll spans the Core's dodge");
                FinishHeroAction();
                yield return HeroFrames(24);
                Assert.IsFalse(HeroShows(hero, "Dodge"), "and gets up again");

                typeof(ArenaSimulation).GetProperty("PlayerHealth").SetValue(Game, Game.MaxPlayerHealth - 200);
                yield return HeroFrames(3);
                Assert.IsTrue(HeroShows(hero, "Hit"), "A blow that gets through makes the hero flinch");
                yield return HeroFrames(24);
                Assert.IsFalse(HeroShows(hero, "Hit"), "The flinch passes");

                Game.Progression.Crafting.AddPotion(PotionType.Heal, PotionTier.Small);
                Assert.IsTrue(Game.DrinkPotion(PotionType.Heal), "The Core accepts the flask");
                yield return HeroFrames(3);
                Assert.IsTrue(HeroShows(hero, "Drink"), "The hero drinks when the Core starts the flask");
                Assert.AreEqual(clips["EntryDrink"].length / (CombatV2Rules.Drink.Total * .1f), hero.GetFloat("ActionSpeed"), .02f, "The drink spans the Core's action");
                FinishHeroAction();
                yield return HeroFrames(24);
                Assert.IsFalse(HeroShows(hero, "Drink"), "The flask is put away");

                Game.SetGuard(true); Game.Step(); Game.Step();
                Assert.AreEqual(CombatActionKind.Guard, Game.ActionKind, "The Core raises the guard");
                yield return HeroFrames(4);
                Assert.IsTrue(HeroShows(hero, "Guard"), "The hero raises his guard");
                for (int tick = 0; tick < 10; tick++) { Game.Step(); yield return HeroFrames(2); }
                Assert.AreEqual(CombatActionKind.Guard, Game.ActionKind, "The Core still holds the guard");
                Assert.IsTrue(HeroShows(hero, "Guard"), "The guard stays up for longer than one loop of its clip");
                Game.SetGuard(false);
                FinishHeroAction();
                yield return HeroFrames(8);
                Assert.IsFalse(HeroShows(hero, "Guard"), "The guard comes down when the Core lowers it");

                typeof(ArenaSimulation).GetProperty("PlayerHealth").SetValue(Game, 0);
                Assert.IsTrue(Game.GameOver);
                yield return HeroFrames(4);
                Assert.IsTrue(HeroShows(hero, "Death"), "A defeated hero falls");
                hero.SetTrigger("Melee");
                yield return HeroFrames(8);
                Assert.IsTrue(HeroShows(hero, "Death"), "A queued strike does not get a fallen hero up");
                hero.ResetTrigger("Melee");
            }
            finally { Time.captureDeltaTime = before; }
        }
    }
}
