using System;
using System.Globalization;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public sealed partial class ArenaSimulation
    {
        /// <summary>Existing ordinary-monster combat can target the nearer living party actor.</summary>
        private bool StepMonsterAgainstCompanion(MonsterState enemy)
        {
            var c=Progression.Companion;
            if(!CompanionPresent||c.Health<=0||c.Status==CompanionStatus.Dead||enemy.Kind>=3)return false;
            long distance2=DistanceSquared(enemy.Xmm,enemy.Zmm,c.Xmm,c.Zmm);
            if(distance2>=DistanceSquared(enemy.Xmm,enemy.Zmm,PlayerXmm,PlayerZmm)||distance2>FixedMath.Square(14000)||!CombatSight(enemy.Xmm,enemy.Zmm,c.Xmm,c.Zmm))return false;
            if(distance2>FixedMath.Square(1650))
            {
                long distance=Math.Max(1,FixedMath.IntegerSqrt(distance2)),speed=enemy.Kind>=2?190:MonsterSpeedMmPerTick;
                long nx=enemy.Xmm+(c.Xmm-enemy.Xmm)*speed/distance,nz=enemy.Zmm+(c.Zmm-enemy.Zmm)*speed/distance;
                if(CanStand(nx,nz)){enemy.Xmm=nx;enemy.Zmm=nz;}else if(CanStand(nx,enemy.Zmm))enemy.Xmm=nx;else if(CanStand(enemy.Xmm,nz))enemy.Zmm=nz;
            }
            else if(enemy.AttackCooldownTicks==0)
            {enemy.AttackCooldownTicks=enemy.Kind>=2?12:17;DamageCompanion(enemy,enemy.Kind>=2?105:enemy.Kind==1?60:40);}
            return true;
        }
        private void DamageCompanion(MonsterState source,int amount)
        {
            var c=Progression.Companion;if(!CompanionPresent||c.Health<=0||amount<=0)return;
            int applied=Math.Max(1,(int)((long)amount*(10000-Math.Min(3500,CompanionBonus(WeaponAffix.Ward)))/10000));
            c.Health=Math.Max(0,c.Health-applied);
            if(c.Health>0)return;
            c.Status=CompanionStatus.Dead;c.TargetId=-1;c.AttackFlash=0;
            var forge=Progression.Social.Forge;
            string actor="mob."+Progression.EntryAct.ToString(CultureInfo.InvariantCulture)+"."+CompanionAreaSeed.ToString(CultureInfo.InvariantCulture)+"."+source.Id.ToString(CultureInfo.InvariantCulture);
            var e=forge.Archive.Append(MemoryKind.Death,actor,"hunter",forge.ProcessedTicks,10000,true,"companion.death",new RelationshipChange(fear:5000));
            foreach(var npc in Npcs)
                if(npc.Memory.Health>0&&DistanceSquared(npc.Xmm,npc.Zmm,c.Xmm,c.Zmm)<=FixedMath.Square(8000)&&CombatSight(npc.Xmm,npc.Zmm,c.Xmm,c.Zmm))
                    forge.Archive.Learn(NpcForgeState.Identity(npc.Id),e.Id,KnowledgeOrigin.Witnessed,NpcForgeState.Identity(npc.Id),forge.ProcessedTicks);
        }
    }
}
