using System;
using System.Collections.Generic;
using System.Linq;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public sealed partial class ArenaSimulation
    {
        private void RememberNpcAction(NpcAgent target,NpcAction action,bool wasAware,string context,int[] explicitWitnesses=null)
        {
            var forge=Progression.Social.Forge;if(forge==null)return;
            var witnesses=explicitWitnesses??(action==NpcAction.Attack||action==NpcAction.Rob?
                Npcs.Where(n=>n.Id!=target.Id&&n.Memory.Health>0&&DistanceSquared(n.Xmm,n.Zmm,PlayerXmm,PlayerZmm)<=FixedMath.Square(8000)&&CombatSight(n.Xmm,n.Zmm,PlayerXmm,PlayerZmm)).Select(n=>n.Id).ToArray():Array.Empty<int>());
            forge.Record(target.Id,action,wasAware||action==NpcAction.Help,witnesses,context);
        }
        private bool StepForgeNpc(NpcAgent npc,long distance,bool oldHostile)
        {
            if(npc.Mind==null)return false;
            var memory=npc.Memory;var relation=npc.Mind.RelationshipTo("player");
            bool hostile=oldHostile||relation.Hostility>=2000;
            bool visible=distance<FixedMath.Square(14000)&&CombatSight(npc.Xmm,npc.Zmm,PlayerXmm,PlayerZmm);
            bool danger=hostile&&visible;
            bool atFood=DistanceSquared(npc.Xmm,npc.Zmm,CampXmm,CampZmm)<=FixedMath.Square(500);
            bool food=Progression.Social.CampProvisions>0;
            var actions=new List<ForgeAction>{ForgeAction.Wait};
            if(danger)
            {
                actions.Add(ForgeAction.Flee);
                if(npc.Personality==NpcPersonality.Brave&&memory.Health>=NpcMemory.MaxHealth/4)actions.Add(ForgeAction.Attack);
            }
            else
            {
                actions.Add(ForgeAction.Rest);
                if(npc.Id==0)actions.Add(ForgeAction.Travel);
                if(memory.Hunger>=700&&food)actions.Add(atFood?ForgeAction.Eat:ForgeAction.SeekFood);
                if(visible&&distance<=FixedMath.Square(3500)&&!hostile&&(relation.Trust>0||memory.Disposition(npc.Personality)==NpcDisposition.Friendly))actions.Add(ForgeAction.Observe);
            }
            var frame=new ObservationFrame(ActionSet.Of(actions.ToArray()),new[]{
                new Observation(Feature.Hunger,memory.Hunger*10),new Observation(Feature.Fatigue,memory.Fatigue*10),
                new Observation(Feature.Injury,(NpcMemory.MaxHealth-memory.Health)*10000/NpcMemory.MaxHealth),new Observation(Feature.Danger,danger?10000:0),
                new Observation(Feature.Food,food?10000:0),new Observation(Feature.Trust,FixedPoint.Clamp(relation.Trust)),new Observation(Feature.Gratitude,relation.Gratitude),
                new Observation(Feature.Fear,relation.Fear),new Observation(Feature.Hostility,hostile?Math.Max(8000,relation.Hostility):relation.Hostility)});
            var state=npc.Mind.DecisionState;
            bool changed=state.LastObservations==null||state.LastObservations[Feature.Danger]!=frame[Feature.Danger]||state.LastObservations[Feature.Gratitude]!=frame[Feature.Gratitude];
            var decision=UtilityEvaluator.Decide(npc.Mind.Controller,frame,state,Progression.Social.Forge.ProcessedTicks,changed);
            switch(decision.Action)
            {
                case ForgeAction.Flee:
                    npc.Behavior=NpcBehavior.Flee;MoveNpc(npc,npc.Xmm+(npc.Xmm==PlayerXmm?1000:npc.Xmm-PlayerXmm),npc.Zmm+(npc.Zmm-PlayerZmm),300);break;
                case ForgeAction.Attack:
                    npc.Behavior=NpcBehavior.Attack;
                    if(distance>FixedMath.Square(1500))MoveNpc(npc,PlayerXmm,PlayerZmm,240);
                    else if(npc.AttackCooldownTicks==0){ReceiveDamage(40);npc.AttackCooldownTicks=15;}break;
                case ForgeAction.SeekFood:npc.Behavior=NpcBehavior.SeekFood;MoveNpc(npc,CampXmm,CampZmm,170);break;
                case ForgeAction.Eat:
                    npc.Behavior=Progression.Social.EatAtCamp(memory)?NpcBehavior.Eat:NpcBehavior.Wait;break;
                case ForgeAction.Rest:
                    npc.Behavior=NpcBehavior.Rest;memory.Fatigue=Math.Max(0,memory.Fatigue-3);memory.Health=Math.Min(NpcMemory.MaxHealth,memory.Health+1);break;
                case ForgeAction.Travel:
                    npc.Behavior=NpcBehavior.Travel;long targetZ=npc.PatrolIndex==0?-3000:-9000;
                    if(DistanceSquared(npc.Xmm,npc.Zmm,-2500,targetZ)<FixedMath.Square(300))npc.PatrolIndex=1-npc.PatrolIndex;
                    MoveNpc(npc,-2500,targetZ,170);if(CurrentTick%10==0)memory.Fatigue=Math.Min(1000,memory.Fatigue+1);break;
                case ForgeAction.Observe:npc.Behavior=NpcBehavior.Observe;break;
                default:npc.Behavior=NpcBehavior.Wait;break;
            }
            return true;
        }
    }
}
