using System;
using System.Collections.Generic;
using System.IO;

namespace AshenCrypt.MemoryForge
{
    public enum RecallStage { Episode, Gist, Fact }

    /// <summary>Sensory words belong to a perceiver, never to the immutable world or quest ledger.</summary>
    public sealed class EpisodeDetails
    {
        public string Category { get; }
        public string Place { get; }
        public string Detail { get; }
        public EpisodeDetails(string category, string place, string detail)
        {
            Category = Key(category); Place = Key(place); Detail = Key(detail);
        }
        private static string Key(string value)
        {
            if (value == null || value.Length > 48) throw new ArgumentException("Invalid sensory key");
            return value.Length == 0 ? value : ContractText.Id(value);
        }
    }

    public sealed class PersonalRecall
    {
        public ulong EventId { get; }
        public string Subject { get; }
        public string Predicate { get; }
        public string Object { get; }
        public KnowledgeOrigin Origin { get; }
        public string Source { get; }
        public RecallStage Stage { get; }
        public EpisodeDetails Details { get; }
        internal PersonalRecall(KnownMemory known, RecallStage stage, EpisodeDetails details)
        {
            EventId = known.Event.Id; Subject = known.Event.Actor; Object = known.Event.Target;
            Predicate = known.Event.Kind == MemoryKind.SemanticClaim ? known.Event.Context : known.Event.Kind.ToString();
            Origin = known.Origin; Source = known.Source; Stage = stage; Details = details;
        }
    }

    /// <summary>Per-identity bounded associative memory and signed, fixed-point action preferences.</summary>
    public sealed class PersonalCognition
    {
        public const ulong TicksPerDay = 14400;
        public const ulong DetailLimit = 3 * TicksPerDay;
        public const int EpisodeCapacity = 8, FactCapacity = 16, NodeCount = 4, ActionCount = 10;
        internal const int MaxAdjustment = NodeCount * 127 * 16;
        private readonly NpcMind _owner;
        private readonly List<ulong> _recent = new List<ulong>();
        private readonly List<KnownMemory> _facts = new List<KnownMemory>();
        private readonly SortedDictionary<ulong, Episode> _episodes = new SortedDictionary<ulong, Episode>();
        private sbyte[] _weights;
        private ulong? _lastOutcome;
        internal PersonalCognition(NpcMind owner) { _owner = owner; }
        public int FactCount => _facts.Count;
        public int EpisodeCount => _episodes.Count;
        public int Weight(ForgeAction action, int node)
        {
            if (action < ForgeAction.Wait || action > ForgeAction.Treat || node < 0 || node >= NodeCount)
                throw new ArgumentOutOfRangeException();
            return _weights == null ? 0 : _weights[(int)action * NodeCount + node];
        }
        private sealed class Episode
        {
            internal ulong Used;
            internal EpisodeDetails Words;
        }

        // Positive need, threat and affiliation signals can coexist. All multiplications are bounded.
        private static int Activation(ObservationFrame f, int node)
        {
            switch (node)
            {
                case 0: return (3 * f[Feature.Hunger] + f[Feature.Food]) / 4;
                case 1: return Math.Max(f[Feature.Fatigue], Math.Max(f[Feature.Injury], f[Feature.TargetInjury]));
                case 2: return Math.Max(f[Feature.Danger], Math.Max(f[Feature.Fear], f[Feature.Hostility]));
                default: return Math.Max(f[Feature.AllyDistance], Math.Max(f[Feature.Trust], f[Feature.Gratitude]));
            }
        }
        internal short[] Project(ObservationFrame f)
        {
            if (_weights == null) return null;
            short[] result = null;
            for (int a = 0; a < ActionCount; a++)
            {
                if (!ActionSet.Contains(f.LegalActions & _owner.Controller.Archetype.AllowedActions, (ForgeAction)a)) continue;
                int sum = 0;
                for (int n = 0; n < NodeCount; n++)
                    sum += FixedPoint.Multiply(16 * _weights[a * NodeCount + n], Activation(f, n));
                if (sum == 0) continue;
                if (result == null) result = new short[ActionCount];
                result[a] = (short)sum;
            }
            return result;
        }

