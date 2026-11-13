using System;
using AshenCrypt.MemoryForge;

internal static class NpcForgeTests
{
    public static void Run(Action<bool,string> require)
    {
        var registry=ForgeDefaults.CreateRegistry();
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
    }
}
