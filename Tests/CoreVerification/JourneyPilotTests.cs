using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using AreArpg.Core;
using AshenCrypt.WorldForge.Core;

internal static class JourneyPilotTests
{
    public static void Run(Action<bool,string> check)
    {
        var station=AschenzollTests.Station();
        station.Progression.Freight.Clues=7;
        station.Progression.Freight.Choice=FreightChoice.Secured;
        station.Progression.Freight.OriginalCollected=true;
        station.Progression.Freight.Reported=true;
        HunterEncounterTests.Move(station,0,18000);
        check(station.AtAnyPassage,"journey-aschenzoll-onward-passage-exists-after-recorded-investigation");
        check(station.TryTraversePassage()==null,"journey-missing-map-refuses-travel-without-replacing-progress");
        var maps=Maps();
        Sweep(check,maps);
        var w=Enter(maps);
        check(w.InJourney&&!w.InWoodlandRegion&&!w.InAschenzoll&&w.CurrentAreaId==11&&w.Progression.EntryAct==10,"journey-separate-location-preserves-legacy-act");
        check(w.Monsters.Count>=4&&w.CanStand(w.PlayerXmm,w.PlayerZmm)&&!w.AtAnyPassage,"journey-safe-entry-outside-auto-return-radius");
        string initial=w.Progression.SaveString();
        check(initial.StartsWith("AC20|")&&CharacterProgression.TryLoad(initial,maps,out var loaded)&&loaded.SaveString()==initial,"journey-exact-save-roundtrip");
        check(!CharacterProgression.TryLoad(initial,out _)&&!CharacterProgression.TryLoad(initial,new JourneyMapCatalog(SyntheticMap(8)),out _),"journey-historical-map-required-no-latest-fallback");
        var clone=Reload(w,maps);
        for(int i=0;i<15;i++){w.SetMoveInput(0,10000);clone.SetMoveInput(0,10000);w.Step();clone.Step();check(w.ComputeWorldHash()==clone.ComputeWorldHash(),"journey-canonical-replay-tick-"+i);}
        var p=w.Progression;var permanent=p.Freight.Token()+p.Rattler.BrandExamined+p.Rattler.Reported+p.Woodland.Choice;
        ClearAndCollect(w);
        check(p.Items.Count>=4&&p.WeaponBonus>2000,"journey-real-combat-catalog-loot-reaches-existing-inventory");
        var weapon=p.Items.Select(p.Equipment.ForItem).FirstOrDefault(i=>i!=null&&i.Definition.Family!=WeaponFamily.Shield);
        check(weapon!=null&&w.TryEquipWeapon(weapon.Ordinal)&&w.AttackRange(0)==weapon.Definition.RangeMm,"journey-earned-weapon-equips-with-real-combat-range");
        var equipped=Reload(w,maps).Progression.Equipment.Weapon;
        check(equipped!=null&&equipped.Ordinal==weapon.Ordinal&&equipped.Definition.Id==weapon.Definition.Id&&equipped.AffixDescription==weapon.AffixDescription,"journey-equipped-model-and-effects-survive-canonical-load");
        w.JourneyGeometry.Chest(0,out var chestX,out var chestZ);Walk(w,chestX,chestZ);
        check(w.OpenJourneyChest(0).Accepted&&!w.OpenJourneyChest(0).Accepted&&w.TryCollectLoot(),"journey-treasure-opens-and-collects-once");
        var before=Reload(w,maps);
        check(before.Progression.Journey.Route.OpenChests==1&&before.Progression.Items.Count==p.Items.Count,"journey-treasure-and-catalog-receipts-survive-load");
        var route=(JourneyRouteLayout)w.JourneyGeometry;Walk(w,route.CaveX,route.CaveZ);w=Travel(w);
        check(w.InJourneyCave&&w.Progression.Journey.Depth==1&&w.Progression.Journey.FloorCount==4,"journey-first-optional-cave-has-four-floors");
        ulong caveSeed=w.Progression.Journey.CaveSeed;var caveRun=w.Progression.Journey.CaveRun;
        var floorHashes=new List<ulong>();
        for(int depth=1;depth<=4;depth++)
        {
            floorHashes.Add(w.JourneyGeometry.LayoutHash);
            check(w.Progression.Journey.CaveSeed==caveSeed&&w.Progression.Journey.CaveRun==caveRun,"journey-floor-change-keeps-run-"+depth);
            check(!w.JourneyForwardOpen,"journey-forward-needs-real-cleared-floor-"+depth);
            ClearAndCollect(w);
            for(int chest=0;chest<w.JourneyGeometry.ChestCount;chest++)
            {w.JourneyGeometry.Chest(chest,out var x,out var z);Walk(w,x,z);check(w.OpenJourneyChest(chest).Accepted&&w.GroundLoot.Single().WeaponBonus==12*depth&&w.TryCollectLoot(),"journey-depth-treasure-real-value-"+depth+"-"+chest);}
            var reloaded=Reload(w,maps);
            check(reloaded.Progression.SaveString()==w.Progression.SaveString()&&reloaded.JourneyGeometry.LayoutHash==floorHashes[depth-1],"journey-floor-load-preserves-state-"+depth);
            if(depth<4){Walk(w,w.JourneyGeometry.ForwardX,w.JourneyGeometry.ForwardZ);w=Travel(w);}
        }
        int items=w.Progression.Items.Count;var kills=w.Progression.LifetimeKills;
        for(int depth=4;depth>=1;depth--){check(w.Progression.Journey.Current.Cleared&&w.JourneyGeometry.LayoutHash==floorHashes[depth-1],"journey-return-floor-retains-layout-and-deaths-"+depth);Walk(w,w.JourneyGeometry.ReturnX,w.JourneyGeometry.ReturnZ);w=Travel(w);}
        check(!w.InJourneyCave&&w.Progression.Journey.CaveNeedsNewRun&&w.Progression.Journey.Route.OpenChests==1,"journey-external-cave-exit-keeps-outdoor-run");
        route=(JourneyRouteLayout)w.JourneyGeometry;Walk(w,route.CaveX,route.CaveZ);w=Travel(w);
        check(w.Progression.Journey.CaveSeed!=caveSeed&&w.Progression.Journey.CaveRun==caveRun+1&&w.Progression.Journey.Current.Monsters.All(m=>!m.IsDead)&&w.Progression.Journey.Current.OpenChests==0,"journey-external-cave-reentry-regenerates-layout-enemies-treasure");
        check(w.Progression.Items.Count==items&&w.Progression.LifetimeKills==kills&&w.Progression.Freight.Token()+w.Progression.Rattler.BrandExamined+w.Progression.Rattler.Reported+w.Progression.Woodland.Choice==permanent,"journey-regeneration-retains-earned-items-and-permanent-story");
        Walk(w,w.JourneyGeometry.ReturnX,w.JourneyGeometry.ReturnZ);w=Travel(w);
        ulong oldRouteSeed=w.Progression.Journey.RouteSeed,oldRouteHash=w.JourneyGeometry.LayoutHash;
        Walk(w,w.JourneyGeometry.ReturnX,w.JourneyGeometry.ReturnZ);w=Travel(w);
        check(w.InAschenzoll&&w.Progression.Journey.RouteNeedsNewRun&&CharacterProgression.TryLoad(w.Progression.SaveString(),maps,out _),"journey-return-retains-legacy-station-geometry-and-load");
        HunterEncounterTests.Move(w,0,18000);w=Travel(w);
        check(w.Progression.Journey.RouteSeed!=oldRouteSeed&&w.JourneyGeometry.LayoutHash!=oldRouteHash&&w.Progression.Journey.Route.OpenChests==0,"journey-outdoor-external-reentry-changes-routes-and-rewards");
        Corruption(check,w,maps);
        CompanionMatrix(check,maps);
        CausalAndDeath(check,maps);
    }

