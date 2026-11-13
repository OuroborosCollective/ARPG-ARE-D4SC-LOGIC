using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace AreArpg.Core
{
    public enum CubChoice { Undecided=0, Spared=1, Skinned=2 }
    /// <summary>Character-owned run checkpoint and permanent story facts; no Unity state is authoritative.</summary>
    public sealed class WoodlandState
    {
        public bool Active { get; internal set; }
        public ulong Seed { get; private set; }
        public BigInteger Run { get; private set; }=BigInteger.One;
        public ulong Tick { get; internal set; }
        public bool NeedsNewRun { get; internal set; }
        public bool HasRun { get; private set; }
        public bool ReachedAschenzoll { get; internal set; }
        public bool CubsInspected { get; internal set; }
        public CubChoice Choice { get; internal set; }
        public bool CubsReported { get; internal set; }
        public bool ProtectionOrganized { get; internal set; }
        public bool ArmorCrafted { get; internal set; }
        public int Pelts => Choice==CubChoice.Skinned&&!ArmorCrafted?3:0;
        public bool WarBearEligibility => Choice==CubChoice.Spared&&CubsReported&&ProtectionOrganized;
        public WoodlandLayout Layout { get; private set; }
        public MonsterState[] Monsters { get; private set; }
        public readonly List<LootState> Loot=new List<LootState>();
        public int OpenChests { get; internal set; }
        internal int SavedAct, SavedHealth, SavedResource;
        internal long SavedX,SavedZ;
        internal readonly int[] SavedCooldowns=new int[4];
        internal ulong Rng0,Rng1;
        public WoodlandState(ulong seed=0) { Seed=seed; Build(); }
        private void Build() { Layout=new WoodlandLayout(Seed); Monsters=Enumerable.Range(0,Layout.MonsterCount).Select(Layout.Monster).ToArray(); Loot.Clear(); OpenChests=0; }
        internal void Accept(ulong seed) { Active=true;Seed=seed;Build(); }
        internal void Enter()
        {
            if(NeedsNewRun) { Seed=new DeterministicRng(Seed).Next64(); Run++;Tick=0;Build(); }
            HasRun=true;NeedsNewRun=false;
        }
        public void RestartPlayer() { SavedAct=0; }
        private static string Numbers(params object[] values) => string.Join(",",values.Select(v=>Convert.ToString(v,CultureInfo.InvariantCulture)));
        internal string Token()
        {
            return string.Join(";",new[] {"1",Seed.ToString(CultureInfo.InvariantCulture),Run.ToString(CultureInfo.InvariantCulture),Tick.ToString(CultureInfo.InvariantCulture),NeedsNewRun?"1":"0",HasRun?"1":"0",ReachedAschenzoll?"1":"0",((int)Choice).ToString(CultureInfo.InvariantCulture),CubsInspected?"1":"0",CubsReported?"1":"0",ProtectionOrganized?"1":"0",ArmorCrafted?"1":"0",
                Numbers(SavedAct,SavedX,SavedZ,SavedHealth,SavedResource,SavedCooldowns[0],SavedCooldowns[1],SavedCooldowns[2],SavedCooldowns[3],Rng0,Rng1),
                string.Join("/",Monsters.Select(m=>Numbers(m.Xmm,m.Zmm,m.Health,m.AttackCooldownTicks,m.LootGranted?1:0))),
                string.Join("/",Loot.Select(l=>Numbers(l.Id,l.Xmm,l.Zmm,l.WeaponBonus)+","+(l.Item==null?"-":l.Item.Token()))),OpenChests.ToString(CultureInfo.InvariantCulture)});
        }
        private static bool N(string s,long min,long max,out long v) => long.TryParse(s,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out v)&&v>=min&&v<=max&&v.ToString(CultureInfo.InvariantCulture)==s;
        private static bool U(string s,out ulong v) => ulong.TryParse(s,NumberStyles.None,CultureInfo.InvariantCulture,out v)&&v.ToString(CultureInfo.InvariantCulture)==s;
        internal static bool TryRead(string token,int act,BigInteger kills,IEnumerable<LootItem> collected,IEnumerable<LootState> dungeonLoot,out WoodlandState state)
        {
            state=null; var f=token.Split(';'); if(f.Length!=16||f[0]!="1"||!U(f[1],out var seed)||!U(f[3],out var tick)||
                !BigInteger.TryParse(f[2],NumberStyles.None,CultureInfo.InvariantCulture,out var run)||run<1||run.ToString(CultureInfo.InvariantCulture)!=f[2]) return false;
            for(int i=4;i<=11;i++) if(!N(f[i],0,i==7?2:1,out _)) return false;
            var w=new WoodlandState(seed) { Active=true,Run=run,Tick=tick,NeedsNewRun=f[4]=="1",HasRun=f[5]=="1",ReachedAschenzoll=f[6]=="1",Choice=(CubChoice)int.Parse(f[7],CultureInfo.InvariantCulture),CubsInspected=f[8]=="1",CubsReported=f[9]=="1",ProtectionOrganized=f[10]=="1",ArmorCrafted=f[11]=="1" };
            if((act>=8&&(!w.HasRun||(act!=10&&w.NeedsNewRun)))||(!w.HasRun&&(w.NeedsNewRun||run!=1||tick!=0||w.ReachedAschenzoll||w.CubsInspected))||
                (w.Choice!=CubChoice.Undecided&&!w.CubsInspected)||(w.CubsReported&&w.Choice!=CubChoice.Spared)||(w.ProtectionOrganized&&!w.CubsReported)||(w.ArmorCrafted&&w.Choice!=CubChoice.Skinned)||(act==10&&!w.ReachedAschenzoll)) return false;
            var c=f[12].Split(','); if(c.Length!=11) return false; var v=new long[9];
            for(int i=0;i<9;i++) if(!N(c[i],i==1||i==2?-53000:0,i==1||i==2?53000:i==0?10:i==3?int.MaxValue:i==4?100:300,out v[i])) return false;
            if(!U(c[9],out w.Rng0)||!U(c[10],out w.Rng1)||(v[0]!=0&&(v[0]!=act||act<8||(w.Rng0|w.Rng1)==0||!ArenaSimulation.CanStandInWoodland(act,w.Layout,v[1],v[2])))) return false;
            w.SavedAct=(int)v[0];w.SavedX=v[1];w.SavedZ=v[2];w.SavedHealth=(int)v[3];w.SavedResource=(int)v[4];for(int i=0;i<4;i++)w.SavedCooldowns[i]=(int)v[5+i];
            if(!N(f[15],0,15,out var open)||(open&~w.Layout.ChestMask)!=0) return false;w.OpenChests=(int)open;
            var ms=f[13].Split('/'); if(ms.Length!=w.Monsters.Length) return false;
            for(int i=0;i<ms.Length;i++)
            {
                var m=w.Monsters[i];var n=ms[i].Split(',');if(n.Length!=5||!N(n[0],-41000,41000,out var x)||!N(n[1],-53000,53000,out var z)||!w.Layout.CanStand(x,z)||!N(n[2],0,m.MaxHealth,out var hp)||!N(n[3],0,17,out var cd)||(n[4]!=(hp==0?"1":"0"))) return false;
                m.Xmm=m.PreviousXmm=x;m.Zmm=m.PreviousZmm=z;m.Health=(int)hp;m.AttackCooldownTicks=(int)cd;m.LootGranted=hp==0;
            }
            var ids=new HashSet<int>();var ordinals=new HashSet<BigInteger>(collected.Select(i=>i.KillOrdinal).Concat(dungeonLoot.Where(l=>l.Item!=null).Select(l=>l.Item.KillOrdinal)));
            if(f[14].Length>0)foreach(var drop in f[14].Split('/'))
            {
                var n=drop.Split(','); if(n.Length<5||!N(n[0],200,303,out var id)||!ids.Add((int)id)||!N(n[1],-41000,41000,out var x)||!N(n[2],-53000,53000,out var z)||!w.Layout.CanStand(x,z)||!N(n[3],0,34,out var bonus))return false;
                var m=w.Monsters.FirstOrDefault(e=>e.Id==id); LootItem item=null;
                if(m!=null)
                {
                    if(!m.IsDead||!LootItem.TryRead(string.Join(",",n.Skip(4)),out item)||item.KillOrdinal>kills||!ordinals.Add(item.KillOrdinal)||bonus!=(m.Kind==1?17:8))return false;
                    var context=item.Token().Split(',');
                    if(context[1]!=seed.ToString("X16",CultureInfo.InvariantCulture)||context[2]!="1"||context[4]!=id.ToString(CultureInfo.InvariantCulture)||context[5]!=m.Kind.ToString(CultureInfo.InvariantCulture)||!U(context[3],out var killedAt)||killedAt>tick)return false;
                }
                else
                {
                    int chest=(int)id-300;if(chest<0||chest>=4||!w.Layout.HasChest(chest)||(w.OpenChests&(1<<chest))==0||n.Length!=5||n[4]!="-"||bonus!=12)return false;
                    w.Layout.Chest(chest,out var cx,out var cz);if(x!=cx||z!=cz)return false;
                }
                w.Loot.Add(new LootState {Id=(int)id,Xmm=x,Zmm=z,WeaponBonus=(int)bonus,Item=item});
            }
            if(!w.HasRun&&(w.OpenChests!=0||w.Loot.Count!=0||w.Monsters.Where((m,i)=>m.Health!=m.MaxHealth||m.Xmm!=w.Layout.Monster(i).Xmm||m.Zmm!=w.Layout.Monster(i).Zmm||m.AttackCooldownTicks!=0).Any()))return false;
            state=w;return true;
        }
    }
    public sealed partial class CharacterProgression
    { public WoodlandState Woodland { get; private set; }=new WoodlandState(); }
}
