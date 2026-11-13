using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AshenCrypt.MemoryForge.Integration
{
    [Serializable] public sealed class ForgeRuleConfig
    { public ForgeAction Action; public int Order; public int BasePriority; public int[] Weights=new int[(int)Feature.Count]; }
    [Serializable] public sealed class ForgeObservationConfig
    { public Feature Feature; [Range(0,10000)] public int Value; }
    [Serializable] public sealed class ForgeRegressionConfig
    {
        public ForgeAction Expected;
        public ForgeAction[] LegalActions=Array.Empty<ForgeAction>();
        public ForgeObservationConfig[] Observations=Array.Empty<ForgeObservationConfig>();
        public RegressionCase Build()=>new RegressionCase(new ObservationFrame(ActionSet.Of(LegalActions),Observations.Select(o=>new Observation(o.Feature,o.Value))),Expected);
    }
    /// <summary>Configuration only. An asset contains neither live NPC state nor an archive/save.</summary>
    [CreateAssetMenu(menuName="Memory Forge/Controller candidate",fileName="ForgeController")]
    public sealed class ForgeControllerAsset : ScriptableObject
    {
        public string ArchetypeId="villager";
        public string ControllerId="custom.villager";
        [Min(1)] public int Version=1;
        public Capability Capabilities;
        public ForgeAction[] ActionWhitelist={ForgeAction.Wait};
        public Feature[] ObservableFeatures=Array.Empty<Feature>();
        public ForgeAction Fallback=ForgeAction.Wait;
        [Range(1,100)] public int DecisionInterval=3;
        [Range(0,100)] public int MinimumDuration=4;
        [Range(0,10000)] public int Hysteresis=300;
        public List<ForgeRuleConfig> Rules=new List<ForgeRuleConfig>{new ForgeRuleConfig{Action=ForgeAction.Wait}};
        public bool UseNeuralNetwork;
        [Range(1,16)] public int HiddenCount=2;
        public int[] InputWeights=new int[(int)Feature.Count*2],HiddenBias=new int[2],OutputWeights=new int[20],OutputBias=new int[10];
        public string SuiteId="custom.regression";
        public List<ForgeRegressionConfig> Regressions=new List<ForgeRegressionConfig>();
        [SerializeField,HideInInspector] private string _reviewedHash="";
        [SerializeField,HideInInspector] private string _regressionHash="";
        public string ReviewedHash=>_reviewedHash;
        public string RegressionHash=>_regressionHash;
        public ControllerArtifact BuildCandidate()
        {
            ulong features=0;foreach(var f in ObservableFeatures){if(f<0||f>=Feature.Count)throw new ArgumentException("Unknown observation feature");features|=1UL<<(int)f;}
            var role=new ArchetypeDefinition(ArchetypeId,Capabilities,ActionSet.Of(ActionWhitelist),features,Fallback);
            var network=UseNeuralNetwork?new QuantizedNetwork(HiddenCount,InputWeights,HiddenBias,OutputWeights,OutputBias):null;
            return new ControllerArtifact(role,ControllerId,Version,Rules.Select(r=>new UtilityRule(r.Action,r.Order,r.BasePriority,r.Weights)),DecisionInterval,MinimumDuration,Hysteresis,network:network);
        }
        public bool EvaluateRegressions(out ControllerArtifact candidate,out ControllerAdmission receipt,out string message)
        {
            candidate=null;receipt=null;
            try
            {
                candidate=BuildCandidate();receipt=ControllerAdmission.Run(candidate,SuiteId,Regressions.Select(r=>r.Build()));
                message=receipt.Passed+" passed / "+receipt.Failed+" failed";return receipt.Failed==0;
            }
            catch(Exception e) when(e is ArgumentException||e is OverflowException||e is InvalidOperationException||e is NullReferenceException)
            {message=e.Message;return false;}
        }
        /// <summary>Explicit authoring operation; never invoked by spawn, restore or the evaluator.</summary>
        public bool ReviewAndSeal(out string message)
        {
            if(!EvaluateRegressions(out var candidate,out var receipt,out message))return false;
            if(!string.IsNullOrEmpty(_reviewedHash)&&(_reviewedHash!=candidate.Hash||_regressionHash!=receipt.EvidenceHash))
            {message="An approved artifact is immutable. Fork a new version and retain this version for existing saves.";return false;}
            _reviewedHash=candidate.Hash;_regressionHash=receipt.EvidenceHash;return true;
        }
        public bool TryGetApproved(out ControllerArtifact controller,out ControllerAdmission receipt,out string message)
        {
            if(!EvaluateRegressions(out controller,out receipt,out message))return false;
            if(controller.Hash!=_reviewedHash||receipt.EvidenceHash!=_regressionHash)
            {controller=null;receipt=null;message="Unapproved or changed artifact. Run regressions and explicitly seal a new version.";return false;}
            return true;
        }
        public void CopyCandidate(ControllerArtifact source,IEnumerable<RegressionCase> cases)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            ArchetypeId=source.ArchetypeId;ControllerId=source.ControllerId;Version=source.Version;Capabilities=source.Archetype.Capabilities;Fallback=source.Archetype.Fallback;
            ActionWhitelist=Enumerable.Range(0,10).Select(x=>(ForgeAction)x).Where(a=>ActionSet.Contains(source.Archetype.AllowedActions,a)).ToArray();
            ObservableFeatures=Enumerable.Range(0,(int)Feature.Count).Where(i=>(source.Archetype.ObservableFeatures&(1UL<<i))!=0).Select(i=>(Feature)i).ToArray();
            DecisionInterval=source.DecisionInterval;MinimumDuration=source.MinimumDuration;Hysteresis=source.Hysteresis;
            Rules=source.Rules.Select(r=>new ForgeRuleConfig{Action=r.Action,Order=r.Order,BasePriority=r.BasePriority,Weights=Enumerable.Range(0,(int)Feature.Count).Select(i=>r.Weight((Feature)i)).ToArray()}).ToList();
            UseNeuralNetwork=source.Network!=null;
            if(UseNeuralNetwork)
            {
                var n=source.Network;HiddenCount=n.HiddenCount;
                InputWeights=Enumerable.Range(0,HiddenCount*(int)Feature.Count).Select(i=>n.InputWeight(i/(int)Feature.Count,(Feature)(i%(int)Feature.Count))).ToArray();
                HiddenBias=Enumerable.Range(0,HiddenCount).Select(n.HiddenBias).ToArray();
                OutputWeights=Enumerable.Range(0,10*HiddenCount).Select(i=>n.OutputWeight((ForgeAction)(i/HiddenCount),i%HiddenCount)).ToArray();
                OutputBias=Enumerable.Range(0,10).Select(i=>n.OutputBias((ForgeAction)i)).ToArray();
            }
            Regressions=cases.Select(c=>new ForgeRegressionConfig{Expected=c.Expected,LegalActions=Enumerable.Range(0,10).Select(i=>(ForgeAction)i).Where(a=>ActionSet.Contains(c.Input.LegalActions,a)).ToArray(),Observations=Enumerable.Range(0,(int)Feature.Count).Where(i=>(c.Input.PresentFeatures&(1UL<<i))!=0).Select(i=>new ForgeObservationConfig{Feature=(Feature)i,Value=c.Input[(Feature)i]}).ToArray()}).ToList();
            _reviewedHash=_regressionHash="";
        }
    }
}
