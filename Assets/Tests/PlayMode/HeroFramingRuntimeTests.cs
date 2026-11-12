using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        private void ProjectCameraFrame()
        { typeof(ArenaRuntime).GetMethod("UpdatePresentation", Flags).Invoke(_runtime, new object[] { 1f }); }

        private void ReachQuietVillage()
        {
            _runtime.enabled = false;
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3); for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory(); Game.SetMoveInput(0, 0); Game.Step(); ProjectCameraFrame();
        }

        private float HeroViewportHeight()
        {
            var hero = Find("Wanderer").transform;
            return Mathf.Abs(Camera.main.WorldToViewportPoint(hero.position + Vector3.up * .55f).y -
                Camera.main.WorldToViewportPoint(hero.position - Vector3.up).y);
        }

        [UnityTest]
        public IEnumerator MovingHeroKeepsTheCloserTravelFraming()
        {
            ReachQuietVillage(); Game.SetMoveInput(10000, 0); Game.Step(); ProjectCameraFrame();
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(HeroFraming.Travel, Camera.main.GetComponent<ArenaCameraRig>().Framing);
            Assert.Greater(HeroViewportHeight(), .17f);
            yield return Capture("hero-camera-travel.png");
        }

        [UnityTest]
        public IEnumerator QuietStandingShowsReadableArmourWithoutChangingTheWorld()
        {
            ReachQuietVillage();
            ulong hash = Game.ComputeWorldHash(); string save = Game.Progression.SaveString();
            yield return new WaitForSeconds(2.8f);
            Assert.Greater(HeroViewportHeight(), .32f, "A quiet hero must fill enough of the real viewport to show armour.");
            Assert.Less(Camera.main.transform.eulerAngles.x, 27f, "Idle must lower the steep overview angle.");
            Assert.AreEqual(hash, Game.ComputeWorldHash()); Assert.AreEqual(save, Game.Progression.SaveString());
            yield return Capture("hero-camera-idle.png");
        }

        [UnityTest]
        public IEnumerator AcceptedDialogueMovesTheActualCameraCloser()
        {
            ReachQuietVillage();
            var ilyan = Game.Npcs.Find(n => n.Id == 0); ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan); ProjectCameraFrame();
            Assert.IsNotEmpty(_runtime.GetComponentInChildren<AshenAtmosphere>().Subtitle);
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(HeroViewportHeight(), .28f, "Dialogue must magnify the real hero, not a screenshot-only camera.");
            Assert.Less(Camera.main.transform.eulerAngles.x, 27f);
            yield return Capture("hero-camera-dialogue.png");
        }
    }
}
