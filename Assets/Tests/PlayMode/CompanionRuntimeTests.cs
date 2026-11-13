using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator RescuedCompanionHasRealModelControlsCombatAndSavedIndividualProgress()
        {
            _runtime.enabled=false;
            var p=CharacterProgression.CreateEntryProfile();
            typeof(CharacterProgression).GetMethod("EnterVillage",Flags).Invoke(p,null);
            typeof(CharacterProgression).GetMethod("DiscoverHungerClue",Flags).Invoke(p,null);
            typeof(CharacterProgression).GetMethod("TravelHunger",Flags).Invoke(p,new object[]{3,false});p.Social.EnableForge();p.Equipment.Enable(p);
            var w=new ArenaSimulation(73,p,1,LootCatalog.Version1);
            // Only encounter health/contact are arranged. The strike, rescue, input, combat and persistence are real.
            DungeonFixtureSet(p.Road,"Started",true);p.Road.Bear.Xmm=w.PlayerXmm+1000;p.Road.Bear.Zmm=w.PlayerZmm;p.Road.Bear.Health=1;
            Assert.Greater(w.ActivateSkill(0).Hits,0);
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{w,"Jäger gerettet"});
            MoveStory(2000,9000);yield return null;
            var queue=typeof(ArenaRuntime).GetMethod("QueueCompanionAction",Flags);
            Assert.IsNotNull(queue,"The rescued hunter needs a playable companion control");
            queue.Invoke(_runtime,new object[]{0});TickOnce();yield return null;
            Assert.AreEqual(CompanionStatus.Following,p.Companion.Status);
            var actor=Find("ARE_Companion_Hunter");Assert.IsNotNull(actor);Assert.IsNotEmpty(actor.GetComponentsInChildren<SkinnedMeshRenderer>());
            Assert.IsFalse(Find("ARE_Road_Hunter").activeSelf,"Recruitment must not leave a duplicate hunter behind");
            Assert.IsNotNull(actor.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="CompanionWeapon_bow_newbie.1"));
            var animator=actor.GetComponentInChildren<Animator>();Assert.IsNotNull(animator);Assert.IsTrue(animator.isHuman);Assert.IsFalse(animator.applyRootMotion);
            Assert.IsTrue(animator.HasState(0,Animator.StringToHash("Move")));
            Assert.IsTrue(animator.runtimeAnimatorController.animationClips.Any(c=>c.name=="WeaponBowShot"));
            var enemy=new MonsterState{Id=91,Kind=0,Xmm=p.Companion.Xmm+1400,Zmm=p.Companion.Zmm,Health=1000,MaxHealth=1000};Game.Monsters.Add(enemy);
            for(int i=0;i<40&&p.Companion.TotalExperience.IsZero;i++)TickOnce();
            Assert.Greater(p.Companion.TotalExperience,System.Numerics.BigInteger.Zero);Assert.Less(enemy.Health,1000);
            enemy.Health=0; // End the arranged encounter; no fabricated kill reward or practice.
            var xp=p.Companion.TotalExperience;
            _runtime.OpenMenu(4);yield return null;
            var wait=Find("CompanionWait");Assert.IsNotNull(wait);wait.GetComponent<Button>().onClick.Invoke();TickOnce();
            Assert.AreEqual(CompanionStatus.Waiting,p.Companion.Status);
            typeof(ArenaRuntime).GetMethod("SaveCharacter",Flags,null,System.Type.EmptyTypes,null).Invoke(_runtime,null);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var loaded));
            Assert.AreEqual(xp,loaded.Companion.TotalExperience);Assert.AreEqual(CompanionStatus.Waiting,loaded.Companion.Status);
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{new ArenaSimulation(73,loaded,1,LootCatalog.Version1),"Jäger wartet"});
            queue.Invoke(_runtime,new object[]{0});TickOnce();yield return null;
            Assert.AreEqual(CompanionStatus.Following,Game.Progression.Companion.Status);
            Assert.AreEqual(xp,Game.Progression.Companion.TotalExperience);
            Assert.AreEqual(10000,Game.Progression.Social.Forge.Archive.Get("hunter").RelationshipTo("player").Gratitude);
            MoveStory(2000,13000);
            typeof(ArenaRuntime).GetMethod("UpdateCompanionPresentation",Flags).Invoke(_runtime,new object[]{1f});DungeonProject();
            actor=Find("ARE_Companion_Hunter");animator=actor.GetComponentInChildren<Animator>();
            animator.Update(.15f);var leg=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var pose=leg.localRotation;animator.Update(.25f);
            Assert.Greater(Quaternion.Angle(pose,leg.localRotation),.1f,"The actual owned walk clip must move the companion rig");
            Assert.Less(Vector3.Distance(actor.transform.position,new Vector3(Game.Progression.Companion.Xmm/1000f,1,Game.Progression.Companion.Zmm/1000f)),.02f);
            yield return null;
            yield return Capture("forge-hunter-companion-after-load.png");
        }
    }
}
