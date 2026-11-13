using System;
using AshenCrypt.MemoryForge;
using NUnit.Framework;

namespace AreArpg.EditMode.Tests
{
    public sealed class ForgePolicyEditModeTests
    {
        [Test]
        public void ActualUnityIntegerNetworkAndAuditedMigrationRoundTrip()
        {
            var registry=ForgeDefaults.CreateRegistry();var guard=registry.BindNew("guard");
            var input=new int[(int)Feature.Count];input[(int)Feature.Danger]=10000;
            var output=new int[10];output[(int)ForgeAction.Attack]=10000;
            var model=new QuantizedNetwork(1,input,new int[1],output,new int[10]);
            var policy=new ControllerArtifact(guard.Archetype,guard.ControllerId,2,new[]{new UtilityRule(ForgeAction.Wait,0,0,new int[(int)Feature.Count]),new UtilityRule(ForgeAction.Attack,1,0,new int[(int)Feature.Count])},network:model);
            var frame=new ObservationFrame(ActionSet.Of(ForgeAction.Wait,ForgeAction.Attack),new[]{new Observation(Feature.Danger,7500)});
            var decision=UtilityEvaluator.Evaluate(policy,frame);Assert.AreEqual(ForgeAction.Attack,decision.Action);Assert.AreEqual(7500,decision.Score);
            var admission=ControllerAdmission.Run(policy,"unity.integer-network",new[]{new RegressionCase(frame,ForgeAction.Attack)});registry.Register(policy,policy.Hash,admission,false);
            var archive=new MemoryArchive();archive.Spawn("watch","guard",registry);
            Assert.IsTrue(archive.TryMigrate("watch",policy,registry,10,out var receipt));
            UtilityEvaluator.Decide(policy,frame,archive.Get("watch").DecisionState,11);
            string saved=archive.Save();Assert.IsTrue(MemoryArchive.TryLoad(saved,registry,out var restored));Assert.AreEqual(saved,restored.Save());
            Assert.AreEqual(receipt.Checksum,restored.Migrations[0].Checksum);Assert.AreEqual(policy.Hash,restored.Get("watch").Controller.Hash);
            var forbidden=new ObservationFrame(ActionSet.Of(ForgeAction.Wait),new[]{new Observation(Feature.Danger,10000)});
            Assert.AreEqual(ForgeAction.Wait,UtilityEvaluator.Decide(policy,forbidden,restored.Get("watch").DecisionState,12).Action);
        }
    }
}
