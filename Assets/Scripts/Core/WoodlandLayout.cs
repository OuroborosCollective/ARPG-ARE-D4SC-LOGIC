using System;
using System.Collections.Generic;

namespace AreArpg.Core
{
    /// <summary>Revision 1: fixed story entrances, connected seeded paths and clearings. Millimetres.</summary>
    public sealed class WoodlandLayout
    {
        public const int CellMm = 4000, HalfWidth = 10, HalfLength = 13;
        private readonly bool[,] _floor = new bool[21,27];
        private readonly List<(int x,int z)> _clearings = new List<(int,int)>();
        private readonly MonsterState[] _spawns;
        private readonly (long x,long z)[] _chests = new (long,long)[4];
        public ulong Seed { get; }
        public int ChestMask { get; }
        public int MonsterCount => _spawns.Length;
        public WoodlandLayout(ulong seed)
        {
            Seed=seed; var rng=new DeterministicRng(seed ^ 0xF04E57UL);
            Clearing(0,-12); Clearing(0,12); Clearing(-9,0);
            for(int i=0;i<8;i++) Clearing((int)(rng.Next64()%13)-6,-9+i*3);
            Join(0,3,rng);
            for(int i=3;i<10;i++) Join(i,i+1,rng);
            Join(10,1,rng); Join(2,6,rng); Join(4,7,rng); Join(5,8,rng);
            _spawns=new MonsterState[16];
            for(int i=0;i<_spawns.Length;i++)
            {
                var c=_clearings[3+i/2]; int kind=rng.Next64()%3==0?1:0;
                long x=c.x*CellMm+(i%2==0?-1200:1200), z=c.z*CellMm;
                int hp=kind==0?380:640;
                _spawns[i]=new MonsterState { Id=200+i,Kind=kind,Xmm=x,Zmm=z,PreviousXmm=x,PreviousZmm=z,Health=hp,MaxHealth=hp };
            }
            int mask=0;
            for(int i=0;i<4;i++)
            {
                var c=_clearings[4+i*2]; _chests[i]=(c.x*CellMm,c.z*CellMm+1800);
                if(rng.Next64()%4!=0) mask|=1<<i;
            }
            ChestMask=mask;
        }
        private void Paint(int x,int z,int radius)
        { for(int a=x-radius;a<=x+radius;a++) for(int b=z-radius;b<=z+radius;b++) if(a>=-10&&a<=10&&b>=-13&&b<=13) _floor[a+10,b+13]=true; }
        private void Clearing(int x,int z) { _clearings.Add((x,z)); Paint(x,z,1); }
        private void Join(int a,int b,DeterministicRng rng)
        {
            var start=_clearings[a]; var end=_clearings[b]; int x=start.x,z=start.z;
            bool horizontal=rng.Next64()%2==0;
            while(x!=end.x||z!=end.z) { Paint(x,z,1); if((horizontal&&x!=end.x)||z==end.z) x+=Math.Sign(end.x-x); else z+=Math.Sign(end.z-z); }
            Paint(x,z,1);
        }
        public bool IsFloor(int x,int z) => x>=-10&&x<=10&&z>=-13&&z<=13&&_floor[x+10,z+13];
        private bool Inside(long x,long z) => x>=-42000&&x<42000&&z>=-54000&&z<54000&&IsFloor((int)((x+42000)/CellMm)-10,(int)((z+54000)/CellMm)-13);
        public bool CanStand(long x,long z)
        {
            if(x< -41000||x>41000||z< -53000||z>53000) return false;
            const long r=ArenaSimulation.PlayerRadiusMm;
            return Inside(x-r,z-r)&&Inside(x+r,z-r)&&Inside(x-r,z+r)&&Inside(x+r,z+r);
        }
        public bool HasSight(long ax,long az,long bx,long bz)
        {
            if(!CanStand(ax,az)||!CanStand(bx,bz)) return false;
            long steps=Math.Max(Math.Abs(bx-ax),Math.Abs(bz-az))/250+1;
            for(long i=0;i<=steps;i++) if(!CanStand(ax+(bx-ax)*i/steps,az+(bz-az)*i/steps)) return false;
            return true;
        }
        /// <summary>Continuous bilinear hills shared by all visual projections; flat at authored doorways.</summary>
        public long HeightMm(long x,long z)
        {
            long gx=(long)Math.Floor(x/16000.0),gz=(long)Math.Floor(z/16000.0),fx=x-gx*16000,fz=z-gz*16000;
            long a=HeightNode(gx,gz)*(16000-fx)+HeightNode(gx+1,gz)*fx;
            long b=HeightNode(gx,gz+1)*(16000-fx)+HeightNode(gx+1,gz+1)*fx;
            long h=(a*(16000-fz)+b*fz)/256000000;
            long nearest=Math.Min(Math.Max(Math.Abs(x),Math.Abs(z+48000)),Math.Min(Math.Max(Math.Abs(x),Math.Abs(z-48000)),Math.Max(Math.Abs(x+36000),Math.Abs(z))));
            return h*Math.Min(12000,Math.Max(0,nearest-4000))/12000;
        }
        private long HeightNode(long x,long z) => (long)(new DeterministicRng(Seed ^ unchecked((ulong)x*0x9E3779B97F4A7C15UL) ^ unchecked((ulong)z*0xD1B54A32D192ED03UL)).Next64()%3001)-1500;
        public bool HasChest(int i) => i>=0&&i<4&&(ChestMask&(1<<i))!=0;
        public void Chest(int i,out long x,out long z) { x=_chests[i].x;z=_chests[i].z; }
        public MonsterState Monster(int i)
        { var m=_spawns[i]; return new MonsterState {Id=m.Id,Kind=m.Kind,Xmm=m.Xmm,Zmm=m.Zmm,PreviousXmm=m.Xmm,PreviousZmm=m.Zmm,Health=m.Health,MaxHealth=m.MaxHealth}; }
    }
}
