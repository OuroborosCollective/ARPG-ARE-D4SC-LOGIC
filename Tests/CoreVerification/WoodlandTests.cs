using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
internal static class WoodlandTests
{
    internal static ArenaSimulation Accepted(ulong seed=71)
    {
        var p=CharacterProgression.CreateEntryProfile();
        for(int i=0;i<4;i++)p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect);p.TravelHunger(2,false);
        p.Investigation.Active=true;p.Investigation.Clues=7;p.Investigation.Approach=SealApproach.Investigated;p.Investigation.TraceFound=true;p.Investigation.Reported=true;
        p.Rattler.Active=true;p.Rattler.Creature.Health=0;p.Rattler.Creature.LootGranted=true;p.Rattler.BrandExamined=true;p.Rattler.Reported=true;
        var w=new ArenaSimulation(seed,p,1,LootCatalog.Version1);
        var npc=w.Npcs[0]; npc.Xmm=w.PlayerXmm+1000; npc.Zmm=w.PlayerZmm;
        if(!w.ActOnHunger(HungerAction.AcceptTransportLead).Accepted)throw new Exception("Transport lead refused");
        p.TravelHunger(4,false);w=new ArenaSimulation(seed,p,1,LootCatalog.Version1);
        HunterEncounterTests.Move(w,ArenaSimulation.MillForestXmm,ArenaSimulation.MillForestZmm);
        return w;
    }
    internal static ArenaSimulation Enter(ulong seed=71) => Accepted(seed).TryTraversePassage()??throw new Exception("Forest entry refused");
    internal static ArenaSimulation Reload(ArenaSimulation w)
    {
        if(!CharacterProgression.TryLoad(w.Progression.SaveString(),out var p))throw new Exception("Forest save rejected: "+w.Progression.SaveString());
        return new ArenaSimulation(71,p,1,LootCatalog.Version1);
    }
    private static (int,int) Cell(long x,long z) => ((int)((x+42000)/4000)-10,(int)((z+54000)/4000)-13);
    internal static void Walk(ArenaSimulation w,long x,long z)
    {
        var start=Cell(w.PlayerXmm,w.PlayerZmm);var end=Cell(x,z);
        var before=new Dictionary<(int,int),(int,int)>{[start]=start};var q=new Queue<(int,int)>();q.Enqueue(start);
        while(q.Count>0&&!before.ContainsKey(end))
        {
            var c=q.Dequeue();foreach(var n in new[]{(c.Item1-1,c.Item2),(c.Item1+1,c.Item2),(c.Item1,c.Item2-1),(c.Item1,c.Item2+1)})
                if(!before.ContainsKey(n)&&w.CanStand(n.Item1*4000,n.Item2*4000)){before[n]=c;q.Enqueue(n);}
        }
        if(!before.ContainsKey(end))throw new Exception("No forest route");
        var path=new List<(int,int)>();var node=end;while(node!=start){path.Add(node);node=before[node];}path.Reverse();
        Segment(w,start.Item1*4000,start.Item2*4000);foreach(var c in path)Segment(w,c.Item1*4000,c.Item2*4000);Segment(w,x,z);
    }
    private static void Segment(ArenaSimulation w,long x,long z)
    {
        for(int t=0;t<800;t++)
        {
            if(w.GameOver)throw new Exception("Forest route killed player");
            long dx=x-w.PlayerXmm,dz=z-w.PlayerZmm;
            bool threat=w.Monsters.Any(m=>!m.IsDead&&Math.Abs(m.Xmm-w.PlayerXmm)<4000&&Math.Abs(m.Zmm-w.PlayerZmm)<4000&&w.Progression.Woodland.Layout.HasSight(w.PlayerXmm,w.PlayerZmm,m.Xmm,m.Zmm));
            if(!threat&&Math.Abs(dx)<450&&Math.Abs(dz)<450){w.SetMoveInput(0,0);return;}
            w.SetMoveInput(threat?0:Math.Abs(dx)<350?0:Math.Sign(dx)*10000,threat?0:Math.Abs(dz)<350?0:Math.Sign(dz)*10000);
            w.ActivateSkill(3);w.ActivateSkill(1);w.ActivateSkill(0);w.ActivateSkill(2);w.TryCollectLoot();w.Step();
        }
        throw new Exception("Forest route blocked");
    }
    private static void Collect(ArenaSimulation w)
    { while(w.GroundLoot.Count>0){var l=w.GroundLoot[0];Walk(w,l.Xmm,l.Zmm);if(!w.TryCollectLoot()&&w.GroundLoot.Contains(l))throw new Exception("Forest loot unreachable");} }
    public static void Run(Action<bool,string> require)
    {
        var layouts=new HashSet<string>();
        for(ulong seed=0;seed<128;seed++)
        {
            var a=new WoodlandLayout(seed);var b=new WoodlandLayout(seed);
            require(a.CanStand(0,-48000)&&a.CanStand(0,48000)&&a.CanStand(-36000,0),"woodland-fixed-entrances-"+seed);
            var seen=new HashSet<(int,int)>{(0,-12)};var q=new Queue<(int,int)>();q.Enqueue((0,-12));
            while(q.Count>0){var c=q.Dequeue();foreach(var n in new[]{(c.Item1-1,c.Item2),(c.Item1+1,c.Item2),(c.Item1,c.Item2-1),(c.Item1,c.Item2+1)})if(a.IsFloor(n.Item1,n.Item2)&&seen.Add(n))q.Enqueue(n);}
            string key="";bool same=true,connected=true;
            for(int x=-10;x<=10;x++)for(int z=-13;z<=13;z++){same&=a.IsFloor(x,z)==b.IsFloor(x,z);connected&=!a.IsFloor(x,z)||seen.Contains((x,z));key+=a.IsFloor(x,z)?"1":"0";}
            require(same&&connected,"woodland-reproducible-and-all-cells-connected-"+seed);layouts.Add(key);
            require(Enumerable.Range(0,a.MonsterCount).All(i=>{var m=a.Monster(i);return a.CanStand(m.Xmm,m.Zmm)&&Math.Abs(m.Zmm)<40000&&Math.Abs(m.Xmm+36000)+Math.Abs(m.Zmm)>14000;}),"woodland-safe-valid-spawns-"+seed);
            require(a.HeightMm(0,-48000)==0&&a.HeightMm(0,48000)==0&&a.HeightMm(-36000,0)==0,"woodland-level-doorways-"+seed);
        }
        require(layouts.Count>120,"woodland-layout-variation");
        var w=Enter();require(w.InForest&&w.Monsters.Count==16,"forest-entered-with-existing-enemy-families");
        require(w.Progression.SaveString().StartsWith("AC15|"),"forest-versioned-save");
        var loaded=Reload(w);require(loaded.Progression.SaveString()==w.Progression.SaveString(),"forest-entry-roundtrip");
        Walk(w,-36000,0);Collect(w);Walk(w,-36000,0);
        var before=w.Progression.Woodland;var cave=w.TryTraversePassage();require(cave!=null&&cave.Progression.EntryAct==9,"forest-actual-cave-passage");
        HunterEncounterTests.Move(cave,0,5000);
        require(!cave.ActOnHunger(HungerAction.SkinCubs).Accepted,"cubs-decision-requires-inspection");
        require(cave.ActOnHunger(HungerAction.InspectCubs).Accepted,"cubs-personal-inspection");
        require(cave.ActOnHunger(HungerAction.SpareCubs).Accepted&&!cave.ActOnHunger(HungerAction.SkinCubs).Accepted,"cubs-exclusive-permanent-choice");
        require(!cave.Progression.Woodland.WarBearEligibility,"sparing-is-not-yet-organized-protection");
        cave=Reload(cave);HunterEncounterTests.Move(cave,0,-18000);w=cave.TryTraversePassage();
        require(w!=null&&w.Progression.Woodland.Seed==before.Seed&&w.Progression.Woodland.Run==before.Run&&w.Monsters.Count(m=>m.IsDead)==before.Monsters.Count(m=>m.IsDead),"cave-preserves-current-run");
        Walk(w,0,48000);Collect(w);Walk(w,0,48000);var post=w.TryTraversePassage();
        require(post!=null&&post.Progression.EntryAct==10&&post.Progression.Woodland.ReachedAschenzoll,"actual-arrival-at-aschenzoll");
        post=Reload(post);HunterEncounterTests.Move(post,0,-18000);w=post.TryTraversePassage();
        require(w!=null&&w.Progression.Woodland.Run==2&&w.Progression.Woodland.Seed!=before.Seed&&w.Monsters.All(m=>!m.IsDead)&&w.GroundLoot.Count==0,"full-exit-reentry-new-run");
        require(w.Progression.Woodland.Choice==CubChoice.Spared&&w.Progression.Rattler.Reported,"new-run-retains-story");
        var seedBefore=w.Progression.Woodland.Seed;w.Progression.Woodland.RestartPlayer();w=Reload(w);require(w.Progression.Woodland.Seed==seedBefore,"restart-does-not-reroll");
    }
}
