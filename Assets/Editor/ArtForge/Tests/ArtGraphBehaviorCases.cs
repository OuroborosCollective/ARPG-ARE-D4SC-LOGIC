using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AshenCrypt.ArtForge.Tests
{
    // The same actual contract is exercised by the console runner and Unity EditMode.
    // These are explicitly synthetic authoring fixtures, never production asset receipts.
    public static class ArtGraphBehaviorCases
    {
        static readonly KeyValuePair<string, Action>[] Cases =
        {
            Case("valid_dag_has_stable_topological_order", ValidDag),
            Case("cycle_is_rejected", Cycle),
            Case("self_cycle_is_rejected", SelfCycle),
            Case("missing_parent_is_rejected", MissingParent),
            Case("duplicate_id_is_rejected", DuplicateId),
            Case("duplicate_parent_is_rejected", DuplicateParent),
            Case("artifact_bytes_drift_is_rejected", HashDrift),
            Case("sha256_has_a_known_independent_vector", Sha256Golden),
            Case("artifact_stream_is_hashed_without_disposal", StreamHash),
            Case("owner_approval_is_rejected", ForbiddenApproval),
            Case("imported_claim_is_rejected", ForbiddenImport),
            Case("accepted_kind_is_rejected", ForbiddenAccepted),
            Case("unknown_schema_is_rejected_without_migration", UnknownSchema),
            Case("project_revision_drift_is_rejected", ProjectDrift),
            Case("graph_revision_drift_is_rejected", GraphDrift),
            Case("missing_expected_revision_is_rejected", MissingExpectedRevision),
            Case("matching_revisions_are_accepted", MatchingRevisions),
            Case("stored_revision_cannot_hide_content_change", StoredRevisionDrift),
            Case("node_and_parent_order_do_not_change_revision", OrderInvariant),
            Case("every_semantic_field_changes_revision", EveryFieldBound),
            Case("culture_does_not_change_revision", CultureInvariant),
            Case("canonical_hash_matches_sha256_of_canonical_bytes", CanonicalDigest),
            Case("documented_license_requires_evidence", LicenseEvidence),
            Case("unknown_license_is_visible_without_approval", UnknownLicense),
            Case("private_path_is_rejected", PrivatePath),
            Case("credential_url_is_rejected", CredentialUrl),
            Case("query_url_is_rejected", QueryUrl),
            Case("opaque_traversal_is_rejected", Traversal),
            Case("malformed_artifact_hash_is_rejected", MalformedHash),
            Case("malformed_source_hash_is_rejected", MalformedSourceHash),
            Case("null_node_is_rejected", NullNode),
            Case("oversized_graph_is_rejected", OversizedGraph),
            Case("empty_graph_is_a_valid_readonly_snapshot", EmptyGraph),
            Case("validation_does_not_mutate_input", InputPreserved),
            Case("schema_version_and_project_are_hash_bound", EnvelopeBound)
        };

        public static IEnumerable<string> Names
        {
            get { foreach (var item in Cases) yield return item.Key; }
        }

        public static void Run(string name)
        {
            foreach (var item in Cases) if (item.Key == name) { item.Value(); return; }
            throw new ArgumentException("Unknown contract test: " + name);
        }

        static KeyValuePair<string, Action> Case(string name, Action action) { return new KeyValuePair<string, Action>(name, action); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Equal(string expected, string actual) { Require(expected == actual, "Expected " + expected + ", observed " + actual); }
        static void Code(string expected, ArtGraphValidation result) { Require(!result.IsValid, "Malformed graph was accepted."); Equal(expected, result.Code); }

        static ArtGraphNode Node(string id, params string[] parents)
        {
            return new ArtGraphNode
            {
                id = id, label = "Fixture " + id, kind = "mesh", category = "props", variantGroup = "fixture-variants",
                parentIds = parents, artifactSha256 = ArtGraphContract.Fingerprint(Encoding.UTF8.GetBytes(id)),
                provenance = new ArtProvenance { provider = "synthetic-test", sourceRef = "fixture:" + id, sourceSha256 = "", receiptSha256 = "" },
                license = new ArtLicense { identifier = "", evidenceRef = "", state = "unverified" }, reviewState = "review-required"
            };
        }
        static ArtGraph Graph(params ArtGraphNode[] nodes)
        {
            var graph = new ArtGraph { projectSha = new string('a', 40), nodes = nodes };
            graph.graphRevision = ArtGraphContract.ComputeRevision(graph);
            return graph;
        }
        static ArtGraph Dag() { return Graph(Node("source"), Node("mesh", "source"), Node("preview", "source", "mesh")); }

        static void ValidDag()
        {
            var result = ArtGraphContract.Validate(Dag());
            Require(result.IsValid, result.Code);
            Equal("source,mesh,preview", string.Join(",", result.TopologicalIds));
        }
        static void Cycle() { var g = Dag(); g.nodes[0].parentIds = new[] { "preview" }; Code("GRAPH_CYCLE", ArtGraphContract.Validate(g)); }
        static void SelfCycle() { var g = Dag(); g.nodes[0].parentIds = new[] { "source" }; Code("GRAPH_CYCLE", ArtGraphContract.Validate(g)); }
        static void MissingParent() { var g = Dag(); g.nodes[2].parentIds = new[] { "absent" }; Code("MISSING_PARENT", ArtGraphContract.Validate(g)); }
        static void DuplicateId() { var g = Dag(); g.nodes[1].id = "source"; Code("DUPLICATE_ID", ArtGraphContract.Validate(g)); }
        static void DuplicateParent() { var g = Dag(); g.nodes[2].parentIds = new[] { "source", "source" }; Code("DUPLICATE_PARENT", ArtGraphContract.Validate(g)); }
        static void HashDrift()
        {
            var n = Node("source");
            Require(ArtGraphContract.VerifyArtifact(n, Encoding.UTF8.GetBytes("source")).IsValid, "Equal bytes rejected.");
            Code("HASH_DRIFT", ArtGraphContract.VerifyArtifact(n, Encoding.UTF8.GetBytes("modified")));
        }
        static void Sha256Golden() { Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ArtGraphContract.Fingerprint(Encoding.UTF8.GetBytes("abc"))); }
        static void StreamHash()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("source")))
            {
                Require(ArtGraphContract.VerifyArtifact(Node("source"), stream).IsValid, "Actual stream bytes rejected.");
                Require(stream.CanRead, "Contract disposed its caller-owned stream.");
            }
        }
        static void ForbiddenApproval() { var g = Dag(); g.nodes[0].reviewState = "owner-approved"; Code("APPROVAL_FORBIDDEN", ArtGraphContract.Validate(g)); }
        static void ForbiddenImport() { var g = Dag(); g.nodes[0].reviewState = "imported"; Code("APPROVAL_FORBIDDEN", ArtGraphContract.Validate(g)); }
        static void ForbiddenAccepted() { var g = Dag(); g.nodes[0].kind = "accepted"; Code("NODE_KIND_INVALID", ArtGraphContract.Validate(g)); }
        static void UnknownSchema() { var g = Dag(); g.schema = "ashen.art-graph.v2"; Code("SCHEMA_UNSUPPORTED", ArtGraphContract.Validate(g)); }
        static void ProjectDrift() { var g = Dag(); Code("PROJECT_REVISION_CONFLICT", ArtGraphContract.CheckExpectedRevision(g, new string('b', 40), g.graphRevision)); }
        static void GraphDrift() { var g = Dag(); Code("GRAPH_REVISION_CONFLICT", ArtGraphContract.CheckExpectedRevision(g, g.projectSha, new string('b', 64))); }
        static void MissingExpectedRevision() { var g = Dag(); Code("EXPECTED_REVISION_REQUIRED", ArtGraphContract.CheckExpectedRevision(g, g.projectSha, "")); }
        static void MatchingRevisions() { var g = Dag(); Require(ArtGraphContract.CheckExpectedRevision(g, g.projectSha, g.graphRevision).IsValid, "Current revisions rejected."); }
        static void StoredRevisionDrift() { var g = Dag(); g.nodes[0].label = "changed"; Code("GRAPH_REVISION_INVALID", ArtGraphContract.Validate(g)); }
        static void OrderInvariant()
        {
            var g = Dag(); string revision = g.graphRevision;
            Array.Reverse(g.nodes); Array.Reverse(g.nodes[0].parentIds);
            Equal(revision, ArtGraphContract.ComputeRevision(g));
            Require(ArtGraphContract.Validate(g).IsValid, "Reordered graph rejected.");
        }
        static void EveryFieldBound()
        {
            Action<ArtGraphNode>[] edits =
            {
                n => n.label = "Other fixture", n => n.kind = "material", n => n.category = "materials",
                n => n.variantGroup = "other-variants", n => n.artifactSha256 = new string('b', 64),
                n => n.provenance.provider = "another-provider", n => n.provenance.sourceRef = "fixture:other",
                n => n.provenance.sourceSha256 = new string('c', 64), n => n.provenance.receiptSha256 = new string('d', 64),
                n => n.license.identifier = "LicenseRef-Private", n => n.license.evidenceRef = "license:fixture",
                n => { n.license.state = "documented"; n.license.identifier = "CC0-1.0"; n.license.evidenceRef = "license:fixture"; },
                n => n.reviewState = "protected"
            };
            foreach (var edit in edits)
            {
                var g = Dag(); string before = g.graphRevision; edit(g.nodes[0]);
                Require(before != ArtGraphContract.ComputeRevision(g), "A changed semantic field was not revision-bound.");
            }
            var parentChange = Dag(); parentChange.nodes[2].parentIds = new[] { "mesh" };
            Require(parentChange.graphRevision != ArtGraphContract.ComputeRevision(parentChange), "Parent relation was not bound.");
            var idChange = Graph(Node("first")); idChange.nodes[0].id = "renamed";
            Require(idChange.graphRevision != ArtGraphContract.ComputeRevision(idChange), "Node identity was not bound.");
        }
        static void CultureInvariant()
        {
            var before = CultureInfo.CurrentCulture;
            try
            {
                var g = Graph(Node("I"), Node("i"), Node("z")); string hash = g.graphRevision;
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                Equal(hash, ArtGraphContract.ComputeRevision(g));
            }
            finally { CultureInfo.CurrentCulture = before; }
        }
        static void CanonicalDigest()
        {
            var g = Dag();
            Equal(g.graphRevision, ArtGraphContract.Fingerprint(ArtGraphContract.CanonicalBytes(g)));
            // Independently encoded with Python struct.pack('>I', length) + UTF-8, not this C# serializer.
            Require(ArtGraphContract.CanonicalBytes(g).Length == 761, "Canonical fixture byte length changed.");
            Equal("71e0dd3a54226f855f6136ae057f35cf9a22302dd1b850491fa5d09cd4538522", g.graphRevision);
        }
        static void LicenseEvidence() { var g = Dag(); g.nodes[0].license.state = "documented"; Code("LICENSE_EVIDENCE_REQUIRED", ArtGraphContract.Validate(g)); }
        static void UnknownLicense() { var g = Dag(); Require(ArtGraphContract.Validate(g).IsValid, "Unknown licenses must remain inspectable."); Equal("review-required", g.nodes[0].reviewState); }
        static void PrivatePath() { var g = Dag(); g.nodes[0].provenance.sourceRef = "/var/lib/private/model.fbx"; Code("PROVENANCE_INVALID", ArtGraphContract.Validate(g)); }
        static void CredentialUrl() { var g = Dag(); g.nodes[0].provenance.sourceRef = "https://user:password@example.org/model"; Code("PROVENANCE_INVALID", ArtGraphContract.Validate(g)); }
        static void QueryUrl() { var g = Dag(); g.nodes[0].provenance.sourceRef = "https://example.org/model?token=secret"; Code("PROVENANCE_INVALID", ArtGraphContract.Validate(g)); }
        static void Traversal() { var g = Dag(); g.nodes[0].provenance.sourceRef = "worldforge:job/../private"; Code("PROVENANCE_INVALID", ArtGraphContract.Validate(g)); }
        static void MalformedHash() { var g = Dag(); g.nodes[0].artifactSha256 = new string('X', 64); Code("ARTIFACT_HASH_INVALID", ArtGraphContract.Validate(g)); }
        static void MalformedSourceHash() { var g = Dag(); g.nodes[0].provenance.sourceSha256 = "unverified"; Code("PROVENANCE_INVALID", ArtGraphContract.Validate(g)); }
        static void NullNode() { var g = Dag(); g.nodes[0] = null; Code("NODE_INVALID", ArtGraphContract.Validate(g)); }
        static void OversizedGraph() { var g = Dag(); g.nodes = new ArtGraphNode[ArtGraphContract.MaxNodes + 1]; Code("GRAPH_LIMIT", ArtGraphContract.Validate(g)); }
        static void EmptyGraph() { Require(ArtGraphContract.Validate(Graph()).IsValid, "An empty verified inventory must be representable."); }
        static void InputPreserved()
        {
            var g = Dag(); Array.Reverse(g.nodes); string ids = string.Join(",", Array.ConvertAll(g.nodes, n => n.id));
            string parentOrder = string.Join(",", g.nodes[0].parentIds); ArtGraphContract.Validate(g);
            Equal(ids, string.Join(",", Array.ConvertAll(g.nodes, n => n.id))); Equal(parentOrder, string.Join(",", g.nodes[0].parentIds));
        }
        static void EnvelopeBound()
        {
            var g = Dag(); string hash = g.graphRevision; g.projectSha = new string('b', 40);
            Require(hash != ArtGraphContract.ComputeRevision(g), "Project SHA must be revision-bound.");
        }
    }
}
