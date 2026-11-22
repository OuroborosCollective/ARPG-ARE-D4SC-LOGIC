using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public enum ChronicleOrigin { Performed, Heard }

    /// <summary>Read-only projection of a verified canonical event or a stored hearing reference.</summary>
    public sealed class ChronicleEntry
    {
        public ulong EventId { get; }
        public ulong Tick { get; }
        public string TextKey { get; }
        public ChronicleOrigin Origin { get; }
        public string SourceNpcId { get; }
        public KnowledgeOrigin KnowledgeOrigin { get; }
        public ulong KnowledgeSequence { get; }

        internal ChronicleEntry(MemoryEvent ev, string key, ChronicleOrigin origin,
                                string speaker = "", KnowledgeOrigin sourceOrigin = KnowledgeOrigin.Self,
                                ulong sequence = 0)
        {
            EventId = ev.Id; Tick = ev.Tick; TextKey = key; Origin = origin;
            SourceNpcId = speaker; KnowledgeOrigin = sourceOrigin; KnowledgeSequence = sequence;
        }
    }

    /// <summary>
    /// Player-visible view. It never treats every archive entry as player knowledge.
    /// An accepted player action can be known directly; a claim must have been spoken in
    /// a Core-validated encounter and stored as a bounded reference in the original owner.
    /// </summary>
    public static class LivingChronicle
    {
        public const string Contract = "ashen.living-chronicle.v1";
        private const int MaxHeard = 64;
        private const string SaveSection = "chronicle.v1";

        private sealed class Hearing
        {
            internal readonly ulong EventId, Sequence;
            internal readonly string Speaker;
            internal Hearing(ulong eventId, ulong sequence, string speaker)
            { EventId = eventId; Sequence = sequence; Speaker = speaker; }
        }

        /// <summary>Explicitly supported pre-existing game events; no client-authored text.</summary>
        private static string TextKey(MemoryEvent ev)
        {
            if (ev.Actor != "player") return null;
            switch (ev.Context)
            {
                case "camp.theft": return "chronicle.camp.theft";
                case "camp.donation": return "chronicle.camp.donation";
                case "camp.decision.stolen": return "chronicle.camp.decision.stolen";
                case "camp.decision.replaced": return "chronicle.camp.decision.replaced";
                default:
                    string key = "chronicle." + ev.Context;
                    return ChronicleFadenwerkBinding.DialogueId(key) != null ? key : null;
            }
        }

        private static bool News(MemoryEvent ev) =>
            ev.Actor == "player" && (ev.Context == "camp.theft" || ev.Context == "camp.donation");

        private static string Number(ulong value) => value.ToString(CultureInfo.InvariantCulture);

        private static bool CanonicalNumber(string text, out ulong value)
        {
            value = 0;
            return !string.IsNullOrEmpty(text) &&
                   ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
                   Number(value) == text && value > 0;
        }

        private static string Encode(IReadOnlyList<Hearing> items) =>
            "1|" + string.Join(";", items.OrderBy(x => x.EventId)
                .ThenBy(x => x.Sequence).Select(x =>
                    Number(x.EventId) + "," + Number(x.Sequence) + "," + x.Speaker));

        private static bool Decode(CharacterProgression p, out List<Hearing> items)
        {
            items = new List<Hearing>();
            if (!p.TryGetSection(SaveSection, out var token)) return true;
            if (token == null || !token.StartsWith("1|", StringComparison.Ordinal)) return false;
            string body = token.Substring(2);
            if (body.Length == 0 || body.Length > 12000) return false;
            var archive = p.Social.Forge?.Archive;
            if (archive == null) return false;
            ulong priorEvent = 0;
            foreach (var chunk in body.Split(';'))
            {
                var parts = chunk.Split(',');
                if (parts.Length != 3 || !CanonicalNumber(parts[0], out ulong id) ||
                    !CanonicalNumber(parts[1], out ulong sequence) ||
                    string.IsNullOrEmpty(parts[2]) || parts[2].Length > 128 ||
                    id <= priorEvent || sequence > (ulong)archive.Acquisitions.Count ||
                    id > (ulong)archive.Events.Count) return false;
                var known = archive.Acquisitions[(int)sequence - 1];
                if (known.Event.Id != id || known.Knower != parts[2] ||
                    !News(known.Event) || (known.Origin != KnowledgeOrigin.Self &&
                    known.Origin != KnowledgeOrigin.Witnessed &&
                    known.Origin != KnowledgeOrigin.Reported)) return false;
                items.Add(new Hearing(id, sequence, parts[2]));
                priorEvent = id;
                if (items.Count > MaxHeard) return false;
            }
            return Encode(items) == token;
        }

        /// <summary>
        /// Called from an already accepted, range-gated Core greeting only.
        /// Never invokes Archive.Report, advances a tick or creates a new memory event.
        /// </summary>
        internal static KnownMemory HearAtRealGreeting(CharacterProgression p, NpcMind npc)
        {
            if (p == null || npc == null || p.Social.Forge == null) return null;
            var archive = p.Social.Forge.Archive;
            ulong tick = p.Social.Forge.ProcessedTicks;
            var known = npc.Relevant(tick).Where(k => News(k.Event) &&
                (k.IsFact || k.Origin == KnowledgeOrigin.Reported) &&
                npc.Cognition.Recall(k.Event.Id, tick) != null)
                .OrderByDescending(k => k.Event.Id).FirstOrDefault();
            if (known == null || npc.Knowledge(known.Event.Id)?.Sequence != known.Sequence)
                return null;
            if (!Decode(p, out var heard)) throw new InvalidOperationException("Invalid checked chronicle references");
            if (!heard.Any(x => x.EventId == known.Event.Id) && heard.Count < MaxHeard)
            {
                heard.Add(new Hearing(known.Event.Id, known.Sequence, npc.Id));
                p.SetSection(SaveSection, Encode(heard));
            }
            // No unreviewed Core text. Unity resolves the whitelisted key
            // using the signed Fadenwerk publication only.
            return known;
        }

        /// <summary>All visible entries are references to actual canonical events, bounded and tick-sorted.</summary>
        public static IReadOnlyList<ChronicleEntry> VisibleToPlayer(CharacterProgression p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (!Decode(p, out var heard)) throw new InvalidOperationException("Corrupt chronicle reference");
            var archive = p.Social.Forge?.Archive;
            if (archive == null) return Array.Empty<ChronicleEntry>();
            var result = new List<ChronicleEntry>();
            foreach (var ev in archive.Events)
            {
                string key = TextKey(ev);
                if (key != null) result.Add(new ChronicleEntry(ev, key, ChronicleOrigin.Performed));
            }
            foreach (var receipt in heard)
            {
                var known = archive.Acquisitions[(int)receipt.Sequence - 1];
                // Hearing is provenance of what the speaker said, NOT confirmation of its truth.
                result.Add(new ChronicleEntry(known.Event, TextKey(known.Event),
                    ChronicleOrigin.Heard, receipt.Speaker, known.Origin, known.Sequence));
            }
            return new ReadOnlyCollection<ChronicleEntry>(
                result.OrderBy(x => x.Tick).ThenBy(x => x.EventId)
                    .ThenBy(x => x.Origin).ThenBy(x => x.KnowledgeSequence).ToArray());
        }

        internal static bool Restore(CharacterProgression p) => Decode(p, out _);
    }

    public sealed partial class CharacterProgression
    {
        internal bool RestoreChronicleSection() => LivingChronicle.Restore(this);
    }
}
