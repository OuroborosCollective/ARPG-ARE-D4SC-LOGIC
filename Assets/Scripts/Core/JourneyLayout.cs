using System;
using System.Collections.Generic;
using System.Linq;
using AshenCrypt.WorldForge.Core;

namespace AreArpg.Core
{
    /// <summary>Explicit immutable authoring catalog. Saved hashes never select a replacement map.</summary>
    public sealed class JourneyMapCatalog
    {
        private readonly ForgedWorldMap[] _maps;
        public ForgedWorldMap Primary { get; }
        public JourneyMapCatalog(params ForgedWorldMap[] maps)
        {
            if(maps==null||maps.Length==0)throw new ArgumentException("Journey needs authored maps");
            _maps=(ForgedWorldMap[])maps.Clone();Primary=_maps[0];
            var hashes=new HashSet<ulong>();
            foreach(var map in _maps)
            {
                if(map==null||map.NodesX!=129||map.NodesZ!=129||map.SpacingMm!=1000||map.OriginXmm!=-64000||map.OriginZmm!=-64000||!hashes.Add(map.ContentHash))
                    throw new ArgumentException("Unsupported or duplicate journey map");
                foreach(string id in new[]{"arrival","onward","cave","treasure","rest"})
                {
                    var poi=map.FindPoi(id);
                    if(poi==null||!map.CanStand(poi.Xmm,poi.Zmm,ArenaSimulation.PlayerRadiusMm))throw new ArgumentException("Missing or blocked journey anchor: "+id);
                }
            }
        }
        public ForgedWorldMap Resolve(ulong hash)=>Array.Find(_maps,m=>m.ContentHash==hash);
    }

    /// <summary>Integer floor cells shared by collision, navigation tests and passive presentation.</summary>
    public abstract class JourneyGeometry
    {
        protected readonly bool[,] Cells;
        public int Size { get; }
        public int CellMm { get; }
        public long OriginMm { get; }
        public long ReturnX { get; protected set; }
        public long ReturnZ { get; protected set; }
        public long ForwardX { get; protected set; }
        public long ForwardZ { get; protected set; }
        public long SpawnX=>ReturnX;
        public long SpawnZ=>ReturnZ;
        protected readonly List<(long x,long z)> Chests=new List<(long,long)>();
        protected readonly List<MonsterState> Spawns=new List<MonsterState>();
        public int ChestCount=>Chests.Count;
        public int MonsterCount=>Spawns.Count;
        protected JourneyGeometry(int size,int cellMm,long origin){Size=size;CellMm=cellMm;OriginMm=origin;Cells=new bool[size,size];}
        public bool IsFloor(int x,int z)=>x>=0&&z>=0&&x<Size&&z<Size&&Cells[x,z];
        public long Centre(int cell)=>OriginMm+(long)cell*CellMm;
        public virtual bool CanStand(long x,long z)
        {
            const long r=ArenaSimulation.PlayerRadiusMm;
            if(x-r<OriginMm-CellMm/2||z-r<OriginMm-CellMm/2||x+r>=OriginMm+(Size-1L)*CellMm+CellMm/2||z+r>=OriginMm+(Size-1L)*CellMm+CellMm/2)return false;
            int ax=(int)((x-r-OriginMm+CellMm/2)/CellMm),bx=(int)((x+r-OriginMm+CellMm/2)/CellMm);
            int az=(int)((z-r-OriginMm+CellMm/2)/CellMm),bz=(int)((z+r-OriginMm+CellMm/2)/CellMm);
            for(int ix=ax;ix<=bx;ix++)for(int iz=az;iz<=bz;iz++)if(!IsFloor(ix,iz))
            {
                long dx=Math.Max(0,Math.Abs(x-Centre(ix))-CellMm/2),dz=Math.Max(0,Math.Abs(z-Centre(iz))-CellMm/2);
                if(dx*dx+dz*dz<r*r)return false;
            }
            return true;
        }
        public virtual bool HasSight(long ax,long az,long bx,long bz)
        {
            if(Math.Abs(ax)>70000||Math.Abs(az)>70000||Math.Abs(bx)>70000||Math.Abs(bz)>70000)return false;
            long steps=Math.Max(Math.Abs(ax-bx),Math.Abs(az-bz))/250+1;
            for(long i=0;i<=steps;i++)if(!CanStand(ax+(bx-ax)*i/steps,az+(bz-az)*i/steps))return false;
            return true;
        }
        public virtual long HeightMm(long x,long z)=>0;
        public void Chest(int index,out long x,out long z){x=Chests[index].x;z=Chests[index].z;}
        public MonsterState Monster(int index)
        {
            var m=Spawns[index];return new MonsterState{Id=m.Id,Kind=m.Kind,Xmm=m.Xmm,Zmm=m.Zmm,PreviousXmm=m.Xmm,PreviousZmm=m.Zmm,MaxHealth=m.MaxHealth,Health=m.MaxHealth};
        }
        protected void AddMonster(int id,int kind,long x,long z,int health)=>Spawns.Add(new MonsterState{Id=id,Kind=kind,Xmm=x,Zmm=z,MaxHealth=health});
        public ulong LayoutHash
        {
            get{var s=new System.Text.StringBuilder();for(int z=0;z<Size;z++)for(int x=0;x<Size;x++)s.Append(Cells[x,z]?'1':'0');return LootCatalog.HashText(s.ToString());}
        }
    }

