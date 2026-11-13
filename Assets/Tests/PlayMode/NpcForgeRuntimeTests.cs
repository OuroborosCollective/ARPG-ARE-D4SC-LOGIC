using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using AshenCrypt.MemoryForge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator CompanionShortcutLeavesNpcAttackInputAvailable()
        {
            _runtime.enabled=false;_runtime.CloseMenu();
            var route=typeof(ArenaRuntime).GetMethod("TryMenuShortcut",Flags);
            Assert.IsFalse((bool)route.Invoke(_runtime,new object[]{KeyCode.B}),"B belongs to NPC attack");
            Assert.IsFalse(_runtime.MenuOpen);
            Assert.IsTrue((bool)route.Invoke(_runtime,new object[]{KeyCode.O}));
            Assert.AreEqual(ArenaRuntime.MenuCompanion,_runtime.MenuTab);
            yield return null;Assert.IsNotNull(Find("CompanionPanel"));
            Assert.IsTrue((bool)route.Invoke(_runtime,new object[]{KeyCode.O}));Assert.IsFalse(_runtime.MenuOpen);
        }
        [UnityTest]
        public IEnumerator ExplicitVillageRestartRevivesPlayerWhileDeadSaveAndNpcHistoryRemainValid()
        {
            _runtime.enabled=false;
            foreach(var enemy in Game.Monsters){enemy.Xmm=Game.PlayerXmm+1000;enemy.Zmm=Game.PlayerZmm;enemy.Health=1;}
            Assert.AreEqual(3,Game.ActivateSkill(3).Kills);
            for(int i=0;i<3;i++)Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0,24000);TravelStory();yield return null;
            Assert.AreEqual(2,Game.Progression.EntryAct);
            var p=Game.Progression;
            ulong seed=(ulong)typeof(ArenaRuntime).GetField("DemoSeed",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetRawConstantValue();
            var ilyan=Game.Npcs.First(n=>n.Id==0);ilyan.Xmm=ilyan.PreviousXmm=Game.PlayerXmm+1200;ilyan.Zmm=ilyan.PreviousZmm=Game.PlayerZmm;
            typeof(ArenaRuntime).GetMethod("QueueNpcAction",Flags).Invoke(_runtime,new object[]{NpcAction.Help});TickOnce();
            Assert.AreEqual(1,ilyan.Mind.Known.Count);
            // Arrange only defeat; the actual save reader and shipped restart adapter execute below.
            DungeonFixtureSet(Game,"PlayerHealth",0);Assert.IsTrue(Game.GameOver);
            var tick=Game.CurrentTick;var history=p.Social.Forge.Archive.Save();var provisions=p.Social.Provisions;
            var positions=Game.Npcs.Select(n=>new Vector2(n.Xmm,n.Zmm)).ToArray();
            typeof(ArenaRuntime).GetMethod("SaveCharacter",Flags,null,System.Type.EmptyTypes,null).Invoke(_runtime,null);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var dead));
            Assert.IsTrue(new ArenaSimulation(seed,dead,1,LootCatalog.Version1).GameOver,"Loading a dead save is not a revival command");
            typeof(ArenaRuntime).GetMethod("Restart",Flags).Invoke(_runtime,null);
            Assert.IsFalse(Game.GameOver,"Explicit restart must not restore zero health");
            Assert.AreEqual(Game.MaxPlayerHealth,Game.PlayerHealth);Assert.AreEqual(tick,Game.CurrentTick,"Restart retains the village timeline");
            Assert.AreEqual(history,Game.Progression.Social.Forge.Archive.Save());Assert.AreEqual(provisions,Game.Progression.Social.Provisions);
            CollectionAssert.AreEqual(positions,Game.Npcs.Select(n=>new Vector2(n.Xmm,n.Zmm)).ToArray());
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var alive));
            Assert.IsFalse(new ArenaSimulation(seed,alive,1,LootCatalog.Version1).GameOver);
            yield return null;
        }
        [UnityTest]
        public IEnumerator IlyanRemembersActualHelpAndReactsAfterNativeSaveReload()
        {
            _runtime.enabled=false;
            Assert.IsNotNull(Game.Progression.Social.Forge,"Shipped runtime must enable the checked NPC contract");
            // The encounter fixture clears the established entry and uses its actual village transition.
            foreach(var enemy in Game.Monsters){enemy.Xmm=Game.PlayerXmm+1000;enemy.Zmm=Game.PlayerZmm;enemy.Health=1;}
            Assert.AreEqual(3,Game.ActivateSkill(3).Kills);
            for(int i=0;i<3;i++)Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0,24000);TravelStory();yield return null;
            Assert.AreEqual(2,Game.Progression.EntryAct);
            // Arrange contact only. Input, transaction, memory, animation and PlayerPrefs are production paths.
            var ilyan=Game.Npcs.First(n=>n.Id==0);ilyan.Xmm=ilyan.PreviousXmm=Game.PlayerXmm+1200;ilyan.Zmm=ilyan.PreviousZmm=Game.PlayerZmm;
            var provisions=Game.Progression.Social.Provisions;
            typeof(ArenaRuntime).GetMethod("QueueNpcAction",Flags).Invoke(_runtime,new object[]{NpcAction.Help});TickOnce();
            Assert.AreEqual(provisions-1,Game.Progression.Social.Provisions);
            var mind=ilyan.Mind;Assert.AreEqual(1,mind.Known.Count);Assert.AreEqual(MemoryKind.Help,mind.Known[0].Event.Kind);
            Assert.AreEqual(KnowledgeOrigin.Self,mind.Known[0].Origin);Assert.AreEqual(2000,mind.RelationshipTo("player").Gratitude);
            Assert.AreEqual(ForgeAction.Observe,mind.DecisionState.Action);
            typeof(ArenaRuntime).GetMethod("SaveCharacter",Flags,null,System.Type.EmptyTypes,null).Invoke(_runtime,null);
            string token=PlayerPrefs.GetString(_keys[0]);Assert.IsTrue(token.StartsWith("AC17|"));
            Assert.IsTrue(CharacterProgression.TryLoad(token,out var loaded));
            string controller=mind.Controller.Hash;ulong episode=mind.Known[0].Event.Id;
            ulong hash=Game.ComputeWorldHash();
            ulong seed=(ulong)typeof(ArenaRuntime).GetField("DemoSeed",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetRawConstantValue();
            var resumed=new ArenaSimulation(seed,loaded,loaded.UnlockedCryptDepth,LootCatalog.Version1);
            Assert.AreEqual(hash,resumed.ComputeWorldHash());
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{resumed,"Ilyan erinnert sich"});
            for(int tick=0;tick<12;tick++)Game.Step();
            typeof(ArenaRuntime).GetMethod("QueueNpcAction",Flags).Invoke(_runtime,new object[]{NpcAction.Greet});TickOnce();
            Assert.That((string)typeof(ArenaRuntime).GetField("_message",Flags).GetValue(_runtime),Does.Contain("Proviant"));
            Assert.AreEqual(controller,Game.Npcs[0].Mind.Controller.Hash);Assert.AreEqual(episode,Game.Npcs[0].Mind.Known[0].Event.Id);
            Assert.AreEqual(System.Numerics.BigInteger.One,Game.Progression.Social.GetMemory(0).HelpfulActions);
            Assert.AreEqual(0,Game.Npcs[1].Mind.Known.Count);
            DungeonProject();yield return null;
            var actor=Find("ARE_NPC_0");Assert.IsNotEmpty(actor.GetComponentsInChildren<SkinnedMeshRenderer>());
            Vector3 toHero=Find("Wanderer").transform.position-actor.transform.position;toHero.y=0;
            Assert.Greater(Vector3.Dot(actor.transform.forward,toHero.normalized),.95f,"Observed social action faces the real player");
            yield return Capture("forge-ilyan-memory-after-load.png");
        }
    }
}
