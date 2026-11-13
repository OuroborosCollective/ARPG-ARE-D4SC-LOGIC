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
            check(seen.Contains((0,6)) && seen.Contains((-4,-1)) && seen.Contains((4,1)), "dungeon-connected-stairs-and-side-rooms-"+depth);
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
    }
}
