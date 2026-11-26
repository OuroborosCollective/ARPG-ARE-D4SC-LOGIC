using NUnit.Framework;
using UnityEditor.PackageManager;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Owner requests 2026-11-26: the Memory Profiler (compare memory snapshots of the Android player between areas), the
    /// Profile Analyzer (compare CPU captures of the same route) and ProBuilder (blockouts and missing pieces, baked to
    /// plain meshes before they go into the game) and the Project Auditor's current rules (finds mobile costs in code and
    /// assets; the auditor itself is part of the editor) are part of the project, from Unity's registry.
    /// </summary>
    public sealed class EditorToolPackagesTests
    {
        [TestCase("com.unity.memoryprofiler")]
        [TestCase("com.unity.performance.profile-analyzer")]
        [TestCase("com.unity.probuilder")]
        [TestCase("com.unity.project-auditor-rules")]
        public void TheEditorToolPackageIsInstalled(string name)
        {
            var package = PackageInfo.FindForPackageName(name);
            Assert.IsNotNull(package, name + " is installed");
            Assert.AreEqual(PackageSource.Registry, package.source, name + " comes from Unity's registry");
        }
    }
}
