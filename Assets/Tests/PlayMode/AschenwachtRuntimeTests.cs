using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// Owner decision 2026-11-25: the old crypt opens into Aschenwacht, a walkable place of its own; the wanderer reads its
    /// traces on foot and leaves through the bell tower's door for the village. The runtime shows the place (the private
    /// sets where installed, a plain floor otherwise), stands the hero on the ground the rules name and offers the traces.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator AschenwachtLiesBetweenCryptAndVillageAndIsWalkedOnFoot()
        {
            _runtime.enabled = false;
            ArrangeCryptKill(); CollectCryptLoot(); MoveStory(0, 24000);
            typeof(ArenaRuntime).GetMethod("AdvanceCrypt", Flags).Invoke(_runtime, null);
            Assert.IsTrue(Game.InCourt, "the cleared crypt opens into Aschenwacht");
            Assert.AreEqual(1, Game.Progression.EntryAct);
            Assert.AreEqual(CourtLayout.SpawnZmm, Game.PlayerZmm, "the wanderer steps out in front of the crypt portal");
            yield return null;

            Transform hero = Find("Wanderer").transform;
            float ground = CourtLayout.HeightMm(Game.PlayerXmm, Game.PlayerZmm) / 1000f;
            Assert.Greater(ground, .9f, "the portal stands on its terrace");
            Assert.AreEqual(1.05f + ground, hero.position.y, .06f, "the hero stands on the terrace, not on the yard below it");
            var court = Find("AshenCrypt_BellCourt");
            Assert.IsNotNull(court ?? Find("Aschenwacht_FallbackFloor"), "the place is shown");
            if (court != null)
            {
                Assert.IsNotNull(court.transform.Find("Art_EntryCourtWorld"), "the private sets are bound as passive art");
                Assert.AreEqual(9, court.GetComponentsInChildren<Light>().Length, "the fire baskets burn");
            }
            GameObject passage = Find("ARE_World_Passage");
            Assert.IsNotNull(passage, "the passage to the village exists");
            Assert.AreEqual(CourtLayout.ExitZmm / 1000f, passage.transform.position.z, .01f, "it waits at the bell tower");
            StringAssert.Contains("Aschenwacht", ArenaRuntime.ObjectiveText(Game));

            // The first trace lies at the portal: the story action offers it and reading it keeps it in the save.
            Assert.IsTrue(Game.NearCourtTrace.HasValue);
            yield return StoryAction(HungerAction.ReadCourtTrace);
            Assert.AreEqual(1, Game.Progression.CourtTraces & 1, "the portal's trace is read");
            StringAssert.Contains("Spuren: 1/8", ArenaRuntime.ObjectiveText(Game));
            _runtime.enabled = false;

            // Down the steps into the yard: the hero follows the ground the rules name.
            Assert.IsTrue(WalkCourt(0, 0, 650, 300), "the yard is reachable from the portal");
            _runtime.enabled = true;
            yield return null; yield return null;
            _runtime.enabled = false;
            Assert.AreEqual(1.05f + CourtLayout.HeightMm(Game.PlayerXmm, Game.PlayerZmm) / 1000f, hero.position.y, .25f, "the hero walked down into the yard");

            CrossCourt();
            Assert.IsTrue(Game.IsVillage, "the bell tower's door leads to the village");
            Assert.IsFalse(Game.InCourt);
            Assert.AreEqual(2, Game.Progression.EntryAct);
        }
    }
}
