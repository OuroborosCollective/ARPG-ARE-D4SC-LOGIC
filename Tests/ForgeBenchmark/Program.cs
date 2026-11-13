using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

internal static class Program
{
    private static object Measure(string name,int batches,int operations,Action action)
    {
        for(int i=0;i<50;i++)action();
        var samples=new double[batches];long allocated=GC.GetAllocatedBytesForCurrentThread();
        for(int batch=0;batch<batches;batch++)
        {
            long start=Stopwatch.GetTimestamp();for(int i=0;i<operations;i++)action();
            samples[batch]=(Stopwatch.GetTimestamp()-start)*1000000d/Stopwatch.Frequency/operations;
        }
        long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;Array.Sort(samples);
        return new{name,operations=batches*operations,meanMicroseconds=samples.Average(),p95BatchMeanMicroseconds=samples[(int)(batches*.95)],allocatedBytesPerOperation=(double)bytes/(batches*operations)};
    }
    private static void Main()
    {
        var results=new List<object>();var registry=ForgeDefaults.CreateRegistry();var guard=registry.BindNew("guard");
        var frame=new ObservationFrame(guard.Archetype.AllowedActions,new[]{new Observation(Feature.Danger,10000),new Observation(Feature.Hostility,10000)});
        results.Add(Measure("utility-guard-with-decision-trace",40,100,()=>UtilityEvaluator.Evaluate(guard,frame)));
        var archive=new MemoryArchive();archive.Spawn("ilyan","traveler",registry);
        for(int i=0;i<1000;i++){var e=archive.Append(MemoryKind.Help,"player","ilyan",(ulong)i,5000,false,"provisions",new RelationshipChange(trust:1,gratitude:1));archive.Learn("ilyan",e.Id,KnowledgeOrigin.Self,"ilyan",(ulong)i);}
        results.Add(Measure("memory-recall-64-from-1000-events",40,50,()=>archive.Get("ilyan").Relevant(2000)));
        results.Add(Measure("archive-save-1000-events",20,5,()=>archive.Save()));
        string saved=archive.Save();results.Add(Measure("archive-load-1000-events",20,5,()=>{if(!MemoryArchive.TryLoad(saved,registry,out _))throw new Exception("Benchmark restore failed");}));
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.Social.EnableForge();var world=new ArenaSimulation(71,p,BigInteger.One,LootCatalog.Version1);
        results.Add(Measure("actual-core-village-tick-3-npcs",40,100,world.Step));
        results.Add(Measure("actual-core-village-save",30,10,()=>p.SaveString()));
        Console.WriteLine(JsonSerializer.Serialize(new{runtime=RuntimeInformation.FrameworkDescription,os=RuntimeInformation.OSDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),processorCount=Environment.ProcessorCount,archiveEvents=archive.Events.Count,archiveTokenBytes=System.Text.Encoding.UTF8.GetByteCount(saved),actualVillageNpcs=world.Npcs.Count,measurement="Managed host measurements; batch-mean p95, not device frame-time evidence",results},new JsonSerializerOptions{WriteIndented=true}));
    }
}
