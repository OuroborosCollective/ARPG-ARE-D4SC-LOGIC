using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator StandingWeaponChainsMoveTheEquippedHero()
        {
            var hero = Find("Wanderer").GetComponentInChildren<Animator>();
            if (hero == null || hero.GetComponent<EntryWeaponGrip>() == null)
                Assert.Ignore("Requires the private humanoid EntryHero and its socket");
            Time.captureDeltaTime = 1f/30;
            var queue = (Queue<int>)typeof(ArenaRuntime).GetField("_pendingSkills", Flags).GetValue(_runtime);
            foreach (var m in Game.Monsters) { m.Xmm = 25000; m.Zmm = 25000; m.AttackCooldownTicks = 99999; }
            Transform hand = hero.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = hero.GetBoneTransform(HumanBodyBones.Hips);
            var socket = hand.Find("EntryWeaponSocket");
            Assert.NotNull(socket);
            string evidence = Environment.GetEnvironmentVariable("ARE_CHAIN_FRAMES");
            int frame = 0;
            foreach (var weapon in new[] {RegionalRelic.M_Wehrsaebel, RegionalRelic.R_Rippenschild, RegionalRelic.R_Scharrbeil})
            {
                var gear = Game.Progression.Equipment;
                var claim = typeof(WeaponEquipment).GetMethod("ClaimRegionalRelic",Flags);
                if (weapon == RegionalRelic.R_Scharrbeil) Assert.True(Game.TrySetRegionalRelicEquipped(RegionalRelic.R_Rippenschild,false));
                claim.Invoke(gear,new object[]{Game.Progression, weapon});
                Assert.True(Game.TrySetRegionalRelicEquipped(weapon,true));
                for (int i=0;i<45;i++) yield return null;
                var ends = new List<Vector3>();
                float maxBodyTurn = 0;
                Quaternion idle = hips.rotation;
                int count = weapon == RegionalRelic.M_Wehrsaebel ? 4 : 3;
                queue.Enqueue(0);
                ulong first = Game.ActionSequence + 1;
                for (int i=0;i<300;i++)
                {
                    yield return null;
                    if (!string.IsNullOrEmpty(evidence))
                    {
                        string name = "chain-frame-" + (frame++).ToString("D4") + ".png";
                        SaveReviewFrame(name);
                        File.Move(Path.Combine(Application.dataPath,"..","Temp",name),Path.Combine(evidence,name));
                    }
                    if (Game.ActionKind != CombatActionKind.Strike) continue;
                    int index = (int)(Game.ActionSequence-first);
                    maxBodyTurn = Mathf.Max(maxBodyTurn,Quaternion.Angle(idle,hips.rotation));
                    if (Game.ActionTick != Game.CurrentActionTiming.Total-1 || ends.Count != index) continue;
                    ends.Add(hero.transform.InverseTransformDirection(socket.up));
                    if (ends.Count == count) break;
                    queue.Enqueue(0); // a real follow-up press inside the two-tick window
                }
                Assert.AreEqual(count,ends.Count,weapon + " accepted every follow-up");
                Assert.Greater(maxBodyTurn,8f,weapon + " turns the body");
                for (int i=0;i<count;i++) Assert.Greater(Vector3.Angle(ends[i],ends[(i+1)%count]),12f,weapon + " distinct blade finish " + i);
            }
        }
    }
}
