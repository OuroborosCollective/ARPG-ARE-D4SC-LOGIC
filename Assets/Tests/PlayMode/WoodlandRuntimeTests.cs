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
        private void InstallWoodlandFixture()
        {
            _runtime.enabled=false;var p=CharacterProgression.CreateEntryProfile();
            for(int i=0;i<4;i++)typeof(CharacterProgression).GetMethod("DiscoverHungerClue",Flags).Invoke(p,null);
            typeof(CharacterProgression).GetMethod("ChooseHungerSupply",Flags).Invoke(p,new object[]{HungerChoice.Protect});
            DungeonFixtureSet(p.Investigation,"Active",true);DungeonFixtureSet(p.Investigation,"Clues",7);DungeonFixtureSet(p.Investigation,"Approach",SealApproach.Investigated);DungeonFixtureSet(p.Investigation,"TraceFound",true);DungeonFixtureSet(p.Investigation,"Reported",true);
            DungeonFixtureSet(p.Rattler,"Active",true);DungeonFixtureSet(p.Rattler,"BrandExamined",true);DungeonFixtureSet(p.Rattler,"Reported",true);p.Rattler.Creature.Health=0;p.Rattler.Creature.LootGranted=true;
            typeof(WoodlandState).GetMethod("Accept",Flags).Invoke(p.Woodland,new object[]{71UL});
            typeof(CharacterProgression).GetMethod("TravelHunger",Flags).Invoke(p,new object[]{4,false});
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{new ArenaSimulation(71,p,1,LootCatalog.Version1),"Transportweg"});
            MoveStory(-20000,18000);TravelStory();
        }
        private void WoodlandWalk(long x,long z)
        {
            Func<long,long,(int,int)> cell=(a,b)=>((int)((a+42000)/4000)-10,(int)((b+54000)/4000)-13);
            var start=cell(Game.PlayerXmm,Game.PlayerZmm);var end=cell(x,z);var previous=new Dictionary<(int,int),(int,int)>{[start]=start};var q=new Queue<(int,int)>();q.Enqueue(start);
            while(q.Count>0&&!previous.ContainsKey(end)){var c=q.Dequeue();foreach(var n in new[]{(c.Item1-1,c.Item2),(c.Item1+1,c.Item2),(c.Item1,c.Item2-1),(c.Item1,c.Item2+1)})if(!previous.ContainsKey(n)&&Game.CanStand(n.Item1*4000,n.Item2*4000)){previous[n]=c;q.Enqueue(n);}}
            Assert.IsTrue(previous.ContainsKey(end));var path=new List<(int,int)>();for(var n=end;n!=start;n=previous[n])path.Add(n);path.Reverse();
            WoodlandSegment(start.Item1*4000,start.Item2*4000);foreach(var c in path)WoodlandSegment(c.Item1*4000,c.Item2*4000);WoodlandSegment(x,z);DungeonProject();
        }
        private void WoodlandSegment(long x,long z)
        {
            for(int t=0;t<800;t++)
            {
                Assert.IsFalse(Game.GameOver);long dx=x-Game.PlayerXmm,dz=z-Game.PlayerZmm;
                bool danger=Game.Monsters.Any(m=>!m.IsDead&&Math.Abs(m.Xmm-Game.PlayerXmm)<4000&&Math.Abs(m.Zmm-Game.PlayerZmm)<4000&&Game.Progression.Woodland.Layout.HasSight(Game.PlayerXmm,Game.PlayerZmm,m.Xmm,m.Zmm));
                if(!danger&&Math.Abs(dx)<450&&Math.Abs(dz)<450){Game.SetMoveInput(0,0);return;}
                Game.SetMoveInput(danger?0:Math.Abs(dx)<350?0:Math.Sign(dx)*10000,danger?0:Math.Abs(dz)<350?0:Math.Sign(dz)*10000);
                Game.ActivateSkill(3);Game.ActivateSkill(1);Game.ActivateSkill(0);Game.ActivateSkill(2);Game.TryCollectLoot();Game.Step();
            }
            Assert.Fail("Actual woodland movement stalled");
        }
        [UnityTest]
        public IEnumerator WoodlandUsesApprovedArtHillsRealTravelAndPersistentCubs()
        {
            InstallWoodlandFixture();yield return null;DungeonProject();
            Assert.IsTrue(Game.InForest);Assert.AreEqual(16,Game.Monsters.Count);
            foreach(string key in new[]{"WoodlandTreeA","WoodlandTreeB","WoodlandShrub","WoodlandFern","WoodlandMushroomRed","WoodlandMushroomMoon","WoodlandRock"})Assert.IsNotNull(Resources.Load<GameObject>("ARE/Visuals/"+key),key);
            var chunks=(List<Transform>)typeof(ArenaRuntime).GetField("_woodlandChunks",Flags).GetValue(_runtime);
            Assert.Greater(chunks.Count,100);Assert.IsTrue(chunks.Any(c=>!c.gameObject.activeSelf),"Distance culling active");
            Assert.IsTrue(chunks.SelectMany(c=>c.GetComponent<MeshFilter>().sharedMesh.vertices).Any(v=>Mathf.Abs(v.y)>.4f),"Actual hill geometry");
            var trees=chunks.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="WoodlandTreeA"||t.name=="WoodlandTreeB").ToArray();
            int adults=0;foreach(var t in trees){var rs=t.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);if(b.size.y>5.9f)adults++;}
            Assert.Greater(adults,40,"Mature trees exceed twice the 1.9m hero height");
            string before=Game.Progression.SaveString();DungeonProject();Assert.AreEqual(before,Game.Progression.SaveString(),"Rendering never advances the world");
            Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();yield return Capture("woodland-entry-gameplay.png");
            WoodlandWalk(0,-28000);DungeonProject();
            var hero=Find("Wanderer").transform;Assert.AreEqual(1.05f+Game.Progression.Woodland.Layout.HeightMm(Game.PlayerXmm,Game.PlayerZmm)/1000f,hero.position.y,.002f);
            Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();yield return Capture("woodland-combat-path.png");yield return DungeonOverview("woodland-clearings-overview.png");
            WoodlandWalk(-36000,0);
            while(Game.GroundLoot.Count>0){var drop=Game.GroundLoot[0];WoodlandWalk(drop.Xmm,drop.Zmm);Game.TryCollectLoot();}
            WoodlandWalk(-36000,0);ulong seed=Game.Progression.Woodland.Seed;TravelStory();yield return null;
            Assert.AreEqual(9,Game.Progression.EntryAct);MoveStory(0,5000);
            yield return StoryAction(HungerAction.InspectCubs);yield return StoryAction(HungerAction.SpareCubs);
            Assert.IsFalse(Game.Progression.Woodland.WarBearEligibility);DungeonProject();
            Assert.IsTrue(Find("BearCub_0").activeSelf);Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();yield return Capture("bear-cubs-story-cave.png");
            MoveStory(0,-18000);TravelStory();yield return null;
            Assert.AreEqual(seed,Game.Progression.Woodland.Seed);Assert.AreEqual(CubChoice.Spared,Game.Progression.Woodland.Choice);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var saved));Assert.AreEqual(seed,saved.Woodland.Seed);
        }
        [UnityTest]
        public IEnumerator EveryOwnedWeaponBindsAndInventoryChangesTheActualHandModel()
        {
            _runtime.enabled=false;var p=Game.Progression;p.Equipment.Enable(p);
            for(int i=0;i<WeaponCatalog.Count;i++)
            {
                var definition=WeaponCatalog.Get(i);var prefab=Resources.Load<GameObject>("ARE/Visuals/"+definition.VisualKey);
                Assert.IsNotNull(prefab,definition.Id);Assert.IsNotEmpty(prefab.GetComponentsInChildren<Renderer>(true));
                var host=new GameObject("WeaponValidation");Assert.IsTrue(PassiveArtVisuals.TryBindPrefab(host.transform,prefab),definition.Id);UnityEngine.Object.Destroy(host);
            }
            var chosen=new List<LootItem>();
            for(ulong seed=0;seed<10000&&chosen.Count<2;seed++)
            {
                var receipt=(LootItem)typeof(LootCatalog).GetMethod("Generate",Flags).Invoke(LootCatalog.Version1,new object[]{seed,System.Numerics.BigInteger.One,1UL,1,0,p.LifetimeKills+1});var item=p.Equipment.ForItem(receipt);
                if(item.Definition.Family!=WeaponFamily.Sword||chosen.Any(c=>p.Equipment.ForItem(c).Definition.Id==item.Definition.Id))continue;
                p.RecordKill();Assert.IsTrue((bool)typeof(CharacterProgression).GetMethod("CollectItem",Flags).Invoke(p,new object[]{receipt}));chosen.Add(receipt);
            }
            Assert.AreEqual(2,chosen.Count);
            foreach(var receipt in chosen)
            {
                var item=p.Equipment.ForItem(receipt);typeof(ArenaRuntime).GetField("_selectedItem",Flags).SetValue(_runtime,p.Items.IndexOf(receipt));
                _runtime.OpenMenu(ArenaRuntime.MenuInventory);yield return null;
                var button=Find("EquipWeapon").GetComponent<UnityEngine.UI.Button>();Assert.IsTrue(button.interactable);button.onClick.Invoke();yield return null;
                _runtime.CloseMenu();yield return null;
                Assert.AreEqual(item.Definition.Id,Game.Progression.Equipment.Weapon.Definition.Id);
                var host=(Transform)typeof(ArenaRuntime).GetField("_equippedVisual",Flags).GetValue(_runtime);Assert.IsNotNull(host);Assert.IsTrue(host.name.Contains(item.Definition.Id));
                Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var saved));Assert.AreEqual(item.Definition.Id,saved.Equipment.Weapon.Definition.Id);
                yield return Capture("equipped-"+item.Definition.Id+".png",heroDetail:true);
            }
            Assert.IsTrue(Game.TryUnequipWeapon());DungeonProject();yield return null;
            Assert.IsNull(typeof(ArenaRuntime).GetField("_equippedVisual",Flags).GetValue(_runtime));
        }
    }
}
