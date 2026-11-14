using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public sealed class BowAnimationEditModeTests
    {
        private const string Source = "Assets/Resources/ARE/Visuals/Vendor/AnimationsStarterPack/Combat/BowShot.fbx";
        private const string Output = "Assets/Resources/ARE/Visuals/WeaponBowShot.anim";
        private Type Builder => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("AreArpg.EditorTools.WoodlandArtBuilder")).First(t => t != null);

        private static AnimationClip[] SourceClips()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the privately owned humanoid source and generated bow clip");
            return AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().ToArray();
        }

        [Test]
        public void ExportedBowContainsTheReviewedShotInsteadOfTheSourceTPose()
        {
            var clips = SourceClips();
            var shot = clips.Single(c => c.name == "BowShot");
            var pose = clips.Single(c => c.name == "tpose");
            var exported = AssetDatabase.LoadAssetAtPath<AnimationClip>(Output);
            Assert.IsNotNull(exported); Assert.IsTrue(exported.humanMotion);
            Assert.Greater(shot.length, pose.length);
            Assert.AreEqual(shot.length, exported.length, .0001f,
                "The private export must contain the reviewed BowShot take, not the one-frame T-pose");
            Assert.IsEmpty(AnimationUtility.GetAnimationEvents(exported));
            Assert.IsFalse(AnimationUtility.GetAnimationClipSettings(exported).loopTime);
            var arm = AnimationUtility.GetCurveBindings(shot)
                .First(b => b.type == typeof(Animator) && b.propertyName == "Right Forearm Stretch");
            var sourceCurve = AnimationUtility.GetEditorCurve(shot, arm);
            var actualCurve = AnimationUtility.GetEditorCurve(exported, arm);
            Assert.IsNotNull(actualCurve);
            for (int i = 0; i <= 12; i++)
                Assert.AreEqual(sourceCurve.Evaluate(shot.length * i / 12f),
                    actualCurve.Evaluate(exported.length * i / 12f), .0001f);
        }

        [Test]
        public void BowSelectionRejectsMissingDuplicateAndRenamedStillTakes()
        {
            var clips = SourceClips();
            var shot = clips.Single(c => c.name == "BowShot");
            var pose = clips.Single(c => c.name == "tpose");
            var select = Builder.GetMethod("SelectBowTake", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(select);
            var renamed = UnityEngine.Object.Instantiate(pose); renamed.name = "BowShot";
            try
            {
                foreach (var candidates in new[] { new UnityEngine.Object[] { pose },
                    new UnityEngine.Object[] { shot, shot }, new UnityEngine.Object[] { renamed } })
                {
                    var error = Assert.Throws<TargetInvocationException>(() => select.Invoke(null, new object[] { candidates }));
                    Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
                }
                Assert.AreSame(shot, select.Invoke(null, new object[] { new UnityEngine.Object[] { pose, shot } }));
            }
            finally { UnityEngine.Object.DestroyImmediate(renamed); }
        }

        [Test]
        public void RebuildingOnlyTheBowPreservesIdentityAndPrivateSourceBytes()
        {
            SourceClips();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var before = File.ReadAllBytes(Output); var meta = File.ReadAllBytes(Output + ".meta");
            var source = File.ReadAllBytes(Source); var sourceMeta = File.ReadAllBytes(Source + ".meta");
            string guid = AssetDatabase.AssetPathToGUID(Output);
            try
            {
                var build = Builder.GetMethod("BuildBowAnimation", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(build);
                build.Invoke(null, null);
                var first = File.ReadAllBytes(Output);
                build.Invoke(null, null);
                CollectionAssert.AreEqual(first, File.ReadAllBytes(Output), "Repeated authoring must keep the same derived artifact");
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(Output));
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(Output + ".meta"));
                CollectionAssert.AreEqual(source, File.ReadAllBytes(Source));
                CollectionAssert.AreEqual(sourceMeta, File.ReadAllBytes(Source + ".meta"));
            }
            finally
            {
                File.WriteAllBytes(Output, before); File.WriteAllBytes(Output + ".meta", meta);
                AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
