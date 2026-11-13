using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenCrypt.MemoryForge
{
    public static class ForgeDefaults
    {
        private static UtilityRule Rule(ForgeAction action,int priority,params (Feature feature,int weight)[] values)
        {var weights=new int[(int)Feature.Count];foreach(var v in values)weights[(int)v.feature]=v.weight;return new UtilityRule(action,(int)action,priority,weights);}
        public static NpcArchetypeControllerRegistry CreateRegistry()
        {
            var registry=new NpcArchetypeControllerRegistry();
            foreach(string id in new[]{"fallback","villager","merchant","guard","healer","traveler","companion","monster","animal"})
            {
                bool safe=id=="fallback",fighter=id=="guard"||id=="companion"||id=="monster"||id=="animal",social=id!="monster"&&id!="animal"&&!safe;
                var actions=new List<ForgeAction>{ForgeAction.Wait};
                Capability caps=Capability.None;
                if(!safe){caps=Capability.Movement|Capability.Needs;actions.AddRange(new[]{ForgeAction.Travel,ForgeAction.Rest,ForgeAction.SeekFood,ForgeAction.Eat,ForgeAction.Flee});}
                if(social){caps|=Capability.Social;actions.Add(ForgeAction.Observe);}
                if(fighter){caps|=Capability.Combat;actions.Add(ForgeAction.Attack);}
                if(id=="companion"){caps|=Capability.Companion;actions.Add(ForgeAction.Follow);}
                if(id=="animal")caps|=Capability.Hearing|Capability.Smell;
                var archetype=new ArchetypeDefinition(id,caps,ActionSet.Of(actions.ToArray()),safe?0:(1UL<<(int)Feature.Count)-1);
                var rules=new List<UtilityRule>{Rule(ForgeAction.Wait,0)};
                if(!safe)rules.AddRange(new[]{
                    Rule(ForgeAction.Travel,1500,(Feature.Fatigue,-4000),(Feature.Hunger,-3000),(Feature.TravelCost,-500)),
                    Rule(ForgeAction.Rest,500,(Feature.Fatigue,12000),(Feature.Injury,3000),(Feature.Danger,-12000)),
                    Rule(ForgeAction.SeekFood,0,(Feature.Hunger,12000),(Feature.TravelCost,-1500)),
                    Rule(ForgeAction.Eat,100,(Feature.Hunger,15000),(Feature.Food,1000)),
                    Rule(ForgeAction.Flee,1000,(Feature.Danger,16000),(Feature.Injury,10000),(Feature.Fear,7000))});
                if(social)rules.Add(Rule(ForgeAction.Observe,700,(Feature.Trust,4000),(Feature.Gratitude,8000),(Feature.Quest,4000),(Feature.Danger,-10000)));
                if(fighter)rules.Add(Rule(ForgeAction.Attack,500,(Feature.Danger,20000),(Feature.Hostility,8000),(Feature.Injury,-8000),(Feature.TargetInjury,1000)));
                if(id=="companion")rules.Add(Rule(ForgeAction.Follow,1800,(Feature.AllyDistance,10000),(Feature.Danger,-2000)));
                var artifact=new ControllerArtifact(archetype,"ashen."+id,1,rules);
                var cases=new List<RegressionCase>{new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>()),ForgeAction.Wait),new RegressionCase(new ObservationFrame(0,Array.Empty<Observation>()),ForgeAction.None)};
                if(!safe)cases.Add(new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Rest,ForgeAction.Travel),new[]{new Observation(Feature.Fatigue,10000)}),ForgeAction.Rest));
                if(fighter)cases.Add(new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Flee,ForgeAction.Attack),new[]{new Observation(Feature.Danger,10000),new Observation(Feature.Hostility,10000)}),ForgeAction.Attack));
                var admission=ControllerAdmission.Run(artifact,"ashen.utility-v1",cases);registry.Register(artifact,artifact.Hash,admission);
            }
            return registry;
        }
    }
}
