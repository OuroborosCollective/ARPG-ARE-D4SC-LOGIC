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
            return TrackPending(path, query, response);
        }

        Request TrackPending(string path, Dictionary<string, string> query, FadenwerkExtensionReply response)
        {
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

        [UnityTest] public IEnumerator AbandonmentIsBoundToTheOriginalTicketAndNeverReturnsItsSnapshot()
        {
            var request = Start();
            var wrong = new Dictionary<string, string>(request.query) { ["cancelScan"] = "1", ["expectedProjectSha"] = new string('a', 40) };
            var refused = Dispatch(request.path, wrong);
            Assert.AreEqual(400, refused.status);
            StringAssert.Contains("ART_SCAN_MISMATCH", Encoding.UTF8.GetString(refused.body));
            var cancel = new Dictionary<string, string>(request.query) { ["cancelScan"] = "1" };
            var abandoned = Dispatch(request.path, cancel);
            Assert.AreEqual(200, abandoned.status, Encoding.UTF8.GetString(abandoned.body));
            StringAssert.Contains("ART_SCAN_ABANDONED", Encoding.UTF8.GetString(abandoned.body));
            Assert.AreEqual(410, Dispatch(request.path, request.query).status);
            unfinished.Remove(request);
            var next = Start();
            yield return Complete(next);
            Assert.AreEqual(200, next.reply.status, Encoding.UTF8.GetString(next.reply.body));
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
            // The real worker may have finished before the ninth request even
            // on a fast machine. Either refuse or reclaim one terminal ticket.
            Request ninth = null;
            if (excess.status == 202) ninth = TrackPending("art/state", new Dictionary<string, string>(), excess);
            else
            {
                Assert.AreEqual(503, excess.status);
                StringAssert.Contains("ART_SCAN_CAPACITY", Encoding.UTF8.GetString(excess.body));
            }
            int reclaimed = 0;
            foreach (var request in requests)
            {
                yield return Complete(request);
                if (request.reply.status == 410)
                {
                    StringAssert.Contains("ART_SCAN_EXPIRED", Encoding.UTF8.GetString(request.reply.body));
                    reclaimed++;
                }
                else Assert.AreEqual(200, request.reply.status, Encoding.UTF8.GetString(request.reply.body));
            }
            Assert.AreEqual(ninth == null ? 0 : 1, reclaimed, "A new admission may reclaim exactly one terminal result.");
            if (ninth != null)
            {
                yield return Complete(ninth);
                Assert.AreEqual(200, ninth.reply.status, Encoding.UTF8.GetString(ninth.reply.body));
            }
            var next = Start();
            yield return Complete(next);
            Assert.AreEqual(200, next.reply.status, Encoding.UTF8.GetString(next.reply.body));
        }

        [UnityTest] public IEnumerator UnclaimedResultsReleaseOneSlotUnderPressureAndOtherTicketsStayPollable()
        {
            // Each real Git check returns a revision conflict without scanning a large asset catalog.
            var query = new Dictionary<string, string> {
                ["expectedProjectSha"] = new string('0', 40), ["expectedGraphRevision"] = new string('0', 64)
            };
            var unclaimed = Enumerable.Range(0, 8).Select(_ => Start("art/graph", query)).ToArray();
            var watch = Stopwatch.StartNew();
            FadenwerkExtensionReply next;
            do
            {
                yield return null;
                next = Dispatch("art/state", new Dictionary<string, string>());
                if (next.status != 503) break;
                StringAssert.Contains("ART_SCAN_CAPACITY", Encoding.UTF8.GetString(next.body));
            } while (watch.Elapsed.TotalSeconds < 15);
            Assert.AreEqual(202, next.status, "A finished, unclaimed read must make space before the 60-second ticket expiry.");
            var nextTicket = JsonUtility.FromJson<Pending>(Encoding.UTF8.GetString(next.body));
            var replacement = new Request { path = "art/state", query = new Dictionary<string, string> { ["scanId"] = nextTicket.scanId } };
            unfinished.Add(replacement);

            int expired = 0;
            foreach (var request in unclaimed)
            {
                yield return Complete(request);
                if (request.reply.status == 410) expired++;
                else Assert.AreEqual(409, request.reply.status, "Every retained ticket must deliver its own revision check.");
            }
            Assert.AreEqual(1, expired, "Admitting one scan must preserve the other unclaimed results.");
            yield return Complete(replacement);
            Assert.AreEqual(200, replacement.reply.status, Encoding.UTF8.GetString(replacement.reply.body));
        }

        [UnityTearDown] public IEnumerator FinishOwnedReadOperations()
        {
            foreach (var request in unfinished.ToArray()) yield return Complete(request);
            maximumDispatchMs = 0;
        }
    }
}
