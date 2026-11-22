using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>Issue #111: a read-only, source-only graph over actual Core and Forge records.</summary>
internal static class CausalKnowledgeGraphTests
{
    private const string SourceRevision = "374fb0641fc720b31e8da21e2149d75f7703ca2e";

    public static void Run(Action<bool, string> check)
    {
        var progress = CharacterProgression.CreateEntryProfile();
        progress.EnterVillage();
        progress.Social.EnableForge();
        var world = new ArenaSimulation(111UL, progress, BigInteger.One, LootCatalog.Version1);
        world.CompleteArrivalScene();
        var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        mara.Xmm = world.PlayerXmm + 1000;
        mara.Zmm = world.PlayerZmm;
        mara.InteractionCooldownTicks = 0;

        check(world.TakeCampRation().Accepted, "knowledge-graph-real-camp-action-accepted");
        var archive = progress.Social.Forge.Archive;
        var theft = archive.Events.Single(e => e.Context == "camp.theft");
        var consequence = archive.Events.Single(e => e.Context == "camp.decision.stolen");
        string saveBefore = progress.SaveString();
        ulong worldBefore = world.ComputeWorldHash();
        var graph = CausalKnowledgeGraph.Build(archive, SourceRevision);

        check(graph.SourceRevision == SourceRevision &&
              graph.Contract == "ashen.knowledge-graph.v1",
              "knowledge-graph-exact-revision-and-contract");
        check(graph.HasEdge("action:" + theft.Id, "event:" + theft.Id, "archived-as") &&
              graph.HasEdge("event:" + theft.Id, "event:" + consequence.Id, "causes"),
              "knowledge-graph-real-camp-causality-is-indexed");
        check(graph.Why("action:" + theft.Id, "event:" + consequence.Id).SequenceEqual(
              new[] { "action:" + theft.Id, "event:" + theft.Id, "event:" + consequence.Id }),
              "knowledge-graph-deterministic-why-path");
        check(graph.Edges.All(e => e.ValidationStatus == "SOURCE_ONLY") &&
              graph.Edges.All(e => !string.IsNullOrEmpty(e.ProofChecksum)),
              "knowledge-graph-source-is-not-mislabeled-runtime");
        check(progress.SaveString() == saveBefore && world.ComputeWorldHash() == worldBefore,
              "knowledge-graph-query-does-not-mutate-gameplay-or-save");

        var repeat = CausalKnowledgeGraph.Build(archive, SourceRevision);
        check(repeat.Digest == graph.Digest && repeat.CanonicalText == graph.CanonicalText &&
              repeat.Why("action:" + theft.Id, "event:" + consequence.Id)
                    .SequenceEqual(graph.Why("action:" + theft.Id, "event:" + consequence.Id)),
              "knowledge-graph-repeat-is-byte-deterministic");
        check(CharacterProgression.TryLoad(saveBefore, out var reloaded) &&
              CausalKnowledgeGraph.Build(reloaded.Social.Forge.Archive, SourceRevision).Digest == graph.Digest,
              "knowledge-graph-saved-archive-has-identical-digest");

        var empty = CausalKnowledgeGraph.Build(new MemoryArchive(), SourceRevision);
        check(!empty.HasNode("npc:mara") && !empty.HasNode("action:1") &&
              empty.Why("action:1", "npc:mara").Count == 0,
              "knowledge-graph-never-invents-an-unobserved-action-or-witness");

        // This second case is an explicit isolated test of the canonical Forge methods,
        // NOT a claim that a particular NPC reacted in the deployed game.
        var forged = new MemoryArchive();
        forged.Spawn("mara", "merchant", ForgeDefaults.CreateRegistry());
        var incident = forged.Append(MemoryKind.Theft, "player", "mara", 0, 8000, false,
                                     "camp.theft", new RelationshipChange());
        var withoutKnowledge = CausalKnowledgeGraph.Build(forged, SourceRevision);
        check(!withoutKnowledge.HasNode("npc:mara") &&
              withoutKnowledge.Why("event:" + incident.Id, "npc:mara").Count == 0,
              "knowledge-graph-unwitnessed-event-proves-no-npc-knowledge");
        check(forged.Learn("mara", incident.Id, KnowledgeOrigin.Self, "mara", 0),
              "knowledge-graph-test-acquisition-created-by-canonical-forge");
        string forgeSave = forged.Save();
        var withKnowledge = CausalKnowledgeGraph.Build(forged, SourceRevision);
        check(withKnowledge.HasEdge("event:" + incident.Id, "knowledge:1", "learned-self") &&
              withKnowledge.HasEdge("knowledge:1", "npc:mara", "known-by") &&
              withKnowledge.Why("action:" + incident.Id, "npc:mara").Count == 4,
              "knowledge-graph-accepted-acquisition-has-provenance-path");
        check(withKnowledge.Digest != withoutKnowledge.Digest && forged.Save() == forgeSave,
              "knowledge-graph-knowledge-changes-index-not-save");

        // A replaced or refuted acquisition remains historical but is NOT current NPC knowledge.
        var belief = new MemoryArchive();
        belief.Spawn("mara", "merchant", ForgeDefaults.CreateRegistry());
        var alleged = belief.Append(MemoryKind.SemanticClaim, "player", "mara", 8, 2000, false,
                                    "camp.allegation", new RelationshipChange());
        check(belief.Learn("mara", alleged.Id, KnowledgeOrigin.Suspected, "mara", 8),
              "knowledge-graph-suspected-acquisition-accepted");
        var suspicion = CausalKnowledgeGraph.Build(belief, SourceRevision);
        check(suspicion.HasEdge("knowledge:1", "npc:mara", "known-by") &&
              suspicion.HasEdge("event:" + alleged.Id, "knowledge:1", "learned-suspected"),
              "knowledge-graph-suspected-remains-labelled-not-fact");
        check(belief.Learn("mara", alleged.Id, KnowledgeOrigin.Refuted, "mara", 9),
              "knowledge-graph-refutation-accepted");
        var refuted = CausalKnowledgeGraph.Build(belief, SourceRevision);
        check(refuted.HasNode("knowledge:1") && refuted.HasNode("knowledge:2") &&
              !refuted.HasEdge("knowledge:1", "npc:mara", "known-by") &&
              !refuted.HasEdge("knowledge:2", "npc:mara", "known-by") &&
              refuted.Why("event:" + alleged.Id, "npc:mara").Count == 0,
              "knowledge-graph-refutation-cannot-pretend-current-knowledge");
        check(belief.Learn("mara", alleged.Id, KnowledgeOrigin.Self, "mara", 10),
              "knowledge-graph-fact-after-refutation-accepted");
        var confirmed = CausalKnowledgeGraph.Build(belief, SourceRevision);
        check(!confirmed.HasEdge("knowledge:1", "npc:mara", "known-by") &&
              !confirmed.HasEdge("knowledge:2", "npc:mara", "known-by") &&
              confirmed.HasEdge("knowledge:3", "npc:mara", "known-by") &&
              confirmed.Why("event:" + alleged.Id, "npc:mara")
                       .SequenceEqual(new[] { "event:" + alleged.Id, "knowledge:3", "npc:mara" }),
              "knowledge-graph-only-current-confirmed-acquisition-is-active");

        bool refused = false;
        try { CausalKnowledgeGraph.Build(forged, "unbound-revision"); }
        catch (ArgumentException) { refused = true; }
        check(refused, "knowledge-graph-invalid-revision-is-rejected");
        check(world.ComputeWorldHash() == worldBefore && progress.SaveString() == saveBefore,
              "knowledge-graph-both-real-owner-states-unchanged");
    }
}
