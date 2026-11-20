using System;
using System.IO;
using AshenCrypt.WorldForge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.Tests
{
    public class WFBlenderIntakeTests
    {
        [Test]
        public void OnlyCanonicalJobIdsCanSelectPrivatePaths()
        {
            string valid = new string('a', 64);
            Assert.IsTrue(WFBlenderIntake.IsJobId(valid));
            foreach (string value in new[] { null, "", valid.ToUpperInvariant(), valid + "\n", "../" + valid })
                Assert.IsFalse(WFBlenderIntake.IsJobId(value));
            Assert.Throws<ArgumentException>(() => WFBlenderIntake.ImportFbx("../sample"));
            Assert.Throws<ArgumentException>(() => WFBlenderIntake.Retry("../sample"));
        }

        [Test]
        public void SubmitRejectsSidecarGltfAndRelativePathsBeforeStartingTheCli()
        {
            Assert.Throws<ArgumentException>(() => WFBlenderIntake.Submit("/tmp/example.gltf"));
            Assert.Throws<ArgumentException>(() => WFBlenderIntake.Submit("relative.blend"));
        }

        [Test]
        public void ExistingCliJobIsResubmittedWithAnOpaqueSourcePath()
        {
            string source = Environment.GetEnvironmentVariable("WF_BLENDER_TEST_SOURCE");
            string id = Environment.GetEnvironmentVariable("WF_BLENDER_TEST_ID");
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(id))
                Assert.Ignore("Set the real job ID and its source to check the native CLI argument boundary.");
            string copy = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") +
                " Entwurf 'ä' & $Blender " + Path.GetExtension(source));
            try
            {
                File.Copy(source, copy);
                Assert.AreEqual(id, WFBlenderIntake.Submit(copy));
            }
            finally { if (File.Exists(copy)) File.Delete(copy); }
        }

        [Test]
        public void ResultNeedsEveryRecordedSidecarAtItsDeclaredSizeAndContainedPath()
        {
            string id = new string('a', 64);
            string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "textures"));
            try
            {
                var names = new[] {"model.fbx", "model.glb", "model.gltf", "model.bin", "textures/surface.png"};
                var entries = new System.Collections.Generic.List<string>();
                foreach (string name in names)
                {
                    File.WriteAllBytes(Path.Combine(folder, name), new byte[] {1, 2});
                    entries.Add("{\"path\":\"" + name + "\",\"size\":2,\"sha256\":\"" + id + "\"}");
                }
                string json = "{\"schema\":\"worldforge.blender-export.v1\",\"jobId\":\"" + id +
                    "\",\"files\":[" + string.Join(",", entries) + "]}";
                string receipt = Path.Combine(folder, "receipt.json");
                File.WriteAllText(receipt, json);
                Assert.IsTrue(WFBlenderIntake.IsCompleteResult(folder, id));
                string buffer = Path.Combine(folder, "model.bin");
                File.Delete(buffer);
                Assert.IsFalse(WFBlenderIntake.IsCompleteResult(folder, id), "Missing buffer");
                File.WriteAllBytes(buffer, new byte[] {1, 2});
                File.WriteAllBytes(Path.Combine(folder, "textures/surface.png"), new byte[] {1});
                Assert.IsFalse(WFBlenderIntake.IsCompleteResult(folder, id), "Truncated texture");
                File.WriteAllText(receipt, json.Replace("textures/surface.png", "textures/../model.bin"));
                Assert.IsFalse(WFBlenderIntake.IsCompleteResult(folder, id), "Noncanonical relative path");
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void DedicatedFailedWorkerJobCanBeRetriedInTheSameQueue()
        {
            string id = Environment.GetEnvironmentVariable("WF_BLENDER_TEST_FAILED_ID");
            if (string.IsNullOrEmpty(id)) Assert.Ignore("Set WF_BLENDER_TEST_FAILED_ID to a disposable failed export.");
            Assert.AreEqual(WFBlenderJobState.Failed, WFBlenderIntake.State(id));
            string error = Path.Combine(WFBlenderIntake.Root, "failed", id, "error.txt");
            var previousFailure = File.GetLastWriteTimeUtc(error);
            Assert.AreEqual(id, WFBlenderIntake.Retry(id));
            var next = WFBlenderIntake.State(id);
            Assert.IsTrue(next == WFBlenderJobState.Queued ||
                next == WFBlenderJobState.Failed && File.GetLastWriteTimeUtc(error) > previousFailure,
                "A successful retry must requeue the same ID or already record a fresh failure.");
        }

        [Test]
        public void RealWorkerFbxEntersManualReviewAndRetainsItsGuid()
        {
            string id = Environment.GetEnvironmentVariable("WF_BLENDER_TEST_ID");
            if (string.IsNullOrEmpty(id)) Assert.Ignore("Set WF_BLENDER_TEST_ID to a completed local Blender export.");
            Assert.IsTrue(WFBlenderIntake.IsJobId(id));
            string folder = "Assets/PrivateArt/WorldForgeBlender/" + id;
            string path = folder + "/model.fbx";
            bool alreadyImported = Directory.Exists(folder);
            bool hadRoot = Directory.Exists("Assets/PrivateArt/WorldForgeBlender");
            try
            {
                Assert.AreEqual(WFBlenderJobState.Ready, WFBlenderIntake.State(id));
                GameObject model = WFBlenderIntake.ImportFbx(id);
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(model));
                Assert.IsTrue(AssetImporter.GetAtPath(path) is ModelImporter);
                Assert.Greater(model.GetComponentsInChildren<Renderer>(true).Length, 0);
                string guid = AssetDatabase.AssetPathToGUID(path);
                Assert.AreEqual(32, guid.Length);
                Assert.AreEqual(WFAssetIntakeKind.ReviewRequired, WFAssetIntakeEvaluator.Assess(model, path).kind);
                WFBlenderIntake.ImportFbx(id);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
                Assert.IsEmpty(Directory.GetFiles(folder, "*.blend"));
                if (!alreadyImported)
                {
                    // Only exercise the conflict on this test's own import.
                    long size = new FileInfo(path).Length;
                    using (var stream = new FileStream(path, FileMode.Append)) stream.WriteByte(0);
                    Assert.Throws<InvalidDataException>(() => WFBlenderIntake.ImportFbx(id));
                    Assert.AreEqual(size + 1, new FileInfo(path).Length);
                    Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
                }
                Debug.Log("[WorldForge Blender] Imported real job " + id + " GUID " + guid);
            }
            finally
            {
                if (!alreadyImported) AssetDatabase.DeleteAsset(folder);
                if (!hadRoot && Directory.Exists("Assets/PrivateArt/WorldForgeBlender") &&
                    Directory.GetFileSystemEntries("Assets/PrivateArt/WorldForgeBlender").Length == 0)
                    AssetDatabase.DeleteAsset("Assets/PrivateArt/WorldForgeBlender");
            }
        }
    }
}
