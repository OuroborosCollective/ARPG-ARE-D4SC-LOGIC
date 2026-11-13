using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;

internal static class CryptDungeonTests
{
    internal static ArenaSimulation Enter(bool broken=false,ulong seed=123)
    {
        var p=CharacterProgression.CreateEntryProfile();
        for(int i=0;i<4;i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect); p.TravelHunger(5,false); p.Investigation.Clues=7;
        var w=new ArenaSimulation(seed,p,1,LootCatalog.Version1);
        HunterEncounterTests.Move(w,0,18000);
        if(!w.ActOnHunger(broken?HungerAction.BreakDungeonSeal:HungerAction.OpenDungeonSeal).Accepted) throw new Exception("Dungeon seal refused");
        HunterEncounterTests.Move(w,0,24000); return w.TryTraversePassage();
    }
    internal static ArenaSimulation Reload(ArenaSimulation w)
    {
        if(!CharacterProgression.TryLoad(w.Progression.SaveString(),out var p)) throw new Exception("Dungeon save rejected: "+w.Progression.SaveString());
        return new ArenaSimulation(123,p,1,LootCatalog.Version1);
    }
    private static (int,int) Cell(long x,long z) { return ((int)((x+26000)/4000)-6,(int)((z+26000)/4000)-6); }
    internal static void WalkFight(ArenaSimulation w,long x,long z)
    {
        var start=Cell(w.PlayerXmm,w.PlayerZmm); var end=Cell(x,z);
        var before=new Dictionary<(int,int),(int,int)> { [start]=start }; var queue=new Queue<(int,int)>(); queue.Enqueue(start);
        while(queue.Count>0 && !before.ContainsKey(end))
        {
            var p=queue.Dequeue();
            foreach(var n in new[]{(p.Item1-1,p.Item2),(p.Item1+1,p.Item2),(p.Item1,p.Item2-1),(p.Item1,p.Item2+1)})
                if(!before.ContainsKey(n) && w.CanStand(n.Item1*4000,n.Item2*4000)) { before[n]=p; queue.Enqueue(n); }
        }
        if(!before.ContainsKey(end)) throw new Exception("No test walking route to "+x+","+z);
        var path=new List<(int,int)>(); var node=end;
        while(node!=start) { path.Add(node); node=before[node]; }
        path.Reverse();
        // Visit cell centres to avoid diagonal corner-cutting; every step is actual movement and combat.
        Segment(w,start.Item1*4000,start.Item2*4000);
        foreach(var p in path) Segment(w,p.Item1*4000,p.Item2*4000);
        Segment(w,x,z);
    }
    private static void Segment(ArenaSimulation w,long x,long z)
    {
        for(int t=0;t<800;t++)
        {
            if(w.GameOver) throw new Exception("Dungeon route killed player");
            long dx=x-w.PlayerXmm,dz=z-w.PlayerZmm;
            bool threat=w.Monsters.Any(m=>!m.IsDead && Math.Abs(m.Xmm-w.PlayerXmm)<4000 && Math.Abs(m.Zmm-w.PlayerZmm)<4000 &&
                w.Progression.Dungeon.Current.Layout.HasSight(w.PlayerXmm,w.PlayerZmm,m.Xmm,m.Zmm));
            if(!threat && Math.Abs(dx)<450 && Math.Abs(dz)<450) { w.SetMoveInput(0,0); return; }
            w.SetMoveInput(threat?0:Math.Abs(dx)<350?0:Math.Sign(dx)*10000,threat?0:Math.Abs(dz)<350?0:Math.Sign(dz)*10000);
            if(w.Progression.EntryAct==7 && w.Progression.Rattler.Creature.BossWindupTicks>0) w.SetMoveInput(0,-10000);
            w.ActivateSkill(3); w.ActivateSkill(1); w.ActivateSkill(0); w.ActivateSkill(2); w.TryCollectLoot(); w.Step();
        }
        throw new Exception("Dungeon route blocked at "+w.PlayerXmm+","+w.PlayerZmm+" toward "+x+","+z);
    }
    internal static void ClearFloor(ArenaSimulation w)
    {
        var map=w.Progression.Dungeon.Current.Layout;
        for(int i=1;i<map.RoomCount;i++) { map.Room(i,out var x,out var z); WalkFight(w,x,z); }
        foreach(var enemy in w.Monsters.ToArray()) if(!enemy.IsDead) WalkFight(w,enemy.Xmm,enemy.Zmm);
        for(int i=0;i<2;i++) if(map.HasChest(i))
        {
            map.Chest(i,out var x,out var z); WalkFight(w,x,z);
            w.ActOnHunger(i==0?HungerAction.OpenWestChest:HungerAction.OpenEastChest); w.TryCollectLoot();
        }
        while(w.GroundLoot.Count>0) { var drop=w.GroundLoot[0]; WalkFight(w,drop.Xmm,drop.Zmm); w.TryCollectLoot(); }
    }
    internal static void Run(Action<bool,string> check)
    {
        var layouts=new HashSet<string>(); var spawns=new HashSet<string>(); bool emptyChests=false;
        for(ulong seed=1;seed<=128;seed++) for(int depth=1;depth<=3;depth++)
        {
            var map=new CryptDungeonLayout(seed,depth); var seen=new HashSet<(int,int)>(); var todo=new Queue<(int,int)>(); todo.Enqueue((0,-6));
            while(todo.Count>0)
            {
                var p=todo.Dequeue();
                if(seen.Contains(p) || !map.CanStand(p.Item1*4000,p.Item2*4000)) continue;
                seen.Add(p); todo.Enqueue((p.Item1-1,p.Item2)); todo.Enqueue((p.Item1+1,p.Item2)); todo.Enqueue((p.Item1,p.Item2-1)); todo.Enqueue((p.Item1,p.Item2+1));
            }
            check(seen.Contains((0,6)) && seen.Count>=40,"proc-connected-entry-exit-"+seed+"-"+depth);
            for(int i=0;i<map.RoomCount;i++) { map.Room(i,out var x,out var z); check(seen.Contains(Cell(x,z)),"proc-room-reachable-"+seed+"-"+depth+"-"+i); }
            for(int i=0;i<map.MonsterCount;i++) { var m=map.Monster(i); check(map.CanStand(m.Xmm,m.Zmm),"proc-monster-walkable-"+seed+"-"+depth+"-"+i); spawns.Add(m.Xmm+","+m.Zmm+","+m.Kind); }
            for(int i=0;i<2;i++) if(map.HasChest(i)) { map.Chest(i,out var x,out var z); check(map.CanStand(x,z) && seen.Contains(Cell(x,z)),"proc-chest-reachable-"+seed+"-"+depth+"-"+i); }
            string fingerprint=""; for(int z=-6;z<=6;z++) for(int x=-6;x<=6;x++) fingerprint+=map.IsFloor(x,z)?"1":"0";
            layouts.Add(fingerprint); emptyChests|=map.ChestMask==0;
        }
        check(layouts.Count>100 && spawns.Count>80 && emptyChests,"proc-varied-layouts-enemies-and-absent-chests");
        var state=new CryptDungeonState(123) {Active=true}; string token=state.Token();
        check(CryptDungeonState.TryRead(token,6,out var copy) && copy.Token()==token,"proc-canonical-world-roundtrip");
        var game=Enter();
        check(game.InDungeon && game.Progression.Dungeon.Depth==1 && !game.Progression.Rattler.Active,"dungeon-first-descent-before-rattler");
        check(game.Progression.SaveString().StartsWith("AC14|") && Reload(game).Progression.SaveString()==game.Progression.SaveString(),"proc-ac14-entry-roundtrip");
        check(!game.ActOnHunger(HungerAction.OpenWestChest).Accepted && !game.HungerForwardOpen,"proc-no-remote-chest-or-early-descent");
        var resumed=Reload(game);
        for(int t=0;t<40;t++) { game.SetMoveInput(0,10000); resumed.SetMoveInput(0,10000); game.ActivateSkill(0); resumed.ActivateSkill(0); game.Step(); resumed.Step(); check(game.Progression.SaveString()==resumed.Progression.SaveString(),"proc-identical-resumed-future-"+t); }
        for(int run=1;run<=2;run++)
        {
            ulong seed=game.Progression.Dungeon.Seed;
            for(int depth=1;depth<=3;depth++)
            {
                ClearFloor(game);
                check(game.Progression.Dungeon.Current.Cleared && game.Progression.Dungeon.Current.OpenChests==game.Progression.Dungeon.Current.Layout.ChestMask,"proc-floor-clear-and-available-chests-"+run+"-"+depth);
                game=Reload(game);
                if(depth==2 && !game.Progression.Rattler.Active) { WalkFight(game,0,15000); check(game.ActOnHunger(HungerAction.InspectCryptTrace).Accepted && game.ActOnHunger(HungerAction.OpenInnerDoor).Accepted,"proc-second-floor-story-door"); }
                if(depth<3) { WalkFight(game,0,24000); int hp=game.PlayerHealth; game=game.TryTraversePassage(); check(game!=null && game.Progression.Dungeon.Depth==depth+1 && game.PlayerHealth==hp,"proc-real-stairs-"+run+"-"+depth); }
            }
            check(game.Progression.Rattler.Creature.IsDead && !game.GameOver && game.Progression.Items.Any(i=>i.DefinitionId=="crown.shard"),"proc-farmable-rattler-and-catalog-boss-loot-"+run);
            WalkFight(game,game.Progression.Rattler.Creature.Xmm,game.Progression.Rattler.Creature.Zmm);
            check(game.ActOnHunger(HungerAction.InspectRoyalBrand).Accepted==(run==1),"proc-brand-discovery-only-once-"+run);
            var kills=game.Progression.LifetimeKills; var xp=game.Progression.TotalExperience;
            for(int d=3;d>=1;d--) { WalkFight(game,0,-24000); game=game.TryTraversePassage(); check(game!=null,"proc-return-stairs-"+run+"-"+d); }
            check(game.Progression.EntryAct==5 && game.Progression.Dungeon.NeedsNewRun && Reload(game).Progression.SaveString()==game.Progression.SaveString(),"proc-run-boundary-saved-"+run);
            HunterEncounterTests.Move(game,0,24000); game=game.TryTraversePassage();
            check(game.Progression.Dungeon.Run==run+1 && game.Progression.Dungeon.Seed!=seed && game.Monsters.All(m=>!m.IsDead) && game.Progression.Dungeon.Current.OpenChests==0,"proc-new-run-new-seed-and-respawns-"+run);
            check(game.Progression.LifetimeKills==kills && game.Progression.TotalExperience==xp && game.Progression.Rattler.BrandExamined && !game.Progression.Rattler.Creature.IsDead,"proc-reset-preserves-character-and-story-"+run);
            check(Reload(game).Progression.SaveString()==game.Progression.SaveString(),"proc-repeated-boss-with-persistent-brand-loads-"+run);
        }
        check(game.Progression.Level>1 && game.Progression.Items.Count>20,"proc-farming-produces-levels-and-unique-items");
    }
}
