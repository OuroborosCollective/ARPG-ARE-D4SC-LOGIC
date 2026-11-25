using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AshenCrypt.WorldForge.EditorTools;
using Fadenwerk.EditorTools;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.ArtForge
{
    /// <summary>Only called by BauModusBridge.Handle after the existing Fadenwerk token check.</summary>
    [InitializeOnLoad]
    public static class ArtForgeCanvasController
    {
        // Read operations are ephemeral transport state, never graph/simulation authority.
        // Only Handle and editor lifecycle callbacks access this bounded dictionary.
        const int MaxPendingReads = 8, ReadTimeoutMilliseconds = 30000;
        const double TicketLifetimeSeconds = 60;
        static readonly Dictionary<string, ReadOperation> Reads = new Dictionary<string, ReadOperation>(StringComparer.Ordinal);
        static readonly SemaphoreSlim ReadGate = new SemaphoreSlim(1, 1);
        static bool stopping;

        sealed class ReadOperation
        {
            public string path, projectSha, graphRevision, assetId;
            public long started;
            public bool abandoned;
            public CancellationTokenSource cancellation;
            public Task<FadenwerkExtensionReply> task;
        }
        [Serializable] sealed class AbandonedReply
        {
            public bool ok = true, readOnly = true;
            public string code = "ART_SCAN_ABANDONED";
        }
        [Serializable] sealed class PendingReply
        {
            public bool ok, readOnly = true;
            public string code = "ART_SCAN_PENDING", scanId;
            public int retryAfterMs = 200;
            public string error = "Die Herkunft wird geprüft. Einen Moment bitte.";
        }

        static ArtForgeCanvasController()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopReads;
            EditorApplication.quitting += StopReads;
        }
        [Serializable] sealed class StateReply
        {
            public bool ok = true, readOnly = true;
            public string schema, projectSha, graphRevision;
            public int nodeCount;
            public ArtForgeDiagnostic[] diagnostics;
        }
        [Serializable] sealed class GraphReply
        {
            public bool ok = true, readOnly = true;
            public string schema, projectSha, graphRevision;
            public ArtGraphNode[] nodes;
            public ArtForgeDiagnostic[] diagnostics;
        }
        [Serializable] sealed class AssetReply
        {
            public bool ok = true, readOnly = true;
            public string projectSha, graphRevision;
            public ArtGraphNode node;
        }
        [Serializable] sealed class Failure { public bool ok; public string code, error; }

        public static FadenwerkExtensionReply Handle(string method, string path, string body, IDictionary<string, string> query)
        {
            // Reject before reading any request body, path, World Forge job or Unity asset.
            if (method != "GET") return Error("READ_ONLY", "Die Asset-Werkstatt ist in dieser Ausbaustufe ausschließlich lesend.", 405);
            if (path != "art/state" && path != "art/graph" && path != "art/asset") return Error("ART_ROUTE_UNKNOWN", "Unbekannter Asset-Lesebefehl.", 404);
            if (query != null && query.Keys.Any(k => k != "expectedProjectSha" && k != "expectedGraphRevision" && k != "id" && k != "scanId" && k != "cancelScan"))
                return Error("QUERY_INVALID", "Nur feste Asset-IDs und Revisionen sind zulässig.", 400);
            bool cancel = query != null && query.ContainsKey("cancelScan");
            if (cancel && (Arg(query, "cancelScan") != "1" || !query.ContainsKey("scanId")))
                return Error("QUERY_INVALID", "Der Abbruch benötigt eine gebundene Leseprüfung.", 400);
            if (path != "art/state" && (!ArtGraphContract.IsProjectSha(Arg(query, "expectedProjectSha")) ||
                !ArtGraphContract.IsSha256(Arg(query, "expectedGraphRevision"))))
                return Error("EXPECTED_REVISION_REQUIRED", "Projekt- und Graphrevision fehlen. Bitte die Ansicht neu laden.", 428);
            if (path == "art/asset" && !ArtGraphContract.IsIdentifier(Arg(query, "id")))
                return Error("ASSET_ID_INVALID", "Ungültige Asset-ID.", 400);
            if (stopping)
                return Error("EDITOR_BUSY", "Unity beendet die Leseprüfungen.", 503);
            PruneExpiredReads();
            // An authenticated, bound cancellation affects ephemeral tickets only.
            // It remains available during compilation/import, without starting IO.
            if (cancel) return PollRead(path, query);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return Error("EDITOR_BUSY", "Unity importiert oder kompiliert gerade. Bitte später erneut laden.", 503);
            if (query != null && query.ContainsKey("scanId")) return PollRead(path, query);
            if (!ArtForgeReadCapacity.TryAdmit(Reads, MaxPendingReads, r => r.task, r => r.started, ForgetRead))
                return Error("ART_SCAN_CAPACITY", "Es laufen bereits mehrere Leseprüfungen. Bitte kurz warten und erneut laden.", 503);

            // Unity state is captured on the main thread. No Git or file reads run here.
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return BeginRead(path, query, projectRoot);
        }

        static FadenwerkExtensionReply BeginRead(string path, IDictionary<string, string> query, string projectRoot)
        {
            // Copy the validated request fields; callers cannot mutate an in-flight request.
            var operation = new ReadOperation { path = path, projectSha = Arg(query, "expectedProjectSha"),
                graphRevision = Arg(query, "expectedGraphRevision"), assetId = Arg(query, "id"),
                started = Stopwatch.GetTimestamp(), cancellation = new CancellationTokenSource() };
            string scanId = Guid.NewGuid().ToString("N"); // Transport ticket only; excluded from all canonical bytes.
            CancellationToken cancellation = operation.cancellation.Token;
            operation.cancellation.CancelAfter(ReadTimeoutMilliseconds);
            operation.task = Task.Run(async () =>
            {
                bool acquired = false;
                try
                {
                    await ReadGate.WaitAsync(cancellation).ConfigureAwait(false);
                    acquired = true;
                    return ReadSnapshot(operation, projectRoot, cancellation);
                }
                catch (OperationCanceledException)
                {
                    return Error("ART_SCAN_EXPIRED", "Die Leseprüfung ist abgelaufen. Bitte neu laden.", 410);
                }
                finally { if (acquired) ReadGate.Release(); }
            });
            Reads.Add(scanId, operation);
            return Pending(scanId);
        }

        static FadenwerkExtensionReply PollRead(string path, IDictionary<string, string> query)
        {
            string scanId = Arg(query, "scanId");
            if (scanId.Length != 32 || scanId.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
                return Error("ART_SCAN_INVALID", "Ungültige Leseprüfung.", 400);
            ReadOperation operation;
            if (!Reads.TryGetValue(scanId, out operation))
                return Error("ART_SCAN_EXPIRED", "Diese Leseprüfung ist abgelaufen oder bereits abgeholt. Bitte neu laden.", 410);
            if (operation.path != path || operation.projectSha != Arg(query, "expectedProjectSha") ||
                operation.graphRevision != Arg(query, "expectedGraphRevision") || operation.assetId != Arg(query, "id"))
                return Error("ART_SCAN_MISMATCH", "Die Leseprüfung gehört zu einer anderen Anfrage.", 400);
            if (Arg(query, "cancelScan") == "1")
            {
                AbandonRead(operation);
                if (operation.task.IsCompleted) ForgetRead(scanId, operation);
                return Json(new AbandonedReply());
            }
            if (operation.abandoned)
                return Error("ART_SCAN_EXPIRED", "Diese Leseprüfung wurde aufgegeben. Bitte neu laden.", 410);
            if (!operation.task.IsCompleted) return Pending(scanId);

            // Consume exactly once. A later new request always verifies actual bytes again.
            Reads.Remove(scanId);
            operation.cancellation.Dispose();
            if (operation.task.IsFaulted)
            {
                _ = operation.task.Exception; // Observe the fault without exposing private paths or details.
                return Error("PROVENANCE_UNAVAILABLE", "Der revisionsgebundene Asset-Stand konnte nicht sicher gelesen werden.", 503);
            }
            if (operation.task.IsCanceled) return Error("ART_SCAN_EXPIRED", "Die Leseprüfung wurde beendet. Bitte neu laden.", 410);
            return operation.task.GetAwaiter().GetResult(); // Already complete; never wait on the editor thread.
        }

        static FadenwerkExtensionReply Pending(string scanId)
        {
            return FadenwerkExtensionReply.Json(JsonUtility.ToJson(new PendingReply { scanId = scanId }), 202);
        }

        static void PruneExpiredReads()
        {
            long now = Stopwatch.GetTimestamp();
            foreach (var pair in Reads.ToArray())
            {
                var operation = pair.Value;
                if ((now - operation.started) / (double)Stopwatch.Frequency >= TicketLifetimeSeconds)
                    AbandonRead(operation);
                // Keep unfinished workers counted even if their token was cancelled.
                if (operation.abandoned && operation.task.IsCompleted)
                    ForgetRead(pair.Key, operation);
            }
        }
        static void AbandonRead(ReadOperation operation)
        {
            operation.abandoned = true;
            if (!operation.cancellation.IsCancellationRequested) operation.cancellation.Cancel();
        }
        static void ForgetRead(string id, ReadOperation operation)
        {
            Reads.Remove(id);
            AbandonRead(operation);
            // Never wait for IO during an editor lifecycle callback.
            operation.task.ContinueWith(task => { _ = task.Exception; operation.cancellation.Dispose(); }, TaskScheduler.Default);
        }
        static void StopReads()
        {
            stopping = true;
            foreach (var pair in Reads.ToArray()) ForgetRead(pair.Key, pair.Value);
        }

        // Runs only in the bounded background reader. JsonUtility handles plain DTOs here;
        // no Unity objects, scene/asset APIs, editor state or simulation are accessed.
        static FadenwerkExtensionReply ReadSnapshot(ReadOperation operation, string projectRoot, CancellationToken cancellation)
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                string projectSha = ReadProjectSha(projectRoot, cancellation);
                if (operation.path != "art/state" && projectSha != operation.projectSha)
                    return Error("PROJECT_REVISION_CONFLICT", "Die Projektfassung hat sich geändert. Bitte neu laden.", 409);
                var snapshot = ArtForgeWorldForgeReader.Read(WFBlenderIntake.Root, projectSha, cancellation);
                if (ReadProjectSha(projectRoot, cancellation) != projectSha) return Error("PROJECT_REVISION_CONFLICT", "Die Projektfassung hat sich während des Lesens geändert.", 409);
                ArtGraph graph = snapshot.graph;
                if (operation.path == "art/state") return Json(new StateReply { schema = graph.schema, projectSha = graph.projectSha,
                    graphRevision = graph.graphRevision, nodeCount = graph.nodes.Length, diagnostics = snapshot.diagnostics });
                var revision = ArtGraphContract.CheckExpectedRevision(graph, operation.projectSha, operation.graphRevision);
                if (!revision.IsValid) return Error(revision.Code, "Der Asset-Graph hat sich geändert. Bitte neu laden.", 409);
                if (operation.path == "art/graph") return Json(new GraphReply { schema = graph.schema, projectSha = graph.projectSha,
                    graphRevision = graph.graphRevision, nodes = graph.nodes, diagnostics = snapshot.diagnostics });
                ArtGraphNode node = graph.nodes.FirstOrDefault(n => n.id == operation.assetId);
                if (node == null) return Error("ASSET_NOT_FOUND", "Dieses Asset gehört nicht mehr zu dieser Graphrevision.", 404);
                return Json(new AssetReply { projectSha = graph.projectSha, graphRevision = graph.graphRevision, node = node });
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidOperationException ||
                                          error is ArgumentException || error is TimeoutException || error is System.ComponentModel.Win32Exception)
            {
                return Error("PROVENANCE_UNAVAILABLE", "Der revisionsgebundene Asset-Stand konnte nicht sicher gelesen werden.", 503);
            }
        }

        static string Arg(IDictionary<string, string> query, string key)
        {
            string value;
            return query != null && query.TryGetValue(key, out value) ? value ?? "" : "";
        }
        static FadenwerkExtensionReply Json(object value) { return FadenwerkExtensionReply.Json(JsonUtility.ToJson(value)); }
        static FadenwerkExtensionReply Error(string code, string message, int status)
        {
            return FadenwerkExtensionReply.Json(JsonUtility.ToJson(new Failure { code = code, error = message }), status);
        }

        // A fixed read-only Git invocation. No query/body input becomes a process argument.
        static string ReadProjectSha(string root, CancellationToken cancellation)
        {
            string head = Git(root, "rev-parse HEAD", cancellation).Trim();
            if (!ArtGraphContract.IsProjectSha(head)) throw new InvalidOperationException("Source revision unavailable.");
            const string surfaces = " -- Assets/Editor/ArtForge Assets/Editor/BauModus Packages/com.ouroboros.fadenwerk Assets/AshenCrypt/WorldForge/Editor";
            // Never label dirty authoring code as the clean Git revision.
            if (Git(root, "status --porcelain=v1 --untracked-files=normal" + surfaces, cancellation).Length != 0)
                throw new InvalidOperationException("Uncommitted authoring source.");
            return head;
        }
        static string Git(string root, string arguments, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var info = new ProcessStartInfo("git", arguments) { WorkingDirectory = root, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                var limit = Stopwatch.StartNew();
                while (!process.WaitForExit(25))
                {
                    if (!cancellation.IsCancellationRequested && limit.ElapsedMilliseconds < 5000) continue;
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    cancellation.ThrowIfCancellationRequested();
                    throw new TimeoutException("Source revision timeout.");
                }
                cancellation.ThrowIfCancellationRequested();
                if (process.ExitCode != 0) throw new InvalidOperationException("Source revision read failed.");
                error.GetAwaiter().GetResult();
                return output.GetAwaiter().GetResult();
            }
        }
    }
}
