using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AshenCrypt.MemoryForge
{
    public sealed class ArchetypeDefinition
    {
        public string Id { get; }
        public Capability Capabilities { get; }
        public ulong AllowedActions { get; }
        public ulong ObservableFeatures { get; }
        public ForgeAction Fallback { get; }
        internal string Canonical => Id+","+ContractText.Number((long)Capabilities)+","+ContractText.Number(AllowedActions)+","+ContractText.Number(ObservableFeatures)+","+ContractText.Number((long)Fallback);
        public ArchetypeDefinition(string id,Capability capabilities,ulong actions,ulong observations,ForgeAction fallback=ForgeAction.Wait)
        {
            Id=ContractText.Id(id);Capabilities=capabilities;AllowedActions=actions;ObservableFeatures=observations;Fallback=fallback;
            if(((int)capabilities&~255)!=0||(actions&~ActionSet.Known)!=0||(observations&~((1UL<<(int)Feature.Count)-1))!=0||!ActionSet.Contains(actions,fallback))throw new ArgumentException("Invalid archetype manifest");
            for(int i=0;i<10;i++)if(ActionSet.Contains(actions,(ForgeAction)i)&&(capabilities&ActionSet.Requires((ForgeAction)i))!=ActionSet.Requires((ForgeAction)i))throw new ArgumentException("Action exceeds archetype capabilities");
        }
    }
    public sealed class UtilityRule
    {
        private readonly int[] _weights;
        public ForgeAction Action { get; }
        public int Order { get; }
        public int BasePriority { get; }
        public int Weight(Feature feature) => _weights[(int)feature];
        internal string Canonical => ContractText.Number((long)Action)+","+ContractText.Number(Order)+","+ContractText.Number(BasePriority)+","+string.Join(",",_weights.Select(w=>ContractText.Number(w)));
        public UtilityRule(ForgeAction action,int order,int basePriority,params int[] weights)
        {
            if(action<ForgeAction.Wait||action>ForgeAction.Treat||order<0||order>1000||Math.Abs((long)basePriority)>10000||weights==null||weights.Length!=(int)Feature.Count||weights.Any(w=>Math.Abs((long)w)>40000))throw new ArgumentException("Invalid bounded utility rule");
            Action=action;Order=order;BasePriority=basePriority;_weights=(int[])weights.Clone();
        }
    }
    public sealed class ControllerArtifact
    {
        public ArchetypeDefinition Archetype { get; }
        public string ArchetypeId => Archetype.Id;
        public string ControllerId { get; }
        public int Version { get; }
        public int RuntimeVersion { get; }
        public int MemoryVersion { get; }
        public int DecisionInterval { get; }
        public int MinimumDuration { get; }
        public int Hysteresis { get; }
        public IReadOnlyList<UtilityRule> Rules { get; }
        public string Hash { get; }
        public string Canonical { get; }
        public ControllerArtifact(ArchetypeDefinition archetype,string controllerId,int version,IEnumerable<UtilityRule> rules,int decisionInterval=3,int minimumDuration=4,int hysteresis=300,int runtimeVersion=1,int memoryVersion=1)
        {
            Archetype=archetype??throw new ArgumentNullException(nameof(archetype));ControllerId=ContractText.Id(controllerId);
            if(version<1||runtimeVersion<1||memoryVersion<1||decisionInterval<1||decisionInterval>100||minimumDuration<0||minimumDuration>100||hysteresis<0||hysteresis>10000)throw new ArgumentException("Invalid controller version or timing");
            Version=version;RuntimeVersion=runtimeVersion;MemoryVersion=memoryVersion;DecisionInterval=decisionInterval;MinimumDuration=minimumDuration;Hysteresis=hysteresis;
            var copy=(rules??throw new ArgumentNullException(nameof(rules))).OrderBy(r=>r.Order).ToArray();
            if(copy.Length==0||copy.Length>10||copy.Select(r=>r.Order).Distinct().Count()!=copy.Length||copy.Select(r=>r.Action).Distinct().Count()!=copy.Length)throw new ArgumentException("Ambiguous action order");
            foreach(var rule in copy)
            {
                if(!ActionSet.Contains(archetype.AllowedActions,rule.Action))throw new ArgumentException("Controller action outside whitelist");
                for(int i=0;i<(int)Feature.Count;i++)if(rule.Weight((Feature)i)!=0&&(archetype.ObservableFeatures&(1UL<<i))==0)throw new ArgumentException("Unobservable controller input");
            }
            if(!copy.Any(r=>r.Action==archetype.Fallback))throw new ArgumentException("Missing fallback rule");
            Rules=new ReadOnlyCollection<UtilityRule>(copy);
            Canonical="AshenCrypt.ControllerArtifact.v1\n"+archetype.Canonical+"\n"+ControllerId+","+ContractText.Number(version)+","+ContractText.Number(runtimeVersion)+","+ContractText.Number(memoryVersion)+","+ContractText.Number(decisionInterval)+","+ContractText.Number(minimumDuration)+","+ContractText.Number(hysteresis)+"\n"+string.Join("\n",copy.Select(r=>r.Canonical));
            Hash=ContractText.Hash(Canonical);
        }
    }
    public sealed class RegressionCase
    {
        public ObservationFrame Input { get; }
        public ForgeAction Expected { get; }
        public RegressionCase(ObservationFrame input,ForgeAction expected){Input=input??throw new ArgumentNullException(nameof(input));Expected=expected;}
    }
    public sealed class ControllerAdmission
    {
        public string ControllerHash { get; }
        public string SuiteId { get; }
        public string EvidenceHash { get; }
        public int Passed { get; }
        public int Failed { get; }
        private ControllerAdmission(string controllerHash,string suite,string evidence,int passed,int failed)
        {ControllerHash=controllerHash;SuiteId=suite;EvidenceHash=evidence;Passed=passed;Failed=failed;}
        /// <summary>Run real evaluator cases; admission cannot be created by setting a claimed success boolean.</summary>
        public static ControllerAdmission Run(ControllerArtifact controller,string suiteId,IEnumerable<RegressionCase> cases)
        {
            ContractText.Id(suiteId);int passed=0,failed=0;string receipt=controller.Hash+"\n"+suiteId;
            foreach(var test in cases??throw new ArgumentNullException(nameof(cases)))
            {var result=UtilityEvaluator.Evaluate(controller,test.Input);if(result.Action==test.Expected)passed++;else failed++;receipt+="\n"+ContractText.Number((long)test.Expected)+":"+result.CanonicalTrace;}
            if(passed+failed==0)throw new ArgumentException("Empty regression suite");
            return new ControllerAdmission(controller.Hash,suiteId,ContractText.Hash(receipt),passed,failed);
        }
    }
    public sealed class NpcArchetypeControllerRegistry
    {
        public const string Contract="AshenCrypt.NpcArchetypeControllerRegistry.v1";
        private readonly SortedDictionary<string,ControllerArtifact> _versions=new SortedDictionary<string,ControllerArtifact>(StringComparer.Ordinal);
        private readonly SortedDictionary<string,ControllerArtifact> _current=new SortedDictionary<string,ControllerArtifact>(StringComparer.Ordinal);
        private readonly SortedDictionary<string,ControllerAdmission> _admissions=new SortedDictionary<string,ControllerAdmission>(StringComparer.Ordinal);
        public int RuntimeVersion { get; }
        public int MemoryVersion { get; }
        public NpcArchetypeControllerRegistry(int runtimeVersion=1,int memoryVersion=1){RuntimeVersion=runtimeVersion;MemoryVersion=memoryVersion;}
        private static string Key(string archetype,string id,int version) => archetype+"/"+id+"/"+ContractText.Number(version);
        public void Register(ControllerArtifact controller,string expectedHash,ControllerAdmission admission,bool selectForNewSpawns=true)
        {
            if(controller==null||controller.Hash!=expectedHash||admission==null||admission.ControllerHash!=expectedHash||admission.Failed!=0||admission.Passed==0||controller.RuntimeVersion!=RuntimeVersion||controller.MemoryVersion!=MemoryVersion)throw new ArgumentException("Controller admission rejected");
            string key=Key(controller.ArchetypeId,controller.ControllerId,controller.Version);
            if(_versions.ContainsKey(key))throw new ArgumentException("Immutable controller version already exists");
            if(_current.TryGetValue(controller.ArchetypeId,out var old)&&old.Archetype.Canonical!=controller.Archetype.Canonical)throw new ArgumentException("An archetype cannot silently change its capabilities");
            _versions.Add(key,controller);_admissions.Add(key,admission);
            if(selectForNewSpawns)_current[controller.ArchetypeId]=controller;
        }
        public ControllerArtifact BindNew(string archetype)
        {if(archetype!=null&&_current.TryGetValue(archetype,out var controller))return controller;if(_current.TryGetValue("fallback",out controller))return controller;throw new InvalidOperationException("Registry has no admitted safe fallback");}
        public bool TryRestore(string archetype,string id,int version,string hash,out ControllerArtifact controller)
        {
            controller=null;if(archetype==null||id==null)return false;
            if(!_versions.TryGetValue(Key(archetype,id,version),out var stored)||stored.Hash!=hash)return false;
            controller=stored;return true;
        }
        public ControllerAdmission Admission(ControllerArtifact controller) => _admissions[Key(controller.ArchetypeId,controller.ControllerId,controller.Version)];
        public IReadOnlyList<ControllerArtifact> Versions => new ReadOnlyCollection<ControllerArtifact>(_versions.Values.ToArray());
    }
}
