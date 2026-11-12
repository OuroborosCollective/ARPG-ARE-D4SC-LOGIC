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
        /// <summary>Actual journal queue, passage/save adapter, full-health Core fight and native private model projection.</summary>
        [UnityTest]
        public IEnumerator RattlerDenUsesBlindCombatAndPersistsTheRoyalBrand()
        {
            yield return CompleteSupplyJourney();
            MoveStory(0, 24000); TravelStory(); MoveStory(0, 0); MoveStory(-6500, 0);
            yield return StoryAction(HungerAction.StoreBandage);
            MoveStory(0, 0); MoveStory(0, -3000); MoveStory(6500, -3000);
            yield return StoryAction(HungerAction.StoreTracks);
            MoveStory(0, -3000); MoveStory(0, 18000);
            yield return StoryAction(HungerAction.InspectSeal); yield return StoryAction(HungerAction.OpenSeal);
            MoveStory(0, 24000); TravelStory(); MoveStory(0, 15000);
            yield return StoryAction(HungerAction.InspectCryptTrace);
            yield return StoryAction(HungerAction.OpenInnerDoor);
            Assert.IsFalse(Find("HiddenCrypt_InnerDoor").activeSelf);
            MoveStory(0, 24000); TravelStory();
            Assert.AreEqual(7, Game.Progression.EntryAct);
            yield return StoryAction(HungerAction.ToggleSneak);
            MoveStory(0, 0);
            Assert.AreEqual(0, Game.Progression.Rattler.MemoryTicks);
            typeof(ArenaRuntime).GetMethod("UpdateRattlerPresentation", Flags).Invoke(_runtime, null);
            var creature = Find("ARE_MaulwurfRattler"); Assert.IsNotNull(creature);
            yield return StoryAction(HungerAction.ToggleSneak);
            bool captured = false;
            for (int tick = 0; tick < 600 && !Game.GameOver && !Game.Progression.Rattler.Creature.IsDead; tick++)
            {
                var r = Game.Progression.Rattler; var m = r.Creature;
                if (m.BossWindupTicks > 0) Game.SetMoveInput(0, -10000);
                else
                {
                    long dz = m.Zmm - Game.PlayerZmm;
                    Game.SetMoveInput(0, System.Math.Abs(dz) > 1800 ? System.Math.Sign(dz) * 10000 : 0);
                    Game.ActivateSkill(3); Game.ActivateSkill(1); Game.ActivateSkill(0);
                }
                Game.Step();
                typeof(ArenaRuntime).GetMethod("UpdateRattlerPresentation", Flags).Invoke(_runtime, null);
                typeof(ArenaRuntime).GetMethod("UpdatePresentation", Flags).Invoke(_runtime, new object[] { 1f });
                if (!captured && m.BossWindupTicks > 0)
                {
                    Assert.IsTrue(Find("ARE_Rattler_Strike_Warning").activeSelf);
                    Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();
                    yield return Capture("rattler-telegraph.png"); captured = true;
                }
            }
            Assert.IsTrue(captured); Assert.IsFalse(Game.GameOver); Assert.IsTrue(Game.Progression.Rattler.Creature.IsDead);
            MoveStory(Game.Progression.Rattler.Creature.Xmm, Game.Progression.Rattler.Creature.Zmm);
            yield return StoryAction(HungerAction.InspectRoyalBrand);
            Assert.IsTrue(Game.Progression.Rattler.BrandExamined);
            Assert.IsFalse(Game.Progression.Rattler.Reported);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), out var checkpoint));
            Assert.IsTrue(checkpoint.Rattler.BrandExamined && checkpoint.Rattler.Creature.IsDead);
            Assert.AreEqual(Game.Progression.LifetimeKills, checkpoint.LifetimeKills);
            yield return Capture("rattler-brand-discovered.png");
            MoveStory(0, -24000); TravelStory();
            Assert.AreEqual(6, Game.Progression.EntryAct); Assert.IsTrue(Game.CanStand(Game.PlayerXmm, Game.PlayerZmm));
        }
        /// <summary>Separate private-art acceptance: absence, an unanimated rig or a missing neck brand is a real failure.</summary>
        [UnityTest]
        public IEnumerator RattlerPrivateAssetBindsBoneAnimationAndBrand()
        {
            _runtime.enabled = false;
            var creature = new GameObject("Rattler_Private_Art_Check");
            Assert.IsTrue(PassiveArtVisuals.TryBindGrounded(creature.transform, "CryptRattler", 0),
                "Creep Horror Creature must be imported as the reviewed private CryptRattler prefab");
            Assert.IsNotEmpty(creature.GetComponentsInChildren<SkinnedMeshRenderer>(), "The requested Creep asset must actually render");
            var animator = creature.GetComponentInChildren<Animator>(); Assert.IsNotNull(animator);
            Assert.IsNotNull(animator.runtimeAnimatorController); Assert.IsFalse(animator.applyRootMotion);
            foreach (var skin in creature.GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach (var material in skin.sharedMaterials) Assert.IsTrue(material != null && material.mainTexture != null);
            Assert.IsTrue(System.Array.Exists(creature.GetComponentsInChildren<Transform>(true), t => t.name == "Immergruen_Royal_Brand"), "The neck brand must be present on the private visual");
            var bones = creature.GetComponentsInChildren<Transform>();
            animator.Play("Idle", 0, .2f); animator.Update(0);
            var poses = new Quaternion[bones.Length]; for (int i = 0; i < poses.Length; i++) poses[i] = bones[i].localRotation;
            animator.Play("Attack", 0, .45f); animator.Update(0);
            int changed = 0; for (int i = 0; i < poses.Length; i++) if (Quaternion.Angle(poses[i], bones[i].localRotation) > .5f) changed++;
            Assert.Greater(changed, 5, "An attached controller without actual bone animation is insufficient");
            animator.Play("Idle", 0, 0); animator.Update(0);
            yield return null;
        }
    }
}
