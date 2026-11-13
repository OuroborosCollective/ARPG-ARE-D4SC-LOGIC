using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static void DungeonFixtureSet(object owner,string property,object value)
        { owner.GetType().GetProperty(property).SetValue(owner,value); }

        private void InstallDungeonFixture()
        {
            _runtime.enabled=false;
            var p=CharacterProgression.CreateEntryProfile();
            for(int i=0;i<4;i++) typeof(CharacterProgression).GetMethod("DiscoverHungerClue",Flags).Invoke(p,null);
            typeof(CharacterProgression).GetMethod("ChooseHungerSupply",Flags).Invoke(p,new object[]{HungerChoice.Protect});
            typeof(CharacterProgression).GetMethod("TravelHunger",Flags).Invoke(p,new object[]{5,false});
            DungeonFixtureSet(p.Investigation,"Clues",7);
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{new ArenaSimulation(123,p,1,LootCatalog.Version1),"Dungeon entry fixture"});
        }

        private void DungeonProject()
        {
            typeof(ArenaRuntime).GetMethod("UpdateRattlerPresentation",Flags).Invoke(_runtime,null);
            typeof(ArenaRuntime).GetMethod("UpdatePresentation",Flags).Invoke(_runtime,new object[]{1f});
            typeof(ArenaRuntime).GetMethod("UpdateHud",Flags).Invoke(_runtime,null);
        }
        private static (int,int) DungeonCell(long x,long z) { return ((int)((x+26000)/4000)-6,(int)((z+26000)/4000)-6); }
        private void DungeonWalk(long x,long z)
        {
            var start=DungeonCell(Game.PlayerXmm,Game.PlayerZmm); var end=DungeonCell(x,z);
            var before=new Dictionary<(int,int),(int,int)> { [start]=start }; var queue=new Queue<(int,int)>(); queue.Enqueue(start);
            while(queue.Count>0 && !before.ContainsKey(end))
            {
                var p=queue.Dequeue();
                foreach(var n in new[]{(p.Item1-1,p.Item2),(p.Item1+1,p.Item2),(p.Item1,p.Item2-1),(p.Item1,p.Item2+1)})
                    if(!before.ContainsKey(n) && Game.CanStand(n.Item1*4000,n.Item2*4000)) { before[n]=p; queue.Enqueue(n); }
            }
            Assert.IsTrue(before.ContainsKey(end),"Real movement route exists");
            var path=new List<(int,int)>(); var node=end;
            while(node!=start) { path.Add(node); node=before[node]; } path.Reverse();
            DungeonSegment(start.Item1*4000,start.Item2*4000);
            foreach(var p in path) DungeonSegment(p.Item1*4000,p.Item2*4000);
            DungeonSegment(x,z); DungeonProject();
        }
        private void DungeonSegment(long x,long z)
        {
            for(int t=0;t<800;t++)
            {
                Assert.IsFalse(Game.GameOver,"Walked combat route must be survivable with shipped skills");
                long dx=x-Game.PlayerXmm,dz=z-Game.PlayerZmm;
                bool threat=Game.Monsters.Any(m=>!m.IsDead && Math.Abs(m.Xmm-Game.PlayerXmm)<4000 && Math.Abs(m.Zmm-Game.PlayerZmm)<4000 && Game.Progression.Dungeon.Current.Layout.HasSight(Game.PlayerXmm,Game.PlayerZmm,m.Xmm,m.Zmm));
                if(!threat && Math.Abs(dx)<450 && Math.Abs(dz)<450) { Game.SetMoveInput(0,0); return; }
                Game.SetMoveInput(threat?0:Math.Abs(dx)<350?0:Math.Sign(dx)*10000,threat?0:Math.Abs(dz)<350?0:Math.Sign(dz)*10000);
                if(Game.Progression.EntryAct==7 && Game.Progression.Rattler.Creature.BossWindupTicks>0) Game.SetMoveInput(0,-10000);
                Game.ActivateSkill(3); Game.ActivateSkill(1); Game.ActivateSkill(0); Game.ActivateSkill(2); Game.TryCollectLoot(); Game.Step();
            }
            Assert.Fail("Dungeon movement stalled");
        }

        [UnityTest]
        public IEnumerator ProceduralDungeonThreeFloorsUseRealTravelLootSavesAndNewRuns()
        {
            InstallDungeonFixture(); MoveStory(0,18000);
            var offered=(HungerAction[])typeof(ArenaRuntime).GetMethod("HungerActions",Flags).Invoke(_runtime,null);
            CollectionAssert.Contains(offered,HungerAction.OpenDungeonSeal,"Normal journal must choose the procedural route");
            yield return StoryAction(HungerAction.OpenDungeonSeal);
            MoveStory(0,24000); TravelStory(); yield return null;
            Assert.IsTrue(Game.InDungeon); ulong firstSeed=Game.Progression.Dungeon.Seed;
            for(int depth=1;depth<=3;depth++)
            {
                Assert.AreEqual(depth,Game.Progression.Dungeon.Depth);
                var map=Game.Progression.Dungeon.Current.Layout; int floors=0;
                for(int z=-6;z<=6;z++) for(int x=-6;x<=6;x++)
                {
                    var tile=Find("Dungeon_Floor_"+x+"_"+z);
                    Assert.AreEqual(map.IsFloor(x,z),tile!=null,"Rendered floor must match Core seed map");
                    if(tile!=null) { floors++; Assert.IsNotEmpty(tile.GetComponentsInChildren<MeshRenderer>()); }
                }
                Assert.Greater(floors,40);
                Assert.IsNull(Find("ARE_Outdoor_Backdrop"));
                Assert.IsNotNull(Find("ARE_Return_Passage"));
                Assert.AreEqual(depth<3,Find("ARE_World_Passage")!=null);
                DungeonProject(); yield return DungeonOverview("dungeon-run1-floor"+depth+".png");
                if(depth==1) { Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget(); yield return Capture("dungeon-run1-entry-gameplay.png"); }
                for(int room=1;room<map.RoomCount;room++) { map.Room(room,out var x,out var z); DungeonWalk(x,z); }
                foreach(var enemy in Game.Monsters.ToArray()) if(!enemy.IsDead) DungeonWalk(enemy.Xmm,enemy.Zmm);
                for(int i=0;i<2;i++)
                {
                    Assert.AreEqual(map.HasChest(i),Find("Dungeon_Chest_"+i)!=null,"Seed decides chest presence");
                    if(!map.HasChest(i)) continue;
                    map.Chest(i,out var x,out var z); DungeonWalk(x,z);
                    yield return StoryAction(i==0?HungerAction.OpenWestChest:HungerAction.OpenEastChest);
                    Assert.IsTrue((Game.Progression.Dungeon.Current.OpenChests&(1<<i))!=0);
                    Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var chestSave));
                    Assert.AreEqual(Game.Progression.Dungeon.Current.Loot.Count,chestSave.Dungeon.Current.Loot.Count,"Unclaimed chest drop saved by actual adapter");
                    Game.TryCollectLoot(); DungeonProject();
                    Assert.AreNotEqual(Quaternion.identity,Find("Dungeon_Chest_Lid_"+i).transform.localRotation);
                }
                while(Game.GroundLoot.Count>0) { var drop=Game.GroundLoot[0]; DungeonWalk(drop.Xmm,drop.Zmm); Game.TryCollectLoot(); }
                if(depth==2)
                {
                    DungeonWalk(0,15000); yield return StoryAction(HungerAction.InspectCryptTrace); yield return StoryAction(HungerAction.OpenInnerDoor);
                }
                if(depth<3) { DungeonWalk(0,24000); TravelStory(); yield return null; }
            }
            Assert.IsTrue(Game.Progression.Rattler.Creature.IsDead);
            Assert.IsTrue(Game.Progression.Items.Any(i=>i.DefinitionId=="crown.shard"));
            DungeonWalk(Game.Progression.Rattler.Creature.Xmm,Game.Progression.Rattler.Creature.Zmm);
            yield return StoryAction(HungerAction.InspectRoyalBrand);
            string saved=PlayerPrefs.GetString(_keys[0]);
            Assert.IsTrue(CharacterProgression.TryLoad(saved,out var reloaded));
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{new ArenaSimulation(123,reloaded,1,LootCatalog.Version1),"Reloaded dungeon"});
            yield return null;
            Assert.IsTrue(Game.Progression.Rattler.Creature.IsDead); Assert.IsTrue(Game.Progression.Rattler.BrandExamined);
            for(int depth=3;depth>=1;depth--) { DungeonWalk(0,-24000); TravelStory(); yield return null; }
            Assert.AreEqual(5,Game.Progression.EntryAct); Assert.IsNull(Find("Dungeon_Floor_0_-5"));
            MoveStory(0,24000); TravelStory(); yield return null;
            Assert.AreEqual(2, (int)Game.Progression.Dungeon.Run); Assert.AreNotEqual(firstSeed,Game.Progression.Dungeon.Seed);
            Assert.IsTrue(Game.Monsters.All(m=>!m.IsDead)); Assert.IsTrue(Game.Progression.Rattler.BrandExamined);
            Assert.IsFalse(Game.Progression.Rattler.Creature.IsDead); DungeonProject();
            yield return DungeonOverview("dungeon-run2-floor1.png");
            // Restart uses the shipped adapter after a real enemy death; it must not reroll the run.
            Game.SetMoveInput(0,10000);
            for(int t=0;t<1000 && !Game.GameOver;t++) Game.Step();
            Assert.IsTrue(Game.GameOver);
            ulong seedBeforeDeath=Game.Progression.Dungeon.Seed;
            int[] enemyHealth=Game.Progression.Dungeon.Current.Monsters.Select(m=>m.Health).ToArray();
            typeof(ArenaRuntime).GetMethod("Restart",Flags).Invoke(_runtime,null); yield return null;
            Assert.IsFalse(Game.GameOver); Assert.AreEqual(seedBeforeDeath,Game.Progression.Dungeon.Seed);
            Assert.AreEqual(2,(int)Game.Progression.Dungeon.Run);
            CollectionAssert.AreEqual(enemyHealth,Game.Progression.Dungeon.Current.Monsters.Select(m=>m.Health).ToArray());
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var afterRestart));
            Assert.AreEqual(seedBeforeDeath,afterRestart.Dungeon.Seed);
        }

        [UnityTest]
        public IEnumerator CompletedLegacyStoryOffersDungeonAndDoesNotAskToRediscoverBrand()
        {
            InstallDungeonFixture();
            var p=Game.Progression;
            DungeonFixtureSet(p.Investigation,"Approach",SealApproach.Investigated);
            DungeonFixtureSet(p.Investigation,"TraceFound",true); DungeonFixtureSet(p.Investigation,"Reported",true);
            DungeonFixtureSet(p.Rattler,"Active",true); DungeonFixtureSet(p.Rattler,"BrandExamined",true); DungeonFixtureSet(p.Rattler,"Reported",true);
            p.Rattler.Creature.Health=0; p.Rattler.Creature.LootGranted=true;
            MoveStory(0,18000);
            Assert.IsTrue(p.SaveString().StartsWith("AC12|"));
            var offered=(HungerAction[])typeof(ArenaRuntime).GetMethod("HungerActions",Flags).Invoke(_runtime,null);
            CollectionAssert.Contains(offered,HungerAction.BeginDungeonRun);
            string status=(string)typeof(ArenaRuntime).GetProperty("RattlerStatusText",Flags).GetValue(_runtime);
            Assert.IsFalse(status.Contains("Untersuche"),"A discovered brand is permanent knowledge");
            yield return StoryAction(HungerAction.BeginDungeonRun);
            MoveStory(0,24000); TravelStory(); yield return null;
            Assert.IsTrue(Game.InDungeon); Assert.IsTrue(Game.Progression.Rattler.Reported);
            Assert.IsTrue(Game.Progression.Rattler.BrandExamined); Assert.IsFalse(Game.Progression.Rattler.Creature.IsDead);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var saved));
            Assert.IsTrue(saved.Dungeon.Active); Assert.IsTrue(saved.Rattler.Reported);
        }

        private IEnumerator DungeonOverview(string name)
        {
            string folder=Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR"); if(string.IsNullOrEmpty(folder)) yield break;
            yield return null;
            var camera=Camera.main; var pos=camera.transform.position; var rot=camera.transform.rotation;
            bool ortho=camera.orthographic; float size=camera.orthographicSize; var previous=camera.targetTexture; var active=RenderTexture.active;
            var target=new RenderTexture(1280,900,24); var pixels=new Texture2D(1280,900,TextureFormat.RGB24,false);
            try
            {
                camera.orthographic=true; camera.orthographicSize=34; camera.transform.position=new Vector3(22,60,-30); camera.transform.LookAt(new Vector3(0,0,0));
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1280,900),0,0); pixels.Apply();
                System.IO.Directory.CreateDirectory(folder); System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name),pixels.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(pos,rot); camera.orthographic=ortho; camera.orthographicSize=size; camera.targetTexture=previous; RenderTexture.active=active;
                UnityEngine.Object.Destroy(pixels); target.Release(); UnityEngine.Object.Destroy(target);
            }
        }
    }
}