        // The host calls this after observing the effect of the action it actually executed.
        public bool RecordOutcome(ForgeAction executed, ulong tick, int effect)
        {
            var state = _owner.DecisionState;
            var before = state.ActiveObservation;
            if (before == null || effect == 0 || state.LastDecision == null || tick != state.LastTick ||
                _lastOutcome.HasValue && tick <= _lastOutcome.Value || state.Action != executed ||
                !ActionSet.Contains(before.LegalActions & _owner.Controller.Archetype.AllowedActions, executed)) return false;
            int strength = (int)Math.Min(8L, Math.Abs((long)effect));
            bool changed = false;
            for (int n = 0; n < NodeCount; n++)
            {
                int activation = Activation(before, n);
                if (activation == 0) continue;
                int step = (strength * activation + FixedPoint.Scale - 1) / FixedPoint.Scale;
                int old = Weight(executed, n);
                int next = Math.Max(-127, Math.Min(127, old + Math.Sign(effect) * step));
                if (next == old) continue;
                if (_weights == null) _weights = new sbyte[NodeCount * ActionCount];
                _weights[(int)executed * NodeCount + n] = (sbyte)next;
                changed = true;
            }
            if (changed) _lastOutcome = tick;
            return changed;
        }

        // Associative learning is separate from action credit. Only direct, accepted contact
        // pairs a perceived social signal with approach or avoidance. A report has no sensory reward.
        internal void ConditionOnContact(KnownMemory k, KnownMemory previous)
        {
            if (!k.IsFact || k.Event.Actor == _owner.Id) return;
            int gain = k.Event.Impact[RelationshipAxis.Gratitude];
            int harm = k.Event.Impact[RelationshipAxis.Hostility];
            int credibility = k.SocialWeight - (previous != null && previous.IsFact ? previous.SocialWeight : 0);
            if (credibility <= 0) return;
            if (gain > 0) Associate(ForgeAction.Observe, 3, FixedPoint.Multiply(gain, credibility));
            if (harm > 0)
            {
                Associate(ForgeAction.Observe, 3, -FixedPoint.Multiply(harm, credibility));
                Associate(_owner.Controller.ArchetypeId == "guard" ? ForgeAction.Attack : ForgeAction.Flee, 2, FixedPoint.Multiply(harm, credibility));
            }
        }
        private void Associate(ForgeAction action, int node, int signal)
        {
            if (signal == 0 || !ActionSet.Contains(_owner.Controller.Archetype.AllowedActions, action)) return;
            if (_weights == null) _weights = new sbyte[ActionCount * NodeCount];
            int slot = (int)action * NodeCount + node;
            int delta = Math.Sign(signal) * Math.Min(8, (Math.Abs(signal) + 249) / 250);
            _weights[slot] = (sbyte)Math.Max(-127, Math.Min(127, _weights[slot] + delta));
        }

        private static int Rank(KnownMemory k) => (k.Event.Essential ? 20000 : 0) +
            FixedPoint.Multiply(k.Event.Importance, FixedPoint.Multiply(k.Credibility, k.PersonalImpact));
        private static bool SameFact(KnownMemory a, KnownMemory b) =>
            a.Event.Kind == b.Event.Kind && a.Event.Actor == b.Event.Actor && a.Event.Target == b.Event.Target &&
            (a.Event.Kind != MemoryKind.SemanticClaim && a.Event.Kind != MemoryKind.Quest || a.Event.Context == b.Event.Context);
        internal void Index(KnownMemory k)
        {
            ulong id = k.Event.Id;
            _recent.Remove(id);
            _facts.RemoveAll(f => f.Event.Id == id);
            // An accepted acquisition replaces the provenance. Only newly supplied senses
            // may accompany it; an old report must never turn into a witnessed detail.
            _episodes.Remove(id);
            if (k.Origin == KnowledgeOrigin.Refuted) return;
            _recent.Add(id);
            if (_recent.Count > EpisodeCapacity) _recent.RemoveAt(0);
            if (!k.Event.Essential && Rank(k) < 2000) return;
            int same = _facts.FindIndex(f => SameFact(f, k));
            if (same >= 0)
            {
                if (Rank(_facts[same]) > Rank(k)) return;
                _facts.RemoveAt(same);
            }
            _facts.Add(k);
            _facts.Sort((a,b) => Rank(a) != Rank(b) ? Rank(b).CompareTo(Rank(a)) : b.Event.Id.CompareTo(a.Event.Id));
            if (_facts.Count > FactCapacity) _facts.RemoveAt(FactCapacity);
        }

