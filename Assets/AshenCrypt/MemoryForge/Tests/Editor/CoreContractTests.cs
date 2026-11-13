using System;
using System.Linq;
using NUnit.Framework;
using AshenCrypt.MemoryForge.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using AshenCrypt.MemoryForge.Samples;

namespace AshenCrypt.MemoryForge.Tests
{
    public sealed class CoreContractTests
    {
        [TestCase(1,false)]
        [TestCase(2,false)]
        [TestCase(1,true)]
        [TestCase(2,true)]
        public void ChecksummedMalformedArchiveReturnsFalseWithoutThrowing(int schema,bool malformedString)
        {
            string payload;
            using(var stream=new System.IO.MemoryStream())
            {
                using(var writer=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    writer.Write(schema);writer.Write(malformedString?1:-1);
                    if(malformedString){writer.Write(1UL);writer.Write((byte)MemoryKind.Help);writer.Write(-1);}
                }
                payload=Convert.ToBase64String(stream.ToArray());
            }
            string hash;
            using(var sha=System.Security.Cryptography.SHA256.Create())
                hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).Replace("-","").ToLowerInvariant();
            MemoryArchive rejected=null;
            Assert.DoesNotThrow(()=>Assert.IsFalse(MemoryArchive.TryLoad("NMF"+schema+"."+payload+"."+hash,ForgeDefaults.CreateRegistry(),out rejected)));
            Assert.IsNull(rejected);
        }
        [Test]
        public void IncludedExampleSceneKeepsBuildTimeShaderDependencies()
        {
            const string path="Assets/AshenCrypt/MemoryForge/Samples/RememberingVillage/RememberingVillage.unity";
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var sample=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RememberingVillage>()).Single();
                Assert.IsNotNull(sample.BuiltinLitShader);Assert.IsNotNull(sample.BuiltinUnlitShader);
                if(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")!=null){Assert.IsNotNull(sample.UniversalLitShader);Assert.IsNotNull(sample.UniversalUnlitShader);}
            }
            finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        [Test]
        public void IncludedRegistryAssetsRebuildTheExactAdmittedDefaultControllers()
        {
            var asset=AssetDatabase.LoadAssetAtPath<ForgeRegistryAsset>("Assets/AshenCrypt/MemoryForge/Samples/RememberingVillage/Controllers/DefaultRegistry.asset");
            Assert.IsNotNull(asset);var registry=asset.Build();Assert.AreEqual(9,registry.Versions.Count);
            foreach(var controller in registry.Versions)Assert.AreEqual(ForgeDefaults.ReviewedHash(controller.ArchetypeId),controller.Hash);
        }
        [Test]
        public void HardPermissionsAreAppliedBeforeWeightedAndNeuralScores()
        {
            var role=ForgeDefaults.CreateRegistry().BindNew("guard");
            var input=new int[(int)Feature.Count];input[(int)Feature.Danger]=10000;
            var output=new int[10];output[(int)ForgeAction.Attack]=40000;
            var policy=new ControllerArtifact(role.Archetype,role.ControllerId,2,role.Rules,network:new QuantizedNetwork(1,input,new int[1],output,new int[10]));
            var frame=new ObservationFrame(ActionSet.Of(ForgeAction.Wait),new[]{new Observation(Feature.Danger,10000)});
            var decision=UtilityEvaluator.Evaluate(policy,frame);Assert.AreEqual(ForgeAction.Wait,decision.Action);Assert.IsFalse(decision.Priorities.First(s=>s.Action==ForgeAction.Attack).Legal);
        }
        [Test]
        public void IndividualKnowledgeDeduplicatesAndRumorsStayUnconfirmed()
        {
            var registry=ForgeDefaults.CreateRegistry();var archive=new MemoryArchive();var a=archive.Spawn("one","traveler",registry);var b=archive.Spawn("two","guard",registry);
            var e=archive.Append(MemoryKind.Help,"player","one",1,10000,true,"rescue",new RelationshipChange(trust:5000));
            Assert.IsTrue(archive.Learn("one",e.Id,KnowledgeOrigin.Self,"one",1));Assert.IsFalse(archive.Learn("one",e.Id,KnowledgeOrigin.Self,"one",2));Assert.AreEqual(5000,a.RelationshipTo("player").Trust);Assert.AreEqual(0,b.Known.Count);
            Assert.IsTrue(archive.Report("one","two",e.Id,2));Assert.AreEqual(KnowledgeOrigin.Reported,b.Known[0].Origin);Assert.IsFalse(b.Known[0].IsFact);
            Assert.IsFalse(archive.Report("two","one",e.Id,3));Assert.AreEqual(1,a.Relevant(1000000).Count);
            string saved=archive.Save();Assert.IsTrue(MemoryArchive.TryLoad(saved,registry,out var restored));Assert.AreEqual(saved,restored.Save());
        }
        [Test]
        public void WrongHashesAndFailedRegressionReceiptsAreRejected()
        {
            var registry=ForgeDefaults.CreateRegistry();var old=registry.BindNew("guard");var candidate=new ControllerArtifact(old.Archetype,old.ControllerId,2,old.Rules);
            var frame=new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>());
            var fail=ControllerAdmission.Run(candidate,"test.fail",new[]{new RegressionCase(frame,ForgeAction.Attack)});
            Assert.Throws<ArgumentException>(()=>registry.Register(candidate,candidate.Hash,fail));
            var good=ControllerAdmission.Run(candidate,"test.pass",new[]{new RegressionCase(frame,ForgeAction.Wait)});
            Assert.Throws<ArgumentException>(()=>registry.Register(candidate,new string('0',64),good));
        }
        [Test]
        public void ExplicitMigrationPreservesHistoricalMemoryAndExactVersion()
        {
            var registry=ForgeDefaults.CreateRegistry();var archive=new MemoryArchive();var mind=archive.Spawn("one","traveler",registry);var old=mind.Controller;
            var candidate=new ControllerArtifact(old.Archetype,old.ControllerId,2,old.Rules);var frame=new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>());
            var admission=ControllerAdmission.Run(candidate,"test.migrate",new[]{new RegressionCase(frame,ForgeAction.Wait)});registry.Register(candidate,candidate.Hash,admission);
            Assert.AreEqual(1,mind.Controller.Version);Assert.IsTrue(archive.TryMigrate("one",candidate,registry,10,out var receipt));
            Assert.IsTrue(MemoryArchive.TryLoad(archive.Save(),registry,out var restored));Assert.AreEqual(2,restored.Get("one").Controller.Version);Assert.AreEqual(receipt.Checksum,restored.Migrations[0].Checksum);
            Assert.IsFalse(MemoryArchive.TryLoad(archive.Save(),ForgeDefaults.CreateRegistry(),out _));
        }
    }
}
