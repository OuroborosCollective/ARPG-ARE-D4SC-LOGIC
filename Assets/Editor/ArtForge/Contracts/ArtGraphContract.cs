using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AshenCrypt.ArtForge
{
    /// <summary>Authoring/provenance projection only. No gameplay state, writes, or approval authority.</summary>
    [Serializable]
    public sealed class ArtGraph
    {
        public string schema = ArtGraphContract.Schema;
        public string projectSha = "";
        public string graphRevision = "";
        public ArtGraphNode[] nodes = new ArtGraphNode[0];
    }

    [Serializable]
    public sealed class ArtGraphNode
    {
        public string id = "", label = "", kind = "", category = "", variantGroup = "";
        // These are the only authoritative relationships. The canvas derives arrows from them.
        public string[] parentIds = new string[0];
        public string artifactSha256 = "";
        public ArtProvenance provenance = new ArtProvenance();
        public ArtLicense license = new ArtLicense();
        public string reviewState = "review-required";
    }

    [Serializable]
    public sealed class ArtProvenance
    {
        public string provider = "", sourceRef = "";
        // Optional receipt assertions. Their presence is NOT a readback of the source bytes.
        public string sourceSha256 = "", receiptSha256 = "";
    }

    [Serializable]
    public sealed class ArtLicense
    {
        public string identifier = "", evidenceRef = "", state = "unverified";
        // "documented" means a reference is recorded, not that usage or release is approved.
    }

    public sealed class ArtGraphValidation
    {
        public bool IsValid { get; private set; }
        public string Code { get; private set; }
        public string Message { get; private set; }
        public string[] TopologicalIds { get; private set; }

        internal static ArtGraphValidation Fail(string code, string message)
        {
            return new ArtGraphValidation { IsValid = false, Code = code, Message = message, TopologicalIds = new string[0] };
        }
        internal static ArtGraphValidation Pass(string[] order = null)
        {
            return new ArtGraphValidation { IsValid = true, Code = "OK", Message = "", TopologicalIds = order ?? new string[0] };
        }
    }

    /// <summary>
    /// Bounded, deterministic v1 validation. The read-only slice accepts review-required/protected observations
    /// only. Future commands need their own authenticated, revision-bound authority; metadata cannot grant it.
    /// </summary>
    public static class ArtGraphContract
    {
        public const string Schema = "ashen.art-graph.v1";
        public const int MaxNodes = 512, MaxParents = 32, MaxIdLength = 128, MaxReferenceLength = 512;
        public const long MaxArtifactBytes = 256L * 1024L * 1024L;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly HashSet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
        { "source", "concept", "mesh", "material", "rig", "animation", "preview" };
        static readonly HashSet<string> Categories = new HashSet<string>(StringComparer.Ordinal)
        { "figures", "creatures", "weapons", "armor", "props", "materials", "animations" };

        public static ArtGraphValidation Validate(ArtGraph graph)
        {
            ArtGraphValidation structure = ValidateStructure(graph);
            if (!structure.IsValid) return structure;
            if (!IsSha256(graph.graphRevision) || graph.graphRevision != Fingerprint(CanonicalBytesUnchecked(graph)))
                return ArtGraphValidation.Fail("GRAPH_REVISION_INVALID", "Die gespeicherte Graphrevision stimmt nicht mit dem Inhalt überein.");
            return structure;
        }

        public static ArtGraphValidation CheckExpectedRevision(ArtGraph graph, string expectedProjectSha, string expectedGraphRevision)
        {
            if (!IsProjectSha(expectedProjectSha) || !IsSha256(expectedGraphRevision))
                return ArtGraphValidation.Fail("EXPECTED_REVISION_REQUIRED", "Vollständige erwartete Projekt- und Graphrevision sind erforderlich.");
            ArtGraphValidation result = Validate(graph);
            if (!result.IsValid) return result;
            if (graph.projectSha != expectedProjectSha)
                return ArtGraphValidation.Fail("PROJECT_REVISION_CONFLICT", "Die Projektversion hat sich geändert; Ansicht neu laden.");
            if (graph.graphRevision != expectedGraphRevision)
                return ArtGraphValidation.Fail("GRAPH_REVISION_CONFLICT", "Der Herkunftsgraph hat sich geändert; Ansicht neu laden.");
            return result;
        }

        /// <summary>Canonical revision excludes only graphRevision itself. It does not prove artifact bytes.</summary>
        public static string ComputeRevision(ArtGraph graph) { return Fingerprint(CanonicalBytes(graph)); }

        /// <summary>
        /// Canonical v1 encoding: big-endian uint32 lengths/counts and strict UTF-8 strings, no BOM.
        /// Schema, project SHA, node count, then nodes by ordinal ID. Node fields: id,label,kind,category,
        /// variantGroup,parent count,ordinal parents,artifactSha256,provider,sourceRef,sourceSha256,
        /// receiptSha256,license identifier,evidenceRef,state,reviewState. No locale or wall-clock inputs.
        /// </summary>
        public static byte[] CanonicalBytes(ArtGraph graph)
        {
            ArtGraphValidation result = ValidateStructure(graph);
            if (!result.IsValid) throw new InvalidDataException(result.Code + ": " + result.Message);
            return CanonicalBytesUnchecked(graph);
        }

        static byte[] CanonicalBytesUnchecked(ArtGraph graph)
        {
            using (var output = new MemoryStream())
            {
                Field(output, graph.schema); Field(output, graph.projectSha);
                var nodes = (ArtGraphNode[])graph.nodes.Clone();
                Array.Sort(nodes, (a, b) => StringComparer.Ordinal.Compare(a.id, b.id));
                Count(output, nodes.Length);
                foreach (ArtGraphNode node in nodes)
                {
                    Field(output, node.id); Field(output, node.label); Field(output, node.kind); Field(output, node.category);
                    Field(output, node.variantGroup);
                    var parents = (string[])node.parentIds.Clone(); Array.Sort(parents, StringComparer.Ordinal);
                    Count(output, parents.Length); foreach (string parent in parents) Field(output, parent);
                    Field(output, node.artifactSha256); Field(output, node.provenance.provider);
                    Field(output, node.provenance.sourceRef); Field(output, node.provenance.sourceSha256);
                    Field(output, node.provenance.receiptSha256); Field(output, node.license.identifier);
                    Field(output, node.license.evidenceRef); Field(output, node.license.state); Field(output, node.reviewState);
                }
                return output.ToArray();
            }
        }

        static void Field(Stream output, string value)
        {
            byte[] bytes = Utf8.GetBytes(value); Count(output, bytes.Length); output.Write(bytes, 0, bytes.Length);
        }
        static void Count(Stream output, int value)
        {
            output.WriteByte((byte)(value >> 24)); output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8)); output.WriteByte((byte)value);
        }

        static ArtGraphValidation ValidateStructure(ArtGraph graph)
        {
            if (graph == null) return ArtGraphValidation.Fail("GRAPH_INVALID", "Der Herkunftsgraph fehlt.");
            if (graph.schema != Schema) return ArtGraphValidation.Fail("SCHEMA_UNSUPPORTED", "Unbekannte Graphversion; keine automatische Migration.");
            if (!IsProjectSha(graph.projectSha)) return ArtGraphValidation.Fail("PROJECT_SHA_INVALID", "Eine vollständige Git-Projekt-SHA ist erforderlich.");
            if (graph.nodes == null || graph.nodes.Length > MaxNodes)
                return ArtGraphValidation.Fail("GRAPH_LIMIT", "Die Knotenliste fehlt oder überschreitet die Grenze.");

            var nodes = new Dictionary<string, ArtGraphNode>(StringComparer.Ordinal);
            foreach (ArtGraphNode node in graph.nodes)
            {
                ArtGraphValidation check = ValidateNode(node);
                if (!check.IsValid) return check;
                if (nodes.ContainsKey(node.id)) return ArtGraphValidation.Fail("DUPLICATE_ID", "Eine Knoten-ID ist mehrfach vorhanden.");
                nodes.Add(node.id, node);
            }

            var degree = new Dictionary<string, int>(StringComparer.Ordinal);
            var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string id in nodes.Keys) { degree.Add(id, 0); children.Add(id, new List<string>()); }
            foreach (ArtGraphNode node in graph.nodes)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string parent in node.parentIds)
                {
                    if (!IsIdentifier(parent) || !nodes.ContainsKey(parent))
                        return ArtGraphValidation.Fail("MISSING_PARENT", "Ein referenzierter Elternknoten fehlt.");
                    if (!seen.Add(parent)) return ArtGraphValidation.Fail("DUPLICATE_PARENT", "Eine Elternbeziehung ist mehrfach vorhanden.");
                    degree[node.id]++; children[parent].Add(node.id);
                }
            }

            // Kahn's algorithm, with ordinal tie breaking. A self-loop also leaves a nonzero degree.
            var ready = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var pair in degree) if (pair.Value == 0) ready.Add(pair.Key);
            var order = new List<string>(nodes.Count);
            while (ready.Count > 0)
            {
                string id = ready.Min; ready.Remove(id); order.Add(id);
                foreach (string child in children[id]) if (--degree[child] == 0) ready.Add(child);
            }
            if (order.Count != nodes.Count) return ArtGraphValidation.Fail("GRAPH_CYCLE", "Der Herkunftsgraph enthält einen Zyklus.");
            return ArtGraphValidation.Pass(order.ToArray());
        }

        static ArtGraphValidation ValidateNode(ArtGraphNode node)
        {
            if (node == null || !IsIdentifier(node.id) || !IsText(node.label, 128, false) ||
                node.variantGroup == null || node.variantGroup.Length > 0 && !IsIdentifier(node.variantGroup) ||
                node.parentIds == null || node.parentIds.Length > MaxParents)
                return ArtGraphValidation.Fail("NODE_INVALID", "Ungültiger Knoten oder Elternliste.");
            if (!Kinds.Contains(node.kind ?? "")) return ArtGraphValidation.Fail("NODE_KIND_INVALID", "Unzulässiger Knotentyp.");
            if (!Categories.Contains(node.category ?? "")) return ArtGraphValidation.Fail("NODE_CATEGORY_INVALID", "Unzulässige Asset-Kategorie.");
            if (!IsSha256(node.artifactSha256)) return ArtGraphValidation.Fail("ARTIFACT_HASH_INVALID", "Der SHA-256-Bytebeleg ist ungültig.");
            if (node.provenance == null || !IsIdentifier(node.provenance.provider) || !IsSafeReference(node.provenance.sourceRef) ||
                !IsOptionalHash(node.provenance.sourceSha256) || !IsOptionalHash(node.provenance.receiptSha256))
                return ArtGraphValidation.Fail("PROVENANCE_INVALID", "Herkunftsbeleg ungültig; private Pfade und Zugangsdaten sind nicht erlaubt.");
            if (node.license == null || !IsText(node.license.identifier, 128, true) || node.license.evidenceRef == null ||
                node.license.evidenceRef.Length > 0 && !IsSafeReference(node.license.evidenceRef))
                return ArtGraphValidation.Fail("LICENSE_INVALID", "Der Lizenzverweis ist ungültig.");
            if (node.license.state != "unverified" && node.license.state != "documented")
                return ArtGraphValidation.Fail("LICENSE_INVALID", "Unzulässiger Lizenzstatus.");
            if (node.license.state == "documented" && (node.license.identifier.Length == 0 || node.license.evidenceRef.Length == 0))
                return ArtGraphValidation.Fail("LICENSE_EVIDENCE_REQUIRED", "Eine dokumentierte Lizenz braucht Kennung und Belegverweis.");
            if (node.reviewState != "review-required" && node.reviewState != "protected")
                return ArtGraphValidation.Fail("APPROVAL_FORBIDDEN", "Diese lesende Ansicht darf keine Freigabe oder Übernahme erklären.");
            return ArtGraphValidation.Pass();
        }

        public static bool IsSha256(string value) { return IsHex(value, 64); }
        public static bool IsProjectSha(string value) { return IsHex(value, 40); }
        static bool IsOptionalHash(string value) { return value != null && (value.Length == 0 || IsSha256(value)); }
        static bool IsHex(string value, int count)
        {
            if (value == null || value.Length != count) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }

        public static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxIdLength || value.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            if (!AsciiLetterOrDigit(value[0])) return false;
            foreach (char c in value) if (!AsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.') return false;
            return true;
        }

        /// <summary>Opaque logical references or public HTTPS references; never dereferenced by this contract.</summary>
        public static bool IsSafeReference(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxReferenceLength || value.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            foreach (char c in value) if (c < 33 || c > 126 || c == '\\' || c == '%' || c == '?' || c == '#' || c == '@') return false;
            if (value.StartsWith("https://", StringComparison.Ordinal))
            {
                Uri uri;
                return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == "https" &&
                    uri.Host.Length > 0 && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
            }
            int separator = value.IndexOf(':');
            if (separator < 1 || separator > 32 || !IsIdentifier(value.Substring(0, separator))) return false;
            // Only these redacted, logical providers are supported; no file:, drive:, shell: or generic URLs.
            string scheme = value.Substring(0, separator);
            if (scheme != "worldforge" && scheme != "asset" && scheme != "license" && scheme != "receipt" && scheme != "fixture" && scheme != "sha256") return false;
            string rest = value.Substring(separator + 1);
            if (rest.Length == 0 || rest[0] == '/' || rest.IndexOf(':') >= 0) return false;
            foreach (string segment in rest.Split('/')) if (!IsIdentifier(segment)) return false;
            return true;
        }

        static bool AsciiLetterOrDigit(char c) { return c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'; }
        static bool IsText(string value, int max, bool allowEmpty)
        {
            if (value == null || value.Length > max || !allowEmpty && value.Length == 0) return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i])) return false;
                if (char.IsHighSurrogate(value[i]))
                {
                    if (++i >= value.Length || !char.IsLowSurrogate(value[i])) return false;
                }
                else if (char.IsLowSurrogate(value[i])) return false;
            }
            return true;
        }

        public static string Fingerprint(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.LongLength > MaxArtifactBytes) throw new InvalidDataException("ARTIFACT_LIMIT");
            using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(bytes));
        }

        /// <summary>Hash complete real bytes only. Matching bytes do not imply license, trust, or owner approval.</summary>
        public static ArtGraphValidation VerifyArtifact(ArtGraphNode node, byte[] bytes)
        {
            if (node == null || !IsSha256(node.artifactSha256)) return ArtGraphValidation.Fail("ARTIFACT_HASH_INVALID", "Der erwartete Artefakthash ist ungültig.");
            if (bytes == null || bytes.LongLength > MaxArtifactBytes) return ArtGraphValidation.Fail("ARTIFACT_LIMIT", "Artefakt fehlt oder überschreitet die Grenze.");
            if (Fingerprint(bytes) != node.artifactSha256) return ArtGraphValidation.Fail("HASH_DRIFT", "Die gelesenen Bytes weichen vom Artefaktbeleg ab.");
            return ArtGraphValidation.Pass();
        }

        /// <summary>The caller owns the stream. Require its complete seekable content, starting at position zero.</summary>
        public static ArtGraphValidation VerifyArtifact(ArtGraphNode node, Stream input)
        {
            if (node == null || !IsSha256(node.artifactSha256)) return ArtGraphValidation.Fail("ARTIFACT_HASH_INVALID", "Der erwartete Artefakthash ist ungültig.");
            if (input == null || !input.CanRead || !input.CanSeek || input.Position != 0)
                return ArtGraphValidation.Fail("ARTIFACT_STREAM_INVALID", "Ein vollständiger lesbarer Artefaktstream ab Position null ist erforderlich.");
            if (input.Length > MaxArtifactBytes) return ArtGraphValidation.Fail("ARTIFACT_LIMIT", "Das Artefakt überschreitet die Grenze.");
            using (var hash = SHA256.Create())
            {
                byte[] buffer = new byte[65536]; long total = 0; int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += count;
                    if (total > MaxArtifactBytes) return ArtGraphValidation.Fail("ARTIFACT_LIMIT", "Das Artefakt überschreitet die Grenze.");
                    hash.TransformBlock(buffer, 0, count, buffer, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                if (Hex(hash.Hash) != node.artifactSha256) return ArtGraphValidation.Fail("HASH_DRIFT", "Die gelesenen Bytes weichen vom Artefaktbeleg ab.");
            }
            return ArtGraphValidation.Pass();
        }

        static string Hex(byte[] value) { return BitConverter.ToString(value).Replace("-", "").ToLowerInvariant(); }
    }
}
