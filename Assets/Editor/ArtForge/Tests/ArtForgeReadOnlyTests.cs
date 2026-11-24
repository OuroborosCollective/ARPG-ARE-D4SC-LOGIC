using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Fadenwerk.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace AshenCrypt.ArtForge.Tests
{
    // Disposable synthetic files exercise real IO and the production adapter.
    // They are never offered as licensed assets or Unity game-runtime evidence.
    public class ArtForgeReadOnlyTests
    {
        string root;
        const string Project = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        readonly string job = new string('b', 64);

        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ashen-art-forge-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "results", job));
        }
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        string Folder { get { return Path.Combine(root, "results", job); } }
        void Fixture(string name = "model.fbx")
        {
            byte[] bytes = Encoding.UTF8.GetBytes("synthetic geometry");
            File.WriteAllBytes(Path.Combine(Folder, name), bytes);
            File.WriteAllText(Path.Combine(Folder, "receipt.json"),
                "{\"schema\":\"worldforge.blender-export.v1\",\"jobId\":\"" + job +
                "\",\"sourceSha256\":\"" + new string('c', 64) + "\",\"files\":[{\"path\":\"" + name +
                "\",\"size\":" + bytes.Length + ",\"sha256\":\"" + ArtGraphContract.Fingerprint(bytes) + "\"}]}");
        }

        [Test] public void ReceiptAndRealBytesBecomeAReadOnlyGraphWithoutInventedLicense()
        {
            Fixture();
            var view = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.IsTrue(ArtGraphContract.Validate(view.graph).IsValid);
            Assert.AreEqual(2, view.graph.nodes.Length);
            var mesh = view.graph.nodes.Single(n => n.kind == "mesh");
            Assert.AreEqual(ArtGraphContract.Fingerprint(File.ReadAllBytes(Path.Combine(Folder, "model.fbx"))), mesh.artifactSha256);
            Assert.AreEqual("unverified", mesh.license.state);
            Assert.AreEqual("review-required", mesh.reviewState);
            Assert.AreEqual(1, mesh.parentIds.Length);
            StringAssert.DoesNotContain(root, JsonUtility.ToJson(view));
        }

        [Test] public void SameLengthHashDriftRejectsTheWholeReceiptAndChangesRevision()
        {
            Fixture();
            var before = ArtForgeWorldForgeReader.Read(root, Project);
            File.WriteAllBytes(Path.Combine(Folder, "model.fbx"), Encoding.UTF8.GetBytes("changed__geometry"));
            var after = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.IsEmpty(after.graph.nodes);
            Assert.IsTrue(after.diagnostics.Any(d => d.code == "HASH_DRIFT"));
            Assert.AreNotEqual(before.graph.graphRevision, after.graph.graphRevision);
        }

        [Test] public void MissingParentArtifactRejectsReceiptWithoutPartialGraph()
        {
            Fixture(); File.Delete(Path.Combine(Folder, "model.fbx"));
            var view = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.IsEmpty(view.graph.nodes);
            Assert.IsTrue(view.diagnostics.Any(d => d.code == "ARTIFACT_MISSING"));
        }

        [Test] public void TraversalIsRejectedBeforeReadingOutsideTheWorldForgeJob()
        {
            Fixture();
            string receipt = Path.Combine(Folder, "receipt.json");
            File.WriteAllText(receipt, File.ReadAllText(receipt).Replace("model.fbx", "../model.fbx"));
            var view = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.IsEmpty(view.graph.nodes);
            Assert.IsTrue(view.diagnostics.Any(d => d.code == "PATH_REJECTED"));
        }

        [Test] public void UnsupportedFilenameIsRejectedPerJobBeforeGraphCanonicalization()
        {
            Fixture("my model.fbx");
            var view = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.IsEmpty(view.graph.nodes);
            Assert.IsTrue(view.diagnostics.Any(d => d.code == "PATH_REJECTED"));
        }

        [Test] public void ForgedApprovalMetadataCannotGrantRelease()
        {
            Fixture();
            string receipt = Path.Combine(Folder, "receipt.json");
            File.WriteAllText(receipt, File.ReadAllText(receipt).Replace("{\"schema\"", "{\"reviewState\":\"owner-approved\",\"schema\""));
            var view = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.IsTrue(view.graph.nodes.All(n => n.reviewState == "review-required"));
            Assert.IsTrue(view.graph.nodes.All(n => n.license.state == "unverified"));
        }

        [Test] public void MissingRootProducesAnHonestEmptySnapshotWithoutCreatingDirectories()
        {
            string absent = Path.Combine(root, "absent");
            var view = ArtForgeWorldForgeReader.Read(absent, Project);
            Assert.IsEmpty(view.graph.nodes);
            Assert.IsFalse(Directory.Exists(absent));
            Assert.IsTrue(view.diagnostics.Any(d => d.code == "WORLD_FORGE_UNAVAILABLE"));
        }

        [Test] public void InvalidJobIdIsNotFollowed()
        {
            Directory.CreateDirectory(Path.Combine(root, "results", "not-a-job"));
            var view = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.IsEmpty(view.graph.nodes);
            Assert.IsTrue(view.diagnostics.Any(d => d.code == "JOB_ID_INVALID"));
        }

        [Test] public void RepeatedReadPreservesAllSourceBytesAndRevision()
        {
            Fixture();
            var files = Directory.GetFiles(Folder).ToDictionary(f => f, File.ReadAllBytes);
            var a = ArtForgeWorldForgeReader.Read(root, Project);
            var b = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.AreEqual(a.graph.graphRevision, b.graph.graphRevision);
            Assert.AreEqual(files.Count, Directory.GetFiles(Folder).Length);
            foreach (var pair in files) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key));
        }

        [Test] public void ExistingBauDispatcherRejectsAllArtMutationsBeforeAreaParsing()
        {
            Assert.IsNotNull(FadenwerkAuthoringBridge.Extension);
            Assert.AreEqual("BauModusBridge", FadenwerkAuthoringBridge.Extension.Method.DeclaringType.Name);
            foreach (string path in new[] { "art/propose", "art/approve-import", "art/request-export", "art/capture-review", "art/validate" })
            {
                var reply = FadenwerkAuthoringBridge.Extension("POST", path, "{}", new Dictionary<string, string>());
                Assert.AreEqual(405, reply.status, path);
                StringAssert.Contains("READ_ONLY", Encoding.UTF8.GetString(reply.body));
            }
        }

        [Test] public void UnsupportedReadRouteDoesNotReadTheFilesystem()
        {
            var response = ArtForgeCanvasController.Handle("GET", "art/../../secrets", "", null);
            Assert.AreEqual(404, response.status);
        }

        [Test] public void GraphAndAssetReadsRequireBothRevisionsBeforeFilesystemAccess()
        {
            foreach (string path in new[] { "art/graph", "art/asset" })
                Assert.AreEqual(428, ArtForgeCanvasController.Handle("GET", path, "", new Dictionary<string, string>()).status);
        }

        [Serializable] sealed class StateData { public bool ok, readOnly; public string projectSha, graphRevision; public int nodeCount; }
        [Serializable] sealed class GraphData { public bool ok, readOnly; public string schema, projectSha, graphRevision; public ArtGraphNode[] nodes; }

        [Test] public void RealProjectDispatcherReadsWorldForgeAndRejectsStaleRevisionsWithoutSceneChanges()
        {
            int objects = Resources.FindObjectsOfTypeAll<GameObject>().Length;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool dirty = scene.isDirty;
            var stateReply = FadenwerkAuthoringBridge.Extension("GET", "art/state", "", new Dictionary<string, string>());
            Assert.AreEqual(200, stateReply.status, Encoding.UTF8.GetString(stateReply.body));
            var state = JsonUtility.FromJson<StateData>(Encoding.UTF8.GetString(stateReply.body));
            Assert.IsTrue(state.ok && state.readOnly);
            var query = new Dictionary<string, string> { { "expectedProjectSha", state.projectSha }, { "expectedGraphRevision", state.graphRevision } };
            var graphReply = FadenwerkAuthoringBridge.Extension("GET", "art/graph", "", query);
            Assert.AreEqual(200, graphReply.status, Encoding.UTF8.GetString(graphReply.body));
            var graph = JsonUtility.FromJson<GraphData>(Encoding.UTF8.GetString(graphReply.body));
            Assert.IsTrue(graph.ok && graph.readOnly);
            Assert.AreEqual(ArtGraphContract.Schema, graph.schema);
            Assert.AreEqual(state.nodeCount, graph.nodes.Length);
            if (graph.nodes.Length > 0)
            {
                query["id"] = graph.nodes[0].id;
                Assert.AreEqual(200, FadenwerkAuthoringBridge.Extension("GET", "art/asset", "", query).status);
                query.Remove("id");
            }
            query["expectedGraphRevision"] = new string(state.graphRevision[0] == 'a' ? 'b' : 'a', 64);
            Assert.AreEqual(409, FadenwerkAuthoringBridge.Extension("GET", "art/graph", "", query).status);
            query["expectedGraphRevision"] = state.graphRevision;
            query["expectedProjectSha"] = new string(state.projectSha[0] == 'a' ? 'b' : 'a', 40);
            Assert.AreEqual(409, FadenwerkAuthoringBridge.Extension("GET", "art/graph", "", query).status);
            Assert.AreEqual(objects, Resources.FindObjectsOfTypeAll<GameObject>().Length);
            Assert.AreEqual(dirty, scene.isDirty);
            string evidence = Environment.GetEnvironmentVariable("ASHEN_ART_FORGE_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(evidence))
            {
                Assert.IsTrue(Path.IsPathRooted(evidence));
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence, "unity-readonly-snapshot.json"), "{\"state\":" + Encoding.UTF8.GetString(stateReply.body) + ",\"graph\":" + Encoding.UTF8.GetString(graphReply.body) + "}");
            }
            Debug.Log("[ArtForge readback] source=" + state.projectSha + " graph=" + state.graphRevision + " nodes=" + state.nodeCount + " scene unchanged; no import/approval");
        }

