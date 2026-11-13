using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace AshenCrypt.MemoryForge
{
    public readonly struct ActionScore
    {
        public ForgeAction Action { get; }
        public bool Legal { get; }
        public int Value { get; }
        private readonly UtilityRule _rule;
        private readonly ObservationFrame _observations;
        private readonly int? _neural;
        public string Contributions
        {
            get
            {
                if(!Legal)return "forbidden";
                var text=new StringBuilder("base=").Append(ContractText.Number(_rule.BasePriority));
                for(int i=0;i<(int)Feature.Count;i++)
                {
                    int weight=_rule.Weight((Feature)i);if(weight==0)continue;int value=_observations[(Feature)i];
                    text.Append(',').Append(ContractText.Number(i)).Append('=').Append(ContractText.Number(value)).Append('*').Append(ContractText.Number(weight)).Append("->").Append(ContractText.Number(FixedPoint.Multiply(weight,value)));
                }
                if(_neural.HasValue)text.Append(",neural=").Append(ContractText.Number(_neural.Value));
                return text.ToString();
            }
        }
        internal ActionScore(ForgeAction action,bool legal,int value,UtilityRule rule,ObservationFrame observations,int? neural=null)
        {Action=action;Legal=legal;Value=value;_rule=rule;_observations=observations;_neural=neural;}
    }
    public sealed class Decision
    {
        public ForgeAction Action { get; }
        public int Score { get; }
        public IReadOnlyList<ActionScore> Priorities { get; }
        public string Reason { get; }
        private string _canonicalTrace;
        public string CanonicalTrace
        {
            get
            {
                if(_canonicalTrace!=null)return _canonicalTrace;
                var text=new StringBuilder().Append(ContractText.Number((long)Action)).Append(':').Append(ContractText.Number(Score)).Append(':').Append(Reason).Append('|');
                bool first=true;
                foreach(var p in Priorities)
                {
                    if(!first)text.Append('|');first=false;
                    text.Append(ContractText.Number((long)p.Action)).Append(':').Append(p.Legal?'1':'0').Append(':').Append(ContractText.Number(p.Value)).Append(':').Append(p.Contributions);
                }
                return _canonicalTrace=text.ToString();
            }
        }
        internal Decision(ForgeAction action,int score,IReadOnlyList<ActionScore> priorities,string reason)
        {
            Action=action;Score=score;Priorities=priorities;Reason=reason;
        }
    }
    /// <summary>Host-owned, persisted decision continuity. Ticks are supplied by the one host simulation.</summary>
    public sealed class DecisionState
    {
        public ForgeAction Action { get; internal set; }=ForgeAction.None;
        public ulong StartedTick { get; internal set; }
        public ulong NextDecisionTick { get; internal set; }
        public ulong LastTick { get; internal set; }
        public ulong EvaluatedTick { get; internal set; }
        public ObservationFrame LastObservations { get; internal set; }
        public Decision LastDecision { get; internal set; }
    }
    public static class UtilityEvaluator
    {
        public static Decision Evaluate(ControllerArtifact controller,ObservationFrame observations)
        {
            if(controller==null||observations==null)throw new ArgumentNullException();
            var scores=new List<ActionScore>(controller.Rules.Count);ForgeAction chosen=ForgeAction.None;int maximum=int.MinValue;
            var neural=controller.Network?.Evaluate(observations,observations.LegalActions&controller.Archetype.AllowedActions);
            foreach(var rule in controller.Rules)
            {
                bool legal=ActionSet.Contains(observations.LegalActions&controller.Archetype.AllowedActions,rule.Action);
                if(!legal){scores.Add(new ActionScore(rule.Action,false,0,null,null));continue;}
                int score=rule.BasePriority;
                for(int i=0;i<(int)Feature.Count;i++)
                {
                    int weight=rule.Weight((Feature)i);if(weight==0)continue;
                    int value=observations[(Feature)i];int term=FixedPoint.Multiply(weight,value);score=checked(score+term);
                }
                if(neural!=null)score=checked(score+neural[(int)rule.Action]);
                scores.Add(new ActionScore(rule.Action,true,score,rule,observations,neural==null?(int?)null:neural[(int)rule.Action]));
                // Rules have a unique, versioned order. Strict greater-than preserves ties.
                if(chosen==ForgeAction.None||score>maximum){maximum=score;chosen=rule.Action;}
            }
            return new Decision(chosen,chosen==ForgeAction.None?0:maximum,new ReadOnlyCollection<ActionScore>(scores),chosen==ForgeAction.None?"no-legal-action":"highest-utility");
        }
        public static Decision Decide(ControllerArtifact controller,ObservationFrame observations,DecisionState state,ulong tick,bool eventChanged=false)
        {
            if(state==null)throw new ArgumentNullException(nameof(state));if(tick<state.LastTick)throw new ArgumentException("Simulation tick moved backwards");
            bool stillLegal=ActionSet.Contains(observations.LegalActions&controller.Archetype.AllowedActions,state.Action);
            state.LastTick=tick;
            if(stillLegal&&tick<state.NextDecisionTick&&!eventChanged&&state.LastDecision!=null)return state.LastDecision;
            var candidate=Evaluate(controller,observations);
            if(stillLegal&&candidate.Action!=state.Action)
            {
                var current=candidate.Priorities.FirstOrDefault(p=>p.Action==state.Action);
                if(tick-state.StartedTick<(ulong)controller.MinimumDuration||candidate.Score-current.Value<controller.Hysteresis)
                    candidate=new Decision(state.Action,current.Value,candidate.Priorities,tick-state.StartedTick<(ulong)controller.MinimumDuration?"minimum-duration":"hysteresis");
            }
            if(candidate.Action!=state.Action){state.Action=candidate.Action;state.StartedTick=tick;}
            state.NextDecisionTick=checked(tick+(ulong)controller.DecisionInterval);state.EvaluatedTick=tick;
            state.LastObservations=observations;state.LastDecision=candidate;return candidate;
        }
    }
}
