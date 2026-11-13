using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AshenCrypt.MemoryForge.Samples
{
    /// <summary>Isolated example host. Never used by Ashen Crypt. Owns its own sample world and supplies every tick.</summary>
    public sealed class DemoWorld
    {
        private static readonly NpcArchetypeControllerRegistry Registry=ForgeDefaults.CreateRegistry();
        public MemoryArchive Archive { get; private set; }
        public ulong Tick { get; private set; }
        public int PlayerX { get; private set; }=-2200;
        public int PlayerZ { get; private set; }=-4000;
        public int PlayerHealth { get; private set; }=1000;
        public int Provisions { get; private set; }=3;
        public int Coins { get; private set; }
        public int Purse { get; private set; }=12;
        public int NaraX { get; private set; }=-2200;
        public int NaraZ { get; private set; }=1500;
        public int Hunger { get; private set; }=800;
        public int Fatigue { get; private set; }=100;
        public int GuardX { get; private set; }=2200;
        public int GuardZ { get; private set; }=1500;
        private int _goalX=-2200,_goalZ=-4000,_guardGoalZ=1500,_attackCooldown;
        public bool NearNara=>Distance(PlayerX,PlayerZ,NaraX,NaraZ)<=1500L*1500&&HasSight(PlayerX,PlayerZ,NaraX,NaraZ);
        public bool GuardCanSeePlayer=>Distance(PlayerX,PlayerZ,GuardX,GuardZ)<=8000L*8000&&HasSight(PlayerX,PlayerZ,GuardX,GuardZ);
        public DemoWorld()
        {Archive=new MemoryArchive();Archive.Spawn("nara","traveler",Registry);Archive.Spawn("watch","guard",Registry);}
        public void Approach(){_goalX=-2200;_goalZ=500;}
        public void Leave(){_goalX=-2200;_goalZ=-4000;}
        public void MoveWitnessToCourtyard(){_guardGoalZ=-2200;}
        public bool GiveFood()
        {
            if(PlayerHealth<=0||!NearNara||Provisions==0||Hunger<400)return false;
            Provisions--;Hunger=Math.Max(0,Hunger-400);
            var e=Archive.Append(MemoryKind.Help,"player","nara",Tick,5000,false,"food",new RelationshipChange(trust:1200,gratitude:2000));
            Archive.Learn("nara",e.Id,KnowledgeOrigin.Self,"nara",Tick);return true;
        }
        public bool Rob()
        {
            if(PlayerHealth<=0||!NearNara||Purse==0)return false;
            Coins+=Purse;Purse=0;
            var e=Archive.Append(MemoryKind.Theft,"player","nara",Tick,8000,true,"market.purse",new RelationshipChange(trust:-2000,fear:1500,mistrust:2500,hostility:3000));
            Archive.Learn("nara",e.Id,KnowledgeOrigin.Self,"nara",Tick);
            if(GuardCanSeePlayer)Archive.Learn("watch",e.Id,KnowledgeOrigin.Witnessed,"watch",Tick,6000);
            return true;
        }
        public string Greet()
        {
            if(!NearNara)return "Come closer so Nara can hear you.";
            var mind=Archive.Get("nara");
            if(mind.RelationshipTo("player").Hostility>0)return "I remember the purse you took. Stay away.";
            return mind.Known.Any(k=>k.Event.Kind==MemoryKind.Help)?"You brought me food when I needed it. Thank you.":"We have not met before. I could use some food.";
        }
        public void Step()
        {
            Tick=checked(Tick+1);if(PlayerHealth<=0)return;
            Move(PlayerX,PlayerZ,_goalX,_goalZ,350,out var px,out var pz);PlayerX=px;PlayerZ=pz;
            if(_attackCooldown>0)_attackCooldown--;
            if(Tick%20==0)Hunger=Math.Min(1000,Hunger+1);
            var nara=Archive.Get("nara");var relation=nara.RelationshipTo("player");
            bool danger=relation.Hostility>0&&Distance(PlayerX,PlayerZ,NaraX,NaraZ)<8000L*8000&&HasSight(PlayerX,PlayerZ,NaraX,NaraZ);
            var legal=danger?ActionSet.Of(ForgeAction.Flee):ActionSet.Of(ForgeAction.Wait,ForgeAction.Rest,ForgeAction.Observe);
            if(!NearNara||relation.Trust<=0)legal&=~ActionSet.Of(ForgeAction.Observe);
            var observations=new ObservationFrame(legal,new[]{new Observation(Feature.Hunger,Hunger*10),new Observation(Feature.Fatigue,Fatigue*10),new Observation(Feature.Danger,danger?10000:0),new Observation(Feature.Trust,FixedPoint.Clamp(relation.Trust)),new Observation(Feature.Gratitude,relation.Gratitude),new Observation(Feature.Fear,relation.Fear)});
            var chosen=UtilityEvaluator.Decide(nara.Controller,observations,nara.DecisionState,Tick);
            if(chosen.Action==ForgeAction.Rest)Fatigue=Math.Max(0,Fatigue-3);
            if(chosen.Action==ForgeAction.Flee)
            {
                Move(NaraX,NaraZ,NaraX+(NaraX==PlayerX?-1000:NaraX-PlayerX),NaraZ+NaraZ-PlayerZ,200,out var nx,out var nz);NaraX=nx;NaraZ=nz;
            }
            var watch=Archive.Get("watch");bool crime=watch.Known.Any(k=>k.IsFact&&k.Event.Kind==MemoryKind.Theft&&k.Event.Actor=="player");
            bool alert=crime&&GuardCanSeePlayer;
            var guardInput=new ObservationFrame(alert?ActionSet.Of(ForgeAction.Attack):ActionSet.Of(ForgeAction.Wait,ForgeAction.Travel),new[]{new Observation(Feature.Danger,alert?10000:0),new Observation(Feature.Hostility,alert?10000:0)});
            var guardAction=UtilityEvaluator.Decide(watch.Controller,guardInput,watch.DecisionState,Tick).Action;
            if(guardAction==ForgeAction.Attack&&Distance(GuardX,GuardZ,PlayerX,PlayerZ)<1500L*1500)
            {if(_attackCooldown==0){PlayerHealth=Math.Max(0,PlayerHealth-40);_attackCooldown=15;}}
            else
            {
                Move(GuardX,GuardZ,alert?PlayerX:2200,alert?PlayerZ:_guardGoalZ,250,out var gx,out var gz);GuardX=gx;GuardZ=gz;
            }
        }
        private static long Distance(int ax,int az,int bx,int bz)=>(long)(ax-bx)*(ax-bx)+(long)(az-bz)*(az-bz);
        public static bool CanStand(int x,int z)=>x>=-7000&&x<=7000&&z>=-6000&&z<=6000&&!(x>=-350&&x<=350&&z>=-100&&z<=3300);
        public static bool HasSight(int ax,int az,int bx,int bz)
        {for(int i=1;i<64;i++)if(!CanStand(ax+(bx-ax)*i/64,az+(bz-az)*i/64))return false;return true;}
        private static void Move(int x,int z,int tx,int tz,int speed,out int nx,out int nz)
        {
            int dx=tx-x,dz=tz-z,distance=Math.Max(Math.Abs(dx),Math.Abs(dz));nx=x;nz=z;if(distance==0)return;
            int step=Math.Min(speed,distance),xx=x+dx*step/distance,zz=z+dz*step/distance;
            if(CanStand(xx,zz)){nx=xx;nz=zz;}else if(CanStand(xx,z))nx=xx;else if(CanStand(x,zz))nz=zz;
        }
        private static string Hash(string value)
        {using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b=>b.ToString("x2",CultureInfo.InvariantCulture)));}
        public string Save()
        {
            object[] data={Tick,PlayerX,PlayerZ,_goalX,_goalZ,PlayerHealth,Provisions,Coins,Purse,NaraX,NaraZ,Hunger,Fatigue,GuardX,GuardZ,_guardGoalZ,_attackCooldown};
            string body="MFD1|"+string.Join("|",data.Select(v=>Convert.ToString(v,CultureInfo.InvariantCulture)))+"|"+Archive.Save();return body+"|"+Hash(body);
        }
        public static bool TryLoad(string token,out DemoWorld world)
        {
            world=null;if(string.IsNullOrEmpty(token)||token.Length>1024*1024)return false;
            var f=token.Split('|');if(f.Length!=20||f[0]!="MFD1"||Hash(string.Join("|",f.Take(19)))!=f[19]||!ulong.TryParse(f[1],NumberStyles.None,CultureInfo.InvariantCulture,out var tick))return false;
            var v=new int[16];for(int i=0;i<v.Length;i++)if(!int.TryParse(f[i+2],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out v[i]))return false;
            if(!CanStand(v[0],v[1])||!CanStand(v[2],v[3])||v[4]<0||v[4]>1000||v[5]<0||v[5]>3||v[6]<0||v[7]<0||v[6]+v[7]!=12||!CanStand(v[8],v[9])||v[10]<0||v[10]>1000||v[11]<0||v[11]>1000||!CanStand(v[12],v[13])||v[14]!=1500&&v[14]!=-2200||v[15]<0||v[15]>15)return false;
            if(!MemoryArchive.TryLoad(f[18],Registry,out var archive)||archive.Minds.Count!=2||archive.Get("nara")?.Controller.ArchetypeId!="traveler"||archive.Get("watch")?.Controller.ArchetypeId!="guard"||archive.Events.Any(e=>e.Tick>tick)||archive.Acquisitions.Any(k=>k.LearnedTick>tick)||archive.Minds.Any(m=>m.DecisionState.LastTick>tick))return false;
            var candidate=new DemoWorld{Tick=tick,PlayerX=v[0],PlayerZ=v[1],_goalX=v[2],_goalZ=v[3],PlayerHealth=v[4],Provisions=v[5],Coins=v[6],Purse=v[7],NaraX=v[8],NaraZ=v[9],Hunger=v[10],Fatigue=v[11],GuardX=v[12],GuardZ=v[13],_guardGoalZ=v[14],_attackCooldown=v[15],Archive=archive};
            if(candidate.Save()!=token)return false;world=candidate;return true;
        }
    }
}