    /// <summary>Runtime routes are regenerated over an actual exported World Forge surface, separately from authoring.</summary>
    public sealed class JourneyRouteLayout:JourneyGeometry
    {
        public ForgedWorldMap Map { get; }
        public long CaveX { get; }
        public long CaveZ { get; }
        public JourneyRouteLayout(ForgedWorldMap map,ulong seed):base(65,2000,-64000)
        {
            Map=map??throw new ArgumentNullException(nameof(map));
            var entry=map.FindPoi("arrival");var exit=map.FindPoi("onward");var cave=map.FindPoi("cave");var treasure=map.FindPoi("treasure");var rest=map.FindPoi("rest");
            ReturnX=entry.Xmm;ReturnZ=entry.Zmm;ForwardX=exit.Xmm;ForwardZ=exit.Zmm;CaveX=cave.Xmm;CaveZ=cave.Zmm;
            var valid=new bool[Size,Size];
            for(int x=1;x<Size-1;x++)for(int z=1;z<Size-1;z++)valid[x,z]=map.CanStand(Centre(x),Centre(z),700);
            var rng=new DeterministicRng(seed^map.ContentHash^0x524F55544531UL);
            // Two distinct bends plus optional branches. Randomized BFS is bounded by the 65x65 graph.
            long southX=(rng.Next64()%2==0?-20000:20000),northX=(rng.Next64()%2==0?-16000:16000);
            Join(ReturnX,ReturnZ,southX,-32000,valid,rng);Join(southX,-32000,rest.Xmm,rest.Zmm,valid,rng);
            Join(rest.Xmm,rest.Zmm,northX,32000,valid,rng);Join(northX,32000,ForwardX,ForwardZ,valid,rng);
            Join(rest.Xmm,rest.Zmm,CaveX,CaveZ,valid,rng);Join(rest.Xmm,rest.Zmm,treasure.Xmm,treasure.Zmm,valid,rng);
            Chests.Add((treasure.Xmm,treasure.Zmm));
            var encounter=new DeterministicRng(seed^0x454E434F554E5445UL);
            AddMonster(1101,0,southX,-32000,420);
            AddMonster(1102,1,northX,32000,680);
            // Extra enemies occupy reachable route centres, never the safe arrival or rest clearing.
            var candidates=new List<(long x,long z)>();
            for(int z=1;z<Size-1;z++)for(int x=1;x<Size-1;x++)
                if(Cells[x,z]&&Math.Abs(Centre(z))>=20000&&Math.Abs(Centre(z))<=40000&&CanStand(Centre(x),Centre(z))&&
                    Spawns.All(m=>FixedMath.Square(m.Xmm-Centre(x))+FixedMath.Square(m.Zmm-Centre(z))>=FixedMath.Square(3000)))candidates.Add((Centre(x),Centre(z)));
            for(int i=0;i<2;i++)
            {
                int at=(int)(encounter.Next64()%(ulong)candidates.Count);var c=candidates[at];candidates.RemoveAt(at);
                AddMonster(1103+i,i,c.x,c.z,450+i*200);
                candidates.RemoveAll(p=>FixedMath.Square(p.x-c.x)+FixedMath.Square(p.z-c.z)<FixedMath.Square(3000));
            }
            if(Spawns.Any(m=>!CanStand(m.Xmm,m.Zmm)))throw new InvalidOperationException("Authored encounter anchor blocked");
        }
        private void Join(long ax,long az,long bx,long bz,bool[,] valid,DeterministicRng rng)
        {
            int sx=(int)((ax-OriginMm)/CellMm),sz=(int)((az-OriginMm)/CellMm),tx=(int)((bx-OriginMm)/CellMm),tz=(int)((bz-OriginMm)/CellMm);
            if(!valid[sx,sz]||!valid[tx,tz])throw new InvalidOperationException("Authored journey anchor lacks clearance");
            var previous=new int[Size*Size];Array.Fill(previous,-1);int start=sz*Size+sx,end=tz*Size+tx;
            var queue=new Queue<int>();queue.Enqueue(start);previous[start]=start;
            int[] dx={1,0,-1,0},dz={0,1,0,-1};
            while(queue.Count>0&&previous[end]<0)
            {
                int n=queue.Dequeue(),x=n%Size,z=n/Size,turn=(int)(rng.Next64()%4);
                for(int i=0;i<4;i++)
                {
                    int k=(turn+i)%4,nx=x+dx[k],nz=z+dz[k];
                    if(nx<1||nz<1||nx>=Size-1||nz>=Size-1||!valid[nx,nz]||previous[nz*Size+nx]>=0)continue;
                    bool clear=true;for(int t=1;t<8;t++)if(!Map.CanStand(Centre(x)+(long)dx[k]*250*t,Centre(z)+(long)dz[k]*250*t,700)){clear=false;break;}
                    if(!clear)continue;previous[nz*Size+nx]=n;queue.Enqueue(nz*Size+nx);
                }
            }
            if(previous[end]<0)throw new InvalidOperationException("Authored journey anchors are disconnected");
            for(int n=end;;n=previous[n])
            {
                int x=n%Size,z=n/Size;Cells[x,z]=true;
                for(int k=0;k<4;k++){int nx=x+dx[k],nz=z+dz[k];if(nx>=1&&nz>=1&&nx<Size-1&&nz<Size-1&&valid[nx,nz]&&Map.HasSight(Centre(x),Centre(z),Centre(nx),Centre(nz)))Cells[nx,nz]=true;}
                if(n==start)break;
            }
        }
        public override bool CanStand(long x,long z)=>base.CanStand(x,z)&&Map.CanStand(x,z,ArenaSimulation.PlayerRadiusMm);
        public override bool HasSight(long ax,long az,long bx,long bz)=>base.HasSight(ax,az,bx,bz)&&Map.HasSight(ax,az,bx,bz);
        public override long HeightMm(long x,long z)=>Map.HeightMm(x,z);
    }

