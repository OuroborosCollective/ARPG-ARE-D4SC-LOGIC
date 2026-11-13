using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public sealed partial class NpcForgeState
    {
        internal static string CreatureRole(int kind)=>kind==4||kind==5?"animal":kind>=0&&kind<=3?"monster":"fallback";
        internal static bool ValidIdentity(NpcMind mind)
        {
            string id=mind.Id,role=mind.Controller.ArchetypeId;
            if(id=="ilyan"||id=="mara"||id=="dorn"||id=="hunter"||id=="road.traveler")return true;
            if(id=="mill.patient.0"||id=="mill.patient.1"||id=="mill.patient.2")return role=="villager";
            if(id=="bear.cub.0"||id=="bear.cub.1"||id=="bear.cub.2")return role=="animal";
            var parts=id.Split('.');
            return parts.Length==4&&parts[0]=="mob"&&parts[1].Length==64&&parts[1].All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')&&
                int.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out int kind)&&kind.ToString(CultureInfo.InvariantCulture)==parts[2]&&
                int.TryParse(parts[3],NumberStyles.None,CultureInfo.InvariantCulture,out int number)&&number.ToString(CultureInfo.InvariantCulture)==parts[3]&&role==CreatureRole(kind);
        }
    }
    public sealed partial class ArenaSimulation
    {
        private string _forgePopulationKey;
        private string ForgeCreatureId(MonsterState enemy)
        {
            if(_forgePopulationKey==null)
            {
                string key=Progression.EntryAct.ToString(CultureInfo.InvariantCulture)+"/"+_runSeed.ToString("X16",CultureInfo.InvariantCulture)+"/"+CryptDepth.ToString(CultureInfo.InvariantCulture)+"/"+
                    (InForest?Progression.Woodland.Seed:InDungeon?Progression.Dungeon.Seed:0).ToString("X16",CultureInfo.InvariantCulture)+"/"+(InDungeon?Progression.Dungeon.Depth:0).ToString(CultureInfo.InvariantCulture);
                using(var sha=SHA256.Create())_forgePopulationKey=string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(key)).Select(b=>b.ToString("x2",CultureInfo.InvariantCulture)));
            }
            return "mob."+_forgePopulationKey+"."+enemy.Kind.ToString(CultureInfo.InvariantCulture)+"."+enemy.Id.ToString(CultureInfo.InvariantCulture);
        }
        private void EnsureCreatureMind(MonsterState enemy)
        {if(Progression.Social.Forge!=null&&enemy.Mind==null)enemy.Mind=Progression.Social.Forge.Ensure(ForgeCreatureId(enemy),NpcForgeState.CreatureRole(enemy.Kind));}
        private void BindForgePopulation()
        {
            foreach(var enemy in Monsters)EnsureCreatureMind(enemy);
            var forge=Progression.Social.Forge;
            if(Progression.EntryAct==4)for(int i=3-HungerShelterPatients;i<3;i++)forge.Ensure("mill.patient."+i.ToString(CultureInfo.InvariantCulture),"villager");
            if(Progression.EntryAct==9)for(int i=0;i<3;i++)forge.Ensure("bear.cub."+i.ToString(CultureInfo.InvariantCulture),"animal");
        }
        private void RememberCreatureAssault(MonsterState enemy,string attacker)
        {
            EnsureCreatureMind(enemy);var forge=Progression.Social.Forge;
            if(enemy.Mind==null||enemy.IsDead||enemy.Mind.Known.Any(k=>k.Event.Kind==MemoryKind.Assault&&k.Event.Actor==attacker))return;
            var e=forge.Archive.Append(MemoryKind.Assault,attacker,enemy.Mind.Id,forge.ProcessedTicks,8000,false,"combat.actual-hit",new RelationshipChange(hostility:10000,fear:1000));
            forge.Archive.Learn(enemy.Mind.Id,e.Id,KnowledgeOrigin.Self,enemy.Mind.Id,forge.ProcessedTicks);
        }
        private bool ForgeCreatureMayAttack(MonsterState enemy,long targetX,long targetZ)
        {
            EnsureCreatureMind(enemy);if(enemy.Mind==null)return true;
            int injury=enemy.MaxHealth<=0?0:FixedPoint.Clamp((int)((long)(enemy.MaxHealth-enemy.Health)*10000/enemy.MaxHealth));
            ulong legal=ActionSet.Of(ForgeAction.Wait,ForgeAction.Attack);
            if(enemy.Kind<=1&&injury>=7500)legal|=ActionSet.Of(ForgeAction.Flee);
            var relation=enemy.Mind.RelationshipTo("player");
            var frame=new ObservationFrame(legal,new[]{new Observation(Feature.Danger,10000),new Observation(Feature.Hostility,10000),new Observation(Feature.Injury,injury),new Observation(Feature.Fear,relation.Fear)});
            var action=UtilityEvaluator.Decide(enemy.Mind.Controller,frame,enemy.Mind.DecisionState,Progression.Social.Forge.ProcessedTicks).Action;
            if(action==ForgeAction.Flee)
            {
                long dx=enemy.Xmm-targetX,dz=enemy.Zmm-targetZ;if(dx==0&&dz==0)dx=1000;
                long distance=Math.Max(1,FixedMath.IntegerSqrt(dx*dx+dz*dz)),nx=enemy.Xmm+dx*MonsterSpeedMmPerTick/distance,nz=enemy.Zmm+dz*MonsterSpeedMmPerTick/distance;
                if(CanStand(nx,nz)){enemy.Xmm=nx;enemy.Zmm=nz;}else if(CanStand(nx,enemy.Zmm))enemy.Xmm=nx;else if(CanStand(enemy.Xmm,nz))enemy.Zmm=nz;
            }
            return action==ForgeAction.Attack;
        }
    }
}
