using System.Collections;
using System.Collections.Generic;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator HcapAcceptedStrikeSamplesPrivateHero()
        {
            if (Resources.Load<TextAsset>("ARE/Hcap/index") == null && string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("HCAP_REQUIRE_BANK")))
                Assert.Ignore("Private EntryHero bank is absent from this checkout.");
            var rig=Find("Wanderer").GetComponentInChildren<Animator>();
            Assert.NotNull(rig);
            var hand=rig.GetBoneTransform(HumanBodyBones.RightHand);
            var queue=(Queue<int>)typeof(ArenaRuntime).GetField("_pendingSkills",Flags).GetValue(_runtime);
            foreach (var m in Game.Monsters) { m.Xmm=25000; m.Zmm=25000; m.AttackCooldownTicks=99999; }
            Time.captureDeltaTime=1f/30;
            for(int i=0;i<4;i++) yield return null;
            var projection=rig.GetComponent<HcapHeroProjection>();
            Assert.NotNull(projection,"Importer and private bank are active on the game's humanoid.");
            int before=projection.SampleCount;
            Quaternion rest=hand.localRotation;
            float turn=0; bool named=false;
            queue.Enqueue(0);
            ulong sequence=Game.ActionSequence;
            for(int i=0;i<45;i++)
            {
                yield return null;
                if(projection.SampledClip=="straight_sword__attack_01")
                {
                    named=true; turn=Mathf.Max(turn,Quaternion.Angle(rest,hand.localRotation));
                }
            }
            Assert.Greater(Game.ActionSequence,sequence);
            Assert.IsTrue(named);
            Assert.Greater(projection.SampleCount,before+2);
            Assert.Greater(turn,3f);
            Debug.Log("HCAP_GAMEPLAY samples="+(projection.SampleCount-before)+" wristDegrees="+turn+" sequence="+Game.ActionSequence);
        }
    }
}
