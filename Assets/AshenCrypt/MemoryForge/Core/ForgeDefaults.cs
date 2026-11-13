using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenCrypt.MemoryForge
{
    public static class ForgeDefaults
    {
        /// <summary>Release-v1 manifest. A weight or capability edit requires a new reviewed version.</summary>
        public static string ReviewedHash(string archetype)
        {
            switch(archetype)
            {
                case "animal":return "7449ddfe37ae25feee387c6a537725e42c8f3c1e64b9124c1265ae8e86193734";
                case "companion":return "d13a9b2dbeca77faa8afb7fad14b86bef3fd319bc092529f06a4ea89d08da844";
                case "fallback":return "43af7a1a576b9139316101477a9be58589d3a5141427791ebf01d56d303a1b2d";
                case "guard":return "eabad422b8127f050594eb2327901652b3fc79286827e7051fc609c10c5a189c";
                case "healer":return "a7bf9ecb59e5699252913f8b760a0ff2b6b7346f4d71fca0a8be6cc88eccd664";
                case "merchant":return "7a14d38e4bd69fb8b6017d73ae173a2814530a9feb245638dad1ef54c5098aa4";
                case "monster":return "470ff8b3cc2847771d70603a0bf01185b0cceebac7fcf11e589b07644ad4955c";
                case "traveler":return "627ed161da4c0868ddf5f5973dac6028aa6adaaaf4e5f4a2f630a156cd83ea74";
                case "villager":return "7f8adda00ed846d9235895d2ae21358678fb93734fb5fec390327efd94daf883";
                default:throw new ArgumentException("No reviewed built-in artifact for this archetype");
            }
        }
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
                var admission=ControllerAdmission.Run(artifact,"ashen.utility-v1",cases);registry.Register(artifact,ReviewedHash(id),admission);
            }
            return registry;
        }
    }
}
