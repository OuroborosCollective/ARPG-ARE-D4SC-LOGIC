using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.UI;
using AshenCrypt.MemoryForge;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator RattlerExecutesItsExplicitlyAdmittedWaitPolicyInTheActualRuntime()
        {
            _runtime.enabled=false;var p=CharacterProgression.CreateEntryProfile();
            typeof(CharacterProgression).GetMethod("EnterVillage",Flags).Invoke(p,null);
            for(int i=0;i<4;i++)typeof(CharacterProgression).GetMethod("DiscoverHungerClue",Flags).Invoke(p,null);
            typeof(CharacterProgression).GetMethod("ChooseHungerSupply",Flags).Invoke(p,new object[]{HungerChoice.Protect});
            typeof(CharacterProgression).GetMethod("TravelHunger",Flags).Invoke(p,new object[]{7,false});p.Social.EnableForge();
            DungeonFixtureSet(p.Investigation,"Clues",7);DungeonFixtureSet(p.Investigation,"Approach",SealApproach.Investigated);DungeonFixtureSet(p.Investigation,"TraceFound",true);DungeonFixtureSet(p.Rattler,"Active",true);
            var world=new ArenaSimulation(73,p,1,LootCatalog.Version1);var creature=p.Rattler.Creature;
            var registry=(NpcArchetypeControllerRegistry)typeof(NpcForgeState).GetField("Registry",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
            var candidate=new ControllerArtifact(creature.Mind.Controller.Archetype,"test.native.calm-animal",1,new[]{new UtilityRule(ForgeAction.Wait,0,0,new int[(int)Feature.Count])});
            var input=new ObservationFrame(ActionSet.Of(ForgeAction.Wait,ForgeAction.Attack),new[]{new Observation(Feature.Danger,10000)});
            registry.Register(candidate,candidate.Hash,ControllerAdmission.Run(candidate,"test.native.calm",new[]{new RegressionCase(input,ForgeAction.Wait)}),false);
            Assert.IsTrue(world.TryMigrateNpcController(creature.Mind.Id,candidate.ControllerId,1,candidate.Hash));
            // Arrange only proximity; actual perception, policy and runtime tick execute normally.
            creature.Xmm=world.PlayerXmm+1000;creature.Zmm=world.PlayerZmm;
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{world,"Geprüfter Testcontroller"});yield return null;
            int health=world.PlayerHealth;for(int i=0;i<20;i++)TickOnce();
            Assert.AreEqual(ForgeAction.Wait,creature.Mind.DecisionState.Action);Assert.AreEqual(0,creature.BossWindupTicks);Assert.AreEqual(health,world.PlayerHealth);
        }

        /// <summary>A real companion strike needs moving arm poses inside its clip, not just blends to and from a still.</summary>
        [UnityTest]
        public IEnumerator HunterBowShotMovesTheHumanoidArmsAndReturnsWithoutChangingCoreOrSave()
        {
            if(string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private hunter and animation overlay");
            _runtime.enabled=false;
            var p=CharacterProgression.CreateEntryProfile();
            typeof(CharacterProgression).GetMethod("EnterVillage",Flags).Invoke(p,null);
            typeof(CharacterProgression).GetMethod("DiscoverHungerClue",Flags).Invoke(p,null);
            typeof(CharacterProgression).GetMethod("TravelHunger",Flags).Invoke(p,new object[]{3,false});p.Social.EnableForge();p.Equipment.Enable(p);
            var world=new ArenaSimulation(73,p,1,LootCatalog.Version1);
            // Arrange encounter contact only; rescue and recruitment use the existing gameplay path.
            DungeonFixtureSet(p.Road,"Started",true);p.Road.Bear.Xmm=world.PlayerXmm+1000;p.Road.Bear.Zmm=world.PlayerZmm;p.Road.Bear.Health=1;
            Assert.Greater(world.ActivateSkill(0).Hits,0);
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{world,"Jäger gerettet"});
            MoveStory(2000,9000);yield return null;
            typeof(ArenaRuntime).GetMethod("QueueCompanionAction",Flags).Invoke(_runtime,new object[]{0});TickOnce();yield return null;
            Assert.AreEqual(CompanionStatus.Following,p.Companion.Status);

            var actor=Find("ARE_Companion_Hunter");Assert.IsNotNull(actor);
            Assert.IsNotEmpty(actor.GetComponentsInChildren<SkinnedMeshRenderer>(),"Inspect the actual skinned hunter, not its fallback capsule");
            var animator=actor.GetComponentInChildren<Animator>();Assert.IsNotNull(animator);Assert.IsTrue(animator.isHuman);
            Assert.IsFalse(animator.applyRootMotion);
            var bow=actor.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="CompanionWeapon_bow_newbie.1");
            Assert.IsNotNull(bow);Assert.IsTrue(bow.IsChildOf(animator.GetBoneTransform(HumanBodyBones.LeftHand)));
            var arms=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm}
                .Select(animator.GetBoneTransform).ToArray();
            foreach(var bone in arms)Assert.IsNotNull(bone,"The real humanoid arms must be available for pose inspection");
            int attackState=Animator.StringToHash("Melee"),idleState=Animator.StringToHash("Idle");
            animator.ResetTrigger("Melee");animator.SetFloat("MoveMagnitude",0f);animator.Play(idleState,0,0f);animator.Update(0f);

            var target=new MonsterState{Id=91,Kind=0,Xmm=p.Companion.Xmm+1400,Zmm=p.Companion.Zmm,Health=1000,MaxHealth=1000};
            Game.Monsters.Add(target);
            for(int tick=0;tick<40&&target.Health==1000;tick++)TickOnce();
            Assert.Less(target.Health,1000,"A real companion hit must precede the bow presentation");
            Assert.Greater(p.Companion.AttackFlash,0);
            animator.Update(0f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).shortNameHash==attackState||
                animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).shortNameHash==attackState,
                "The accepted companion strike must enter the actual attack controller state");

            ulong hash=Game.ComputeWorldHash();string save=p.SaveString();
            var stored=_keys.Select(key=>PlayerPrefs.GetString(key,"")).ToArray();
            var hadKeys=_keys.Select(PlayerPrefs.HasKey).ToArray();
            var actorPosition=actor.transform.position;var actorRotation=actor.transform.rotation;
            var rigPosition=animator.transform.localPosition;var rigRotation=animator.transform.localRotation;
            var controller=animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.IsNotNull(controller,"Inspect the actual equipped bow controller");
            var overrides=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>>();
            controller.GetOverrides(overrides);
            var attacks=overrides.Where(pair=>pair.Key.name.Contains("Strike")).ToArray();
            Assert.AreEqual(1,attacks.Length,"The production controller must expose one attack binding");
            var shot=attacks[0].Value??attacks[0].Key;
            Assert.IsNotNull(shot);Assert.IsTrue(shot.humanMotion);Assert.Greater(shot.length,0f);
            Assert.AreSame(Resources.Load<AnimationClip>("ARE/Visuals/WeaponBowShot"),shot,
                "Sample the bow clip actually bound to the production attack state");

            // Finish the genuinely triggered controller cycle before isolated clip inspection.
            // Positive, bounded presentation steps traverse its .04 entry and .08 exit blends.
            animator.SetFloat("MoveMagnitude",0f);
            for(int step=0;step<32;step++)animator.Update((shot.length+.2f)/32f);
            Assert.AreEqual(idleState,animator.GetCurrentAnimatorStateInfo(0).shortNameHash,"The accepted shot must recover to locomotion");
            Assert.IsFalse(animator.IsInTransition(0),"The actual attack exit must complete");

            var poses=new Quaternion[13][];
            var graph=PlayableGraph.Create("HunterBowMotionRegression");
            try
            {
                // A single clip drives the real humanoid with manual evaluation. No controller
                // transition or empty ClipInfo result can substitute for movement inside the clip.
                animator.runtimeAnimatorController=null;
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,shot);
                playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);playable.SetSpeed(0d);
                var output=AnimationPlayableOutput.Create(graph,"Hunter bow pose",animator);
                output.SetSourcePlayable(playable);output.SetWeight(1f);graph.Play();
                playable.SetTime(0d);graph.Evaluate(0f); // Initialize native bindings before recording.
                for(int sample=0;sample<poses.Length;sample++)
                {
                    playable.SetTime((double)shot.length*sample/(poses.Length-1));graph.Evaluate(0f);
                    poses[sample]=arms.Select(bone=>bone.localRotation).ToArray();
                }
                playable.SetTime(0d);graph.Evaluate(0f);
                Assert.LessOrEqual(HunterArmPoseDistance(poses[0],arms.Select(bone=>bone.localRotation).ToArray()),.1f,
                    "Sampling the same clip time twice must reproduce the same arm pose");
            }
            finally
            {
                if(graph.IsValid())graph.Destroy();
                animator.runtimeAnimatorController=controller;
            }
            Assert.AreEqual(actorPosition,actor.transform.position);Assert.AreEqual(actorRotation,actor.transform.rotation);
            Assert.AreEqual(rigPosition,animator.transform.localPosition);Assert.AreEqual(rigRotation,animator.transform.localRotation);
            Assert.AreEqual(hash,Game.ComputeWorldHash(),"Animation must not advance or mutate the authoritative world");
            Assert.AreEqual(save,p.SaveString(),"Animation must not change companion progress or save contents");
            for(int i=0;i<_keys.Length;i++)
            {Assert.AreEqual(hadKeys[i],PlayerPrefs.HasKey(_keys[i]));Assert.AreEqual(stored[i],PlayerPrefs.GetString(_keys[i],""));}

            // Match the existing rig-motion test's .1-degree noise tolerance; this is a still-pose
            // regression gate, not an invented art-direction threshold for the final draw timing.
            int second=-1,third=-1;float maximumMotion=0f;
            for(int sample=1;sample<poses.Length;sample++)
            {
                float fromStart=HunterArmPoseDistance(poses[0],poses[sample]);maximumMotion=Mathf.Max(maximumMotion,fromStart);
                if(fromStart<=.1f)continue;
                if(second<0)second=sample;
                else if(third<0&&HunterArmPoseDistance(poses[second],poses[sample])>.1f)third=sample;
            }
            TestContext.WriteLine("Actual bow clip: "+shot.name+", length="+shot.length+"s, sampled arm motion="+maximumMotion+" degrees");
            Assert.Greater(maximumMotion,.1f,"The actual bow clip is a still pose: the hunter's arms must move within the attack, independently of transition blends");
            Assert.Greater(third,second,"The bow shot needs at least three distinct arm poses through its motion, not a switch between two frozen poses");
        }

        private static float HunterArmPoseDistance(Quaternion[] first,Quaternion[] second)
        {
            float angle=0f;
            for(int bone=0;bone<first.Length;bone++)angle=Mathf.Max(angle,Quaternion.Angle(first[bone],second[bone]));
            return angle;
        }

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
            var playerXp=p.TotalExperience;var playerKills=p.LifetimeKills;var runKills=Game.Kills;
            var enemy=new MonsterState{Id=91,Kind=0,Xmm=p.Companion.Xmm+1400,Zmm=p.Companion.Zmm,Health=1,MaxHealth=1000};Game.Monsters.Add(enemy);
            for(int i=0;i<40&&p.Companion.TotalExperience.IsZero;i++)TickOnce();
            Assert.Greater(p.Companion.TotalExperience,System.Numerics.BigInteger.Zero);Assert.Less(enemy.Health,1000);
            Assert.IsTrue(enemy.IsDead);Assert.AreEqual(playerXp,p.TotalExperience);Assert.AreEqual(playerKills,p.LifetimeKills);Assert.AreEqual(runKills,Game.Kills);
            MoveStory(enemy.Xmm,enemy.Zmm);Assert.IsTrue(Game.TryCollectLoot());Assert.AreEqual(1,p.Items.Count);
            var xp=p.Companion.TotalExperience;
            _runtime.OpenMenu(4);yield return null;
            var wait=Find("CompanionWait");Assert.IsNotNull(wait);wait.GetComponent<Button>().onClick.Invoke();TickOnce();
            Assert.AreEqual(CompanionStatus.Waiting,p.Companion.Status);
            typeof(ArenaRuntime).GetMethod("SaveCharacter",Flags,null,System.Type.EmptyTypes,null).Invoke(_runtime,null);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var loaded));
            Assert.AreEqual(xp,loaded.Companion.TotalExperience);Assert.AreEqual(CompanionStatus.Waiting,loaded.Companion.Status);
            Assert.AreEqual(playerXp,loaded.TotalExperience);Assert.AreEqual(playerKills,loaded.LifetimeKills);Assert.AreEqual(1,loaded.Items.Count);
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
