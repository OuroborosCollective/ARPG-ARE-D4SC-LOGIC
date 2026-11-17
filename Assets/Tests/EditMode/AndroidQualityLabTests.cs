using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Actual editor measurements must never masquerade as physical Android acceptance.</summary>
    public sealed class AndroidQualityLabTests
    {
        [Serializable] private sealed class ReportProbe
        {
            public string schema;
            public string evidenceKind;
            public string sourceCommit;
            public string sourceDiffSha256;
            public string deviceAcceptance;
            public long allocatedBytes;
            public string unityVersion;
        }

        [Test]
        public void NativeReportSeparatesEditorMeasurementsFromAndroidAcceptance()
        {
            Type type = Type.GetType("AreArpg.EditorTools.AndroidQualityLab, Assembly-CSharp-Editor");
            Assert.IsNotNull(type, "The existing editor must provide the quality lab, not an external authority");
            MethodInfo capture = type.GetMethod("CaptureJson", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(capture);
            string json = (string)capture.Invoke(null, null);
            var report = JsonUtility.FromJson<ReportProbe>(json);
            Assert.AreEqual("ashen.android-quality-lab.v1", report.schema);
            Assert.AreEqual("native-editor-observation", report.evidenceKind);
            Assert.AreEqual("NOT_MEASURED", report.deviceAcceptance);
            StringAssert.IsMatch("^[0-9a-f]{40}$", report.sourceCommit);
            StringAssert.IsMatch("^[0-9a-f]{64}$", report.sourceDiffSha256);
            Assert.AreEqual(Application.unityVersion, report.unityVersion);
            Assert.Greater(report.allocatedBytes, 0, "Must sample the actual native editor");
        }
    }
}
