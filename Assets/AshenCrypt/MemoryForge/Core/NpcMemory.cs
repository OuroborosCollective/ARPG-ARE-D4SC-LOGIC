using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AshenCrypt.MemoryForge
{
    public enum MemoryKind { Greeting, Help, Assault, Theft, Rescue, Death, Quest, Threat, Visit, SemanticClaim, CompanionPractice }
    public enum KnowledgeOrigin { Self, Witnessed, Reported, Suspected, Refuted }
    public enum RelationshipAxis { Trust, Fear, Gratitude, Affection, Respect, Mistrust, Hostility, Familiarity }
    public sealed class RelationshipChange
    {
        private readonly int[] _values;
        public RelationshipChange(int trust=0,int fear=0,int gratitude=0,int affection=0,int respect=0,int mistrust=0,int hostility=0,int familiarity=0)
        {
            _values=new[]{trust,fear,gratitude,affection,respect,mistrust,hostility,familiarity};
            if(_values.Any(v=>Math.Abs((long)v)>10000))throw new ArgumentOutOfRangeException(nameof(trust));
        }
        public int this[RelationshipAxis axis] => _values[(int)axis];
        internal string Canonical => string.Join(",",_values.Select(v=>ContractText.Number(v)));
    }
    public sealed class Relationship
    {
        private readonly long[] _raw=new long[8];
        public int this[RelationshipAxis axis] => (int)Math.Max(axis==RelationshipAxis.Trust||axis==RelationshipAxis.Affection||axis==RelationshipAxis.Respect?-10000:0,Math.Min(10000,_raw[(int)axis]));
        public int Trust=>this[RelationshipAxis.Trust];public int Fear=>this[RelationshipAxis.Fear];public int Gratitude=>this[RelationshipAxis.Gratitude];
        public int Hostility=>this[RelationshipAxis.Hostility];public int Mistrust=>this[RelationshipAxis.Mistrust];public int Familiarity=>this[RelationshipAxis.Familiarity];
        internal void Apply(RelationshipChange change,int weight,int direction)
        {for(int i=0;i<8;i++)_raw[i]=checked(_raw[i]+(long)direction*FixedPoint.Multiply(change[(RelationshipAxis)i],weight));}
    }
    public sealed class MemoryEvent
    {
        public ulong Id { get; }
        public ulong Tick { get; }
        public MemoryKind Kind { get; }
        public string Actor { get; }
        public string Target { get; }
        public string Context { get; }
        public int Importance { get; }
        public bool Essential { get; }
        public RelationshipChange Impact { get; }
        public string Checksum { get; }
        internal string Canonical=>ContractText.Number(Id)+","+ContractText.Number(Tick)+","+ContractText.Number((long)Kind)+","+Actor+","+Target+","+Context+","+ContractText.Number(Importance)+","+(Essential?"1":"0")+","+Impact.Canonical;
        internal MemoryEvent(ulong id,MemoryKind kind,string actor,string target,ulong tick,int importance,bool essential,string context,RelationshipChange impact)
        {
            if(id==0||kind<MemoryKind.Greeting||kind>MemoryKind.CompanionPractice||importance<0||importance>10000)throw new ArgumentException("Invalid memory event");
            Id=id;Kind=kind;Actor=ContractText.Id(actor);Target=ContractText.Id(target);Tick=tick;Importance=importance;Essential=essential;Context=ContractText.Id(context);Impact=impact??throw new ArgumentNullException(nameof(impact));Checksum=ContractText.Hash(Canonical);
        }
    }
    public sealed class KnownMemory
    {
        public ulong Sequence { get; }
        public MemoryEvent Event { get; }
        public string Knower { get; }
        public KnowledgeOrigin Origin { get; }
        public string Source { get; }
        public ulong SourceSequence { get; }
        public ulong LearnedTick { get; }
        public int PersonalImpact { get; }
        public int SocialCloseness { get; }
        public int Credibility=>Origin==KnowledgeOrigin.Self?10000:Origin==KnowledgeOrigin.Witnessed?9000:Origin==KnowledgeOrigin.Reported?5000:Origin==KnowledgeOrigin.Suspected?2000:0;
        public bool IsFact=>Origin==KnowledgeOrigin.Self||Origin==KnowledgeOrigin.Witnessed;
        public string Checksum { get; }
        internal int SocialWeight=>FixedPoint.Multiply(PersonalImpact,Credibility);
        internal string Canonical=>ContractText.Number(Sequence)+","+ContractText.Number(Event.Id)+","+Knower+","+ContractText.Number((long)Origin)+","+Source+","+ContractText.Number(SourceSequence)+","+ContractText.Number(LearnedTick)+","+ContractText.Number(PersonalImpact)+","+ContractText.Number(SocialCloseness);
        internal KnownMemory(ulong sequence,MemoryEvent ev,string knower,KnowledgeOrigin origin,string source,ulong sourceSequence,ulong learnedTick,int personal,int social)
        {
            if(sequence==0||origin<KnowledgeOrigin.Self||origin>KnowledgeOrigin.Refuted||learnedTick<ev.Tick||personal<0||personal>10000||social<0||social>10000)throw new ArgumentException("Invalid knowledge provenance");
            Sequence=sequence;Event=ev;Knower=ContractText.Id(knower);Origin=origin;Source=ContractText.Id(source);SourceSequence=sourceSequence;LearnedTick=learnedTick;PersonalImpact=personal;SocialCloseness=social;Checksum=ContractText.Hash(Canonical+"|"+ev.Checksum);
        }
        public int Relevance(ulong tick,int context=10000,int repetitions=0)
        {
            if(tick<LearnedTick||context<0||context>10000||repetitions<0)throw new ArgumentOutOfRangeException(nameof(tick));
            ulong age=tick-Event.Tick;
            int retention=Event.Essential?10000:age>=36000?0:10000-(int)(age*10000/36000);
            int importance=Math.Min(10000,Event.Importance+Math.Min(repetitions,8)*250);
            int value=FixedPoint.Multiply(importance,PersonalImpact);value=FixedPoint.Multiply(value,Credibility);
            value=FixedPoint.Multiply(value,SocialCloseness);value=FixedPoint.Multiply(value,context);return FixedPoint.Multiply(value,retention);
        }
    }
    public sealed class NpcMind
    {
        public const string Contract="AshenCrypt.NpcMemory.v1";
        private readonly SortedDictionary<ulong,KnownMemory> _known=new SortedDictionary<ulong,KnownMemory>();
        private readonly SortedDictionary<string,Relationship> _relations=new SortedDictionary<string,Relationship>(StringComparer.Ordinal);
        private readonly List<ulong> _recent=new List<ulong>();
        private readonly SortedSet<ulong> _essential=new SortedSet<ulong>();
        public string Id { get; }
        public ControllerArtifact Controller { get; private set; }
        public DecisionState DecisionState { get; }=new DecisionState();
        public IReadOnlyList<KnownMemory> Known=>new ReadOnlyCollection<KnownMemory>(_known.Values.ToArray());
        public NpcMind(string id,ControllerArtifact controller){Id=ContractText.Id(id);Controller=controller??throw new ArgumentNullException(nameof(controller));}
        public Relationship RelationshipTo(string actor)
        {ContractText.Id(actor);return _relations.TryGetValue(actor,out var relationship)?relationship:new Relationship();}
        public KnownMemory Knowledge(ulong id)=>_known.TryGetValue(id,out var known)?known:null;
        internal bool Accept(KnownMemory next)
        {
            var prior=Knowledge(next.Event.Id);
            if(prior!=null)
            {
                if(prior.Origin==next.Origin)return false;
                if(next.Origin==KnowledgeOrigin.Refuted&&prior.IsFact)return false;
                if(next.Origin!=KnowledgeOrigin.Refuted&&(prior.Origin==KnowledgeOrigin.Refuted&&!next.IsFact||next.Credibility<=prior.Credibility))return false;
            }
            if(!_relations.TryGetValue(next.Event.Actor,out var relationship))_relations.Add(next.Event.Actor,relationship=new Relationship());
            // Raw sums are retained before clamping, so retracting one rumor cannot erase another event's impact.
            if(prior!=null)relationship.Apply(prior.Event.Impact,prior.SocialWeight,-1);
            relationship.Apply(next.Event.Impact,next.SocialWeight,1);_known[next.Event.Id]=next;
            if(next.Event.Essential)_essential.Add(next.Event.Id);
            else{_recent.Remove(next.Event.Id);_recent.Add(next.Event.Id);if(_recent.Count>64)_recent.RemoveAt(0);}
            return true;
        }
        public IReadOnlyList<KnownMemory> Relevant(ulong tick,int context=10000)
        {
            var entries=_essential.Concat(_recent).Distinct().Select(id=>_known[id]).Where(k=>k.Relevance(tick,context)>0).OrderByDescending(k=>k.Relevance(tick,context)).ThenBy(k=>k.Event.Id).ToArray();
            return new ReadOnlyCollection<KnownMemory>(entries);
        }
    }
    /// <summary>Host-owned event history and individual references; no simulation clock, world knowledge or gameplay effects.</summary>
    public sealed partial class MemoryArchive
    {
        private readonly List<MemoryEvent> _events=new List<MemoryEvent>();
        private readonly List<KnownMemory> _acquisitions=new List<KnownMemory>();
        private readonly SortedDictionary<string,NpcMind> _minds=new SortedDictionary<string,NpcMind>(StringComparer.Ordinal);
        public IReadOnlyList<MemoryEvent> Events=>_events.AsReadOnly();
        public IReadOnlyList<KnownMemory> Acquisitions=>_acquisitions.AsReadOnly();
        public IReadOnlyList<NpcMind> Minds=>new ReadOnlyCollection<NpcMind>(_minds.Values.ToArray());
        public NpcMind Spawn(string id,string archetype,NpcArchetypeControllerRegistry registry)
        {
            ContractText.Id(id);if(_minds.ContainsKey(id))throw new ArgumentException("NPC identity already exists; use Get to retain its bound controller");
            var mind=new NpcMind(id,registry.BindNew(archetype));_minds.Add(id,mind);return mind;
        }
        public NpcMind Get(string id)=>_minds.TryGetValue(id,out var mind)?mind:null;
        public MemoryEvent Append(MemoryKind kind,string actor,string target,ulong tick,int importance,bool essential,string context,RelationshipChange impact)
        {
            if(_events.Count>0&&tick<_events[_events.Count-1].Tick)throw new ArgumentException("Event tick moved backwards");
            var ev=new MemoryEvent(checked((ulong)_events.Count+1),kind,actor,target,tick,importance,essential,context,impact);_events.Add(ev);return ev;
        }
        public bool Learn(string knower,ulong eventId,KnowledgeOrigin origin,string source,ulong tick,int personal=10000,int social=10000)
        {
            if(origin==KnowledgeOrigin.Reported)throw new ArgumentException("Use Report to prove the source knows this event");
            return LearnCore(knower,eventId,origin,source,0,tick,personal,social);
        }
        private bool LearnCore(string knower,ulong eventId,KnowledgeOrigin origin,string source,ulong sourceSequence,ulong tick,int personal,int social)
        {
            var mind=Get(knower);if(mind==null||eventId==0||eventId>(ulong)_events.Count)return false;
            var ev=_events[(int)eventId-1];
            if(origin!=KnowledgeOrigin.Reported&&source!=knower)return false;
            if(origin==KnowledgeOrigin.Self&&knower!=ev.Target&&knower!=ev.Actor)return false;
            if(origin==KnowledgeOrigin.Refuted&&(mind.Knowledge(eventId)==null||mind.Knowledge(eventId).IsFact))return false;
            var known=new KnownMemory(checked((ulong)_acquisitions.Count+1),ev,knower,origin,source,sourceSequence,tick,personal,social);
            if(!mind.Accept(known))return false;_acquisitions.Add(known);return true;
        }
        /// <summary>The host must first validate a real encounter. This method additionally verifies knowledge and one-hop provenance.</summary>
        public bool Report(string source,string recipient,ulong eventId,ulong tick,int personal=6000,int social=10000)
        {
            var memory=Get(source)?.Knowledge(eventId);
            return source!=recipient&&memory!=null&&memory.IsFact&&tick>=memory.LearnedTick&&LearnCore(recipient,eventId,KnowledgeOrigin.Reported,source,memory.Sequence,tick,personal,social);
        }
    }
}
