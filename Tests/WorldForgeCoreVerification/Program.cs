using System;
using System.Collections.Generic;
using System.Linq;
using AshenCrypt.WorldForge.Core;
internal static class Program
{
    static int failures;
    static int scenarios;
    static ForgedWorldMap Map() => new ForgedWorldMap(0,0,1000,3,3,new int[9],new[]{true,true,true,true},
        new[]{new ForgedObstacle(1000,1000,100,ForgedObstacleFlags.BlocksMovement)},
        new[]{new ForgedPoiRecord("camp",ForgedPoiKind.Landmark,1000,1000,300,"safe")});
    static ForgedWorldMap Slope() => new ForgedWorldMap(0,0,1000,3,3,
        new[]{0,1000,2000, 0,1000,2000, 0,1000,2000},
        new[]{true,true,true,true}, Array.Empty<ForgedObstacle>(), Array.Empty<ForgedPoiRecord>());
    static ForgedWorldMap PartlyBlocked() => new ForgedWorldMap(0,0,1000,3,3,new int[9],
        new[]{false,true,true,true}, Array.Empty<ForgedObstacle>(), Array.Empty<ForgedPoiRecord>());
    static ForgedWorldMap SightWall() => new ForgedWorldMap(0,0,1000,5,5,new int[25],
        new bool[16].Select(_ => true).ToArray(),
        new[]{new ForgedObstacle(2000,0,200,ForgedObstacleFlags.BlocksSight)},
        new[]{new ForgedPoiRecord("gate",ForgedPoiKind.Waypoint,2000,0,50,"north,stone"),
              new ForgedPoiRecord("home",ForgedPoiKind.NpcHome,0,0,10,"safe")});
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    static void Run(string name,Action test)
    {
        scenarios++;
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
        Run("bounds-and-walkable-queries",()=>{
            var m=Map();
            Check(m.WidthMm==2000 && m.DepthMm==2000 && m.CellsX==2 && m.CellsZ==2,"derived dimensions");
            Check(m.InBounds(0,0) && m.InBounds(2000,2000),"corner points are inside");
            Check(!m.InBounds(-1,0) && !m.InBounds(2001,0) && !m.InBounds(0,2001),"outside points are rejected");
            Check(m.IsWalkableCell(0,0) && m.IsWalkableCell(1,1),"in-range walkable cells");
            Check(!m.IsWalkableCell(-1,0) && !m.IsWalkableCell(2,0),"out-of-range cells are not walkable");
            Check(m.IsWalkablePoint(1500,1500),"walkable point");
            Check(!m.IsWalkablePoint(2500,0) && !m.IsWalkablePoint(-1,0),"out-of-bounds point is not walkable");
            var blocked=PartlyBlocked();
            Check(!blocked.IsWalkableCell(0,0) && !blocked.IsWalkablePoint(500,500),"blocked cell is not walkable");
            Check(blocked.IsWalkablePoint(1500,1500),"unblocked cell stays walkable");
        });
        Run("height-bilinear-interpolation-and-clamping",()=>{
            var m=Slope();
            Check(m.HeightMm(0,0)==0,"node height at origin");
            Check(m.HeightMm(1000,0)==1000,"node height one cell over");
            Check(m.HeightMm(500,0)==500,"half-cell interpolates linearly");
            Check(m.HeightMm(2000,0)==2000,"far node height");
            Check(m.HeightMm(-100,-100)==0,"negative coordinates clamp to the near edge");
            Check(m.HeightMm(99999,99999)==2000,"far coordinates clamp to the last node");
        });
        Run("canstand-radius-bounds-and-movement-obstacle",()=>{
            var m=Slope();
            Check(m.CanStand(1000,1000,450),"centre with radius inside the map");
            Check(!m.CanStand(100,100,450),"radius larger than the margin is rejected");
            Check(!m.CanStand(1000,1000,3000),"radius larger than the map is rejected");
            Check(m.CanStand(0,0,0),"zero radius at the corner is standable");
            var blocked=Map();
            Check(!blocked.CanStand(1000,1000,0),"a movement obstacle blocks the point");
            Check(!blocked.CanStand(1000,1000,50),"a movement obstacle blocks a radius");
            Check(blocked.CanStand(200,200,50),"a clear point stays standable");
        });
        Run("sight-is-blocked-by-a-sight-obstacle",()=>{
            var m=SightWall();
            Check(!m.HasSight(0,0,4000,0),"a sight obstacle blocks the line");
            Check(m.HasSight(0,0,0,4000),"a perpendicular line stays clear");
            Check(m.HasSight(1000,0,1500,0),"a short clear line stays visible");
        });
        Run("poi-find-tags-and-containment",()=>{
            var m=SightWall();
            var gate=m.FindPoi("gate");
            Check(gate!=null && gate.Kind==ForgedPoiKind.Waypoint,"find POI by id");
            Check(m.FindPoi("missing")==null,"unknown POI id returns null");
            Check(m.FindPois(ForgedPoiKind.NpcHome).Count==1 && m.FindPois(ForgedPoiKind.Shrine).Count==0,"find POIs by kind");
            Check(gate.HasTag("north") && gate.HasTag("STONE") && !gate.HasTag("south"),"tags are trimmed and case-insensitive");
            Check(!gate.HasTag("") && !gate.HasTag(null),"empty tag is never matched");
            Check(gate.Contains(2000,0) && gate.Contains(2040,0) && !gate.Contains(2100,0),"containment radius");
        });
        Run("serialization-roundtrip-and-corruption-rejection",()=>{
            var m=SightWall();
            byte[] bytes=m.ToBytes();
            var back=ForgedWorldMap.FromBytes(bytes);
            Check(back.ContentHash==m.ContentHash,"content hash survives a round-trip");
            Check(back.ToBytes().SequenceEqual(bytes),"byte-identical re-serialization");
            Check(back.FindPoi("gate")!=null && back.CanStand(0,0,0),"restored map answers queries");
            var corrupt=(byte[])bytes.Clone();corrupt[bytes.Length/2]^=0xFF;
            Check(Throws(()=>ForgedWorldMap.FromBytes(corrupt)),"a flipped byte is rejected");
            Check(Throws(()=>ForgedWorldMap.FromBytes(bytes.Take(bytes.Length-1).ToArray())),"truncated data is rejected");
            Check(Throws(()=>ForgedWorldMap.FromBytes(new byte[]{1,2,3})),"too-short data is rejected");
            var badMagic=(byte[])bytes.Clone();badMagic[0]^=0xFF;
            Check(Throws(()=>ForgedWorldMap.FromBytes(badMagic)),"bad magic is rejected");
        });
        Run("fnv1a64-is-deterministic-and-sensitivity-to-input",()=>{
            Check(ForgedWorldMap.Fnv1a64(Array.Empty<byte>())==14695981039346656037UL,"empty input is the FNV offset basis");
            var a=new byte[]{1,2,3,4};var b=new byte[]{1,2,3,4};
            Check(ForgedWorldMap.Fnv1a64(a)==ForgedWorldMap.Fnv1a64(b),"identical input hashes identically");
            Check(ForgedWorldMap.Fnv1a64(a)!=ForgedWorldMap.Fnv1a64(new byte[]{1,2,3,5}),"a changed byte changes the hash");
            Check(ForgedWorldMap.Fnv1a64(a,2)!=ForgedWorldMap.Fnv1a64(a),"a shorter prefix hashes differently");
        });
        Run("constructor-rejects-invalid-dimensions",()=>{
            Check(Throws(()=>new ForgedWorldMap(0,0,0,3,3,new int[9],new bool[4],null,null)),"non-positive spacing is rejected");
            Check(Throws(()=>new ForgedWorldMap(0,0,1000,1,3,new int[3],new bool[2],null,null)),"fewer than 2x2 nodes is rejected");
            Check(Throws(()=>new ForgedWorldMap(0,0,1000,3,3,new int[8],new bool[4],null,null)),"height node count mismatch is rejected");
            Check(Throws(()=>new ForgedWorldMap(0,0,1000,3,3,new int[9],new bool[3],null,null)),"walkable cell count mismatch is rejected");
        });
        Console.WriteLine("RESULT: "+(scenarios-failures)+"/"+scenarios+" real world-map scenarios passed");
        return failures==0?0:1;
    }
    static bool Throws(Action action)
    {
        try { action();return false; } catch(FormatException) { return true; } catch(ArgumentException) { return true; }
    }
}
