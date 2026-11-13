using System;
using System.Linq;
using AshenCrypt.MemoryForge.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AshenCrypt.MemoryForge.Tests
{
    public sealed class ControllerAssetTests
    {
        [Test]
        public void EditableConfigurationRequiresRealAdmissionAndRejectsChangedBytes()
        {
            var source=ForgeDefaults.CreateRegistry().BindNew("guard");var asset=ScriptableObject.CreateInstance<ForgeControllerAsset>();
            try
            {
                asset.CopyCandidate(source,new[]{new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>()),ForgeAction.Wait),new RegressionCase(new ObservationFrame(0,Array.Empty<Observation>()),ForgeAction.None)});
                Assert.IsFalse(asset.TryGetApproved(out _,out _,out _));
                Assert.IsTrue(asset.ReviewAndSeal(out var message),message);
                Assert.IsTrue(asset.TryGetApproved(out var approved,out var receipt,out message),message);Assert.AreEqual(source.Hash,approved.Hash);Assert.AreEqual(2,receipt.Passed);
                asset.Rules.First(r=>r.Action==ForgeAction.Wait).BasePriority++;
                Assert.IsFalse(asset.TryGetApproved(out _,out _,out _),"Editing approved bytes must invalidate the stored hash");
            }
            finally{UnityEngine.Object.DestroyImmediate(asset);}
        }
        [Test]
        public void FailingRegressionCannotSealAConfiguration()
        {
            var asset=ScriptableObject.CreateInstance<ForgeControllerAsset>();
            try
            {
                asset.CopyCandidate(ForgeDefaults.CreateRegistry().BindNew("guard"),new[]{new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>()),ForgeAction.Attack)});
                Assert.IsFalse(asset.ReviewAndSeal(out _));Assert.IsFalse(asset.TryGetApproved(out _,out _,out _));
            }
            finally{UnityEngine.Object.DestroyImmediate(asset);}
        }
    }
}
