using System;
using System.Collections;
using System.Collections.Generic;
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
        private JourneyMapCatalog NativeJourneyMaps=>(JourneyMapCatalog)typeof(ArenaRuntime).GetField("_journeyMaps",Flags).GetValue(_runtime);
        private void InstallJourneyStationFixture()
        {
            _runtime.enabled=false;Assert.IsNotNull(NativeJourneyMaps,"Actual World Forge map resources must be present");
            var p=CharacterProgression.CreateEntryProfile();
            for(int i=0;i<4;i++)typeof(CharacterProgression).GetMethod("DiscoverHungerClue",Flags).Invoke(p,null);
            typeof(CharacterProgression).GetMethod("ChooseHungerSupply",Flags).Invoke(p,new object[]{HungerChoice.Protect});
            DungeonFixtureSet(p.Investigation,"Active",true);DungeonFixtureSet(p.Investigation,"Clues",7);DungeonFixtureSet(p.Investigation,"Approach",SealApproach.Investigated);DungeonFixtureSet(p.Investigation,"TraceFound",true);DungeonFixtureSet(p.Investigation,"Reported",true);
            DungeonFixtureSet(p.Rattler,"Active",true);DungeonFixtureSet(p.Rattler,"BrandExamined",true);DungeonFixtureSet(p.Rattler,"Reported",true);p.Rattler.Creature.Health=0;p.Rattler.Creature.LootGranted=true;
            typeof(WoodlandState).GetMethod("Accept",Flags).Invoke(p.Woodland,new object[]{71UL});typeof(WoodlandState).GetMethod("Enter",Flags).Invoke(p.Woodland,null);DungeonFixtureSet(p.Woodland,"ReachedAschenzoll",true);
            DungeonFixtureSet(p.Freight,"Clues",7);DungeonFixtureSet(p.Freight,"Choice",FreightChoice.Secured);DungeonFixtureSet(p.Freight,"OriginalCollected",true);DungeonFixtureSet(p.Freight,"Reported",true);
            typeof(CharacterProgression).GetMethod("TravelHunger",Flags).Invoke(p,new object[]{10,false});p.Social.EnableForge();p.Equipment.Enable(p);p.AddWeaponUpgrade(2000);
            // Prepared prior-story/power fixture. New travel, combat, loot and persistence below use production paths.
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{new ArenaSimulation(71,p,1,LootCatalog.Version1,NativeJourneyMaps),"Aschenzoll"});
        }
        // Software-rendered native verification runs under the shared one-core VPS budget.
        [UnityTest, Timeout(600000)]
        public IEnumerator WorldForgePilotWalksFightsSavesAndRegeneratesItsFourFloorSideCave()
        {
            InstallJourneyStationFixture();MoveStory(0,18000);TravelStory();yield return null;DungeonProject();
            Assert.IsTrue(Game.InJourney);Assert.IsFalse(Game.InAschenzoll);Assert.AreEqual(11,Game.CurrentAreaId);
            Assert.That(ArenaRuntime.DisplayChapterName(Game),Does.Contain("jenseits"));
            var chunks=(List<Transform>)typeof(ArenaRuntime).GetField("_woodlandChunks",Flags).GetValue(_runtime);
            Assert.AreEqual(64,chunks.Count);Assert.IsTrue(chunks.All(c=>c.GetComponent<Collider>()==null),"Core owns collision");
            var map=((JourneyRouteLayout)Game.JourneyGeometry).Map;
            foreach(var chunk in chunks)foreach(var vertex in chunk.GetComponent<MeshFilter>().sharedMesh.vertices)
            {var at=chunk.TransformPoint(vertex);Assert.AreEqual(map.HeightMm((long)Math.Round(at.x*1000),(long)Math.Round(at.z*1000))/1000f,at.y,.002f,"Rendered terrain uses canonical authored nodes");}
            var trees=chunks.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="WoodlandTreeA"||t.name=="WoodlandTreeB").ToArray();Assert.Greater(trees.Length,20);
            foreach(var tree in trees){var rs=tree.GetComponentsInChildren<Renderer>(true);var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);Assert.GreaterOrEqual(bounds.size.y,4.9f,"Adult trees exceed twice hero height");}
            foreach(string plant in new[]{"WoodlandMushroomRed","WoodlandFern","WoodlandShrub"})
                Assert.IsTrue(chunks.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).Any(t=>t.name==plant),"Reviewed forest undergrowth must be visible: "+plant);
            string before=Game.Progression.SaveString();DungeonProject();Assert.AreEqual(before,Game.Progression.SaveString(),"Projection never creates rewards or advances ticks");
            var treeline = Find("Journey_AshPine_Stands");
            Assert.IsNotNull(treeline);
            Assert.IsEmpty(treeline.GetComponentsInChildren<Collider>());
            var grove = treeline.GetComponent<BorderlandLandscape>();
            var batches = (Matrix4x4[][])typeof(BorderlandLandscape).GetField("_groves", Flags).GetValue(grove);
            var treesOnBanks = batches.SelectMany(batch => batch).Select(matrix => (Vector3)matrix.GetColumn(3)).ToArray();
            Assert.That(treesOnBanks.Length, Is.InRange(40, 300));
            Assert.IsTrue(treesOnBanks.All(at => !Game.JourneyGeometry.CanStand(Mathf.RoundToInt(at.x*1000), Mathf.RoundToInt(at.z*1000))));
            Assert.GreaterOrEqual(treesOnBanks.Count(at => (new Vector2(at.x - Game.PlayerXmm*.001f, at.z - Game.PlayerZmm*.001f)).sqrMagnitude < 900f), 3);
            Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();yield return Capture("journey-worldforge-entry.png");
            var firstEnemy=Game.Monsters[0];JourneyNativeWalk(firstEnemy.Xmm,firstEnemy.Zmm,false);
            var pending=(Queue<int>)typeof(ArenaRuntime).GetField("_pendingSkills",Flags).GetValue(_runtime);int killsBefore=Game.Kills;pending.Enqueue(0);
            _runtime.enabled=true;for(int frame=0;frame<120&&pending.Count>0;frame++)yield return null;_runtime.enabled=false;
            Assert.AreEqual(0,pending.Count,"Actual runtime fixed step consumes combat input");Assert.Greater(Game.Kills,killsBefore,"Queued skill defeats the reachable enemy");
            var firstLoot=Game.GroundLoot.First();JourneyNativeWalk(firstLoot.Xmm,firstLoot.Zmm,false);
            var pickup=typeof(ArenaRuntime).GetField("_pendingPickup",Flags);var priorCollected=Game.Progression.LootCollected;pickup.SetValue(_runtime,true);
            _runtime.enabled=true;for(int frame=0;frame<120&&(bool)pickup.GetValue(_runtime);frame++)yield return null;_runtime.enabled=false;
            Assert.IsFalse((bool)pickup.GetValue(_runtime));Assert.Greater(Game.Progression.LootCollected,priorCollected,"Queued pickup applies canonical inventory transaction");
            JourneyNativeClear();Assert.GreaterOrEqual(Game.Progression.Items.Count,4);
            var hero=Find("Wanderer").transform;DungeonProject();Assert.AreEqual(1.05f+Game.JourneyGeometry.HeightMm(Game.PlayerXmm,Game.PlayerZmm)/1000f,hero.position.y,.002f);
            Game.JourneyGeometry.Chest(0,out var tx,out var tz);JourneyNativeWalk(tx,tz);yield return StoryAction(HungerAction.OpenWestChest);Assert.IsTrue(Game.TryCollectLoot());
            typeof(ArenaRuntime).GetMethod("SaveCharacter",Flags,null,Type.EmptyTypes,null).Invoke(_runtime,null);
            string saved=PlayerPrefs.GetString(_keys[0]);Assert.IsTrue(CharacterProgression.TryLoad(saved,NativeJourneyMaps,out var p));Assert.AreEqual(saved,p.SaveString());
            string captureDirectory=Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(captureDirectory))
            {
                System.IO.Directory.CreateDirectory(captureDirectory);
                System.IO.File.WriteAllText(System.IO.Path.Combine(captureDirectory,"journey-pilot-test-save.txt"),saved);
            }
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{new ArenaSimulation(71,p,1,LootCatalog.Version1,NativeJourneyMaps),"Wald geladen"});yield return null;
            Assert.AreEqual(1,Game.Progression.Journey.Route.OpenChests);
            var route=(JourneyRouteLayout)Game.JourneyGeometry;JourneyNativeWalk(route.CaveX,route.CaveZ);TravelStory();yield return null;
            Assert.IsTrue(Game.InJourneyCave);Assert.AreEqual(4,Game.Progression.Journey.FloorCount);ulong seed=Game.Progression.Journey.CaveSeed;
            for(int d=1;d<=4;d++)
            {
                Assert.AreEqual(d,Game.Progression.Journey.Depth);JourneyNativeClear();
                Game.JourneyGeometry.Chest(0,out tx,out tz);JourneyNativeWalk(tx,tz);yield return StoryAction(HungerAction.OpenWestChest);Assert.AreEqual(12*d,Game.GroundLoot.Single().WeaponBonus);Assert.IsTrue(Game.TryCollectLoot());DungeonProject();
                Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();yield return Capture("journey-cave-floor"+d+".png");
                typeof(ArenaRuntime).GetMethod("SaveCharacter",Flags,null,Type.EmptyTypes,null).Invoke(_runtime,null);
                Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),NativeJourneyMaps,out p));Assert.AreEqual(seed,p.Journey.CaveSeed);Assert.AreEqual(1,p.Journey.Current.OpenChests);
                if(d<4){JourneyNativeWalk(Game.JourneyGeometry.ForwardX,Game.JourneyGeometry.ForwardZ);TravelStory();yield return null;}
            }
            var items=Game.Progression.Items.Count;var xp=Game.Progression.TotalExperience;
            for(int d=4;d>=1;d--){JourneyNativeWalk(Game.JourneyGeometry.ReturnX,Game.JourneyGeometry.ReturnZ);TravelStory();yield return null;}
            Assert.IsFalse(Game.InJourneyCave);Assert.IsTrue(Game.Progression.Journey.CaveNeedsNewRun);
            route=(JourneyRouteLayout)Game.JourneyGeometry;JourneyNativeWalk(route.CaveX,route.CaveZ);TravelStory();yield return null;
            Assert.AreNotEqual(seed,Game.Progression.Journey.CaveSeed);Assert.IsTrue(Game.Monsters.All(m=>!m.IsDead));Assert.AreEqual(0,Game.Progression.Journey.Current.OpenChests);
            Assert.AreEqual(items,Game.Progression.Items.Count);Assert.AreEqual(xp,Game.Progression.TotalExperience);Assert.IsTrue(Game.Progression.Freight.Reported&&Game.Progression.Rattler.Reported);
        }
        [UnityTest]
        public IEnumerator MissingAuthoredJourneyMapDoesNotLoadOlderBackupOrOverwritePrimary()
        {
            InstallJourneyStationFixture();string legacy=Game.Progression.SaveString();MoveStory(0,18000);TravelStory();yield return null;
            string saved=PlayerPrefs.GetString(_keys[0]);Assert.That(saved,Does.StartWith("AC20|"));PlayerPrefs.SetString(_keys[1],legacy);
            var field=typeof(ArenaRuntime).GetField("_journeyMaps",Flags);var maps=field.GetValue(_runtime);field.SetValue(_runtime,null);
            try
            {
                object[] args={null};Assert.IsFalse((bool)typeof(ArenaRuntime).GetMethod("TryLoadCharacter",Flags).Invoke(_runtime,args));
                Assert.IsNull(args[0]);Assert.AreEqual(saved,PlayerPrefs.GetString(_keys[0]));Assert.AreEqual(legacy,PlayerPrefs.GetString(_keys[1]));
            }
            finally{field.SetValue(_runtime,maps);}
        }
        /// <summary>Review fix: a corrupted AC20 primary recovers from a valid AC20 backup, never from an older one.</summary>
        [UnityTest]
        public IEnumerator CorruptedJourneyCheckpointRecoversOnlyFromAMatchingJourneyBackup()
        {
            InstallJourneyStationFixture();string legacy=Game.Progression.SaveString();MoveStory(0,18000);TravelStory();yield return null;
            string saved=PlayerPrefs.GetString(_keys[0]);Assert.That(saved,Does.StartWith("AC20|"));
            string corrupted=saved.Substring(0,saved.Length-24);
            PlayerPrefs.SetString(_keys[0],corrupted);PlayerPrefs.SetString(_keys[1],saved);
            object[] args={null};
            Assert.IsTrue((bool)typeof(ArenaRuntime).GetMethod("TryLoadCharacter",Flags).Invoke(_runtime,args),"A valid AC20 backup must recover a truncated AC20 primary");
            Assert.AreEqual(Game.Progression.SaveString(),((CharacterProgression)args[0]).SaveString(),"The recovered checkpoint is the exact backup");
            PlayerPrefs.SetString(_keys[1],legacy);args[0]=null;
            Assert.IsFalse((bool)typeof(ArenaRuntime).GetMethod("TryLoadCharacter",Flags).Invoke(_runtime,args),"An older backup must still be refused");
            Assert.IsNull(args[0]);Assert.AreEqual(corrupted,PlayerPrefs.GetString(_keys[0]));Assert.AreEqual(legacy,PlayerPrefs.GetString(_keys[1]));
        }
        /// <summary>Review PR #59: weapon skills wrap a journey checkpoint in AC21; it still never falls back to an older backup.</summary>
        [UnityTest]
        public IEnumerator CorruptedWeaponSkillJourneyCheckpointNeverFallsBackToAnOlderBackup()
        {
            InstallJourneyStationFixture();string legacy=Game.Progression.SaveString();MoveStory(0,18000);TravelStory();yield return null;
            var p=Game.Progression;Assert.That(legacy,Does.Not.StartWith("AC21|"));
            typeof(WeaponMasteryState).GetMethod("Record",Flags).Invoke(p.WeaponMastery,new object[]{WeaponFamily.Sword,100L});
            string saved=p.SaveString();Assert.That(saved,Does.StartWith("AC21|"));
            Assert.IsTrue(CharacterProgression.TryLoad(saved,NativeJourneyMaps,out _),"The weapon-skill journey checkpoint itself is valid");
            string corrupted=saved.Substring(0,saved.Length-24);
            PlayerPrefs.SetString(_keys[0],corrupted);PlayerPrefs.SetString(_keys[1],legacy);
            object[] args={null};
            Assert.IsFalse((bool)typeof(ArenaRuntime).GetMethod("TryLoadCharacter",Flags).Invoke(_runtime,args),"An older backup must not replace a corrupted AC21 journey checkpoint");
            Assert.IsNull(args[0]);Assert.AreEqual(corrupted,PlayerPrefs.GetString(_keys[0]));Assert.AreEqual(legacy,PlayerPrefs.GetString(_keys[1]));
            PlayerPrefs.SetString(_keys[1],saved);args[0]=null;
            Assert.IsTrue((bool)typeof(ArenaRuntime).GetMethod("TryLoadCharacter",Flags).Invoke(_runtime,args),"A valid AC21 journey backup recovers the corrupted primary");
            Assert.AreEqual(saved,((CharacterProgression)args[0]).SaveString(),"The recovered checkpoint is the exact AC21 backup");
            // Review PR #59 (CodeRabbit): a damaged first encoded character must not open the older backup either.
            string damaged="AC21|"+(saved[5]=='Q'?"R":"Q")+saved.Substring(6);
            PlayerPrefs.SetString(_keys[0],damaged);PlayerPrefs.SetString(_keys[1],legacy);args[0]=null;
            Assert.IsFalse((bool)typeof(ArenaRuntime).GetMethod("TryLoadCharacter",Flags).Invoke(_runtime,args),"A damaged AC21 discriminator must not fall back to an older backup");
            Assert.IsNull(args[0]);Assert.AreEqual(damaged,PlayerPrefs.GetString(_keys[0]));
        }
        private void JourneyNativeWalk(long x,long z,bool fight=true)
        {
            var g=Game.JourneyGeometry;Func<long,long,(int,int)> cell=(a,b)=>((int)((a-g.OriginMm+g.CellMm/2)/g.CellMm),(int)((b-g.OriginMm+g.CellMm/2)/g.CellMm));
            var start=cell(Game.PlayerXmm,Game.PlayerZmm);var end=cell(x,z);var before=new Dictionary<(int,int),(int,int)>{{start,start}};var q=new Queue<(int,int)>();q.Enqueue(start);
            while(q.Count>0&&!before.ContainsKey(end))
            {var a=q.Dequeue();foreach(var n in new[]{(a.Item1+1,a.Item2),(a.Item1-1,a.Item2),(a.Item1,a.Item2+1),(a.Item1,a.Item2-1)})if(!before.ContainsKey(n)&&g.IsFloor(n.Item1,n.Item2)&&g.HasSight(g.Centre(a.Item1),g.Centre(a.Item2),g.Centre(n.Item1),g.Centre(n.Item2))){before[n]=a;q.Enqueue(n);}}
            Assert.IsTrue(before.ContainsKey(end),"450mm actor has a route to the target");var path=new List<(int,int)>();for(var n=end;n!=start;n=before[n])path.Add(n);path.Reverse();
            JourneyNativeSegment(g.Centre(start.Item1),g.Centre(start.Item2),fight);foreach(var n in path)JourneyNativeSegment(g.Centre(n.Item1),g.Centre(n.Item2),fight);JourneyNativeSegment(x,z,fight);DungeonProject();
        }
        private void JourneyNativeSegment(long x,long z,bool fight)
        {
            for(int i=0;i<500;i++)
            {
                Assert.IsFalse(Game.GameOver);long dx=x-Game.PlayerXmm,dz=z-Game.PlayerZmm;
                bool threat=fight&&Game.Monsters.Any(m=>!m.IsDead&&FixedMath.Square(m.Xmm-Game.PlayerXmm)+FixedMath.Square(m.Zmm-Game.PlayerZmm)<FixedMath.Square(3000)&&Game.JourneyGeometry.HasSight(Game.PlayerXmm,Game.PlayerZmm,m.Xmm,m.Zmm));
                if(!threat&&Math.Abs(dx)<350&&Math.Abs(dz)<350){Game.SetMoveInput(0,0);return;}
                Game.SetMoveInput(threat?0:dx*10000/ArenaSimulation.PlayerSpeedMmPerTick,threat?0:dz*10000/ArenaSimulation.PlayerSpeedMmPerTick);
                if(fight){Game.ActivateSkill(3);Game.ActivateSkill(1);Game.ActivateSkill(0);Game.ActivateSkill(2);Game.TryCollectLoot();}Game.Step();
            }
            Assert.Fail("Journey native movement stalled at "+Game.PlayerXmm+","+Game.PlayerZmm);
        }
        private void JourneyNativeClear()
        {
            for(int i=0;i<50&&Game.Monsters.Any(m=>!m.IsDead);i++){var m=Game.Monsters.First(e=>!e.IsDead);JourneyNativeWalk(m.Xmm,m.Zmm);}
            Assert.IsTrue(Game.Monsters.All(m=>m.IsDead));while(Game.GroundLoot.Count>0){var loot=Game.GroundLoot[0];JourneyNativeWalk(loot.Xmm,loot.Zmm);Game.TryCollectLoot();}
        }
    }
}