        internal void Remember(KnownMemory known, EpisodeDetails words, ulong tick)
        {
            if (words == null || tick - known.Event.Tick >= DetailLimit ||
                words.Category.Length == 0 && words.Place.Length == 0 && words.Detail.Length == 0) return;
            if (!_episodes.ContainsKey(known.Event.Id) && _episodes.Count == EpisodeCapacity)
            {
                ulong victim = 0, oldest = ulong.MaxValue;
                foreach (var pair in _episodes)
                    if (victim == 0 || pair.Value.Used < oldest) { victim = pair.Key; oldest = pair.Value.Used; }
                _episodes.Remove(victim);
            }
            _episodes[known.Event.Id] = new Episode { Used = tick, Words = words };
        }
        private static ulong Deadline(ulong tick, ulong interval) => tick > ulong.MaxValue - interval ? ulong.MaxValue : tick + interval;
        private static bool Detailed(EpisodeDetails words) => words.Place.Length != 0 || words.Detail.Length != 0;
        internal ulong NextExpiry()
        {
            ulong next = ulong.MaxValue;
            foreach (var pair in _episodes)
            {
                next = Math.Min(next, Deadline(_owner.Knowledge(pair.Key).Event.Tick, DetailLimit));
                if (Detailed(pair.Value.Words)) next = Math.Min(next, Deadline(pair.Value.Used, TicksPerDay));
            }
            return next;
        }
        internal void Age(ulong tick)
        {
            List<ulong> expired = null;
            foreach (var pair in _episodes)
            {
                var e = pair.Value;
                if (tick >= Deadline(_owner.Knowledge(pair.Key).Event.Tick, DetailLimit))
                { if (expired == null) expired = new List<ulong>(); expired.Add(pair.Key); }
                else if (tick >= Deadline(e.Used, TicksPerDay) && Detailed(e.Words))
                {
                    if (e.Words.Category.Length == 0) { if (expired == null) expired = new List<ulong>(); expired.Add(pair.Key); }
                    else e.Words = new EpisodeDetails(e.Words.Category, "", "");
                }
            }
            if (expired != null) foreach (ulong id in expired) _episodes.Remove(id);
        }
        public PersonalRecall Recall(ulong id, ulong tick)
        {
            var k = _owner.Knowledge(id);
            if (k == null || k.Origin == KnowledgeOrigin.Refuted || tick < k.LearnedTick) return null;
            if (tick - k.Event.Tick < DetailLimit)
            {
                if (_episodes.TryGetValue(id, out var e) && tick >= e.Used)
                {
                    var words = e.Words;
                    if (Detailed(words) && tick - e.Used < TicksPerDay) return new PersonalRecall(k, RecallStage.Episode, words);
                    if (words.Category.Length != 0) return new PersonalRecall(k, RecallStage.Gist, new EpisodeDetails(words.Category, "", ""));
                }
                if (_recent.Contains(id)) return new PersonalRecall(k, RecallStage.Fact, null);
            }
            return _facts.Exists(f => f.Event.Id == id) ? new PersonalRecall(k, RecallStage.Fact, null) : null;
        }
        internal void Rehearse(ulong id, ulong tick)
        {
            // A later use can only sustain words that still exist, never recover forgotten ones.
            if (_episodes.TryGetValue(id, out var e) && tick >= e.Used) e.Used = tick;
        }
        public PersonalRecall SocialRecall(string actor, ulong tick)
        {
            foreach (var fact in _facts)
                if ((fact.IsFact || fact.Origin == KnowledgeOrigin.Reported) && fact.Event.Actor == actor && (fact.Event.Kind == MemoryKind.Help ||
                    fact.Event.Kind == MemoryKind.Assault || fact.Event.Kind == MemoryKind.Theft))
                    return Recall(fact.Event.Id, tick);
            return null;
        }
        internal bool HasState => _weights != null || _episodes.Count != 0;
        public bool IsCurrentAt(ulong tick) => (!_lastOutcome.HasValue || _lastOutcome.Value <= tick) && (_episodes.Count == 0 || NextExpiry() > tick) &&
            System.Linq.Enumerable.All(_episodes.Values, e => e.Used <= tick);

