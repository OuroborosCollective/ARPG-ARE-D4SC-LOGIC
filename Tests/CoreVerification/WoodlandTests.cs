using System;
using AreArpg.Core;
internal static class WoodlandTests
{
    public static void Run(Action<bool,string> require)
    {
        var a=new WoodlandLayout(71); var b=new WoodlandLayout(71);
        require(a.CanStand(0,-48000) && a.CanStand(0,48000) && a.CanStand(-36000,0), "woodland-fixed-story-entrances-walkable");
        require(a.MonsterCount>=14 && a.MonsterCount<=20, "woodland-populated-combat-region");
        for(int x=-10;x<=10;x++) for(int z=-13;z<=13;z++)
            require(a.IsFloor(x,z)==b.IsFloor(x,z), "woodland-seeded-layout-"+x+"-"+z);
    }
}