    /// <summary>Independent optional cave generator; the legacy three-floor story crypt is unchanged.</summary>
    public sealed class JourneyCaveLayout:JourneyGeometry
    {
        public int Depth { get; }
        public int FloorCount { get; }
        public int RewardCount=>Depth+1;
        public int EnemyHealth=>380+Depth*140;
        public JourneyCaveLayout(ulong seed,int depth,int floorCount):base(13,4000,-24000)
        {
            if(floorCount<3||floorCount>6||depth<1||depth>floorCount)throw new ArgumentOutOfRangeException(nameof(floorCount));
            Depth=depth;FloorCount=floorCount;ReturnX=ForwardX=0;ReturnZ=-24000;ForwardZ=24000;
            var rng=new DeterministicRng(seed^((ulong)depth*0x9E3779B97F4A7C15UL)^0x4341564531UL);
            var rooms=new List<(int x,int z)>{(6,1),(3+(int)(rng.Next64()%7),4),(2,6+(int)(rng.Next64()%2)),(10,6+(int)(rng.Next64()%2)),(6,10)};
            foreach(var r in rooms)for(int x=r.x-1;x<=r.x+1;x++)for(int z=r.z-1;z<=r.z+1;z++)Cells[x,z]=true;
            for(int i=1;i<rooms.Count;i++)Join(rooms[i-1],rooms[i],rng);
            Join(rooms[1],rooms[4],rng);Cells[6,0]=Cells[6,12]=true;
            for(int i=1;i<rooms.Count;i++)AddMonster(1200+depth*10+i,i==rooms.Count-1?1:0,Centre(rooms[i].x),Centre(rooms[i].z),EnemyHealth+(i==rooms.Count-1?180:0));
            for(int i=0;i<depth-1;i++){var r=rooms[1+i%3];AddMonster(1205+depth*10+i,0,Centre(r.x)+(i<3?-1000:1000),Centre(r.z)+1000,EnemyHealth);}
            Chests.Add((Centre(rooms[2].x)+1000,Centre(rooms[2].z)));Chests.Add((Centre(rooms[3].x)+1000,Centre(rooms[3].z)));
        }
        private void Join((int x,int z)a,(int x,int z)b,DeterministicRng rng)
        {
            bool xFirst=rng.Next64()%2==0;
            while(a.x!=b.x||a.z!=b.z){Cells[a.x,a.z]=true;if((xFirst&&a.x!=b.x)||a.z==b.z)a.x+=Math.Sign(b.x-a.x);else a.z+=Math.Sign(b.z-a.z);}
            Cells[b.x,b.z]=true;
        }
    }
}
