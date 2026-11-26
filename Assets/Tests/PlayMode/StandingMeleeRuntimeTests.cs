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
            string evidence = Environment.GetEnvironmentVariable("ARE_CHAIN_FRAMES");
            if ((hero == null || hero.GetComponent<EntryWeaponGrip>() == null) && string.IsNullOrEmpty(evidence))
                Assert.Ignore("Requires the private humanoid EntryHero and its socket");
            Assert.NotNull(hero);
            Assert.NotNull(hero.GetComponent<EntryWeaponGrip>());
            Time.captureDeltaTime = 1f/30;
            var queue = (Queue<int>)typeof(ArenaRuntime).GetField("_pendingSkills", Flags).GetValue(_runtime);
            foreach (var m in Game.Monsters) { m.Xmm = 25000; m.Zmm = 25000; m.AttackCooldownTicks = 99999; }
            Transform hand = hero.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = hero.GetBoneTransform(HumanBodyBones.Hips);
            var socket = hand.Find("EntryWeaponSocket");
            Assert.NotNull(socket);
            if (!string.IsNullOrEmpty(evidence))
            {
                // A front three-quarter inspection light makes hand paths readable in the dark entry vault.
                var light = new GameObject("Chain review light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.LookRotation(hero.transform.rotation * new Vector3(-.5f, -.7f, -1f));
                light.intensity = 2.8f;
            }
            int frame = 0;
            foreach (var weapon in new[] {RegionalRelic.M_Lotklinge, RegionalRelic.R_Rippenschild, RegionalRelic.R_Scharrbeil})
            {
                var gear = Game.Progression.Equipment;
                var claim = typeof(WeaponEquipment).GetMethod("ClaimRegionalRelic",Flags);
                if (weapon == RegionalRelic.R_Scharrbeil) Assert.True(Game.TrySetRegionalRelicEquipped(RegionalRelic.R_Rippenschild,false));
                claim.Invoke(gear,new object[]{Game.Progression, weapon});
                Assert.True(Game.TrySetRegionalRelicEquipped(weapon,true));
                for (int i = 0; i < 45; i++)
                {
                    yield return null;
                    if (i >= 30) CaptureChainFrame(evidence, hero.transform, ref frame);
                }
                Assert.NotNull(Find("Equipped_" + gear.Weapon.Definition.Id), "Visible equipped weapon");
                if (gear.Shield != null)
                    Assert.NotNull(Find("Equipped_" + gear.Shield.Definition.Id), "Visible equipped shield");
                if (weapon == RegionalRelic.M_Lotklinge)
                {
                    var left = hero.GetBoneTransform(HumanBodyBones.LeftHand);
                    var elbow = hero.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    Assert.Less(left.position.y, elbow.position.y, "The free hand hangs below the elbow at rest");
                }
                var ends = new List<Vector3>();
                float maxBodyTurn = 0;
                Quaternion idle = hips.rotation;
                int count = weapon == RegionalRelic.M_Lotklinge ? 4 : 3;
                queue.Enqueue(0);
                ulong first = Game.ActionSequence + 1;
                for (int i=0;i<300;i++)
                {
                    yield return null;
                    CaptureChainFrame(evidence, hero.transform, ref frame);
                    if (Game.ActionKind != CombatActionKind.Strike) continue;
                    int index = (int)(Game.ActionSequence-first);
                    maxBodyTurn = Mathf.Max(maxBodyTurn,Quaternion.Angle(idle,hips.rotation));
                    if (Game.ActionTick != Game.CurrentActionTiming.Total-1 || ends.Count != index) continue;
                    ends.Add(hero.transform.InverseTransformDirection(socket.up));
                    if (ends.Count == count) break;
                    queue.Enqueue(0); // a real follow-up press inside the two-tick window
                }
                Debug.Log(weapon + " strokes=" + ends.Count + " maxHipsTurnDegrees=" + maxBodyTurn + " finishes=" + string.Join(", ", ends));
                Assert.AreEqual(count,ends.Count,weapon + " accepted every follow-up");
                Assert.Greater(maxBodyTurn,8f,weapon + " turns the body");
                for (int i=0;i<count;i++) Assert.Greater(Vector3.Angle(ends[i],ends[(i+1)%count]),12f,weapon + " distinct blade finish " + i);
            }
        }

        private static void CaptureChainFrame(string directory, Transform hero, ref int frame)
        {
            if (string.IsNullOrEmpty(directory)) return;
            string name = "chain-frame-" + (frame++).ToString("D4") + ".png";
            var camera = Camera.main;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            try
            {
                var focus = hero.position + Vector3.up * .95f;
                camera.transform.position = focus + hero.rotation * new Vector3(1.45f, .32f, 2.25f);
                camera.transform.LookAt(focus);
                SaveReviewFrame(name);
            }
            finally { camera.transform.SetPositionAndRotation(position, rotation); }
            string temp = System.IO.Path.Combine(Application.dataPath, "..", "Temp", name);
            File.Copy(temp, System.IO.Path.Combine(directory, name), true);
            File.Delete(temp);
        }
    }
}