        internal void WriteState(BinaryWriter w)
        {
            w.Write(_weights != null);
            if (_weights != null)
            {
                w.Write(_lastOutcome.HasValue);
                if (_lastOutcome.HasValue) w.Write(_lastOutcome.Value);
                foreach (sbyte weight in _weights) w.Write(weight);
            }
            w.Write((byte)_episodes.Count);
            foreach (var pair in _episodes)
            {
                w.Write(pair.Key); w.Write(pair.Value.Used);
                w.Write(pair.Value.Words.Category); w.Write(pair.Value.Words.Place); w.Write(pair.Value.Words.Detail);
            }
        }
        internal void ReadState(BinaryReader r)
        {
            if (r.ReadBoolean())
            {
                if (r.ReadBoolean()) _lastOutcome = r.ReadUInt64();
                if (_lastOutcome.HasValue && _owner.DecisionState.LastDecision != null && _lastOutcome.Value > _owner.DecisionState.LastTick)
                    throw new InvalidDataException("Outcome after last action");
                _weights = new sbyte[NodeCount * ActionCount];
                for (int i = 0; i < _weights.Length; i++)
                {
                    _weights[i] = r.ReadSByte();
                    if (_weights[i] == sbyte.MinValue) throw new InvalidDataException("Weight out of range");
                }
            }
            int count = r.ReadByte();
            if (count > EpisodeCapacity) throw new InvalidDataException("Episode capacity exceeded");
            for (int i = 0; i < count; i++)
            {
                ulong id = r.ReadUInt64(), used = r.ReadUInt64();
                var k = _owner.Knowledge(id);
                if (k == null || k.Origin == KnowledgeOrigin.Refuted || used < k.LearnedTick || used - k.Event.Tick >= DetailLimit)
                    throw new InvalidDataException("Invalid personal episode");
                var words = new EpisodeDetails(r.ReadString(), r.ReadString(), r.ReadString());
                if (words.Category.Length == 0 && !Detailed(words)) throw new InvalidDataException("Empty sensory record");
                if (!k.IsFact && (words.Place.Length != 0 || words.Detail.Length != 0))
                    throw new InvalidDataException("Hearsay has sensory details");
                _episodes.Add(id, new Episode { Used = used, Words = words });
            }
        }
    }

    public sealed partial class MemoryArchive
    {
        private ulong _nextPersonalExpiry = ulong.MaxValue;
        public void AdvancePersonalTime(ulong tick)
        {
            if (tick < _nextPersonalExpiry) return;
            _nextPersonalExpiry = ulong.MaxValue;
            foreach (var mind in _minds.Values)
            {
                mind.Cognition.Age(tick);
                _nextPersonalExpiry = Math.Min(_nextPersonalExpiry, mind.Cognition.NextExpiry());
            }
        }
        public bool Perceive(string knower, ulong id, KnowledgeOrigin origin, ulong tick, EpisodeDetails details, int personal = 10000)
        {
            if (origin != KnowledgeOrigin.Self && origin != KnowledgeOrigin.Witnessed) throw new ArgumentException("Direct perception required");
            AdvancePersonalTime(tick);
            var mind = Get(knower);
            var previous = mind?.Knowledge(id);
            if (!Learn(knower, id, origin, knower, tick, personal)) return false;
            var accepted = mind.Knowledge(id);
            mind.Cognition.ConditionOnContact(accepted, previous);
            mind.Cognition.Remember(accepted, details, tick);
            _nextPersonalExpiry = Math.Min(_nextPersonalExpiry, mind.Cognition.NextExpiry());
            return true;
        }
        public PersonalRecall Recollect(string knower, ulong id, ulong tick)
        {
            AdvancePersonalTime(tick);
            var mind = Get(knower);
            var recall = mind?.Cognition.Recall(id, tick);
            if (recall != null) mind.Cognition.Rehearse(id, tick);
            return recall;
        }
    }
}
