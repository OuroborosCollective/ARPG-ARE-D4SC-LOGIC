using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AshenCrypt.MemoryForge
{
    /// <summary>A source-only node derived from an existing canonical Forge record.</summary>
    public sealed class CausalGraphNode
    {
        public string Id { get; }
        public string Kind { get; }
        public string ProofChecksum { get; }
        internal CausalGraphNode(string id, string kind, string checksum)
        {
            Id = id; Kind = kind; ProofChecksum = checksum;
        }
    }

    /// <summary>Never equate this source record with a verified live runtime receipt.</summary>
    public sealed class CausalGraphEdge
    {
        public string FromId { get; }
        public string ToId { get; }
        public string Relation { get; }
        public string ValidationStatus => "SOURCE_ONLY";
        public string ProofChecksum { get; }
        internal CausalGraphEdge(string from, string to, string relation, string checksum)
        {
            FromId = from; ToId = to; Relation = relation; ProofChecksum = checksum;
        }
    }

    /// <summary>
    /// Opt-in, derived, read-only view of the existing MemoryArchive. No simulation,
    /// memory, save, clock, RNG, quest, controller or Unity projection ownership.
    /// </summary>
    public sealed class CausalKnowledgeGraph
    {
        public const string Schema = "ashen.knowledge-graph.v1";
        public string Contract => Schema;
        public string SourceRevision { get; }
        public IReadOnlyList<CausalGraphNode> Nodes { get; }
        public IReadOnlyList<CausalGraphEdge> Edges { get; }
        public string CanonicalText { get; }
        public string Digest { get; }

        private CausalKnowledgeGraph(string revision, CausalGraphNode[] nodes, CausalGraphEdge[] edges)
        {
            SourceRevision = revision;
            Nodes = Array.AsReadOnly(nodes);
            Edges = Array.AsReadOnly(edges);
            var text = new StringBuilder();
            AppendFields(text, "schema", Schema, revision);
            foreach (var node in nodes)
                AppendFields(text, "node", node.Id, node.Kind, node.ProofChecksum);
            foreach (var edge in edges)
                AppendFields(text, "edge", edge.FromId, edge.Relation, edge.ToId,
                             edge.ValidationStatus, edge.ProofChecksum);
            CanonicalText = text.ToString();
            using (var hash = SHA256.Create())
                Digest = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(CanonicalText)))
                    .Replace("-", "").ToLowerInvariant();
        }

        public static CausalKnowledgeGraph Build(MemoryArchive archive, string sourceRevision)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            if (!ValidRevision(sourceRevision)) throw new ArgumentException("Expected full lowercase Git SHA", nameof(sourceRevision));
            if (archive.Events.Count > 20000 || archive.Acquisitions.Count > 40000)
                throw new InvalidOperationException("Causal graph exceeds inspection quota");

            var nodes = new SortedDictionary<string, CausalGraphNode>(StringComparer.Ordinal);
            var edges = new List<CausalGraphEdge>();
            var eventMap = new Dictionary<ulong, MemoryEvent>();

            void Node(string id, string kind, string checksum)
            {
                if (string.IsNullOrEmpty(checksum))
                    throw new InvalidOperationException("Missing provenance checksum");
                if (nodes.TryGetValue(id, out var previous))
                {
                    if (previous.Kind != kind || previous.ProofChecksum != checksum)
                        throw new InvalidOperationException("Contradictory graph identity");
                    return;
                }
                nodes.Add(id, new CausalGraphNode(id, kind, checksum));
            }

            void Edge(string from, string to, string relation, string checksum)
            {
                if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to))
                    throw new InvalidOperationException("Orphan graph reference");
                edges.Add(new CausalGraphEdge(from, to, relation, checksum));
            }

            foreach (var ev in archive.Events.OrderBy(e => e.Id))
            {
                if (ev.Id == 0 || eventMap.ContainsKey(ev.Id))
                    throw new InvalidOperationException("Noncanonical event ID");
                eventMap.Add(ev.Id, ev);
                Node(EventId(ev.Id), "canonical-event", ev.Checksum);
                bool theft = ev.Context == "camp.theft" && ev.Kind == MemoryKind.Theft;
                bool donation = ev.Context == "camp.donation" && ev.Kind == MemoryKind.Help;
                if ((theft || donation) && ev.Actor == "player" && ev.Target == "mara")
                {
                    Node(ActionId(ev.Id), "accepted-camp-action", ev.Checksum);
                    Edge(ActionId(ev.Id), EventId(ev.Id), "archived-as", ev.Checksum);
                }
            }

            foreach (var ev in archive.Events.OrderBy(e => e.Id))
            {
                if (!ev.CauseEventId.HasValue) continue;
                ulong parent = ev.CauseEventId.Value;
                if (parent == 0 || parent >= ev.Id || !eventMap.TryGetValue(parent, out var cause) ||
                    cause.Tick > ev.Tick)
                    throw new InvalidOperationException("Invalid causal parent");
                Edge(EventId(parent), EventId(ev.Id), "causes", ev.Checksum);
            }

            foreach (var known in archive.Acquisitions.OrderBy(k => k.Sequence))
            {
                if (known.Event == null || !eventMap.TryGetValue(known.Event.Id, out var ev) ||
                    ev.Checksum != known.Event.Checksum || known.LearnedTick < ev.Tick ||
                    archive.Get(known.Knower) == null)
                    throw new InvalidOperationException("Unbound knowledge provenance");
                string knowledge = "knowledge:" + Number(known.Sequence);
                Node(knowledge, "canonical-acquisition", known.Checksum);
                Edge(EventId(ev.Id), knowledge,
                     "learned-" + known.Origin.ToString().ToLowerInvariant(), known.Checksum);
                // Acquisitions are immutable history; the NPC's current knowledge is
                // the latest admitted entry. Old rumors and refutations cannot prove
                // that the NPC still knows the alleged event as a current belief.
                var current = archive.Get(known.Knower).Knowledge(ev.Id);
                if (current != null && current.Sequence == known.Sequence &&
                    current.Origin != KnowledgeOrigin.Refuted)
                {
                    string npc = "npc:" + known.Knower;
                    Node(npc, "known-by-npc", ContractText.Hash(npc));
                    Edge(knowledge, npc, "known-by", known.Checksum);
                }
            }

            var canonicalNodes = nodes.Values.ToArray();
            var canonicalEdges = edges.OrderBy(e => e.FromId, StringComparer.Ordinal)
                .ThenBy(e => e.Relation, StringComparer.Ordinal)
                .ThenBy(e => e.ToId, StringComparer.Ordinal)
                .ThenBy(e => e.ProofChecksum, StringComparer.Ordinal).ToArray();
            return new CausalKnowledgeGraph(sourceRevision, canonicalNodes, canonicalEdges);
        }

        public bool HasNode(string id) => Nodes.Any(n => n.Id == id);
        public bool HasEdge(string from, string to, string relation) =>
            Edges.Any(e => e.FromId == from && e.ToId == to && e.Relation == relation);

        /// <summary>Stable shortest proof path. Empty means that no path is supported.</summary>
        public IReadOnlyList<string> Why(string from, string to)
        {
            if (!HasNode(from) || !HasNode(to)) return Array.Empty<string>();
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal) { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (current == to)
                {
                    var path = new List<string> { to };
                    while (previous.TryGetValue(path[path.Count - 1], out var parent))
                        path.Add(parent);
                    path.Reverse();
                    return new ReadOnlyCollection<string>(path);
                }
                foreach (var next in Edges.Where(e => e.FromId == current).Select(e => e.ToId).Distinct())
                    if (visited.Add(next))
                    {
                        previous.Add(next, current);
                        queue.Enqueue(next);
                    }
            }
            return Array.Empty<string>();
        }

        private static string Number(ulong value) => value.ToString(CultureInfo.InvariantCulture);
        private static string EventId(ulong id) => "event:" + Number(id);
        private static string ActionId(ulong id) => "action:" + Number(id);
        private static bool ValidRevision(string value)
        {
            if (value == null || value.Length != 40) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
        private static void AppendFields(StringBuilder output, params string[] fields)
        {
            foreach (string field in fields)
            {
                if (field == null) throw new ArgumentException("Null graph field");
                output.Append(field.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(field);
            }
            output.Append('\n');
        }
    }
}
