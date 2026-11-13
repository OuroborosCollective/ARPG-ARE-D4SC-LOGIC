using System;
using System.Collections.Generic;

namespace AreArpg.Core
{
    /// <summary>Seeded rooms and orthogonal corridors, revision 1. Core and rendering share integer geometry.</summary>
    public sealed class CryptDungeonLayout
    {
        public const int CellMm=4000, MinCell=-6, MaxCell=6;
        private readonly bool[,] _cells=new bool[13,13];
        private readonly List<(int x,int z)> _rooms=new List<(int,int)>();
        private readonly MonsterState[] _spawns;
        private readonly (long x,long z)[] _chests=new (long,long)[2];
        public int Depth { get; }
        public int ChestMask { get; }
        public int MonsterCount { get { return _spawns.Length; } }
        public int RoomCount { get { return _rooms.Count; } }
        public void Room(int index,out long x,out long z) { x=_rooms[index].x*CellMm; z=_rooms[index].z*CellMm; }
        public CryptDungeonLayout(ulong seed,int depth)
        {
            if(depth<1 || depth>3) throw new ArgumentOutOfRangeException(nameof(depth));
            Depth=depth;
            var rng=new DeterministicRng(seed ^ ((ulong)depth*0x9E3779B97F4A7C15UL));
            AddRoom(0,-5,1,1); AddRoom(Roll(rng,3)-1,-1,1,1);
            AddRoom(-4,Roll(rng,3)-2,1,1); AddRoom(4,Roll(rng,3),1,1);
            AddRoom(0,4,2+Roll(rng,2),1);
            if(Roll(rng,2)==1) AddRoom(-4,4,1,1);
            if(Roll(rng,2)==1) AddRoom(4,-4,1,1);
            Join(0,1,rng); Join(1,2,rng); Join(1,3,rng); Join(1,4,rng);
            Join(2,4,rng);
            for(int i=5;i<_rooms.Count;i++) { Join(i,1,rng); Join(i,4,rng); }
            _cells[6,12]=true;
            _spawns=new MonsterState[_rooms.Count-1];
            for(int i=0;i<_spawns.Length;i++)
            {
                var room=_rooms[i+1]; int kind=Roll(rng,3)==0?1:0;
                long x=room.x*CellMm+(Roll(rng,3)-1)*1000, z=room.z*CellMm+(Roll(rng,3)-1)*1000;
                int hp=kind==0?320+depth*40:480+depth*50;
                _spawns[i]=new MonsterState {Id=100+depth*10+i,Kind=kind,Xmm=x,Zmm=z,PreviousXmm=x,PreviousZmm=z,Health=hp,MaxHealth=hp};
            }
            int mask=0;
            for(int i=0;i<2;i++)
            {
                var room=_rooms[i+2]; _chests[i]=(room.x*CellMm+1000,room.z*CellMm+1000);
                if(Roll(rng,4)!=0) mask|=1<<i;
            }
            ChestMask=mask;
        }
        private static int Roll(DeterministicRng rng,int count) { return (int)(rng.Next64()%(ulong)count); }
        private void AddRoom(int x,int z,int hx,int hz)
        {
            _rooms.Add((x,z));
            for(int a=x-hx;a<=x+hx;a++) for(int b=z-hz;b<=z+hz;b++) _cells[a+6,b+6]=true;
        }
        private void Join(int a,int b,DeterministicRng rng)
        {
            var from=_rooms[a]; var to=_rooms[b]; bool horizontalFirst=Roll(rng,2)==0;
            int x=from.x,z=from.z;
            while(x!=to.x || z!=to.z)
            {
                _cells[x+6,z+6]=true;
                if((horizontalFirst && x!=to.x) || z==to.z) x+=Math.Sign(to.x-x); else z+=Math.Sign(to.z-z);
            }
            _cells[x+6,z+6]=true;
        }
        public bool IsFloor(int x,int z) { return x>=-6 && x<=6 && z>=-6 && z<=6 && _cells[x+6,z+6]; }
        private bool Inside(long x,long z)
        { return x>=-26000 && x<26000 && z>=-26000 && z<26000 && IsFloor((int)((x+26000)/CellMm)-6,(int)((z+26000)/CellMm)-6); }
        public bool CanStand(long x,long z)
        {
            if(x < -25000 || x > 25000 || z < -25000 || z > 25000) return false;
            long r=ArenaSimulation.PlayerRadiusMm;
            return Inside(x-r,z-r) && Inside(x+r,z-r) && Inside(x-r,z+r) && Inside(x+r,z+r);
        }
        public bool HasSight(long ax,long az,long bx,long bz)
        {
            if(!Inside(ax,az) || !Inside(bx,bz)) return false;
            long steps=Math.Max(Math.Abs(bx-ax),Math.Abs(bz-az))/250+1;
            // Reserve the same wall clearance as movement, including diagonal joins at a solid corner.
            for(long i=0;i<=steps;i++) if(!CanStand(ax+(bx-ax)*i/steps,az+(bz-az)*i/steps)) return false;
            return true;
        }
        public bool HasChest(int index) { return index>=0 && index<2 && (ChestMask&(1<<index))!=0; }
        public void Chest(int index,out long x,out long z) { x=_chests[index].x; z=_chests[index].z; }
        public MonsterState Monster(int index)
        {
            var m=_spawns[index];
            return new MonsterState {Id=m.Id,Kind=m.Kind,Xmm=m.Xmm,Zmm=m.Zmm,PreviousXmm=m.Xmm,PreviousZmm=m.Zmm,Health=m.Health,MaxHealth=m.MaxHealth};
        }
    }
}
