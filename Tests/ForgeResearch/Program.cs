using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using AshenCrypt.MemoryForge;

internal static class Program
{
    private static readonly ArchetypeDefinition Role=ForgeDefaults.CreateRegistry().BindNew("guard").Archetype;
    private static ObservationFrame Frame(int danger,int injury,bool forbidAttack=false) => new ObservationFrame(danger==0?ActionSet.Of(ForgeAction.Wait):forbidAttack?ActionSet.Of(ForgeAction.Wait,ForgeAction.Flee):ActionSet.Of(ForgeAction.Wait,ForgeAction.Attack,ForgeAction.Flee),new[]{new Observation(Feature.Danger,danger),new Observation(Feature.Injury,injury)});
    // Small deterministic behavior tree: world permission, then injury threshold, then defense, then wait.
    private static ForgeAction BehaviorTree(ObservationFrame f)
    {
        if(!ActionSet.Contains(f.LegalActions,ForgeAction.Attack))return ActionSet.Contains(f.LegalActions,ForgeAction.Flee)?ForgeAction.Flee:ForgeAction.Wait;
        return f[Feature.Injury]>7500?ForgeAction.Flee:ForgeAction.Attack;
    }
    private static UtilityRule Rule(ForgeAction a,int order,int priority,int injury=0)
    {var w=new int[(int)Feature.Count];w[(int)Feature.Injury]=injury;return new UtilityRule(a,order,priority,w);}
    private static ControllerArtifact Neural(int repairWeight,bool fullDomain=false)
    {
        var input=new int[2*(int)Feature.Count];input[(int)Feature.Danger]=fullDomain?0:10000;input[(int)Feature.Count+(int)Feature.Injury]=10000;
        var output=new int[20];output[(int)ForgeAction.Attack*2]=repairWeight;output[(int)ForgeAction.Attack*2+1]=-20000;output[(int)ForgeAction.Flee*2+1]=20000;
        var network=new QuantizedNetwork(2,input,new[]{fullDomain?10000:0,-5000},output,new int[10]);
        return new ControllerArtifact(Role,"research.guard",fullDomain?3:repairWeight<0?1:2,new[]{Rule(ForgeAction.Wait,0,100),Rule(ForgeAction.Attack,1,0),Rule(ForgeAction.Flee,2,0)},network:network);
    }
    private static ControllerArtifact Utility()=>new ControllerArtifact(Role,"research.utility",1,new[]{Rule(ForgeAction.Wait,0,-10000),Rule(ForgeAction.Attack,1,10000,-10000),Rule(ForgeAction.Flee,2,-5000,10000)});
    private static object Score(ControllerArtifact c,ObservationFrame[] frames)
    {
        var failures=frames.Where(f=>UtilityEvaluator.Evaluate(c,f).Action!=BehaviorTree(f)).ToArray();
        return new {c.ControllerId,c.Version,c.Hash,cases=frames.Length,passed=frames.Length-failures.Length,failed=failures.Length,firstFailures=failures.Take(4).Select(f=>new{danger=f[Feature.Danger],injury=f[Feature.Injury],legal=f.LegalActions,expected=BehaviorTree(f).ToString(),actual=UtilityEvaluator.Evaluate(c,f).Action.ToString()})};
    }
    private static double Time(ControllerArtifact c,ObservationFrame[] frames)
    {
        for(int i=0;i<100;i++)UtilityEvaluator.Evaluate(c,frames[i%frames.Length]);
        var sw=Stopwatch.StartNew();for(int i=0;i<20000;i++)UtilityEvaluator.Evaluate(c,frames[i%frames.Length]);sw.Stop();return sw.Elapsed.TotalMilliseconds*1000/20000;
    }
    public static int Main()
    {
        var train=(from d in new[]{0,10000} from injury in new[]{0,2000,5000,6000,8500,10000} select Frame(d,injury)).ToArray();
        var heldout=(from d in new[]{0,10000} from injury in new[]{500,1500,2500,3500,4500,5500,6500,7000,7500,8000,9000,9500} select Frame(d,injury)).ToArray();
        var grid=(from d in Enumerable.Range(0,41) from i in Enumerable.Range(0,41) select Frame(d*250,i*250)).ToArray();
        var forbidden=Enumerable.Range(0,41).Select(i=>Frame(10000,i*250,true)).ToArray();
        var candidates=new[]{-10000,-5000,0,5000,10000,15000,20000};
        int selected=candidates.OrderByDescending(w=>train.Count(f=>UtilityEvaluator.Evaluate(Neural(w),f).Action==BehaviorTree(f))).ThenBy(w=>Math.Abs(w+10000)).ThenBy(w=>w).First();
        var faulty=Neural(-10000);var repaired=Neural(selected);var full=Neural(selected,true);var utility=Utility();
        // World permissions must filter forbidden attack before either model scores it. Flee is the safe legal response.
        var cases=grid.Concat(forbidden).Select(f=>new RegressionCase(f,BehaviorTree(f))).ToArray();
        var admission=ControllerAdmission.Run(full,"research.guard-full-domain",cases);
        var utilityAdmission=ControllerAdmission.Run(utility,"research.utility-full-domain",cases);
        string revision="unknown";
        try{using(var p=Process.Start(new ProcessStartInfo("git","rev-parse HEAD"){RedirectStandardOutput=true,UseShellExecute=false})){revision=p.StandardOutput.ReadToEnd().Trim();p.WaitForExit();}}catch(InvalidOperationException){}
        var output=new{
            schema="AshenCrypt.ForgeResearch.v1",revision,runtime=RuntimeInformation.FrameworkDescription,os=RuntimeInformation.OSDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),
            experiment="Guard threat response: isolated output-weight fault, then a broader observation-domain counterexample",
            cause="Threat observations and Attack permission are present. The faulty neural Attack output weight is -10000; its healthy-threat logit is negative.",
            search=new{candidates,selected,objective="Training correct count, then minimum absolute weight edit, then integer order"},
            faulty=Score(faulty,train),repairedTraining=Score(repaired,train),repairedHeldout=Score(repaired,heldout),repairedBroaderDomain=Score(repaired,grid),
            fullDomainRepair=Score(full,grid),simpleUtility=Score(utility,grid),
            admission=new{admission.Passed,admission.Failed,admission.EvidenceHash},utilityAdmission=new{utilityAdmission.Passed,utilityAdmission.Failed,utilityAdmission.EvidenceHash},
            measuredManagedMicrosecondsPerEvaluation=new{neural=Time(full,grid),utility=Time(utility,grid),iterations=20000},
            decision="Keep shipped utility profiles. Integer neural inference is supported and deterministic; no gameplay superiority is demonstrated. Partial-domain repair is rejected for general use. No runtime training, native module or Android performance claim.",
            fullDomainArtifact=full.Canonical
        };
        Console.WriteLine(JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
        return admission.Failed==0&&utilityAdmission.Failed==0?0:1;
    }
}
