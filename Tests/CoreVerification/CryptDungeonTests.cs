using System;
using System.Collections.Generic;
using AreArpg.Core;

internal static class CryptDungeonTests
{
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
    }
}
