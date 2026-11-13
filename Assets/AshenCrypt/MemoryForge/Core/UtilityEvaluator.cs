using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AshenCrypt.MemoryForge
{
    public readonly struct ActionScore
    {
        public ForgeAction Action { get; }
        public bool Legal { get; }
        public int Value { get; }
        public string Contributions { get; }
        internal ActionScore(ForgeAction action,bool legal,int value,string contributions){Action=action;Legal=legal;Value=value;Contributions=contributions;}
    }
    public sealed class Decision
    {
        public ForgeAction Action { get; }
        public int Score { get; }
        public IReadOnlyList<ActionScore> Priorities { get; }
        public string Reason { get; }
        public string CanonicalTrace { get; }
        internal Decision(ForgeAction action,int score,IReadOnlyList<ActionScore> priorities,string reason)
        {
            Action=action;Score=score;Priorities=priorities;Reason=reason;
            CanonicalTrace=ContractText.Number((long)action)+":"+ContractText.Number(score)+":"+reason+"|"+string.Join("|",priorities.Select(p=>ContractText.Number((long)p.Action)+":"+(p.Legal?"1":"0")+":"+ContractText.Number(p.Value)+":"+p.Contributions));
        }
    }
    /// <summary>Host-owned, persisted decision continuity. Ticks are supplied by the one host simulation.</summary>
    public sealed class DecisionState
    {
        public ForgeAction Action { get; internal set; }=ForgeAction.None;
        public ulong StartedTick { get; internal set; }
        public ulong NextDecisionTick { get; internal set; }
        public ulong LastTick { get; internal set; }
        public Decision LastDecision { get; internal set; }
    }
    public static class UtilityEvaluator
    {
        public static Decision Evaluate(ControllerArtifact controller,ObservationFrame observations)
        {
            if(controller==null||observations==null)throw new ArgumentNullException();
            var scores=new List<ActionScore>(controller.Rules.Count);ForgeAction chosen=ForgeAction.None;int maximum=int.MinValue;
            foreach(var rule in controller.Rules)
            {
                bool legal=ActionSet.Contains(observations.LegalActions&controller.Archetype.AllowedActions,rule.Action);
                if(!legal){scores.Add(new ActionScore(rule.Action,false,0,"forbidden"));continue;}
                int score=rule.BasePriority;string contributions="base="+ContractText.Number(score);
                for(int i=0;i<(int)Feature.Count;i++)
                {
                    int weight=rule.Weight((Feature)i);if(weight==0)continue;
                    int value=observations[(Feature)i];int term=FixedPoint.Multiply(weight,value);score=checked(score+term);
                    contributions+=","+ContractText.Number(i)+"="+ContractText.Number(value)+"*"+ContractText.Number(weight)+"->"+ContractText.Number(term);
                }
                scores.Add(new ActionScore(rule.Action,true,score,contributions));
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
            state.NextDecisionTick=checked(tick+(ulong)controller.DecisionInterval);state.LastDecision=candidate;return candidate;
        }
    }
}
