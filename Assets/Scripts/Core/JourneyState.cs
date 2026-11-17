using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    public sealed class JourneyFloor
    {
        public JourneyGeometry Geometry { get; }
        public MonsterState[] Monsters { get; }
        public List<LootState> Loot { get; }=new List<LootState>();
        public int OpenChests { get; internal set; }
        public bool Visited { get; internal set; }
        internal long X,Z,PreviousX,PreviousZ,DirectionX,DirectionZ;
        internal int Health,Resource=100,Kills;
        internal ulong Tick,Rng0,Rng1;
        internal readonly int[] Cooldowns=new int[4];
        public bool Cleared=>Monsters.All(m=>m.IsDead);
        internal JourneyFloor(JourneyGeometry geometry){Geometry=geometry;Monsters=Enumerable.Range(0,geometry.MonsterCount).Select(geometry.Monster).ToArray();}
        internal string Token()
        {
            string head=JourneyNumbers.Join(Visited?1:0,OpenChests,X,Z,PreviousX,PreviousZ,DirectionX,DirectionZ,Health,Resource,Kills,Tick,Rng0,Rng1,Cooldowns[0],Cooldowns[1],Cooldowns[2],Cooldowns[3]);
            string monsters=string.Join("/",Monsters.Select(m=>JourneyNumbers.Join(m.Xmm,m.Zmm,m.PreviousXmm,m.PreviousZmm,m.Health,m.AttackCooldownTicks,m.LootGranted?1:0)));
            string loot=string.Join("/",Loot.Select(l=>JourneyNumbers.Join(l.Id,l.Xmm,l.Zmm,l.WeaponBonus)+","+(l.Item==null?"-":l.Item.Token())));
            return head+"~"+monsters+"~"+loot;
        }
        internal bool Read(string token,int maximumHealth,ulong seed,int depth,CharacterProgression p,HashSet<BigInteger> ordinals)
        {
            var f=token.Split('~');if(f.Length!=3)return false;
            var a=JourneyNumbers.Read(f[0],18);if(a==null)return false;
            if(a[0]<0||a[0]>1||a[1]<0||a[1]>=(1<<Geometry.ChestCount)||a[8]<0||a[8]>maximumHealth||a[9]<0||a[9]>100||a[10]<0||a[10]>Monsters.Length)return false;
            for(int i=2;i<8;i++)if(a[i]<-70000||a[i]>70000)return false;
            if(a[6]<-10000||a[6]>10000||a[7]<-10000||a[7]>10000)return false;
            for(int i=11;i<14;i++)if(a[i]<0||a[i]>ulong.MaxValue)return false;
            for(int i=14;i<18;i++)if(a[i]<0||a[i]>1000)return false;
            Visited=a[0]==1;OpenChests=(int)a[1];X=(long)a[2];Z=(long)a[3];PreviousX=(long)a[4];PreviousZ=(long)a[5];DirectionX=(long)a[6];DirectionZ=(long)a[7];Health=(int)a[8];Resource=(int)a[9];Kills=(int)a[10];Tick=(ulong)a[11];Rng0=(ulong)a[12];Rng1=(ulong)a[13];
            for(int i=0;i<4;i++)Cooldowns[i]=(int)a[14+i];
            if(Visited&&(!Geometry.CanStand(X,Z)||!Geometry.CanStand(PreviousX,PreviousZ)||(Rng0|Rng1)==0))return false;
            var ms=f[1].Split('/');if(ms.Length!=Monsters.Length)return false;
            for(int i=0;i<ms.Length;i++)
            {
                var n=JourneyNumbers.Read(ms[i],7);var m=Monsters[i];if(n==null)return false;
                for(int j=0;j<4;j++)if(n[j]<-70000||n[j]>70000)return false;
                if(!Geometry.CanStand((long)n[0],(long)n[1])||!Geometry.CanStand((long)n[2],(long)n[3])||n[4]<0||n[4]>m.MaxHealth||n[5]<0||n[5]>17||n[6]<0||n[6]>1||(n[4]==0)!=(n[6]==1))return false;
                if(!Visited&&(n[0]!=m.Xmm||n[1]!=m.Zmm||n[2]!=m.PreviousXmm||n[3]!=m.PreviousZmm||n[4]!=m.MaxHealth||n[5]!=0||n[6]!=0))return false;
                m.Xmm=(long)n[0];m.Zmm=(long)n[1];m.PreviousXmm=(long)n[2];m.PreviousZmm=(long)n[3];m.Health=(int)n[4];m.AttackCooldownTicks=(int)n[5];m.LootGranted=n[6]==1;
            }
            if(!Visited&&(OpenChests!=0||X!=0||Z!=0||PreviousX!=0||PreviousZ!=0||Health!=0||Resource!=100||Kills!=0||Tick!=0||Rng0!=0||Rng1!=0||DirectionX!=0||DirectionZ!=0||Cooldowns.Any(c=>c!=0)||f[2].Length!=0))return false;
            var ids=new HashSet<int>();
            foreach(string text in f[2].Length==0?Array.Empty<string>():f[2].Split('/'))
            {
                var v=text.Split(',');if(v.Length<5)return false;
                var n=JourneyNumbers.Read(string.Join(",",v.Take(4)),4);if(n==null||n[0]<1||n[0]>10000||!ids.Add((int)n[0])||n[1]<-70000||n[1]>70000||n[2]<-70000||n[2]>70000||n[3]<1||n[3]>1000||!Geometry.CanStand((long)n[1],(long)n[2]))return false;
                int id=(int)n[0];var m=Monsters.FirstOrDefault(e=>e.Id==id);LootItem item=null;
                if(m!=null)
                {
                    string receipt=string.Join(",",v.Skip(4));
                    if(!m.IsDead||n[3]!=(m.Kind==0?8:m.Kind==1?17:34)||!LootItem.TryRead(receipt,out item)||item.KillOrdinal>p.LootOrdinal||!ordinals.Add(item.KillOrdinal))return false;
                    var c=receipt.Split(',');if(c[1]!=seed.ToString("X16",CultureInfo.InvariantCulture)||c[2]!=Math.Max(1,depth).ToString(CultureInfo.InvariantCulture)||c[4]!=id.ToString(CultureInfo.InvariantCulture)||c[5]!=m.Kind.ToString(CultureInfo.InvariantCulture)||!ulong.TryParse(c[3],out var killTick)||killTick>Tick)return false;
                }
                else
                {
                    int chest=id-9000;
                    if(chest<0||chest>=Geometry.ChestCount||(OpenChests&(1<<chest))==0||v.Length!=5||v[4]!="-"||n[3]!=12*Math.Max(1,depth))return false;
                    Geometry.Chest(chest,out var x,out var z);if(n[1]!=x||n[2]!=z)return false;
                }
                Loot.Add(new LootState{Id=id,Xmm=(long)n[1],Zmm=(long)n[2],WeaponBonus=(int)n[3],Item=item});
            }
            return true;
        }
    }
    internal static class JourneyNumbers
    {
        internal static string Join(params object[] values)=>string.Join(",",values.Select(v=>Convert.ToString(v,CultureInfo.InvariantCulture)));
        internal static BigInteger[] Read(string token,int count)
        {
            var f=token.Split(',');if(f.Length!=count)return null;var n=new BigInteger[count];
            for(int i=0;i<count;i++)if(f[i].Length>4096||!BigInteger.TryParse(f[i],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out n[i])||n[i].ToString(CultureInfo.InvariantCulture)!=f[i])return null;
            return n;
        }
    }
    public sealed class JourneyState
    {
        public const int GeneratorRevision=1;
        public bool Active { get; private set; }
        public int Location { get; internal set; } // 0: retained world; 1: connection; 2: optional cave
        public int Depth { get; internal set; }=1;
        public int UnlockedDepth { get; internal set; }=1;
        public int FloorCount { get; private set; }=4;
        public ulong MapHash { get; private set; }
        public ulong RouteSeed { get; private set; }
        public ulong CaveSeed { get; private set; }
        public BigInteger RouteRun { get; private set; }=BigInteger.One;
        public BigInteger CaveRun { get; private set; }=BigInteger.One;
        public bool RouteNeedsNewRun { get; internal set; }
        public bool CaveNeedsNewRun { get; internal set; }
        public JourneyMapCatalog Maps { get; private set; }
        public JourneyFloor Route { get; private set; }
        private JourneyFloor[] _caves;
        public JourneyFloor CaveFloor(int depth)=>_caves[depth-1];
        public JourneyFloor Current=>Location==2?CaveFloor(Depth):Route;
        internal void Begin(JourneyMapCatalog maps,ulong seed)
        {
            if(Active)throw new InvalidOperationException("Journey already bound");
            Maps=maps??throw new ArgumentNullException(nameof(maps));MapHash=maps.Primary.ContentHash;RouteSeed=seed;CaveSeed=new DeterministicRng(seed^0x43415645UL).Next64();Active=true;Build();
        }
        private void Build()
        {
            var map=Maps?.Resolve(MapHash);if(map==null)throw new InvalidOperationException("Exact authored journey map is unavailable");
            Route=new JourneyFloor(new JourneyRouteLayout(map,RouteSeed));BuildCaves();
        }
        private void BuildCaves()=>_caves=Enumerable.Range(1,FloorCount).Select(d=>new JourneyFloor(new JourneyCaveLayout(CaveSeed,d,FloorCount))).ToArray();
        internal void EnterRoute()
        {
            if(RouteNeedsNewRun){RouteSeed=new DeterministicRng(RouteSeed).Next64();RouteRun++;Route=new JourneyFloor(new JourneyRouteLayout(Maps.Resolve(MapHash),RouteSeed));RouteNeedsNewRun=false;}
            Location=1;
        }
        internal void EnterCave()
        {
            if(CaveNeedsNewRun){CaveSeed=new DeterministicRng(CaveSeed).Next64();CaveRun++;BuildCaves();UnlockedDepth=1;CaveNeedsNewRun=false;}
            Depth=1;Location=2;
        }
        internal string Token()=>JourneyNumbers.Join(GeneratorRevision,Location,Depth,UnlockedDepth,FloorCount,MapHash,RouteSeed,CaveSeed,RouteRun,CaveRun,RouteNeedsNewRun?1:0,CaveNeedsNewRun?1:0)+";"+Route.Token()+";"+string.Join(";",_caves.Select(f=>f.Token()));
        internal static bool TryRead(string token,JourneyMapCatalog maps,CharacterProgression p,out JourneyState state)
        {
            state=null;var f=token.Split(';');if(f.Length<2)return false;var n=JourneyNumbers.Read(f[0],12);
            if(n==null||n[0]!=GeneratorRevision||n[1]<0||n[1]>2||n[2]<1||n[2]>n[3]||n[3]>n[4]||n[4]<3||n[4]>6||f.Length!=2+(int)n[4]||n[8]<1||n[9]<1||n[10]<0||n[10]>1||n[11]<0||n[11]>1)return false;
            for(int i=5;i<=7;i++)if(n[i]<0||n[i]>ulong.MaxValue)return false;
            if(maps?.Resolve((ulong)n[5])==null||!p.Woodland.Active||!p.Freight.Reported||(n[1]>0&&p.EntryAct!=10)||(n[1]>0&&n[10]!=0)||(n[1]==2&&n[11]!=0)||(n[1]!=2&&n[2]!=1))return false;
            var j=new JourneyState{Active=true,Maps=maps,Location=(int)n[1],Depth=(int)n[2],UnlockedDepth=(int)n[3],FloorCount=(int)n[4],MapHash=(ulong)n[5],RouteSeed=(ulong)n[6],CaveSeed=(ulong)n[7],RouteRun=n[8],CaveRun=n[9],RouteNeedsNewRun=n[10]==1,CaveNeedsNewRun=n[11]==1};j.Build();
            var ordinals=new HashSet<BigInteger>(p.Items.Select(i=>i.KillOrdinal));
            foreach(var loot in p.Woodland.Loot.Concat(Enumerable.Range(1,3).SelectMany(d=>p.Dungeon.Floor(d).Loot)))if(loot.Item!=null)ordinals.Add(loot.Item.KillOrdinal);
            if(!j.Route.Read(f[1],CombatRules.MaximumHealth(p),j.RouteSeed,0,p,ordinals))return false;
            for(int d=1;d<=j.FloorCount;d++)
            {
                var floor=j.CaveFloor(d);
                if(!floor.Read(f[d+1],CombatRules.MaximumHealth(p),j.CaveSeed,d,p,ordinals)||(d>j.UnlockedDepth&&floor.Visited)||(d<j.UnlockedDepth&&!floor.Cleared))return false;
            }
            if(j.Location>0&&!j.Current.Visited)return false;
            state=j;return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        public JourneyState Journey { get; private set; }=new JourneyState();
        private string Payload()
        {
            JourneyCapture?.Invoke();
            string previous=FreightPayload();if(!Journey.Active)return previous;
            previous+="|"+HashText(previous).ToString("X16",CultureInfo.InvariantCulture);
            return "AC20|"+EncodeJourney(previous)+"|"+EncodeJourney(Journey.Token())+"|"+EncodeJourney(Companion.HasJoined?Companion.Token():"-");
        }
        private static string EncodeJourney(string s)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        private static string DecodeJourney(string s){var bytes=Convert.FromBase64String(s);if(Convert.ToBase64String(bytes)!=s)throw new FormatException();return new UTF8Encoding(false,true).GetString(bytes);}
        public static bool TryLoad(string saved,JourneyMapCatalog maps,out CharacterProgression progress)
        {
            progress=null;if(saved!=null&&saved.StartsWith("AC21|",StringComparison.Ordinal))return TryLoadWeaponMastery(saved,maps,out progress);
            if(saved==null||!saved.StartsWith("AC20|",StringComparison.Ordinal))return TryLoad(saved,out progress);
            if(saved.Length>64*1024*1024)return false;
            var f=saved.Split('|');if(f.Length!=5||f[4]!=HashText(string.Join("|",f,0,4)).ToString("X16",CultureInfo.InvariantCulture))return false;
            try
            {
                string inner=DecodeJourney(f[1]);
                if(!inner.StartsWith("AC19|",StringComparison.Ordinal)||!TryLoad(inner,out var p)||p.Companion.HasJoined||!JourneyState.TryRead(DecodeJourney(f[2]),maps,p,out var j))return false;
                p.Journey=j;string c=DecodeJourney(f[3]);
                if(c!="-"){if(!CompanionState.TryRead(c,p,out var companion))return false;p.Companion=companion;}
                if(p.SaveString()!=saved)return false;
                progress=p;return true;
            }
            catch(Exception e)when(e is FormatException||e is ArgumentException||e is OverflowException||e is InvalidOperationException){return false;}
        }
    }
}
