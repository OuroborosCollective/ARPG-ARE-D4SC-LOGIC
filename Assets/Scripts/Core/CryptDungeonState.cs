using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace AreArpg.Core
{
    public sealed class CryptDungeonFloor
    {
        public readonly CryptDungeonLayout Layout;
        public readonly MonsterState[] Monsters;
        public readonly List<LootState> Loot = new List<LootState>();
        public int OpenChests { get; internal set; }
        internal CryptDungeonFloor(int depth, ulong seed)
        { Layout=new CryptDungeonLayout(seed,depth); Monsters = Enumerable.Range(0,Layout.MonsterCount).Select(Layout.Monster).ToArray(); }
        public bool Cleared { get { return Monsters.All(m => m.IsDead); } }
    }

    /// <summary>Three persistent floors. Items retain the shipped catalog receipt; chests drop ordinary upgrade fragments.</summary>
    public sealed class CryptDungeonState
    {
        public bool Active { get; internal set; }
        public int Depth { get; internal set; } = 1;
        public int UnlockedDepth { get; internal set; } = 1;
        public ulong Tick { get; internal set; }
        public ulong Seed { get; private set; }
        public BigInteger Run { get; private set; } = BigInteger.One;
        public bool NeedsNewRun { get; internal set; }
        private CryptDungeonFloor[] _floors;
        public CryptDungeonState(ulong seed=0) { Seed=seed; BuildFloors(); }
        private void BuildFloors() { _floors=Enumerable.Range(1,3).Select(d=>new CryptDungeonFloor(d,Seed)).ToArray(); }
        internal void Begin(ulong seed) { Active=true; Seed=seed; BuildFloors(); }
        internal void NextRun()
        {
            Seed=new DeterministicRng(Seed).Next64(); Run++; Depth=UnlockedDepth=1; Tick=0; NeedsNewRun=false; BuildFloors();
        }
        public CryptDungeonFloor Floor(int depth) { return _floors[depth-1]; }
        public CryptDungeonFloor Current { get { return Floor(Depth); } }

        internal string Token()
        {
            var floors = new List<string>();
            foreach (var floor in _floors)
            {
                string monsters = string.Join("/", floor.Monsters.Select(m => Numbers(m.Xmm,m.Zmm,m.Health,m.AttackCooldownTicks,m.LootGranted?1:0)));
                string loot = string.Join("/", floor.Loot.Select(l => Numbers(l.Id,l.Xmm,l.Zmm,l.WeaponBonus) + "," + (l.Item == null ? "-" : l.Item.Token())));
                floors.Add(floor.OpenChests.ToString(CultureInfo.InvariantCulture)+"~"+monsters+"~"+loot);
            }
            return "2;"+Depth+";"+UnlockedDepth+";"+Tick.ToString(CultureInfo.InvariantCulture)+";"+Seed.ToString(CultureInfo.InvariantCulture)+";"+Run.ToString(CultureInfo.InvariantCulture)+";"+(NeedsNewRun?"1":"0")+";"+string.Join("^",floors);
        }
        private static string Numbers(params long[] values)
        { return string.Join(",", values.Select(n => n.ToString(CultureInfo.InvariantCulture))); }
        private static bool Number(string text, long min, long max, out long value)
        { return long.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out value) && value>=min && value<=max && value.ToString(CultureInfo.InvariantCulture)==text; }
        private static bool Unsigned(string text, out ulong value)
        { return ulong.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out value) && value.ToString(CultureInfo.InvariantCulture)==text; }

        internal static bool TryRead(string token, int act, out CryptDungeonState result)
        {
            result=null; var p=token.Split(';');
            if(p.Length!=8 || p[0]!="2" || !Number(p[1],1,3,out var depth) || !Number(p[2],depth,3,out var unlocked) ||
                !Unsigned(p[3],out var tick) || !Unsigned(p[4],out var seed) ||
                !BigInteger.TryParse(p[5],NumberStyles.None,CultureInfo.InvariantCulture,out var run) || run<1 || run.ToString(CultureInfo.InvariantCulture)!=p[5] ||
                (p[6]!="0" && p[6]!="1") || ((act==6 || act==7) && p[6]!="0") || (act==7 && depth!=3) || (act==6 && depth==3)) return false;
            var r=new CryptDungeonState(seed) { Active=true,Depth=(int)depth,UnlockedDepth=(int)unlocked,Tick=tick,Run=run,NeedsNewRun=p[6]=="1" };
            var floors=p[7].Split('^'); if(floors.Length!=3) return false;
            for(int d=1;d<=3;d++)
            {
                var f=floors[d-1].Split('~'); var floor=r.Floor(d);
                if(f.Length!=3 || !Number(f[0],0,3,out var chests) || (chests & ~floor.Layout.ChestMask)!=0) return false;
                floor.OpenChests=(int)chests;
                var ms=f[1].Split('/'); if(ms.Length!=floor.Monsters.Length) return false;
                for(int i=0;i<floor.Monsters.Length;i++)
                {
                    var m=floor.Monsters[i]; var v=ms[i].Split(',');
                    if(v.Length!=5 || !Number(v[0],-25000,25000,out var x) || !Number(v[1],-25000,25000,out var z) ||
                        !floor.Layout.CanStand(x,z) || !Number(v[2],0,m.MaxHealth,out var hp) ||
                        !Number(v[3],0,17,out var cd) || !Number(v[4],0,1,out var granted) || (hp==0)!=(granted==1)) return false;
                    if(d>unlocked && (x!=m.Xmm || z!=m.Zmm || hp!=m.MaxHealth || cd!=0)) return false;
                    m.Xmm=m.PreviousXmm=x; m.Zmm=m.PreviousZmm=z; m.Health=(int)hp; m.AttackCooldownTicks=(int)cd; m.LootGranted=granted==1;
                }
                if(d>unlocked && (chests!=0 || f[2].Length!=0)) return false;
                if(d<unlocked && !floor.Cleared) return false;
                var ids=new HashSet<int>();
                foreach(string drop in f[2].Length==0 ? new string[0] : f[2].Split('/'))
                {
                    var v=drop.Split(',');
                    if(v.Length<5 || !Number(v[0],1,1000,out var id) || !ids.Add((int)id) ||
                        !Number(v[1],-25000,25000,out var x) || !Number(v[2],-25000,25000,out var z) || !floor.Layout.CanStand(x,z) ||
                        !Number(v[3],1,34,out var bonus)) return false;
                    var m=floor.Monsters.FirstOrDefault(e=>e.Id==id); LootItem item=null;
                    if(m!=null || (d==3 && id==42))
                    {
                        string receipt=string.Join(",",v.Skip(4));
                        int kind=m==null?2:m.Kind;
                        if((m!=null && !m.IsDead) || bonus!=(kind==0?8:kind==1?17:34) || !LootItem.TryRead(receipt,out item)) return false;
                        var context=receipt.Split(',');
                        if(context[1]!=seed.ToString("X16",CultureInfo.InvariantCulture) || context[2]!="1" || context[4]!=id.ToString(CultureInfo.InvariantCulture) || context[5]!=kind.ToString(CultureInfo.InvariantCulture)) return false;
                    }
                    else
                    {
                        int chest=(int)id-(900+d*2);
                        if(chest<0 || chest>1 || (chests & (1<<chest))==0 || bonus!=12 || v.Length!=5 || v[4]!="-") return false;
                        floor.Layout.Chest(chest,out var cx,out var cz); if(x!=cx || z!=cz) return false;
                    }
                    floor.Loot.Add(new LootState {Id=(int)id,Xmm=x,Zmm=z,WeaponBonus=(int)bonus,Item=item});
                }
            }
            result=r; return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        public CryptDungeonState Dungeon { get; private set; } = new CryptDungeonState();
    }
}
