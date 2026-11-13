using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using AreArpg.Core;
using AshenCrypt.MemoryForge;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public sealed class NpcForgePerformanceTests
    {
        [Test]
        public void RecordActualUnityManagedDecisionMemoryAndHostTickCosts()
        {
            var rows=new List<string>{"Unity "+Application.unityVersion+" / "+SystemInfo.operatingSystem+" / "+SystemInfo.processorType,"Editor managed measurements, not Android device or rendered frame-time evidence.","operation,operations,median_batch_us,p95_batch_us,allocated_bytes_per_operation"};
            var registry=ForgeDefaults.CreateRegistry();var policy=registry.BindNew("guard");
            var frame=new ObservationFrame(policy.Archetype.AllowedActions,new[]{new Observation(Feature.Danger,10000),new Observation(Feature.Hostility,10000)});
            Measure(rows,"guard-decision",20,100,()=>UtilityEvaluator.Evaluate(policy,frame));
            foreach(int population in new[]{10,100,1000})
            {
                var archive=new MemoryArchive();for(int i=0;i<population;i++)archive.Spawn("npc."+i,"guard",registry);
                var minds=archive.Minds;ulong tick=0;
                Measure(rows,"decision-population-"+population,10,3,()=>{tick+=3;foreach(var mind in minds)UtilityEvaluator.Decide(mind.Controller,frame,mind.DecisionState,tick);});
            }
            var memory=new MemoryArchive();memory.Spawn("witness","guard",registry);
            for(int i=0;i<1000;i++){var e=memory.Append(MemoryKind.Help,"player","witness",(ulong)i,5000,false,"provision",new RelationshipChange(trust:1));memory.Learn("witness",e.Id,KnowledgeOrigin.Self,"witness",(ulong)i);}
            Measure(rows,"recall-recent-64-of-1000",20,10,()=>memory.Get("witness").Relevant(1500));
            Measure(rows,"save-1000-events",10,2,()=>memory.Save());string token=memory.Save();
            Measure(rows,"restore-1000-events",10,2,()=>{Assert.IsTrue(MemoryArchive.TryLoad(token,registry,out _));});
            var character=CharacterProgression.CreateEntryProfile();typeof(CharacterProgression).GetMethod("EnterVillage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(character,null);character.Social.EnableForge();
            var world=new ArenaSimulation(71,character,1,LootCatalog.Version1);Assert.AreEqual(3,world.Npcs.Count);
            Measure(rows,"actual-village-host-tick-3-npcs",20,100,world.Step);
            string report=string.Join("\n",rows)+"\n";TestContext.Out.WriteLine(report);
            string output=Environment.GetEnvironmentVariable("FORGE_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"unity-managed-npc-costs.csv"),report);}
        }
        private static void Measure(List<string> rows,string name,int batches,int operations,Action action)
        {
            for(int i=0;i<20;i++)action();var times=new double[batches];long before=GC.GetAllocatedBytesForCurrentThread();
            for(int b=0;b<batches;b++){long start=Stopwatch.GetTimestamp();for(int i=0;i<operations;i++)action();times[b]=(Stopwatch.GetTimestamp()-start)*1000000d/Stopwatch.Frequency/operations;}
            long after=GC.GetAllocatedBytesForCurrentThread(),allocated=after-before;Array.Sort(times);
            // Some Unity Mono runtimes return zero for this API. Never call that zero-allocation evidence.
            string allocation=before==0&&after==0?"unavailable":((double)allocated/(batches*operations)).ToString("F1",CultureInfo.InvariantCulture);
            rows.Add(string.Join(",",name,(batches*operations).ToString(CultureInfo.InvariantCulture),times[batches/2].ToString("F3",CultureInfo.InvariantCulture),times[Math.Min(batches-1,(int)(batches*.95))].ToString("F3",CultureInfo.InvariantCulture),allocation));
        }
    }
}
