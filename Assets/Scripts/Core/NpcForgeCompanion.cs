using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public enum CompanionStatus { Unavailable, Following, Waiting, Dead }
    /// <summary>After recruitment, owns the hunter's live state. Road retains the historical encounter outcome.</summary>
    public sealed class CompanionState
    {
        public CompanionStatus Status { get; internal set; }
        public bool HasJoined => Status!=CompanionStatus.Unavailable;
        public int Health { get; internal set; }
        public const int MaximumHealth=700;
        public int Hunger { get; internal set; }
        public int Fatigue { get; internal set; }
        public int Area { get; internal set; }
        public ulong AreaSeed { get; internal set; }
        public long Xmm { get; internal set; }
        public long Zmm { get; internal set; }
        public long PreviousXmm { get; internal set; }
        public long PreviousZmm { get; internal set; }
        public int AttackCooldown { get; internal set; }
        public int AttackFlash { get; internal set; }
        public int TargetId { get; internal set; }=-1;
        public BigInteger TotalExperience { get; private set; }
        private readonly BigInteger[] _practice=new BigInteger[4];
        public BigInteger Level => CharacterProgression.LevelForExperience(TotalExperience);
        public BigInteger PracticeExperience(int slot) => _practice[slot];
        public BigInteger PracticeLevel(int slot) => CharacterProgression.LevelForExperience(_practice[slot]);
        public BigInteger WeaponOrdinal { get; internal set; }
        public BigInteger ShieldOrdinal { get; internal set; }
        public ulong LastPracticeTick { get; private set; }
        internal bool Practice(ulong tick,int slot,int effectiveAmount)
        {
            if(Status!=CompanionStatus.Following||Health<=0||effectiveAmount<=0||tick<=LastPracticeTick)return false;
            var gain=PracticeRules.ExperienceFromEffectiveAmount(effectiveAmount);_practice[slot]+=gain;TotalExperience+=gain;LastPracticeTick=tick;return true;
        }
        internal string Token()
        {
            object[] values={1,(int)Status,Health,Hunger,Fatigue,Area,AreaSeed,Xmm,Zmm,PreviousXmm,PreviousZmm,AttackCooldown,AttackFlash,TargetId,TotalExperience,_practice[0],_practice[1],_practice[2],_practice[3],WeaponOrdinal,ShieldOrdinal,LastPracticeTick};
            return string.Join(",",values.Select(x=>Convert.ToString(x,CultureInfo.InvariantCulture)));
        }
        internal static bool TryRead(string token,CharacterProgression p,out CompanionState state)
        {
            state=null;var f=token.Split(',');if(f.Length!=22||f[0]!="1"||p.Social.Forge==null||!p.Road.Rescued)return false;
            var n=new BigInteger[22];for(int i=0;i<n.Length;i++)if(!BigInteger.TryParse(f[i],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out n[i])||n[i].ToString(CultureInfo.InvariantCulture)!=f[i])return false;
            if(n[1]<1||n[1]>3||n[2]<0||n[2]>MaximumHealth||(n[1]==3)!=(n[2]==0)||n[3]<0||n[3]>1000||n[4]<0||n[4]>1000||n[5]<2||n[5]>10||n[6]<0||n[6]>ulong.MaxValue)return false;
            for(int i=7;i<=10;i++)if(n[i]<-60000||n[i]>60000)return false;
            if(n[11]<0||n[11]>100||n[12]<0||n[12]>3||n[13]<-1||n[13]>int.MaxValue)return false;
            for(int i=14;i<=21;i++)if(n[i]<0)return false;
            if(n[14]!=n[15]+n[16]+n[17]+n[18]||n[21]>p.Social.Forge.ProcessedTicks)return false;
            var c=new CompanionState{Status=(CompanionStatus)(int)n[1],Health=(int)n[2],Hunger=(int)n[3],Fatigue=(int)n[4],Area=(int)n[5],AreaSeed=(ulong)n[6],Xmm=(long)n[7],Zmm=(long)n[8],PreviousXmm=(long)n[9],PreviousZmm=(long)n[10],AttackCooldown=(int)n[11],AttackFlash=(int)n[12],TargetId=(int)n[13],TotalExperience=n[14],WeaponOrdinal=n[19],ShieldOrdinal=n[20],LastPracticeTick=(ulong)n[21]};
            for(int i=0;i<4;i++)c._practice[i]=n[15+i];
            foreach(var ordinal in new[]{c.WeaponOrdinal,c.ShieldOrdinal})if(ordinal>0&&(!p.Items.Any(x=>x.KillOrdinal==ordinal)||ordinal==p.Equipment.Weapon?.Ordinal||ordinal==p.Equipment.Shield?.Ordinal))return false;
            var weapon=c.WeaponOrdinal>0?p.Equipment.ForItem(p.Items.FirstOrDefault(x=>x.KillOrdinal==c.WeaponOrdinal)):null;
            var shield=c.ShieldOrdinal>0?p.Equipment.ForItem(p.Items.FirstOrDefault(x=>x.KillOrdinal==c.ShieldOrdinal)):null;
            if(c.WeaponOrdinal>0&&(weapon==null||weapon.Definition.Family==WeaponFamily.Shield)||c.ShieldOrdinal>0&&(shield==null||shield.Definition.Family!=WeaponFamily.Shield||weapon==null||weapon.Definition.TwoHanded))return false;
            if((c.Status==CompanionStatus.Dead)!=p.Social.Forge.Archive.Events.Any(e=>e.Kind==MemoryKind.Death&&e.Target=="hunter"&&e.Context=="companion.death"&&e.Essential))return false;
            if(p.Social.Forge.Archive.Get("hunter")?.Controller.ArchetypeId!="companion")return false;
            state=c;return true;
        }
    }
    public sealed partial class CharacterProgression
    {
        public CompanionState Companion { get; private set; }=new CompanionState();
        private string Payload()
        {
            string body=ForgePayload();if(!Companion.HasJoined)return body;
            string prior=body+"|"+HashText(body).ToString("X16",CultureInfo.InvariantCulture);
            return "AC18|"+Convert.ToBase64String(Encoding.UTF8.GetBytes(prior))+"|"+Companion.Token();
        }
        private static bool TryLoadCompanion(string saved,out CharacterProgression progress)
        {
            progress=null;var f=saved.Split('|');if(f.Length!=4||f[3]!=HashText(string.Join("|",f,0,3)).ToString("X16",CultureInfo.InvariantCulture))return false;
            try
            {
                byte[] bytes=Convert.FromBase64String(f[1]);if(Convert.ToBase64String(bytes)!=f[1])return false;
                string inner=new UTF8Encoding(false,true).GetString(bytes);
                if(!inner.StartsWith("AC17|",StringComparison.Ordinal)||!TryLoad(inner,out var p)||!CompanionState.TryRead(f[2],p,out var c))return false;
                p.Companion=c;progress=p;return true;
            }
            catch(Exception e) when(e is FormatException||e is ArgumentException||e is OverflowException){return false;}
        }
    }
    public sealed partial class ArenaSimulation
    {
        private ulong CompanionAreaSeed => InForest?Progression.Woodland.Seed:InDungeon?Progression.Dungeon.Seed+(ulong)Progression.Dungeon.Depth:0;
        public bool CompanionPresent => Progression.Companion.HasJoined&&Progression.Companion.Area==Progression.EntryAct&&Progression.Companion.AreaSeed==CompanionAreaSeed;
        private void BindCompanion()
        {
            var forge=Progression.Social.Forge;if(forge==null)return;
            if(Progression.EntryAct==3||Progression.Companion.HasJoined)forge.Ensure("hunter","companion");
            var c=Progression.Companion;
            if(c.Status==CompanionStatus.Following&&(c.Area!=Progression.EntryAct||c.AreaSeed!=CompanionAreaSeed))
            {
                c.Area=Progression.EntryAct;c.AreaSeed=CompanionAreaSeed;c.Xmm=c.PreviousXmm=PlayerXmm;c.Zmm=c.PreviousZmm=PlayerZmm;
                foreach(var offset in new[]{(-1200L,0L),(1200L,0L),(0L,-1200L),(0L,1200L)})
                    if(CanStand(PlayerXmm+offset.Item1,PlayerZmm+offset.Item2)){c.Xmm=c.PreviousXmm=PlayerXmm+offset.Item1;c.Zmm=c.PreviousZmm=PlayerZmm+offset.Item2;break;}
            }
        }
        public SocialResult RecruitHunter()
        {
            var c=Progression.Companion;var r=new SocialResult();
            if(GameOver||Progression.Social.Forge==null||c.Status==CompanionStatus.Dead||c.Status==CompanionStatus.Following)return r;
            long x=c.HasJoined?c.Xmm:HunterEncounter.HunterX,z=c.HasJoined?c.Zmm:HunterEncounter.HunterZ;
            if(c.HasJoined?!CompanionPresent:Progression.EntryAct!=3||!Progression.Road.Rescued)return r;
            if(DistanceSquared(PlayerXmm,PlayerZmm,x,z)>FixedMath.Square(2400)||!CombatSight(PlayerXmm,PlayerZmm,x,z))return r;
            if(!c.HasJoined&&!Progression.Road.FavorClaimed&&!InteractHunter(false).Accepted)return r;
            Progression.Social.Forge.Ensure("hunter","companion");
            if(!c.HasJoined){c.Health=Progression.Road.HunterHealth;c.Xmm=c.PreviousXmm=x;c.Zmm=c.PreviousZmm=z;c.Area=Progression.EntryAct;c.AreaSeed=CompanionAreaSeed;}
            c.Status=CompanionStatus.Following;r.Accepted=true;r.SpeakerId=3;r.Text="Jäger: Du hast mich gerettet. Ich komme mit dir. Meine Erfahrung werde ich mir selbst verdienen.";return r;
        }
        public SocialResult DismissCompanion()
        {
            var c=Progression.Companion;var r=new SocialResult();
            if(GameOver||!CompanionPresent||c.Status!=CompanionStatus.Following||DistanceSquared(PlayerXmm,PlayerZmm,c.Xmm,c.Zmm)>FixedMath.Square(3500))return r;
            if(!(Progression.EntryAct==2||Progression.EntryAct==3||Progression.EntryAct==4||Progression.EntryAct==10)||Monsters.Any(e=>!e.IsDead&&DistanceSquared(c.Xmm,c.Zmm,e.Xmm,e.Zmm)<FixedMath.Square(14000)))
            {r.Text="Bringe den Jäger zuerst an einen sicheren Ort im Dorf, an der Straße, der Mühle oder der Handelsstation.";return r;}
            c.Status=CompanionStatus.Waiting;c.TargetId=-1;r.Accepted=true;r.SpeakerId=3;r.Text="Jäger: Ich warte hier auf dich.";return r;
        }
        private void MoveCompanion(long x,long z,long speed)
        {
            var c=Progression.Companion;long dx=x-c.Xmm,dz=z-c.Zmm,d=FixedMath.IntegerSqrt(dx*dx+dz*dz);if(d==0)return;
            long step=Math.Min(d,speed),nx=c.Xmm+dx*step/d,nz=c.Zmm+dz*step/d;
            if(CanStand(nx,nz)){c.Xmm=nx;c.Zmm=nz;}else if(CanStand(nx,c.Zmm))c.Xmm=nx;else if(CanStand(c.Xmm,nz))c.Zmm=nz;
        }
        private void StepCompanion()
        {
            var c=Progression.Companion;if(!CompanionPresent||c.Status!=CompanionStatus.Following||c.Health<=0||GameOver)return;
            c.PreviousXmm=c.Xmm;c.PreviousZmm=c.Zmm;if(c.AttackCooldown>0)c.AttackCooldown--;if(c.AttackFlash>0)c.AttackFlash--;
            var forge=Progression.Social.Forge;var mind=forge.Archive.Get("hunter");
            if(forge.ProcessedTicks%20==0)c.Hunger=Math.Min(1000,c.Hunger+1);
            MonsterState target=null;long best=FixedMath.Square(14000);
            foreach(var enemy in Monsters.OrderBy(e=>e.Id))
            {
                if(enemy.IsDead||enemy.Kind>=4)continue;
                long d=DistanceSquared(c.Xmm,c.Zmm,enemy.Xmm,enemy.Zmm);
                if(d<best&&CombatSight(c.Xmm,c.Zmm,enemy.Xmm,enemy.Zmm)){target=enemy;best=d;}
            }
            c.TargetId=target?.Id??-1;
            long ally=DistanceSquared(c.Xmm,c.Zmm,PlayerXmm,PlayerZmm);
            var actions=target==null?ActionSet.Of(ForgeAction.Wait,ForgeAction.Follow,ForgeAction.Rest):ActionSet.Of(ForgeAction.Follow,ForgeAction.Attack,ForgeAction.Flee);
            var frame=new ObservationFrame(actions,new[]{new Observation(Feature.Danger,target!=null?10000:0),new Observation(Feature.Hostility,target!=null?10000:0),new Observation(Feature.Injury,(CompanionState.MaximumHealth-c.Health)*10000/CompanionState.MaximumHealth),new Observation(Feature.AllyDistance,(int)Math.Min(10000,FixedMath.IntegerSqrt(ally))),new Observation(Feature.Fatigue,c.Fatigue*10),new Observation(Feature.Trust,FixedPoint.Clamp(mind.RelationshipTo("player").Trust))});
            bool changed=mind.DecisionState.LastObservations==null||mind.DecisionState.LastObservations[Feature.Danger]!=frame[Feature.Danger];
            var decision=UtilityEvaluator.Decide(mind.Controller,frame,mind.DecisionState,forge.ProcessedTicks,changed);
            if(decision.Action==ForgeAction.Attack&&target!=null)
            {
                if(best>FixedMath.Square(CompanionWeaponDefinition.RangeMm))MoveCompanion(target.Xmm,target.Zmm,260);
                else if(c.AttackCooldown==0)
                {
                    int power=65+CompanionWeaponDefinition.BasePower;
                    int damage=Math.Min(target.Health,power*(10000+PracticeRules.DamageBonusBp(c.PracticeLevel(0))+CompanionBonus(WeaponAffix.Power))/10000);target.Health-=damage;
                    c.AttackCooldown=Math.Max(1,(CompanionWeaponDefinition.StrikeCooldown*(10000-CompanionBonus(WeaponAffix.Haste))+9999)/10000);c.AttackFlash=3;
                    c.Health=Math.Min(CompanionState.MaximumHealth,c.Health+(int)((long)damage*CompanionBonus(WeaponAffix.Siphon)/10000));
                    c.Practice(forge.ProcessedTicks,0,damage);ResolveMonsterDefeat(target,new SkillResult());
                }
            }
            else if(decision.Action==ForgeAction.Flee&&target!=null)MoveCompanion(c.Xmm+(c.Xmm==target.Xmm?1000:c.Xmm-target.Xmm),c.Zmm+c.Zmm-target.Zmm,300);
            else if(decision.Action==ForgeAction.Rest){c.Fatigue=Math.Max(0,c.Fatigue-3);c.Health=Math.Min(CompanionState.MaximumHealth,c.Health+1);}
            else if(ally>FixedMath.Square(1800)){MoveCompanion(PlayerXmm,PlayerZmm,450);if(forge.ProcessedTicks%10==0)c.Fatigue=Math.Min(1000,c.Fatigue+1);}
        }
    }
}
