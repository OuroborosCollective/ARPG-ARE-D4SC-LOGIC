using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator FadenwerkTextUsesRealStoryInputAndExistingSaveWithoutDuplicateAuthority()
        {
            _runtime.enabled=false;
            var text=Resources.Load<AshenFadenwerkText>(AshenFadenwerkText.ResourceName);
            Assert.IsNotNull(text);Assert.IsTrue(text.Valid);
            foreach(var enemy in Game.Monsters){enemy.Xmm=Game.PlayerXmm+1000;enemy.Zmm=Game.PlayerZmm;enemy.Health=1;}
            Assert.AreEqual(3,Game.ActivateSkill(3).Kills);
            for(int i=0;i<3;i++)Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0,24000);TravelStory();yield return null;
            var pending=typeof(ArenaRuntime).GetField("_pendingHungerAction",Flags);
            pending.SetValue(_runtime,(int)HungerAction.HearIlyan);TickOnce();
            Assert.AreEqual(0,Game.Progression.HungerStage,"Authoring never bypasses proximity");
            var ilyan=Game.Npcs.First(n=>n.Id==0);ilyan.Xmm=ilyan.PreviousXmm=Game.PlayerXmm+1200;ilyan.Zmm=ilyan.PreviousZmm=Game.PlayerZmm;
            string original=text.responses[0],originalHash=text.sourceHash;
            try
            {
                text.responses[0]="Fadenwerk-Liveprüfung: "+original;text.sourceHash=text.ContentHash();
                pending.SetValue(_runtime,(int)HungerAction.HearIlyan);TickOnce();
                Assert.AreEqual(1,Game.Progression.HungerStage);
                Assert.AreEqual(text.responses[0],typeof(ArenaRuntime).GetField("_message",Flags).GetValue(_runtime));
                string saved=PlayerPrefs.GetString(_keys[0]);
                Assert.IsTrue(CharacterProgression.TryLoad(saved,out var loaded));Assert.AreEqual(1,loaded.HungerStage);
                var provisions=loaded.Social.Provisions;var memory=loaded.Social.Forge.Archive.Get("ilyan").Known.Select(e=>e.Event.Id).ToArray();
                ulong seed=(ulong)typeof(ArenaRuntime).GetField("DemoSeed",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetRawConstantValue();
                var resumed=new ArenaSimulation(seed,loaded,loaded.UnlockedCryptDepth,LootCatalog.Version1);
                typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{resumed,"Fadenwerk-Reload"});
                pending.SetValue(_runtime,(int)HungerAction.HearIlyan);TickOnce();
                Assert.AreEqual(1,Game.Progression.HungerStage);Assert.AreEqual(provisions,Game.Progression.Social.Provisions);
                CollectionAssert.AreEqual(memory,Game.Progression.Social.Forge.Archive.Get("ilyan").Known.Select(e=>e.Event.Id).ToArray());
                Assert.AreEqual(text.sourceHash,_runtime.AuthoredHungerHash);
                Assert.IsFalse(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x=>x.GetType().Name=="FadenwerkManager"));
                yield return null;
            }
            finally {text.responses[0]=original;text.sourceHash=originalHash;}
        }
    }
}
