using System;
using System.Collections.Generic;
using System.Linq;
using AshenCrypt.WorldForge.Core;
internal static class Program
{
    static int failures;
    static ForgedWorldMap Map() => new ForgedWorldMap(0,0,1000,3,3,new int[9],new[]{true,true,true,true},
        new[]{new ForgedObstacle(1000,1000,100,ForgedObstacleFlags.BlocksMovement)},
        new[]{new ForgedPoiRecord("camp",ForgedPoiKind.Landmark,1000,1000,300,"safe")});
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    static void Run(string name,Action test)
    {
        try { test();Console.WriteLine("PASS "+name); }
        catch(Exception e) { failures++;Console.Error.WriteLine("FAIL "+name+": "+e.Message); }
    }
    static int Main()
    {
        Run("obstacle-exposure-cannot-change-collision-or-hash",()=>{
            var m=Map();byte[] before=m.ToBytes();ulong hash=m.ContentHash;
            try { if(m.Obstacles is IList<ForgedObstacle> list)list[0]=new ForgedObstacle(1700,1700,100,ForgedObstacleFlags.BlocksMovement); }
            catch(NotSupportedException) { }
            Check(!m.CanStand(1000,1000,50),"external collection mutation removed real collision");
            Check(before.SequenceEqual(m.ToBytes()),"external mutation changed serialized content");
            Check(m.ContentHash==hash && ForgedWorldMap.FromBytes(m.ToBytes()).ContentHash==hash,"content identity drifted");
        });
        Run("poi-exposure-cannot-change-identity-or-serialization",()=>{
            var m=Map();byte[] before=m.ToBytes();
            try { if(m.Pois is IList<ForgedPoiRecord> list)list[0]=new ForgedPoiRecord("mutated",ForgedPoiKind.Landmark,1700,1700,20,""); }
            catch(NotSupportedException) { }
            Check(m.FindPoi("camp")!=null && m.FindPoi("mutated")==null,"external collection mutation replaced map POI");
            Check(before.SequenceEqual(m.ToBytes()),"POI mutation changed serialized content");
        });
        Console.WriteLine("RESULT: "+(2-failures)+"/2 real immutable map scenarios passed");
        return failures==0?0:1;
    }
}
