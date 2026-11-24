using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AshenCrypt.WorldForge.EditorTools;
using Fadenwerk.EditorTools;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.ArtForge
{
    /// <summary>Only called by BauModusBridge.Handle after the existing Fadenwerk token check.</summary>
    public static class ArtForgeCanvasController
    {
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
            if (query != null && query.Keys.Any(k => k != "expectedProjectSha" && k != "expectedGraphRevision" && k != "id"))
                return Error("QUERY_INVALID", "Nur feste Asset-IDs und Revisionen sind zulässig.", 400);
            if (path != "art/state" && (!ArtGraphContract.IsProjectSha(Arg(query, "expectedProjectSha")) ||
                !ArtGraphContract.IsSha256(Arg(query, "expectedGraphRevision"))))
                return Error("EXPECTED_REVISION_REQUIRED", "Projekt- und Graphrevision fehlen. Bitte die Ansicht neu laden.", 428);
            if (path == "art/asset" && !ArtGraphContract.IsIdentifier(Arg(query, "id")))
                return Error("ASSET_ID_INVALID", "Ungültige Asset-ID.", 400);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return Error("EDITOR_BUSY", "Unity importiert oder kompiliert gerade. Bitte später erneut laden.", 503);
            try
            {
                string projectSha = ReadProjectSha();
                if (path != "art/state" && projectSha != Arg(query, "expectedProjectSha"))
                    return Error("PROJECT_REVISION_CONFLICT", "Die Projektfassung hat sich geändert. Bitte neu laden.", 409);
                var snapshot = ArtForgeWorldForgeReader.Read(WFBlenderIntake.Root, projectSha);
                if (ReadProjectSha() != projectSha) return Error("PROJECT_REVISION_CONFLICT", "Die Projektfassung hat sich während des Lesens geändert.", 409);
                ArtGraph graph = snapshot.graph;
                if (path == "art/state") return Json(new StateReply { schema = graph.schema, projectSha = graph.projectSha,
                    graphRevision = graph.graphRevision, nodeCount = graph.nodes.Length, diagnostics = snapshot.diagnostics });
                var revision = ArtGraphContract.CheckExpectedRevision(graph, Arg(query, "expectedProjectSha"), Arg(query, "expectedGraphRevision"));
                if (!revision.IsValid) return Error(revision.Code, "Der Asset-Graph hat sich geändert. Bitte neu laden.", 409);
                if (path == "art/graph") return Json(new GraphReply { schema = graph.schema, projectSha = graph.projectSha,
                    graphRevision = graph.graphRevision, nodes = graph.nodes, diagnostics = snapshot.diagnostics });
                ArtGraphNode node = graph.nodes.FirstOrDefault(n => n.id == Arg(query, "id"));
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
        static string ReadProjectSha()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string head = Git(root, "rev-parse HEAD").Trim();
            if (!ArtGraphContract.IsProjectSha(head)) throw new InvalidOperationException("Source revision unavailable.");
            const string surfaces = " -- Assets/Editor/ArtForge Assets/Editor/BauModus Packages/com.ouroboros.fadenwerk Assets/AshenCrypt/WorldForge/Editor";
            // Never label dirty authoring code as the clean Git revision.
            if (Git(root, "status --porcelain=v1 --untracked-files=normal" + surfaces).Length != 0)
                throw new InvalidOperationException("Uncommitted authoring source.");
            return head;
        }
        static string Git(string root, string arguments)
        {
            var info = new ProcessStartInfo("git", arguments) { WorkingDirectory = root, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(5000)) { process.Kill(); throw new TimeoutException("Source revision timeout."); }
                if (process.ExitCode != 0) throw new InvalidOperationException("Source revision read failed.");
                error.GetAwaiter().GetResult();
                return output.GetAwaiter().GetResult();
            }
        }
    }
}
