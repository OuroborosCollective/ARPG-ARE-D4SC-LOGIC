using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AshenCrypt.ArtForge
{
    [Serializable] public sealed class ArtForgeDiagnostic
    {
        public string code, message;
        public ArtForgeDiagnostic(string code, string message) { this.code = code; this.message = message; }
    }
    [Serializable] public sealed class ArtForgeSnapshot
    {
        public ArtGraph graph;
        public ArtForgeDiagnostic[] diagnostics;
    }

    /// <summary>
    /// A bounded read projection of the existing World Forge receipts, not another asset store.
    /// Every emitted artifact hash is independently checked against real bytes on each read.
    /// Receipt source hashes remain claims about the original source, not a source-file verification.
    /// </summary>
    public static class ArtForgeWorldForgeReader
    {
        public const int MaxJobs = 128, MaxFilesPerJob = 16, MaxReceiptBytes = 262144;
        public const long MaxFileBytes = 16L * 1024 * 1024, MaxReadBytes = 64L * 1024 * 1024;
        [Serializable] sealed class Receipt { public string schema, jobId, sourceSha256; public ExportFile[] files; }
        [Serializable] sealed class ExportFile { public string path, sha256; public long size; }
        sealed class Refusal : Exception
        {
            public readonly string Code;
            public Refusal(string code) { Code = code; }
        }

        public static ArtForgeSnapshot Read(string root, string projectSha)
        {
            var nodes = new List<ArtGraphNode>();
            var diagnostics = new List<ArtForgeDiagnostic>();
            long budget = MaxReadBytes;
            try
            {
                if (!Directory.Exists(root)) throw new Refusal("WORLD_FORGE_UNAVAILABLE");
                RejectLinks(root);
                string results = Path.Combine(root, "results");
                if (!Directory.Exists(results)) throw new Refusal("WORLD_FORGE_UNAVAILABLE");
                RejectLinks(results);
                // Enumeration itself is bounded; do not silently publish an arbitrary subset.
                string[] jobs = Directory.EnumerateDirectories(results).Take(MaxJobs + 1).ToArray();
                if (jobs.Length > MaxJobs) throw new Refusal("CATALOG_LIMIT");
                Array.Sort(jobs, StringComparer.Ordinal);
                foreach (string folder in jobs)
                {
                    try
                    {
                        string jobId = Path.GetFileName(folder);
                        if (!ArtGraphContract.IsSha256(jobId)) throw new Refusal("JOB_ID_INVALID");
                        var jobNodes = ReadJob(folder, jobId, ref budget);
                        if (nodes.Count + jobNodes.Length > ArtGraphContract.MaxNodes) throw new Refusal("CATALOG_NODE_LIMIT");
                        nodes.AddRange(jobNodes);
                    }
                    catch (Refusal error) { diagnostics.Add(Diagnostic(error.Code)); }
                    catch (IOException) { diagnostics.Add(Diagnostic("READ_UNAVAILABLE")); }
                    catch (UnauthorizedAccessException) { diagnostics.Add(Diagnostic("READ_UNAVAILABLE")); }
                    catch (ArgumentException) { diagnostics.Add(Diagnostic("RECEIPT_INVALID")); }
                }
            }
            catch (Refusal error) { diagnostics.Add(Diagnostic(error.Code)); }
            catch (IOException) { diagnostics.Add(Diagnostic("READ_UNAVAILABLE")); }
            catch (UnauthorizedAccessException) { diagnostics.Add(Diagnostic("READ_UNAVAILABLE")); }
            catch (ArgumentException) { diagnostics.Add(Diagnostic("PATH_REJECTED")); }
            var graph = new ArtGraph { projectSha = projectSha, nodes = nodes.ToArray() };
            graph.graphRevision = ArtGraphContract.ComputeRevision(graph);
            return new ArtForgeSnapshot { graph = graph, diagnostics = diagnostics.ToArray() };
        }

        static ArtGraphNode[] ReadJob(string folder, string jobId, ref long budget)
        {
            RejectLinks(folder);
            string receiptPath = Path.Combine(folder, "receipt.json");
            byte[] receiptBytes = ReadSmallFile(receiptPath, MaxReceiptBytes, ref budget);
            Receipt receipt;
            try { receipt = JsonUtility.FromJson<Receipt>(Encoding.UTF8.GetString(receiptBytes)); }
            catch (ArgumentException) { throw new Refusal("RECEIPT_INVALID"); }
            if (receipt == null || receipt.schema != "worldforge.blender-export.v1" || receipt.jobId != jobId ||
                !ArtGraphContract.IsSha256(receipt.sourceSha256) || receipt.files == null ||
                receipt.files.Length == 0 || receipt.files.Length > MaxFilesPerJob)
                throw new Refusal("RECEIPT_INVALID");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in receipt.files)
            {
                if (file == null || !SafeRelative(file.path) || file.path == "receipt.json") throw new Refusal("PATH_REJECTED");
                if (!seen.Add(file.path) || !ArtGraphContract.IsSha256(file.sha256) || file.size <= 0) throw new Refusal("RECEIPT_INVALID");
            }
            var files = receipt.files.OrderBy(f => f.path, StringComparer.Ordinal).ToArray();
            string receiptHash = ArtGraphContract.Fingerprint(receiptBytes);
            string parentId = "wf-" + jobId + "-receipt";
            string variant = "source-" + receipt.sourceSha256;
            var nodes = new List<ArtGraphNode>
            {
                Node(parentId, "Exportbeleg · " + jobId.Substring(0, 10), "source", "props", variant,
                    new string[0], receiptHash, jobId, "receipt.json", receipt.sourceSha256, receiptHash)
            };
            for (int index = 0; index < files.Length; index++)
            {
                ExportFile file = files[index];
                string path = ContainedFile(folder, file.path);
                if (!File.Exists(path)) throw new Refusal("ARTIFACT_MISSING");
                RejectLinks(path);
                var info = new FileInfo(path);
                if (info.Length != file.size) throw new Refusal("HASH_DRIFT");
                if (file.size > MaxFileBytes || file.size > budget) throw new Refusal("READ_BUDGET_EXCEEDED");
                budget -= file.size;
                long previousLength = info.Length;
                DateTime previousWrite = info.LastWriteTimeUtc; // IO race observation only, never simulation input.
                string kind = Kind(file.path);
                var node = Node("wf-" + jobId + "-f" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Path.GetFileName(file.path), kind, kind == "material" ? "materials" : "props", variant,
                    new[] { parentId }, file.sha256, jobId, file.path, receipt.sourceSha256, receiptHash);
                using (var stream = File.OpenRead(path))
                {
                    VerifyReservedArtifact(node, stream, file.size);
                }
                RejectLinks(path);
                info.Refresh();
                if (info.Length != previousLength || info.LastWriteTimeUtc != previousWrite) throw new Refusal("HASH_DRIFT");
                nodes.Add(node);
            }
            if (ArtGraphContract.Fingerprint(ReadSmallFile(receiptPath, MaxReceiptBytes, ref budget)) != receiptHash)
                throw new Refusal("HASH_DRIFT");
            return nodes.ToArray(); // All-or-nothing per receipt, never a dangling partial chain.
        }

        static ArtGraphNode Node(string id, string label, string kind, string category, string variant,
            string[] parents, string hash, string job, string relative, string sourceHash, string receiptHash)
        {
            return new ArtGraphNode
            {
                id = id, label = label, kind = kind, category = category, variantGroup = variant,
                parentIds = parents, artifactSha256 = hash,
                provenance = new ArtProvenance { provider = "worldforge-blender", sourceRef = "worldforge:" + job + "/" + relative,
                    sourceSha256 = sourceHash, receiptSha256 = receiptHash },
                license = new ArtLicense { state = "unverified", identifier = "", evidenceRef = "" },
                reviewState = "review-required"
            };
        }
        static string Kind(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".fbx" || extension == ".glb" || extension == ".gltf") return "mesh";
            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga") return "material";
            return "source";
        }
        static bool SafeRelative(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 180 || Path.IsPathRooted(path) || path.Contains("\\") ||
                path.Any(c => char.IsControl(c) || c == ':' || c == '?' || c == '#' || c == '%')) return false;
            return path.Split('/').All(ArtGraphContract.IsIdentifier);
        }
        static string ContainedFile(string folder, string relative)
        {
            if (!SafeRelative(relative)) throw new Refusal("PATH_REJECTED");
            string path = folder;
            foreach (string part in relative.Split('/'))
            {
                path = Path.Combine(path, part);
                if (File.Exists(path) || Directory.Exists(path)) RejectLinks(path);
            }
            return path;
        }
        static byte[] ReadSmallFile(string path, int maximum, ref long budget)
        {
            if (!File.Exists(path)) throw new Refusal("RECEIPT_MISSING");
            RejectLinks(path);
            var info = new FileInfo(path);
            // Reject empty/special files before opening: a FIFO must not block the Editor thread.
            if (info.Length <= 0 || info.Length > maximum || (info.Attributes & FileAttributes.Directory) != 0)
                throw new Refusal("RECEIPT_INVALID");
            if (info.Length > budget) throw new Refusal("READ_BUDGET_EXCEEDED");
            budget -= info.Length;
            using (var input = File.OpenRead(path))
            {
                byte[] data = ReadReceiptBytes(input, info.Length);
                RejectLinks(path);
                return data;
            }
        }
        static byte[] ReadReceiptBytes(Stream input, long expectedLength)
        {
            if (input.Length != expectedLength) throw new Refusal("HASH_DRIFT");
            byte[] data = new byte[(int)input.Length];
            int offset = 0, read;
            while (offset < data.Length && (read = input.Read(data, offset, data.Length - offset)) > 0) offset += read;
            if (offset != data.Length || input.ReadByte() != -1) throw new Refusal("HASH_DRIFT");
            return data;
        }
        static void VerifyReservedArtifact(ArtGraphNode node, Stream input, long reservedLength)
        {
            if (input.Length != reservedLength || !ArtGraphContract.VerifyArtifact(node, input).IsValid)
                throw new Refusal("HASH_DRIFT");
        }
        static void RejectLinks(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new Refusal("PATH_REJECTED");
                current = Path.GetDirectoryName(current);
            }
        }
        static ArtForgeDiagnostic Diagnostic(string code)
        {
            // Fixed messages keep local paths, exception details and receipt contents private.
            string message;
            switch (code)
            {
                case "WORLD_FORGE_UNAVAILABLE": message = "Der vorhandene World-Forge-Ergebniseingang ist nicht verfügbar. Es wurden keine Beispiel-Assets ergänzt."; break;
                case "HASH_DRIFT": message = "Ein Export weicht von seinem Herkunftsbeleg ab und wurde vollständig aus der Ansicht ausgeschlossen."; break;
                case "PATH_REJECTED": message = "Ein unzulässiger Pfad oder symbolischer Link wurde abgewiesen."; break;
                case "READ_BUDGET_EXCEEDED": message = "Ein Export überschreitet das Lesebudget dieser ersten Ansicht und wurde ausgelassen."; break;
                case "CATALOG_LIMIT": message = "Mehr als 128 Exportaufträge: Diese erste Ansicht verlangt eine spätere Seitenauswahl und zeigt keinen willkürlichen Ausschnitt."; break;
                case "CATALOG_NODE_LIMIT": message = "Ein weiterer Export würde die Grenze von 512 Knoten überschreiten und wurde vollständig ausgelassen."; break;
                default: message = "Ein World-Forge-Beleg ist unvollständig, ungültig oder nicht lesbar (" + code + ")."; break;
            }
            return new ArtForgeDiagnostic(code, message);
        }
    }
}
