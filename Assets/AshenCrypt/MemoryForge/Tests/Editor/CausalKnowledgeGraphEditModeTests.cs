using System;
using System.Linq;
using NUnit.Framework;

namespace AshenCrypt.MemoryForge.Tests
{
    /// <summary>Unity 6 native EditMode contract over the real MemoryForge assembly.</summary>
    public sealed class CausalKnowledgeGraphEditModeTests
    {
        private static readonly string DeclaredSourceRevision = new string('a', 40);

        [Test]
        public void CampConsequenceWhyPathIsStableAndCannotMutateArchive()
        {
            var archive = new MemoryArchive();
            var theft = archive.Append(MemoryKind.Theft, "player", "mara", 9, 8000, false,
                "camp.theft", new RelationshipChange());
            var decision = archive.Append(MemoryKind.Quest, "player", "mara", 9, 4000, true,
                "camp.decision.stolen", new RelationshipChange(), theft.Id);
            string saved = archive.Save();

            var graph = CausalKnowledgeGraph.Build(archive, DeclaredSourceRevision);
            CollectionAssert.AreEqual(new[]
            {
                "action:" + theft.Id, "event:" + theft.Id, "event:" + decision.Id
            }, graph.Why("action:" + theft.Id, "event:" + decision.Id));
            Assert.AreEqual(CausalKnowledgeGraph.Schema, graph.Contract);
            Assert.AreEqual(DeclaredSourceRevision, graph.SourceRevision);
            Assert.IsTrue(graph.Edges.All(edge => edge.ValidationStatus == "SOURCE_ONLY"));
            Assert.AreEqual(64, graph.Digest.Length);
            Assert.AreEqual(saved, archive.Save(), "Graph inspection must not write canonical save bytes");

            var again = CausalKnowledgeGraph.Build(archive, DeclaredSourceRevision);
            Assert.AreEqual(graph.CanonicalText, again.CanonicalText);
            Assert.AreEqual(graph.Digest, again.Digest);
        }

        [Test]
        public void NpcKnowledgeRequiresActualForgeAcquisitionAndSurvivesSaveLoad()
        {
            var registry = ForgeDefaults.CreateRegistry();
            var archive = new MemoryArchive();
            archive.Spawn("mara", "merchant", registry);
            var incident = archive.Append(MemoryKind.Theft, "player", "mara", 8, 8000, false,
                "camp.theft", new RelationshipChange());

            var before = CausalKnowledgeGraph.Build(archive, DeclaredSourceRevision);
            Assert.IsFalse(before.HasNode("npc:mara"), "Spawning alone must not invent observed knowledge");
            Assert.IsEmpty(before.Why("event:" + incident.Id, "npc:mara"));

            Assert.IsTrue(archive.Learn("mara", incident.Id, KnowledgeOrigin.Self, "mara", 8));
            string saved = archive.Save();
            var learned = CausalKnowledgeGraph.Build(archive, DeclaredSourceRevision);

            Assert.IsTrue(learned.HasEdge("event:" + incident.Id, "knowledge:1", "learned-self"));
            Assert.IsTrue(learned.HasEdge("knowledge:1", "npc:mara", "known-by"));
            CollectionAssert.AreEqual(new[]
            {
                "action:" + incident.Id, "event:" + incident.Id, "knowledge:1", "npc:mara"
            }, learned.Why("action:" + incident.Id, "npc:mara"));
            Assert.AreNotEqual(before.Digest, learned.Digest);
            Assert.AreEqual(saved, archive.Save(), "Read-only graph must not change source archive");
            Assert.IsTrue(MemoryArchive.TryLoad(saved, registry, out var restored));
            Assert.AreEqual(saved, restored.Save(), "Canonical Forge save must stay byte-identical");
            Assert.AreEqual(learned.Digest, CausalKnowledgeGraph.Build(restored, DeclaredSourceRevision).Digest);
        }

        [Test]
        public void MissingEvidenceAndUnboundRevisionAreRejected()
        {
            var archive = new MemoryArchive();
            var graph = CausalKnowledgeGraph.Build(archive, DeclaredSourceRevision);
            Assert.IsEmpty(graph.Nodes);
            Assert.IsEmpty(graph.Edges);
            Assert.IsEmpty(graph.Why("action:1", "npc:mara"));
            Assert.Throws<ArgumentException>(() => CausalKnowledgeGraph.Build(archive, "not-a-full-sha"));
        }
    }
}
