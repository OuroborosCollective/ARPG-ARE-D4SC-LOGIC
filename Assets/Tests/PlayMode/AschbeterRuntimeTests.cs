using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// Owner decision 2026-11-26: the Aschbeter is an enemy by the archive's rules. The runtime shows him where the rules put
    /// him, plays the archive's clip for each of his modes, and lets a swing from behind, while he turns, throw him down dead.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator TheAschbeterTurnsToTheWandererAndFallsToASwingFromBehind()
        {
            _runtime.enabled = false;
            ArrangeCryptKill(); CollectCryptLoot(); MoveStory(0, 24000);
            typeof(ArenaRuntime).GetMethod("AdvanceCrypt", Flags).Invoke(_runtime, null);
            Assert.IsTrue(Game.InCourt && Game.Aschbeter != null, "the Aschbeter stands in Aschenwacht");
            _runtime.enabled = true;
            yield return null; yield return null;
            _runtime.enabled = false;
            Transform view = Find("ARE_Aschbeter").transform;
            Assert.AreEqual(AschbeterState.StartZmm / 1000f, view.position.z, .05f, "he is shown where the rules put him");
            Assert.AreEqual(180f, view.eulerAngles.y, 1f, "facing the crypt portal");
            Animator animator = view.GetComponentInChildren<Animator>();
            bool figure = animator != null && animator.runtimeAnimatorController != null;
            if (figure) Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") || animator.GetNextAnimatorStateInfo(0).IsName("Idle"), "he rests");

            // Round him at a distance, then behind him: he turns towards the wanderer, slowly.
            Assert.IsTrue(WalkCourt(0, AschbeterState.StartZmm + 2250, 300, 900), "the wanderer reaches the Aschbeter's back");
            var a = Game.Aschbeter;
            for (int i = 0; i < 4 && (Game.PlayerXmm - a.Xmm) * (Game.PlayerXmm - a.Xmm) + (Game.PlayerZmm - a.Zmm) * (Game.PlayerZmm - a.Zmm) > 1900L * 1900L; i++)
            {
                long dx = a.Xmm - Game.PlayerXmm, dz = a.Zmm - Game.PlayerZmm, scale = System.Math.Max(1, System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)));
                Game.SetMoveInput(dx * 3000 / scale, dz * 3000 / scale); Game.Step();
            }
            Game.SetMoveInput(0, 0);
            Assert.IsTrue(a.Mode == AschbeterMode.TurnPositive || a.Mode == AschbeterMode.TurnNegative, "he turns towards the wanderer");
            _runtime.enabled = true;
            yield return null; yield return null; yield return null;
            _runtime.enabled = false;
            if (figure) Assert.IsTrue(IsAny(animator, "TurnLeft", "TurnRight"), "his turn is shown with the archive's turning clip");

            // One swing from behind while he turns: he falls dead and leaves loot where he lies.
            int loot = Game.GroundLoot.Count;
            var swing = Game.ActivateSkill(0);
            Assert.IsTrue(swing.Activated, "the swing goes out: " + swing.Message);
            for (int i = 0; i < 12 && a.Mode != AschbeterMode.Fall; i++) Game.Step();
            Assert.AreEqual(AschbeterSwing.Fatal, a.LastSwing, "a swing from behind into his turn is fatal");
            _runtime.enabled = true;
            yield return null; yield return null;
            StringAssert.Contains("fällt", ReadMessage(), "the wanderer reads that he falls");
            if (figure) Assert.IsTrue(IsAny(animator, "Fall"), "he falls with the archive's clip");
            for (int i = 0; i < 40 && !a.IsDead; i++) yield return new WaitForSeconds(.05f);
            for (int i = 0; i < 6; i++) yield return null;
            _runtime.enabled = false;
            Assert.IsTrue(a.IsDead && Game.Progression.AschbeterFallen, "he lies dead");
            Assert.AreEqual(loot + 1, Game.GroundLoot.Count, "his loot lies where he fell");
            if (figure) Assert.IsTrue(IsAny(animator, "DeathStay"), "he lies in the death pose");
        }

        private static bool IsAny(Animator animator, params string[] states)
        {
            foreach (string state in states)
                if (animator.GetCurrentAnimatorStateInfo(0).IsName(state) || animator.GetNextAnimatorStateInfo(0).IsName(state)) return true;
            return false;
        }

        private string ReadMessage() { return (string)typeof(ArenaRuntime).GetField("_message", Flags).GetValue(_runtime); }
    }
}
