using System;
using System.Collections.Generic;
using AreArpg.Core;

internal static class CryptDungeonTests
{
    internal static ArenaSimulation Enter(bool broken=false)
    {
        var p=CharacterProgression.CreateEntryProfile();
        for(int i=0;i<4;i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect); p.TravelHunger(5,false); p.Investigation.Clues=7;
        var w=new ArenaSimulation(123,p,1,LootCatalog.Version1);
        HunterEncounterTests.Move(w,0,18000);
        if(!w.ActOnHunger(broken?HungerAction.BreakDungeonSeal:HungerAction.OpenDungeonSeal).Accepted) throw new Exception("Dungeon seal refused");
        HunterEncounterTests.Move(w,0,24000); return w.TryTraversePassage();
    }
    internal static ArenaSimulation Reload(ArenaSimulation w)
    {
        if(!CharacterProgression.TryLoad(w.Progression.SaveString(),out var p)) throw new Exception("Dungeon save rejected: "+w.Progression.SaveString());
        return new ArenaSimulation(123,p,1,LootCatalog.Version1);
    }
    internal static void WalkFight(ArenaSimulation w,long x,long z)
    {
        for(int t=0;t<600;t++)
        {
            if(w.GameOver) throw new Exception("Dungeon route killed player");
            long dx=x-w.PlayerXmm,dz=z-w.PlayerZmm;
            bool threat=false;
            foreach(var m in w.Monsters)
                if(!m.IsDead && Math.Abs(m.Xmm-w.PlayerXmm)<4000 && Math.Abs(m.Zmm-w.PlayerZmm)<4000 &&
                    CryptDungeonLayout.HasSight(w.Progression.Dungeon.Depth,w.PlayerXmm,w.PlayerZmm,m.Xmm,m.Zmm)) threat=true;
            if(!threat && Math.Abs(dx)<600 && Math.Abs(dz)<600) { w.SetMoveInput(0,0); return; }
            w.SetMoveInput(threat?0:Math.Abs(dx)<500?0:Math.Sign(dx)*10000,threat?0:Math.Abs(dz)<500?0:Math.Sign(dz)*10000);
            if(w.Progression.EntryAct==7 && w.Progression.Rattler.Creature.BossWindupTicks>0) w.SetMoveInput(0,-10000);
            w.ActivateSkill(3); w.ActivateSkill(1); w.ActivateSkill(0); w.ActivateSkill(2); w.TryCollectLoot(); w.Step();
        }
        throw new Exception("Dungeon route blocked at "+w.PlayerXmm+","+w.PlayerZmm+" toward "+x+","+z);
    }
    internal static void ClearFloor(ArenaSimulation w)
    {
        int side=w.Progression.Dungeon.Depth==2?-1:1;
        WalkFight(w,0,-8000); WalkFight(w,-16000*side,-4000); WalkFight(w,-18000*side,0);
        w.ActOnHunger(HungerAction.OpenWestChest); w.TryCollectLoot();
        WalkFight(w,-16000*side,0); WalkFight(w,-16000*side,12000); WalkFight(w,0,12000);
        WalkFight(w,0,8000); WalkFight(w,0,4000); WalkFight(w,16000*side,4000); WalkFight(w,18000*side,8000);
        w.ActOnHunger(HungerAction.OpenEastChest); w.TryCollectLoot();
        WalkFight(w,16000*side,4000); WalkFight(w,0,4000); WalkFight(w,0,16000);
        // Drops can lie off the walking line; collect them by physically visiting their saved locations.
        while(w.GroundLoot.Count>0) { var drop=w.GroundLoot[0]; WalkFight(w,drop.Xmm,drop.Zmm); w.TryCollectLoot(); }
    }
    internal static void Run(Action<bool,string> check)
    {
        for (int depth=1; depth<=3; depth++)
        {
            var seen = new HashSet<(int,int)>(); var todo = new Queue<(int,int)>(); todo.Enqueue((0,-6));
            while (todo.Count > 0)
            {
                var p = todo.Dequeue();
                if (seen.Contains(p) || !CryptDungeonLayout.CanStand(depth,p.Item1*4000,p.Item2*4000)) continue;
                seen.Add(p);
                todo.Enqueue((p.Item1-1,p.Item2)); todo.Enqueue((p.Item1+1,p.Item2));
                todo.Enqueue((p.Item1,p.Item2-1)); todo.Enqueue((p.Item1,p.Item2+1));
            }
            int side = depth == 2 ? -1 : 1;
            check(seen.Contains((0,6)) && seen.Contains((-4*side,-1)) && seen.Contains((4*side,1)), "dungeon-connected-stairs-and-side-rooms-"+depth);
            check(!CryptDungeonLayout.CanStand(depth,8000,-20000), "dungeon-solid-rock-blocks-shortcut-"+depth);
            check(!CryptDungeonLayout.HasSight(depth,0,-20000,16000,4000), "dungeon-no-sight-through-room-walls-"+depth);
            for (int i=0;i<4;i++)
            {
                var m=CryptDungeonLayout.Monster(depth,i);
                check(CryptDungeonLayout.CanStand(depth,m.Xmm,m.Zmm),"dungeon-enemy-in-connected-room-"+depth+"-"+i);
            }
            for (int i=0;i<2;i++)
            {
                CryptDungeonLayout.Chest(depth,i,out var x,out var z);
                check(CryptDungeonLayout.CanStand(depth,x,z),"dungeon-chest-accessible-"+depth+"-"+i);
            }
        }
        check(!CryptDungeonLayout.CanStand(1,long.MinValue,long.MaxValue),"dungeon-extreme-coordinates-rejected");
        var state = new CryptDungeonState { Active=true, Seed=123 };
        string token=state.Token();
        check(CryptDungeonState.TryRead(token,6,out var copy) && copy.Token()==token,"dungeon-canonical-world-roundtrip");
        var dead=state.Current.Monsters[0]; dead.Health=0; dead.LootGranted=true;
        var item=LootCatalog.Version1.Generate(123,1,10,dead.Id,dead.Kind,1);
        state.Current.Loot.Add(new LootState { Id=dead.Id,Xmm=dead.Xmm,Zmm=dead.Zmm,WeaponBonus=8,Item=item });
        token=state.Token();
        check(CryptDungeonState.TryRead(token,6,out copy) && copy.Current.Loot[0].Item.InstanceId==item.InstanceId && copy.Token()==token,"dungeon-uncollected-catalog-receipt-survives");
        state.UnlockedDepth=2;
        check(!CryptDungeonState.TryRead(state.Token(),6,out _),"dungeon-deeper-floor-with-living-guards-rejected");
        state.UnlockedDepth=1; state.Current.Loot.Add(state.Current.Loot[0]);
        check(!CryptDungeonState.TryRead(state.Token(),6,out _),"dungeon-duplicate-drops-rejected");
        var game=Enter();
        check(game.InDungeon && game.Progression.Dungeon.Depth==1 && game.Monsters.Count==4 && !game.Progression.Rattler.Active,"dungeon-real-first-descent-before-rattler");
        check(game.Progression.SaveString().StartsWith("AC14|") && Reload(game).Progression.SaveString()==game.Progression.SaveString(),"dungeon-ac14-entry-roundtrip");
        check(!game.ActOnHunger(HungerAction.OpenWestChest).Accepted && !game.HungerForwardOpen,"dungeon-no-remote-chest-or-early-descent");
        WalkFight(game,0,-10000); var resumed=Reload(game);
        for(int t=0;t<20;t++)
        {
            game.SetMoveInput(0,0); resumed.SetMoveInput(0,0);
            game.ActivateSkill(0); resumed.ActivateSkill(0); game.Step(); resumed.Step();
            check(game.Progression.SaveString()==resumed.Progression.SaveString(),"dungeon-mid-combat-exact-save-continuation-"+t);
        }
        for(int depth=1;depth<=2;depth++)
        {
            ClearFloor(game);
            check(game.Progression.Dungeon.Current.Cleared && game.Progression.Dungeon.Current.OpenChests==3 && game.GroundLoot.Count==0,"dungeon-combat-and-both-side-chests-"+depth);
            game=Reload(game);
            if(depth==2)
            {
                WalkFight(game,0,15000);
                check(game.ActOnHunger(HungerAction.InspectCryptTrace).Accepted && game.ActOnHunger(HungerAction.OpenInnerDoor).Accepted,"dungeon-rattler-door-only-after-second-floor");
            }
            WalkFight(game,0,24000); int hp=game.PlayerHealth,focus=game.Resource;
            game=game.TryTraversePassage();
            check(game!=null && game.Progression.Dungeon.Depth==depth+1 && game.PlayerHealth==hp && game.Resource==focus,"dungeon-real-stairs-preserve-player-"+depth);
        }
        check(game.Progression.EntryAct==7 && game.Monsters.Count==5 && game.Progression.Rattler.Creature.Zmm==17000,"dungeon-rattler-waits-in-third-floor-hall");
        check(game.Progression.Level>1 && game.Progression.Items.Count==8 && game.Progression.LootCollected==12,"dungeon-real-xp-levels-and-item-receipts");
        ClearFloor(game);
        check(game.Progression.Rattler.Creature.IsDead && game.Progression.Dungeon.Current.Cleared && !game.GameOver,"dungeon-third-floor-and-rattler-completed-with-shipped-skills");
        WalkFight(game,game.Progression.Rattler.Creature.Xmm,game.Progression.Rattler.Creature.Zmm);
        check(game.ActOnHunger(HungerAction.InspectRoyalBrand).Accepted,"dungeon-existing-brand-story-follows-boss");
        game=Reload(game);
        var kills=game.Progression.LifetimeKills;
        WalkFight(game,0,12000); WalkFight(game,0,4000); WalkFight(game,0,-8000); WalkFight(game,0,-24000); game=game.TryTraversePassage();
        check(game.Progression.Dungeon.Depth==2 && game.Progression.Dungeon.Current.Cleared && game.GroundLoot.Count==0 && game.Progression.LifetimeKills==kills,"dungeon-return-keeps-cleared-floor-and-rewards");
        for(int d=2;d>=1;d--) { WalkFight(game,0,12000); WalkFight(game,0,4000); WalkFight(game,0,-8000); WalkFight(game,0,-24000); game=game.TryTraversePassage(); }
        check(game.Progression.EntryAct==5 && Reload(game).Progression.Rattler.BrandExamined,"dungeon-full-return-to-store-with-story-and-loot");
        HunterEncounterTests.Move(game,0,24000); game=game.TryTraversePassage();
        check(game.Progression.Dungeon.Depth==1 && game.Progression.Dungeon.Current.Cleared && game.Progression.Dungeon.Current.OpenChests==3,"dungeon-revisit-does-not-respawn-rewards");
        var death=Enter(); HunterEncounterTests.Move(death,0,-8000); death.SetMoveInput(0,0);
        for(int t=0;t<1000 && !death.GameOver;t++) death.Step();
        check(death.GameOver && Reload(death).GameOver && !death.ActOnHunger(HungerAction.OpenWestChest).Accepted,"dungeon-death-save-and-action-gate");
        death.Progression.Investigation.RestartPlayer(); death=new ArenaSimulation(123,death.Progression,1,LootCatalog.Version1);
        check(!death.GameOver && death.PlayerZmm==-21000 && death.CanStand(death.PlayerXmm,death.PlayerZmm),"dungeon-restart-at-walkable-entrance");
    }
}