#if UNITY_EDITOR_LINUX
        [Test] public void SymlinkedFileAndRootAreRejected()
        {
            Fixture();
            string real = Path.Combine(root, "real.fbx");
            File.Move(Path.Combine(Folder, "model.fbx"), real);
            Link(real, Path.Combine(Folder, "model.fbx"));
            var view = ArtForgeWorldForgeReader.Read(root, Project);
            Assert.IsEmpty(view.graph.nodes);
            Assert.IsTrue(view.diagnostics.Any(d => d.code == "PATH_REJECTED"));
            string linkedRoot = Path.Combine(root, "linked-root");
            Link(root, linkedRoot);
            try
            {
                view = ArtForgeWorldForgeReader.Read(linkedRoot, Project);
                Assert.IsEmpty(view.graph.nodes);
                Assert.IsTrue(view.diagnostics.Any(d => d.code == "PATH_REJECTED"));
            }
            finally { File.Delete(linkedRoot); }
        }
        static void Link(string target, string path)
        {
            var info = new System.Diagnostics.ProcessStartInfo("/bin/ln") { UseShellExecute = false };
            info.ArgumentList.Add("-s"); info.ArgumentList.Add(target); info.ArgumentList.Add(path);
            using (var process = System.Diagnostics.Process.Start(info)) { process.WaitForExit(); Assert.AreEqual(0, process.ExitCode); }
        }
#endif
    }
}
