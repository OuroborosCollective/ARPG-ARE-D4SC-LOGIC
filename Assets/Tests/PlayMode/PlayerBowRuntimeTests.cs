using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        /// <summary>Inspect the shared bow take on the real player after a Core hit, then restore melee through inventory.</summary>
        [UnityTest]
        public IEnumerator PlayerBowShotMovesAfterAcceptedHitAndInventoryRestoresMelee()
        {
            if(string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private player, equipment and animation overlay");
            _runtime.enabled=false;
            var p=Game.Progression;p.Equipment.Enable(p);
            var hero=Find("Wanderer");Assert.IsNotNull(hero);
            Assert.IsNotEmpty(hero.GetComponentsInChildren<SkinnedMeshRenderer>());
            var animator=hero.GetComponentInChildren<Animator>();Assert.IsNotNull(animator);Assert.IsTrue(animator.isHuman);
            Assert.IsFalse(animator.applyRootMotion);
            var meleeController=animator.runtimeAnimatorController;Assert.IsNotNull(meleeController);

            // Arrange owned inventory with real catalog generation and collection; equipping uses the actual UI/Core path.
            LootItem bowReceipt=null,swordReceipt=null;
            for(ulong seed=0;seed<10000&&(bowReceipt==null||swordReceipt==null);seed++)
            {
                var receipt=(LootItem)typeof(LootCatalog).GetMethod("Generate",Flags).Invoke(LootCatalog.Version1,
                    new object[]{seed,System.Numerics.BigInteger.One,1UL,1,0,p.LifetimeKills+1});
                var family=p.Equipment.ForItem(receipt).Definition.Family;
                if(!((family==WeaponFamily.Bow&&bowReceipt==null)||(family==WeaponFamily.Sword&&swordReceipt==null)))continue;
                p.RecordKill();Assert.IsTrue((bool)typeof(CharacterProgression).GetMethod("CollectItem",Flags).Invoke(p,new object[]{receipt}));
                if(family==WeaponFamily.Bow)bowReceipt=receipt;else swordReceipt=receipt;
            }
            Assert.IsNotNull(bowReceipt);Assert.IsNotNull(swordReceipt);
            yield return EquipPlayerBowTestItem(bowReceipt);
            Assert.AreEqual(WeaponFamily.Bow,p.Equipment.Weapon.Definition.Family);
            var bowVisual=(Transform)typeof(ArenaRuntime).GetField("_equippedVisual",Flags).GetValue(_runtime);
            Assert.IsNotNull(bowVisual);Assert.IsTrue(bowVisual.IsChildOf(animator.GetBoneTransform(HumanBodyBones.LeftHand)));
            var bowController=animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.IsNotNull(bowController);
            var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();bowController.GetOverrides(overrides);
            var attacks=overrides.Where(pair=>pair.Key.name.Contains("Strike")).ToArray();Assert.AreEqual(1,attacks.Length);
            var shot=attacks[0].Value??attacks[0].Key;
            Assert.IsNotNull(shot);
            Assert.AreSame(Resources.Load<AnimationClip>("ARE/Visuals/WeaponBowShot"),shot);
            Assert.IsTrue(shot.humanMotion);Assert.Greater(shot.length,0f);
            int attackState=Animator.StringToHash("Melee"),idleState=Animator.StringToHash("Idle");
            animator.ResetTrigger("Melee");animator.SetFloat("MoveMagnitude",0);animator.Play(idleState,0,0);animator.Update(0);

            var queue=(Queue<int>)typeof(ArenaRuntime).GetField("_pendingSkills",Flags).GetValue(_runtime);
            Assert.IsNotEmpty(Game.Monsters);
            foreach(var enemy in Game.Monsters){enemy.Xmm=20000;enemy.Zmm=20000;}
            queue.Enqueue(0);TickOnce();animator.Update(.05f);
            Assert.AreNotEqual(attackState,animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                "An out-of-range rejected bow attack must not animate");
            var target=Game.Monsters[0];target.Xmm=Game.PlayerXmm+1400;target.Zmm=Game.PlayerZmm;target.Health=1000;target.MaxHealth=1000;
            queue.Enqueue(0);TickOnce();
            Assert.Less(target.Health,1000,"The actual equipped bow must land a Core hit before presentation is inspected");
            animator.Update(0);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).shortNameHash==attackState||
                animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).shortNameHash==attackState,
                "The real accepted hit must enter the equipped attack controller state");

            ulong hash=Game.ComputeWorldHash();string save=p.SaveString();
            var stored=_keys.Select(k=>PlayerPrefs.GetString(k,"")).ToArray();var hadKeys=_keys.Select(PlayerPrefs.HasKey).ToArray();
            var position=hero.transform.position;var rotation=hero.transform.rotation;
            var rigPosition=animator.transform.localPosition;var rigRotation=animator.transform.localRotation;
            for(int step=0;step<32;step++)animator.Update((shot.length+.2f)/32f);
            Assert.AreEqual(idleState,animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
            Assert.IsFalse(animator.IsInTransition(0),"The actual bow controller must finish its recovery");

            var arms=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm}
                .Select(animator.GetBoneTransform).ToArray();
            foreach(var bone in arms)Assert.IsNotNull(bone);
            var poses=new Quaternion[13][];
            var graph=PlayableGraph.Create("PlayerBowMotionRegression");
            try
            {
                // Remove controller transitions from measurement: sample the actual bound clip on the actual rig.
                animator.runtimeAnimatorController=null;graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,shot);
                playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);playable.SetSpeed(0d);
                var output=AnimationPlayableOutput.Create(graph,"Player bow pose",animator);
                output.SetSourcePlayable(playable);output.SetWeight(1);graph.Play();
                playable.SetTime(0);graph.Evaluate(0);
                for(int sample=0;sample<poses.Length;sample++)
                {
                    playable.SetTime((double)shot.length*sample/(poses.Length-1));graph.Evaluate(0);
                    poses[sample]=arms.Select(b=>b.localRotation).ToArray();
                }
                playable.SetTime(0);graph.Evaluate(0);
                Assert.LessOrEqual(PlayerBowArmPoseDistance(poses[0],arms.Select(b=>b.localRotation).ToArray()),.1f,
                    "Repeated sampling of one clip time must reproduce the player's arm pose");
            }
            finally
            {
                if(graph.IsValid())graph.Destroy();
                animator.runtimeAnimatorController=bowController;
            }
            Assert.AreEqual(position,hero.transform.position);Assert.AreEqual(rotation,hero.transform.rotation);
            Assert.AreEqual(rigPosition,animator.transform.localPosition);Assert.AreEqual(rigRotation,animator.transform.localRotation);
            Assert.AreEqual(hash,Game.ComputeWorldHash());Assert.AreEqual(save,p.SaveString());
            for(int i=0;i<_keys.Length;i++)
            {Assert.AreEqual(hadKeys[i],PlayerPrefs.HasKey(_keys[i]));Assert.AreEqual(stored[i],PlayerPrefs.GetString(_keys[i],""));}

            float maximum=0;int second=-1,third=-1;
            for(int sample=1;sample<poses.Length;sample++)
            {
                float distance=PlayerBowArmPoseDistance(poses[0],poses[sample]);maximum=Mathf.Max(maximum,distance);
                if(distance<=.1f)continue;
                if(second<0)second=sample;else if(third<0&&PlayerBowArmPoseDistance(poses[second],poses[sample])>.1f)third=sample;
            }
            TestContext.WriteLine("Player bound bow clip: "+shot.name+", duration="+shot.length+"s, arm motion="+maximum+" degrees");
            Assert.Greater(maximum,.1f,"The player's equipped bow must move inside its clip, not merely blend to a frozen pose");
            Assert.Greater(third,second,"The player's bow take needs at least three distinct arm poses");

            // Pose the verified production controller only for native visual inspection; these are not gameplay inputs.
            float speed=animator.speed;
            try
            {
                foreach(float normalized in new[]{.2f,.5f,.8f})
                {
                    animator.speed=1;animator.Play(attackState,0,normalized);animator.Update(0);animator.speed=0;
                    yield return Capture("player-bow-"+Mathf.RoundToInt(normalized*100)+".png",heroDetail:true);
                }
            }
            finally{animator.speed=speed;}
            Assert.AreEqual(position,hero.transform.position);Assert.AreEqual(rotation,hero.transform.rotation);
            Assert.AreEqual(rigPosition,animator.transform.localPosition);Assert.AreEqual(rigRotation,animator.transform.localRotation);
            Assert.AreEqual(hash,Game.ComputeWorldHash());Assert.AreEqual(save,p.SaveString());
            for(int i=0;i<_keys.Length;i++)
            {Assert.AreEqual(hadKeys[i],PlayerPrefs.HasKey(_keys[i]));Assert.AreEqual(stored[i],PlayerPrefs.GetString(_keys[i],""));}

            yield return EquipPlayerBowTestItem(swordReceipt);
            Assert.AreEqual(WeaponFamily.Sword,p.Equipment.Weapon.Definition.Family);
            Assert.AreSame(meleeController,animator.runtimeAnimatorController,"Leaving the bow must restore the original melee controller");
            var swordVisual=(Transform)typeof(ArenaRuntime).GetField("_equippedVisual",Flags).GetValue(_runtime);
            Assert.IsNotNull(swordVisual);Assert.IsTrue(swordVisual.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightHand)));
            Assert.IsTrue(bowVisual==null,"Inventory switching must remove the old left-hand bow");
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var saved));
            Assert.AreEqual(p.Equipment.Weapon.Definition.Id,saved.Equipment.Weapon.Definition.Id);
            foreach(var enemy in Game.Monsters){enemy.Xmm=20000;enemy.Zmm=20000;}
            // Clear the real bow cooldown through scheduled Core ticks before checking the next accepted melee strike.
            for(int i=0;i<10;i++)TickOnce();
            target.Xmm=Game.PlayerXmm+1000;target.Zmm=Game.PlayerZmm;target.Health=1000;
            animator.ResetTrigger("Melee");animator.SetFloat("MoveMagnitude",0);animator.Play(idleState,0,0);animator.Update(0);
            queue.Enqueue(0);TickOnce();Assert.Less(target.Health,1000);
            hash=Game.ComputeWorldHash();save=p.SaveString();
            animator.Update(0);animator.Update(.15f);
            Assert.AreEqual(attackState,animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
            Assert.IsFalse(animator.runtimeAnimatorController.animationClips.Contains(shot),"The sword must no longer use the bow override");
            Assert.AreEqual(hash,Game.ComputeWorldHash());Assert.AreEqual(save,p.SaveString());
        }

        private IEnumerator EquipPlayerBowTestItem(LootItem receipt)
        {
            var p=Game.Progression;
            typeof(ArenaRuntime).GetField("_selectedItem",Flags).SetValue(_runtime,p.Items.IndexOf(receipt));
            _runtime.OpenMenu(ArenaRuntime.MenuInventory);yield return null;
            var button=Find("EquipWeapon").GetComponent<Button>();Assert.IsTrue(button.interactable);
            button.onClick.Invoke();yield return null;_runtime.CloseMenu();yield return null;
            Assert.AreEqual(p.Equipment.ForItem(receipt).Definition.Id,p.Equipment.Weapon.Definition.Id);
        }

        private static float PlayerBowArmPoseDistance(Quaternion[] first,Quaternion[] second)
        {
            float angle=0;for(int i=0;i<first.Length;i++)angle=Mathf.Max(angle,Quaternion.Angle(first[i],second[i]));return angle;
        }
    }
}
