using System;
using AshenCrypt.MemoryForge;

internal static class NpcForgeTests
{
    public static void Run(Action<bool,string> require)
    {
        var registry=ForgeDefaults.CreateRegistry();
        require(ForgeDefaults.ReviewedHash("traveler")=="627ed161da4c0868ddf5f5973dac6028aa6adaaaf4e5f4a2f630a156cd83ea74", "forge-release-manifest-pins-reviewed-policy-bytes");
        var guard=registry.BindNew("guard");
        require(guard.ArchetypeId=="guard"&&guard.Version==1&&guard.Hash.Length==64,"forge-spawn-binds-approved-controller-hash");
        require(registry.BindNew("unknown").ArchetypeId=="fallback","forge-unknown-archetype-safe-fallback");
        require(registry.TryRestore(guard.ArchetypeId,guard.ControllerId,guard.Version,guard.Hash,out var restored)&&restored.Hash==guard.Hash,"forge-restore-pins-exact-controller");
        require(!registry.TryRestore(guard.ArchetypeId,guard.ControllerId,guard.Version,new string('0',64),out _),"forge-invalid-controller-hash-rejected");
        require(!registry.TryRestore(guard.ArchetypeId,guard.ControllerId,99,guard.Hash,out _),"forge-missing-historical-version-is-not-latest");
        var frame=new ObservationFrame(ActionSet.Of(ForgeAction.Rest,ForgeAction.Flee,ForgeAction.Attack),new[]{new Observation(Feature.Danger,10000),new Observation(Feature.Hostility,10000)});
        var a=UtilityEvaluator.Evaluate(guard,frame);var b=UtilityEvaluator.Evaluate(guard,frame);
        require(a.Action==ForgeAction.Attack&&a.CanonicalTrace==b.CanonicalTrace,"forge-danger-deterministic-explained-decision");
        var forbidden=new ObservationFrame(ActionSet.Of(ForgeAction.Rest),new[]{new Observation(Feature.Danger,10000),new Observation(Feature.Hostility,10000)});
        require(UtilityEvaluator.Evaluate(guard,forbidden).Action==ForgeAction.Rest,"forge-hard-world-ban-precedes-utility");
        var state=new DecisionState();
        var calm=new ObservationFrame(ActionSet.Of(ForgeAction.Rest,ForgeAction.Travel),new[]{new Observation(Feature.Fatigue,9000)});
        require(UtilityEvaluator.Decide(guard,calm,state,1).Action==ForgeAction.Rest,"forge-fatigue-priority");
        require(UtilityEvaluator.Decide(guard,new ObservationFrame(ActionSet.Of(ForgeAction.Attack),new[]{new Observation(Feature.Danger,10000)}),state,2).Action==ForgeAction.Attack,"forge-hard-danger-interrupts-minimum-duration");
        bool invalid=false;try{new Observation(Feature.Hunger,10001);}catch(ArgumentOutOfRangeException){invalid=true;}
        require(invalid,"forge-input-range-checked");
        require(FixedPoint.Multiply(-1,5000)==-1&&FixedPoint.Multiply(1,5000)==1,"forge-rounding-nearest-away-from-zero");
        var archive=new MemoryArchive();var ilyan=archive.Spawn("ilyan","traveler",registry);var mara=archive.Spawn("mara","merchant",registry);
        var helped=archive.Append(MemoryKind.Help,"player","ilyan",10,5000,false,"provisions",new RelationshipChange(trust:1200,gratitude:2000,familiarity:500));
        require(archive.Learn("ilyan",helped.Id,KnowledgeOrigin.Self,"ilyan",10),"forge-help-personally-remembered");
        require(!archive.Learn("ilyan",helped.Id,KnowledgeOrigin.Self,"ilyan",10)&&ilyan.RelationshipTo("player").Trust==1200,"forge-same-event-cannot-double-apply-relationship");
        require(mara.Known.Count==0&&mara.RelationshipTo("player").Trust==0,"forge-memory-is-not-omniscient");
        require(archive.Report("ilyan","mara",helped.Id,11)&&mara.Known[0].Origin==KnowledgeOrigin.Reported&&!mara.Known[0].IsFact,"forge-reported-help-is-not-direct-fact");
        var saved=archive.Save();require(MemoryArchive.TryLoad(saved,registry,out var loaded)&&loaded.Save()==saved,"forge-memory-controller-and-relations-roundtrip");
        require(!loaded.Learn("ilyan",helped.Id,KnowledgeOrigin.Self,"ilyan",12)&&loaded.Get("ilyan").RelationshipTo("player").Trust==1200,"forge-load-does-not-replay-reward");
        var death=archive.Append(MemoryKind.Death,"bear","hunter",20,10000,true,"road.hunter",new RelationshipChange(fear:3000));
        archive.Learn("ilyan",death.Id,KnowledgeOrigin.Witnessed,"ilyan",20);
        require(ilyan.Relevant(1000000).Count==1&&ilyan.Known.Count==2&&archive.Events.Count==2,"forge-forgetting-retains-history-and-essential-death");
        require(archive.Learn("mara",helped.Id,KnowledgeOrigin.Refuted,"mara",30)&&mara.RelationshipTo("player").Trust==0,"forge-refuted-rumor-retracts-its-own-impact");
        UtilityEvaluator.Decide(ilyan.Controller,calm,ilyan.DecisionState,31);
        bool continuitySaved=false;
        try
        {
            string checkpoint=archive.Save();
            continuitySaved=MemoryArchive.TryLoad(checkpoint,registry,out var resumed)&&resumed.Save()==checkpoint&&
                UtilityEvaluator.Decide(ilyan.Controller,calm,ilyan.DecisionState,32).CanonicalTrace==
                UtilityEvaluator.Decide(resumed.Get("ilyan").Controller,calm,resumed.Get("ilyan").DecisionState,32).CanonicalTrace&&
                resumed.Get("ilyan").DecisionState.StartedTick==ilyan.DecisionState.StartedTick;
        }
        catch(InvalidOperationException) { }
        require(continuitySaved,"forge-active-decision-cooldown-and-trace-survive-load");
        bool capabilityRejected=false;
        try{new ArchetypeDefinition("invalid",Capability.None,ActionSet.Of(ForgeAction.Wait,ForgeAction.Attack),0);}catch(ArgumentException){capabilityRejected=true;}
        require(capabilityRejected,"forge-capability-cannot-authorize-unimplemented-action");
        var traveler=registry.BindNew("traveler");
        var v2=new ControllerArtifact(traveler.Archetype,traveler.ControllerId,2,traveler.Rules);
        var suite=new[]{new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>()),ForgeAction.Wait)};
        var admission=ControllerAdmission.Run(v2,"tests.version-2",suite);
        bool badHash=false;try{registry.Register(v2,new string('0',64),admission);}catch(ArgumentException){badHash=true;}
        require(badHash,"forge-register-rejects-tampered-controller-hash");
        registry.Register(v2,v2.Hash,admission);
        require(registry.BindNew("traveler").Version==2&&ilyan.Controller.Version==1,"forge-new-spawn-version-does-not-migrate-active-npc");
        require(MemoryArchive.TryLoad(archive.Save(),registry,out var historical)&&historical.Get("ilyan").Controller.Version==1,"forge-restore-historical-binding-after-new-release");
        require(!archive.Report("mara","ilyan",helped.Id,40),"forge-rumor-cannot-be-rebroadcast-as-direct-knowledge");
        require(!archive.Learn("mara",death.Id,KnowledgeOrigin.Self,"mara",40),"forge-self-knowledge-requires-personal-involvement");
        require(!archive.Learn("ilyan",death.Id,KnowledgeOrigin.Refuted,"ilyan",40),"forge-rumor-retraction-cannot-erase-observed-death");
        var immutable=archive.Save();
        require(!MemoryArchive.TryLoad(immutable.Substring(0,immutable.Length-1)+"x",registry,out _),"forge-memory-checksum-rejects-corruption");
        var ties=new ControllerArtifact(guard.Archetype,"tests.tie",1,new[]{
            new UtilityRule(ForgeAction.Wait,1,100,new int[(int)Feature.Count]),new UtilityRule(ForgeAction.Rest,0,100,new int[(int)Feature.Count])});
        var tied=new ObservationFrame(ActionSet.Of(ForgeAction.Wait,ForgeAction.Rest),Array.Empty<Observation>());
        require(UtilityEvaluator.Evaluate(ties,tied).Action==ForgeAction.Rest,"forge-tie-uses-versioned-order-not-enum-or-dictionary");
        var stable=new DecisionState();UtilityEvaluator.Decide(guard,calm,stable,100);
        var nearlyCalm=new ObservationFrame(ActionSet.Of(ForgeAction.Rest,ForgeAction.Travel),new[]{new Observation(Feature.Fatigue,0)});
        require(UtilityEvaluator.Decide(guard,nearlyCalm,stable,101,true).Reason=="minimum-duration","forge-weight-change-cannot-oscillate-every-tick");
        require(UtilityEvaluator.Decide(guard,nearlyCalm,stable,105,true).Action==ForgeAction.Travel,"forge-stable-action-can-change-after-minimum-duration");
        bool overflow=false;try{FixedPoint.Multiply(int.MaxValue,int.MaxValue);}catch(OverflowException){overflow=true;}
        require(overflow,"forge-fixed-point-overflow-is-not-silent");
    }
}
