using System;
using System.Linq;
using AshenCrypt.MemoryForge;

internal static class ForgePolicyTests
{
    public static void Run(Action<bool,string> check)
    {
        var registry=ForgeDefaults.CreateRegistry();var guard=registry.BindNew("guard");
        var alternate=new NpcArchetypeControllerRegistry();alternate.Register(guard,guard.Hash,registry.Admission(guard),false);
        var changed=new ArchetypeDefinition("guard",Capability.None,ActionSet.Of(ForgeAction.Wait),0);
        var wrong=new ControllerArtifact(changed,"test.guard",2,new[]{new UtilityRule(ForgeAction.Wait,0,0,new int[(int)Feature.Count])});
        var waitFrame=new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>());
        bool rejected=false;try{alternate.Register(wrong,wrong.Hash,ControllerAdmission.Run(wrong,"test.manifest",new[]{new RegressionCase(waitFrame,ForgeAction.Wait)}));}catch(ArgumentException){rejected=true;}
        check(rejected,"forge-historical-registration-cannot-bypass-archetype-manifest");
        var input=new int[(int)Feature.Count*2];input[(int)Feature.Danger]=10000;input[(int)Feature.Count+(int)Feature.Injury]=10000;
        var output=new int[20];output[(int)ForgeAction.Attack*2]=10000;output[(int)ForgeAction.Attack*2+1]=-20000;output[(int)ForgeAction.Flee*2+1]=10000;
        var network=new QuantizedNetwork(2,input,new int[2],output,new int[10]);
        var v2=new ControllerArtifact(guard.Archetype,guard.ControllerId,2,new[]{new UtilityRule(ForgeAction.Wait,0,100,new int[(int)Feature.Count]),new UtilityRule(ForgeAction.Attack,1,0,new int[(int)Feature.Count]),new UtilityRule(ForgeAction.Flee,2,0,new int[(int)Feature.Count])},network:network);
        var threat=new ObservationFrame(ActionSet.Of(ForgeAction.Wait,ForgeAction.Attack,ForgeAction.Flee),new[]{new Observation(Feature.Danger,10000)});
        var hurt=new ObservationFrame(threat.LegalActions,new[]{new Observation(Feature.Danger,10000),new Observation(Feature.Injury,10000)});
        check(UtilityEvaluator.Evaluate(v2,threat).Action==ForgeAction.Attack&&UtilityEvaluator.Evaluate(v2,hurt).Action==ForgeAction.Flee,"forge-integer-neural-hidden-layer-selects-contextual-response");
        check(UtilityEvaluator.Evaluate(v2,waitFrame).Action==ForgeAction.Wait,"forge-neural-output-cannot-overrule-hard-ban");
        input[0]=9999;output[0]=9999;
        check(UtilityEvaluator.Evaluate(v2,threat).CanonicalTrace==UtilityEvaluator.Evaluate(v2,threat).CanonicalTrace&&network.InputWeight(0,Feature.Hunger)==0,"forge-neural-weights-immutable-and-reproducible");
        var admission=ControllerAdmission.Run(v2,"test.neural-v2",new[]{new RegressionCase(threat,ForgeAction.Attack),new RegressionCase(hurt,ForgeAction.Flee),new RegressionCase(waitFrame,ForgeAction.Wait)});
        registry.Register(v2,v2.Hash,admission,false);
        var archive=new MemoryArchive();var npc=archive.Spawn("watch","guard",registry);
        var help=archive.Append(MemoryKind.Help,"player","watch",1,5000,true,"rescue",new RelationshipChange(trust:7000));archive.Learn("watch",help.Id,KnowledgeOrigin.Self,"watch",1);
        UtilityEvaluator.Decide(npc.Controller,threat,npc.DecisionState,2);
        check(!archive.TryMigrate("watch",v2,registry,1,out _)&&npc.Controller.Version==1,"forge-controller-migration-rejects-time-reversal");
        check(archive.TryMigrate("watch",v2,registry,3,out var receipt)&&npc.Controller.Hash==v2.Hash&&receipt.FromHash==guard.Hash&&receipt.RegressionHash==admission.EvidenceHash,"forge-explicit-migration-has-pinned-source-target-and-regression-receipt");
        check(!archive.TryMigrate("watch",v2,registry,4,out _)&&archive.Migrations.Count==1,"forge-same-migration-cannot-apply-twice");
        UtilityEvaluator.Decide(npc.Controller,hurt,npc.DecisionState,4);
        var save=archive.Save();check(MemoryArchive.TryLoad(save,registry,out var loaded)&&loaded.Save()==save&&loaded.Migrations[0].Checksum==receipt.Checksum&&loaded.Get("watch").RelationshipTo("player").Trust==7000,"forge-migration-history-neural-decision-and-memory-survive-restore");
        var withoutV2=ForgeDefaults.CreateRegistry();check(!MemoryArchive.TryLoad(save,withoutV2,out _),"forge-missing-neural-history-must-not-select-newest");
        for(int i=0;i<100;i++)UtilityEvaluator.Evaluate(guard,threat);
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)UtilityEvaluator.Evaluate(guard,threat);
        long perDecision=(GC.GetAllocatedBytesForCurrentThread()-before)/1000;
        check(perDecision<2000,"forge-gameplay-decision-does-not-allocate-unused-diagnostic-text-actual-bytes-"+perDecision);
        check(UtilityEvaluator.Evaluate(guard,threat).Priorities.First(s=>s.Action==ForgeAction.Attack).Contributions.Contains("3=10000*20000->20000"),"forge-on-demand-trace-retains-actual-input-weight-and-contribution");
    }
}