    internal static JourneyMapCatalog Maps()
    {
        const string folder="Assets/Resources/ARE/JourneyMaps";
        if(File.Exists(folder+"/Wald_v1_731904.bytes"))
        {
            Console.WriteLine("JOURNEY_MAP_SOURCE authored World Forge exports");
            return new JourneyMapCatalog(ForgedWorldMap.FromBytes(File.ReadAllBytes(folder+"/Wald_v1_731904.bytes")),
                ForgedWorldMap.FromBytes(File.ReadAllBytes(folder+"/Wald_v1_731905.bytes")));
        }
        // Explicit unit fixture only; native acceptance separately requires the actual authored exports.
        Console.WriteLine("JOURNEY_MAP_SOURCE synthetic unit fixture; authored/native acceptance outstanding");
        return new JourneyMapCatalog(SyntheticMap());
    }
    private static ForgedWorldMap SyntheticMap(int offset=0)
    {
        var heights=new int[129*129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z*129+x]=offset+x*3+z*2;
        return new ForgedWorldMap(-64000,-64000,1000,129,129,heights,Enumerable.Repeat(true,128*128).ToArray(),null,new[]{
            new ForgedPoiRecord("arrival",ForgedPoiKind.Waypoint,0,-56000,4000,"exit"),new ForgedPoiRecord("onward",ForgedPoiKind.Waypoint,0,56000,4000,"exit"),
            new ForgedPoiRecord("cave",ForgedPoiKind.DungeonEntrance,-52000,0,4000,"cave"),new ForgedPoiRecord("treasure",ForgedPoiKind.Landmark,48000,0,4000,"treasure"),new ForgedPoiRecord("rest",ForgedPoiKind.Landmark,0,0,4000,"rest")});
    }
    internal static ArenaSimulation Enter(JourneyMapCatalog maps,CompanionStatus status=CompanionStatus.Unavailable)
    {
        var p=status==CompanionStatus.Unavailable?AschenzollTests.Station().Progression:CompanionForgeTests.RescueFixture().Progression;
        if(status!=CompanionStatus.Unavailable)
        {
            var rescue=new ArenaSimulation(73,p,1,LootCatalog.Version1);
            HunterEncounterTests.Move(rescue,HunterEncounter.HunterX,HunterEncounter.HunterZ);if(!rescue.RecruitHunter().Accepted)throw new Exception("Journey companion fixture recruit failed");
            if(status==CompanionStatus.Waiting&&!rescue.DismissCompanion().Accepted)throw new Exception("Journey waiting fixture failed");
            for(int i=p.HungerStage;i<4;i++)p.DiscoverHungerClue();p.ChooseHungerSupply(HungerChoice.Protect);
            p.Investigation.Active=true;p.Investigation.Clues=7;p.Investigation.Approach=SealApproach.Investigated;p.Investigation.TraceFound=true;p.Investigation.Reported=true;
            p.Rattler.Active=true;p.Rattler.Creature.Health=0;p.Rattler.Creature.LootGranted=true;p.Rattler.BrandExamined=true;p.Rattler.Reported=true;p.Woodland.Accept(991);p.Woodland.Enter();
        }
        p.Woodland.ReachedAschenzoll=true;p.TravelHunger(10,false);p.Social.EnableForge();p.Equipment.Enable(p);
        p.Freight.Clues=7;p.Freight.Choice=FreightChoice.Secured;p.Freight.OriginalCollected=true;p.Freight.Reported=true;
        p.AddWeaponUpgrade(2000); // Explicit combat-power fixture; every tested reward below comes from real combat/pickup.
        var w=new ArenaSimulation(71,p,1,LootCatalog.Version1,maps);HunterEncounterTests.Move(w,0,18000);return Travel(w);
    }
    private static ArenaSimulation Travel(ArenaSimulation w)=>w.TryTraversePassage()??throw new Exception("Journey travel refused at "+w.PlayerXmm+","+w.PlayerZmm+" passage "+w.JourneyPassage);
    private static ArenaSimulation Reload(ArenaSimulation w,JourneyMapCatalog maps)
    {string s=w.Progression.SaveString();if(!CharacterProgression.TryLoad(s,maps,out var p))throw new Exception("Journey save rejected");return new ArenaSimulation(71,p,1,LootCatalog.Version1,maps);}
    internal static void Walk(ArenaSimulation w,long x,long z,bool fight=true)
    {
        var g=w.JourneyGeometry;
        (int,int) Cell(long a,long b)=>((int)((a-g.OriginMm+g.CellMm/2)/g.CellMm),(int)((b-g.OriginMm+g.CellMm/2)/g.CellMm));
        var start=Cell(w.PlayerXmm,w.PlayerZmm);var end=Cell(x,z);var before=new Dictionary<(int,int),(int,int)>{{start,start}};var q=new Queue<(int,int)>();q.Enqueue(start);
        while(q.Count>0&&!before.ContainsKey(end))
        {
            var a=q.Dequeue();foreach(var n in new[]{(a.Item1+1,a.Item2),(a.Item1-1,a.Item2),(a.Item1,a.Item2+1),(a.Item1,a.Item2-1)})
                if(!before.ContainsKey(n)&&g.IsFloor(n.Item1,n.Item2)&&g.HasSight(g.Centre(a.Item1),g.Centre(a.Item2),g.Centre(n.Item1),g.Centre(n.Item2))){before[n]=a;q.Enqueue(n);}
        }
        if(!before.ContainsKey(end))throw new Exception("Journey target not reachable "+x+","+z);
        var path=new List<(int,int)>();var at=end;while(at!=start){path.Add(at);at=before[at];}path.Reverse();
        Segment(w,g.Centre(start.Item1),g.Centre(start.Item2),fight);foreach(var n in path)Segment(w,g.Centre(n.Item1),g.Centre(n.Item2),fight);Segment(w,x,z,fight);
    }
    private static void Segment(ArenaSimulation w,long x,long z,bool fight)
    {
        for(int i=0;i<500;i++)
        {
            if(w.GameOver)throw new Exception("Journey movement fixture died");long dx=x-w.PlayerXmm,dz=z-w.PlayerZmm;
            bool threat=fight&&w.Monsters.Any(m=>!m.IsDead&&FixedMath.Square(m.Xmm-w.PlayerXmm)+FixedMath.Square(m.Zmm-w.PlayerZmm)<FixedMath.Square(3000)&&w.JourneyGeometry.HasSight(w.PlayerXmm,w.PlayerZmm,m.Xmm,m.Zmm));
            if(!threat&&Math.Abs(dx)<350&&Math.Abs(dz)<350){w.SetMoveInput(0,0);return;}
            w.SetMoveInput(threat?0:dx*10000/ArenaSimulation.PlayerSpeedMmPerTick,threat?0:dz*10000/ArenaSimulation.PlayerSpeedMmPerTick);
            if(fight){w.ActivateSkill(3);w.ActivateSkill(1);w.ActivateSkill(0);w.ActivateSkill(2);w.TryCollectLoot();}w.Step();
        }
        throw new Exception("Journey movement stalled at "+w.PlayerXmm+","+w.PlayerZmm+" targeting "+x+","+z);
    }
    private static void ClearAndCollect(ArenaSimulation w)
    {
        for(int i=0;i<50&&w.Monsters.Any(m=>!m.IsDead);i++){var m=w.Monsters.First(e=>!e.IsDead);Walk(w,m.Xmm,m.Zmm);}
        if(w.Monsters.Any(m=>!m.IsDead))throw new Exception("Journey encounters not cleared");
        while(w.GroundLoot.Count>0){var l=w.GroundLoot[0];Walk(w,l.Xmm,l.Zmm);w.TryCollectLoot();}
    }
    internal static void Sweep(Action<bool,string> check,JourneyMapCatalog maps)
    {
        var digest=new StringBuilder();var unique=new HashSet<ulong>();
        for(ulong seed=0;seed<200;seed++)
        {
            var route=new JourneyRouteLayout(maps.Primary,seed);unique.Add(route.LayoutHash);AppendGeometryDigest(digest,route,seed,1);
            CheckGeometry(check,route,"route-"+seed);check(route.LayoutHash==new JourneyRouteLayout(maps.Primary,seed).LayoutHash,"journey-route-deterministic-"+seed);
            for(int count=3;count<=6;count++)
            {
                int value=0,health=0;
                for(int d=1;d<=count;d++)
                {
                    var cave=new JourneyCaveLayout(seed,d,count);AppendGeometryDigest(digest,cave,seed,d);CheckGeometry(check,cave,"cave-"+seed+"-"+count+"-"+d);
                    int next=Enumerable.Range(0,cave.MonsterCount).Sum(i=>cave.Monster(i).Kind==0?8:17);
                    check(next>value&&cave.EnemyHealth>health,"journey-deeper-real-catalog-value-and-health-"+seed+"-"+count+"-"+d);value=next;health=cave.EnemyHealth;
                }
            }
        }
        check(unique.Count>=190,"journey-200-seeds-vary-routes");
        foreach(int count in new[]{2,7}){bool refused=false;try{new JourneyCaveLayout(1,1,count);}catch(ArgumentOutOfRangeException){refused=true;}check(refused,"journey-invalid-floor-count-"+count);}
        Console.WriteLine("JOURNEY_SWEEP_HASH "+LootCatalog.HashText(digest.ToString()).ToString("X16")+" distinct="+unique.Count+" map="+maps.Primary.ContentHash.ToString("X16"));
    }
    private static void AppendGeometryDigest(StringBuilder digest,JourneyGeometry geometry,ulong seed,int depth)
    {
        digest.Append(geometry.LayoutHash.ToString("X16"));
        for(int i=0;i<geometry.MonsterCount;i++)
        {
            var monster=geometry.Monster(i);
            digest.Append(JourneyNumbers.Join(monster.Id,monster.Kind,monster.Xmm,monster.Zmm,monster.MaxHealth));
            digest.Append(LootCatalog.Version1.Generate(seed,depth,1,monster.Id,monster.Kind,i+1).Token());
        }
    }
    private static void CheckGeometry(Action<bool,string> check,JourneyGeometry g,string key)
    {
        var seen=new HashSet<(int,int)>();var q=new Queue<(int,int)>();
        var start=((int)((g.ReturnX-g.OriginMm)/g.CellMm),(int)((g.ReturnZ-g.OriginMm)/g.CellMm));seen.Add(start);q.Enqueue(start);
        while(q.Count>0){var a=q.Dequeue();foreach(var n in new[]{(a.Item1+1,a.Item2),(a.Item1-1,a.Item2),(a.Item1,a.Item2+1),(a.Item1,a.Item2-1)})if(!seen.Contains(n)&&g.IsFloor(n.Item1,n.Item2)&&g.HasSight(g.Centre(a.Item1),g.Centre(a.Item2),g.Centre(n.Item1),g.Centre(n.Item2))){seen.Add(n);q.Enqueue(n);}}
        bool Reach(long x,long z)=>g.CanStand(x,z)&&seen.Contains(((int)((x-g.OriginMm+g.CellMm/2)/g.CellMm),(int)((z-g.OriginMm+g.CellMm/2)/g.CellMm)));
        bool connected=Reach(g.ReturnX,g.ReturnZ)&&Reach(g.ForwardX,g.ForwardZ);
        for(int i=0;i<g.ChestCount;i++){g.Chest(i,out var x,out var z);connected&=Reach(x,z);}
        for(int i=0;i<g.MonsterCount;i++){var m=g.Monster(i);connected&=Reach(m.Xmm,m.Zmm);}
        if(g is JourneyRouteLayout route)connected&=Reach(route.CaveX,route.CaveZ);
        for(int z=0;z<g.Size;z++)for(int x=0;x<g.Size;x++)if(g.IsFloor(x,z))connected&=seen.Contains((x,z));
        check(connected,"journey-450mm-radius-reachability-"+key);
        bool spacing=true;
        for(int i=0;i<g.MonsterCount;i++)
        {
            var m=g.Monster(i);
            spacing&=FixedMath.Square(m.Xmm-g.ReturnX)+FixedMath.Square(m.Zmm-g.ReturnZ)>=FixedMath.Square(8000);
            for(int j=0;j<i;j++){var other=g.Monster(j);spacing&=FixedMath.Square(m.Xmm-other.Xmm)+FixedMath.Square(m.Zmm-other.Zmm)>=FixedMath.Square(1000);}
            if(g is JourneyRouteLayout outdoor){var rest=outdoor.Map.FindPoi("rest");spacing&=FixedMath.Square(m.Xmm-rest.Xmm)+FixedMath.Square(m.Zmm-rest.Zmm)>=FixedMath.Square(12000);}
        }
        check(spacing,"journey-separated-enemies-and-safe-arrival-rest-"+key);
    }
    private static void Corruption(Action<bool,string> check,ArenaSimulation w,JourneyMapCatalog maps)
    {
        string outer=w.Progression.SaveString();string original=WeaponMasteryTests.Unwrapped(outer);var parts=original.Split('|');string token=Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
        var f=token.Split(';');var head=f[0].Split(',');
        foreach(var mutation in new[]{(0,"2"),(2,"7"),(4,"2"),(4,"7"),(5,"0"),(8,"0")})
        {
            var copy=(string[])head.Clone();copy[mutation.Item1]=mutation.Item2;var fields=(string[])f.Clone();fields[0]=string.Join(",",copy);var envelope=(string[])parts.Clone();envelope[2]=Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(";",fields)));
            string body=string.Join("|",envelope.Take(4));check(!CharacterProgression.TryLoad(WeaponMasteryTests.Rewrap(outer,body+"|"+LootCatalog.HashText(body).ToString("X16")),maps,out _),"journey-rechecksummed-impossible-state-"+mutation.Item1+"-"+mutation.Item2);
        }
        check(w.Progression.SaveString()==outer,"journey-rejected-load-does-not-replace-valid-progression");
    }
    private static void CompanionMatrix(Action<bool,string> check,JourneyMapCatalog maps)
    {
        foreach(var status in new[]{CompanionStatus.Following,CompanionStatus.Waiting})
        {
            var w=Enter(maps,status);var c=w.Progression.Companion;var xp=c.TotalExperience;
            check((status==CompanionStatus.Following)==w.CompanionPresent,"journey-companion-area-binding-"+status);
            w=Reload(w,maps);check(w.Progression.Companion.Status==status&&w.Progression.Companion.TotalExperience==xp,"journey-companion-state-restores-"+status);
            ClearAndCollect(w);
            var route=(JourneyRouteLayout)w.JourneyGeometry;Walk(w,route.CaveX,route.CaveZ);w=Travel(w);
            check((status==CompanionStatus.Following)==w.CompanionPresent&&w.Progression.Companion.Status==status,"journey-companion-optional-cave-binding-"+status);
            check(CharacterProgression.TryLoad(w.Progression.SaveString(),maps,out _),"journey-companion-cave-save-valid-"+status);
        }
    }
    private static void CausalAndDeath(Action<bool,string> check,JourneyMapCatalog maps)
    {
        var w=Enter(maps,CompanionStatus.Following);var p=w.Progression;var hunter=p.Companion;
        p.EarnExperience(BigInteger.Pow(10,65));var playerXp=p.TotalExperience;var playerKills=p.LifetimeKills;var practices=Enumerable.Range(0,4).Select(p.PracticeExperience).ToArray();
        var m=w.Monsters[0];m.Health=1;
        m.Xmm=m.PreviousXmm=hunter.Xmm-1000;m.Zmm=m.PreviousZmm=hunter.Zmm;
        check(w.CanStand(m.Xmm,m.Zmm),"journey-hunter-causal-target-fixture-clearance");
        for(int i=0;i<40&&!m.IsDead;i++)w.Step();
        check(m.IsDead&&hunter.LifetimeKills==1&&hunter.PracticeExperience(0)>0,"journey-hunter-earns-own-effective-hit-and-defeat");
        check(p.TotalExperience==playerXp&&p.LifetimeKills==playerKills&&practices.SequenceEqual(Enumerable.Range(0,4).Select(p.PracticeExperience)),"journey-hunter-only-kill-gives-no-player-xp-or-practice");
        w=Reload(w,maps);p=w.Progression;hunter=p.Companion;
        check(p.TotalExperience==playerXp&&hunter.LifetimeKills==1&&w.GroundLoot.Count==1,"journey-big-integer-and-causal-ground-receipt-restore");
        var drop=w.GroundLoot.Single();Walk(w,drop.Xmm,drop.Zmm);w.TryCollectLoot();int items=p.Items.Count;
        check(items==1&&!w.TryCollectLoot()&&Reload(w,maps).Progression.Items.Count==items,"journey-hunter-loot-collects-once-through-player-inventory");
        // A real ordinary-enemy strike, rather than a story flag, kills the already-recruited hunter.
        hunter.Health=1;hunter.AttackCooldown=100;hunter.Status=CompanionStatus.Waiting;
        m=w.Monsters.First(e=>!e.IsDead);
        var offset=new[]{(-1000L,0L),(1000L,0L),(0L,-1000L),(0L,1000L)}
            .Where(o=>w.CanStand(hunter.Xmm+o.Item1,hunter.Zmm+o.Item2)&&w.JourneyGeometry.HasSight(hunter.Xmm,hunter.Zmm,hunter.Xmm+o.Item1,hunter.Zmm+o.Item2))
            .OrderByDescending(o=>FixedMath.Square(hunter.Xmm+o.Item1-w.PlayerXmm)+FixedMath.Square(hunter.Zmm+o.Item2-w.PlayerZmm)).First();
        m.Xmm=m.PreviousXmm=hunter.Xmm+offset.Item1;m.Zmm=m.PreviousZmm=hunter.Zmm+offset.Item2;m.AttackCooldownTicks=0;
        for(int i=0;i<40&&hunter.Status!=CompanionStatus.Dead;i++)w.Step();
        check(hunter.Status==CompanionStatus.Dead&&hunter.Health==0,"journey-real-enemy-hit-kills-companion");
        w=Reload(w,maps);check(w.Progression.Companion.Status==CompanionStatus.Dead&&!w.RecruitHunter().Accepted,"journey-dead-companion-stays-dead-on-load");
        // The player waits beside that existing live enemy until actual combat causes defeat.
        m=w.Monsters.First(e=>!e.IsDead);Walk(w,m.Xmm,m.Zmm,false);
        for(int i=0;i<1500&&!w.GameOver;i++)w.Step();
        check(w.GameOver,"journey-player-death-is-real-core-combat");
        ulong seed=w.Progression.Journey.RouteSeed,layout=w.JourneyGeometry.LayoutHash;int defeated=w.Monsters.Count(e=>e.IsDead);int inventory=w.Progression.Items.Count;
        w=Reload(w,maps);check(w.GameOver,"journey-dead-player-save-restores-health-zero");
        w.RestartJourneyPlayer();w=Reload(w,maps);
        check(!w.GameOver&&w.Progression.Journey.RouteSeed==seed&&w.JourneyGeometry.LayoutHash==layout&&w.Monsters.Count(e=>e.IsDead)==defeated&&w.Progression.Items.Count==inventory,"journey-death-restart-retains-run-deaths-and-earned-items");
        check(w.Progression.Companion.Status==CompanionStatus.Dead,"journey-player-restart-does-not-revive-hunter");
    }
}
