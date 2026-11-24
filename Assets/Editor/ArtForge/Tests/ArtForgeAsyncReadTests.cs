using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Fadenwerk.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshenCrypt.ArtForge.Tests
{
    // Real dispatcher, Git and existing World Forge files. Timings are observations,
    // never simulation inputs or a claim about the physical tablet.
    public class ArtForgeAsyncReadTests
    {
        [Serializable] sealed class Pending { public string code, scanId; public int retryAfterMs; }
        [Serializable] sealed class State { public bool ok, readOnly; public string projectSha, graphRevision; public int nodeCount; }
        sealed class Request
        {
            public string path;
            public Dictionary<string, string> query;
            public FadenwerkExtensionReply reply;
        }
        readonly List<Request> unfinished = new List<Request>();
        double maximumDispatchMs;

        FadenwerkExtensionReply Dispatch(string path, Dictionary<string, string> query)
        {
            var watch = Stopwatch.StartNew();
            var response = FadenwerkAuthoringBridge.Extension("GET", path, "", query);
            watch.Stop();
            maximumDispatchMs = Math.Max(maximumDispatchMs, watch.Elapsed.TotalMilliseconds);
            return response;
        }

        Request Start(string path = "art/state", Dictionary<string, string> query = null)
        {
            query = query ?? new Dictionary<string, string>();
            var response = Dispatch(path, query);
            UnityEngine.Debug.Log("[ArtForge async start] status=" + response.status + " dispatchMs=" + maximumDispatchMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual(202, response.status, "A new byte verification must yield before delivering a result: " + Encoding.UTF8.GetString(response.body));
            var pending = JsonUtility.FromJson<Pending>(Encoding.UTF8.GetString(response.body));
            Assert.AreEqual("ART_SCAN_PENDING", pending.code);
            StringAssert.IsMatch("^[a-f0-9]{32}$", pending.scanId);
            Assert.That(pending.retryAfterMs, Is.InRange(100, 1000));
            var request = new Request { path = path, query = new Dictionary<string, string>(query), reply = response };
            request.query["scanId"] = pending.scanId;
            unfinished.Add(request);
            return request;
        }

        IEnumerator Complete(Request request)
        {
            var deadline = Stopwatch.StartNew();
            do
            {
                yield return null; // Exercise the real Editor update loop; never Wait/Result on the main thread.
                request.reply = Dispatch(request.path, request.query);
                Assert.Less(deadline.Elapsed.TotalSeconds, 40, "Read did not reach a terminal response.");
            } while (request.reply.status == 202);
            unfinished.Remove(request);
        }

        [UnityTest] public IEnumerator StateReadYieldsToEditorUpdatesBeforeDeliveringRealSnapshot()
        {
            int updates = 0;
            EditorApplication.CallbackFunction heartbeat = () => updates++;
            EditorApplication.update += heartbeat;
            try
            {
                var request = Start();
                yield return Complete(request);
                Assert.AreEqual(200, request.reply.status, Encoding.UTF8.GetString(request.reply.body));
                var state = JsonUtility.FromJson<State>(Encoding.UTF8.GetString(request.reply.body));
                Assert.IsTrue(state.ok && state.readOnly);
                Assert.IsTrue(ArtGraphContract.IsProjectSha(state.projectSha));
                Assert.IsTrue(ArtGraphContract.IsSha256(state.graphRevision));
                Assert.Greater(updates, 0, "The editor must continue updating during the read operation.");
                UnityEngine.Debug.Log("[ArtForge async readback] source=" + state.projectSha + " nodes=" + state.nodeCount + " editorUpdates=" + updates + " maxDispatchMs=" + maximumDispatchMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            }
            finally { EditorApplication.update -= heartbeat; }
        }

        [UnityTest] public IEnumerator ScanTicketCannotBeReboundToAnotherRouteOrQuery()
        {
            var request = Start();
            var changed = new Dictionary<string, string>(request.query) {
                ["expectedProjectSha"] = new string('a', 40), ["expectedGraphRevision"] = new string('b', 64)
            };
            var wrongRoute = Dispatch("art/graph", changed);
            Assert.AreEqual(400, wrongRoute.status);
            StringAssert.Contains("ART_SCAN_MISMATCH", Encoding.UTF8.GetString(wrongRoute.body));
            var wrongQuery = Dispatch("art/state", changed);
            Assert.AreEqual(400, wrongQuery.status);
            yield return Complete(request);
            Assert.AreEqual(200, request.reply.status, Encoding.UTF8.GetString(request.reply.body));
            Assert.AreEqual(410, Dispatch(request.path, request.query).status, "A consumed result must not become a reusable stale snapshot cache.");
        }

        [UnityTest] public IEnumerator ANewReadStartsANewVerificationInsteadOfReusingCompletedMetadata()
        {
            var first = Start();
            yield return Complete(first);
            Assert.AreEqual(200, first.reply.status, Encoding.UTF8.GetString(first.reply.body));
            var second = Start();
            Assert.AreNotEqual(first.query["scanId"], second.query["scanId"]);
            yield return Complete(second);
            Assert.AreEqual(200, second.reply.status, Encoding.UTF8.GetString(second.reply.body));
        }

        [UnityTest] public IEnumerator PendingReadsHaveABoundedCapacityAndReleaseAfterConsumption()
        {
            var requests = Enumerable.Range(0, 8).Select(_ => Start()).ToArray();
            var excess = Dispatch("art/state", new Dictionary<string, string>());
            Assert.AreEqual(503, excess.status);
            StringAssert.Contains("ART_SCAN_CAPACITY", Encoding.UTF8.GetString(excess.body));
            foreach (var request in requests)
            {
                yield return Complete(request);
                Assert.AreEqual(200, request.reply.status, Encoding.UTF8.GetString(request.reply.body));
            }
            var next = Start();
            yield return Complete(next);
            Assert.AreEqual(200, next.reply.status, Encoding.UTF8.GetString(next.reply.body));
        }

        [UnityTearDown] public IEnumerator FinishOwnedReadOperations()
        {
            foreach (var request in unfinished.ToArray()) yield return Complete(request);
            maximumDispatchMs = 0;
        }
    }
}
