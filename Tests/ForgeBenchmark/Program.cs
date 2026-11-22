using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>
/// External harness for the real Core and the Memory Forge (issue #6). Wall-clock time is taken only here, around the
/// simulation, never inside its 100-ms tick. Results are managed-host measurements, not Android frame times.
/// </summary>
internal static class Program
{
    private sealed record Timing(string name, int operations, double meanMicroseconds, double p50BatchMeanMicroseconds, double p95BatchMeanMicroseconds, double allocatedBytesPerOperation);

    private static Timing Measure(string name, int batches, int operations, Action action)
    {
        // Warm up for at least 50 calls and 250 ms, so tiered JIT has promoted the hot paths before measuring.
        long warm = Stopwatch.GetTimestamp();
        for (int i = 0; i < 50 || Stopwatch.GetTimestamp() - warm < Stopwatch.Frequency / 4; i++) action();
        var samples = new double[batches]; long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int batch = 0; batch < batches; batch++)
        {
            long start = Stopwatch.GetTimestamp(); for (int i = 0; i < operations; i++) action();
            samples[batch] = (Stopwatch.GetTimestamp() - start) * 1000000d / Stopwatch.Frequency / operations;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated; Array.Sort(samples);
        return new Timing(name, batches * operations, samples.Average(), Percentile(samples, .50), Percentile(samples, .95), (double)bytes / (batches * operations));
    }

    private static double Percentile(double[] sorted, double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Floor(q * sorted.Length))];

    /// <summary>The host's 1/5/15-minute load average (Linux), so timings taken on a shared machine can be judged.</summary>
    private static string HostLoad() => System.IO.File.Exists("/proc/loadavg") ? System.IO.File.ReadAllText("/proc/loadavg").Trim() : "unavailable";

    private static object Memory()
    {
        using var process = Process.GetCurrentProcess();
        return new { managedHeapBytes = GC.GetTotalMemory(false), workingSetBytes = process.WorkingSet64, peakWorkingSetBytes = process.PeakWorkingSet64 };
    }

    private static void Near(ArenaSimulation world, int npcId)
    {
        var npc = world.Npcs.Single(n => n.Id == npcId);
        npc.Xmm = world.PlayerXmm + 1000; npc.Zmm = world.PlayerZmm; npc.InteractionCooldownTicks = 0;
    }

    /// <summary>
    /// The supported world: the three village NPCs with the causal camp decision (two donations, the second one the
    /// cause), a real meeting in which Mara tells Ilyan, and 10,000 ticks of 100 ms. Per-tick timings give p50/p95.
    /// </summary>
    private static object CausalVillage(ulong seed, int ticks)
    {
        var p = CharacterProgression.CreateEntryProfile(); p.EnterVillage(); p.Social.EnableForge();
        var world = new ArenaSimulation(seed, p, BigInteger.One, LootCatalog.Version1);
        if (!world.CompleteArrivalScene()) throw new Exception("arrival scene not pending");
        for (int i = 0; i < 2; i++) { Near(world, SocialLedger.MaraNpcId); if (!world.DonateCampRation().Accepted) throw new Exception("donation refused"); }
        if (!p.CampDecisionMade) throw new Exception("camp decision missing");
        var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId); var ilyan = world.Npcs.Single(n => n.Id == 0);
        var perTick = new double[ticks];
        using var process = Process.GetCurrentProcess();
        TimeSpan cpuStart = process.TotalProcessorTime; long allocated = GC.GetAllocatedBytesForCurrentThread(); long wallStart = Stopwatch.GetTimestamp();
        for (int tick = 0; tick < ticks; tick++)
        {
            if (tick < 300) { ilyan.Xmm = mara.Xmm + 850; ilyan.Zmm = mara.Zmm; }
            long start = Stopwatch.GetTimestamp(); world.Step();
            perTick[tick] = (Stopwatch.GetTimestamp() - start) * 1000000d / Stopwatch.Frequency;
        }
        long wall = Stopwatch.GetTimestamp() - wallStart; long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        process.Refresh(); TimeSpan cpu = process.TotalProcessorTime - cpuStart;
        var sorted = (double[])perTick.Clone(); Array.Sort(sorted);
        int slowest = Array.IndexOf(perTick, sorted[sorted.Length - 1]);
        var archive = p.Social.Forge.Archive; string save = p.SaveString(); string token = archive.Save();
        // Save and reload: the reloaded profile must write the same bytes and carry the same archive checksum.
        bool reloaded = CharacterProgression.TryLoad(save, out var loaded);
        string reloadedToken = reloaded ? loaded.Social.Forge.Archive.Save() : "";
        return new
        {
            name = "causal-village-3-npcs", seed, ticks, simulatedSeconds = ticks / 10.0, villageNpcs = world.Npcs.Count,
            cpuMilliseconds = cpu.TotalMilliseconds, wallMilliseconds = wall * 1000d / Stopwatch.Frequency,
            tickMicroseconds = new { p50 = Percentile(sorted, .50), p95 = Percentile(sorted, .95), p99 = Percentile(sorted, .99), max = sorted[sorted.Length - 1], maxAtTick = slowest, mean = sorted.Average() },
            allocatedBytes = bytes, allocatedBytesPerTick = (double)bytes / ticks,
            events = archive.Events.Count, acquisitions = archive.Acquisitions.Count,
            reportedAcquisitions = archive.Acquisitions.Count(k => k.Origin == KnowledgeOrigin.Reported),
            saveBytes = System.Text.Encoding.UTF8.GetByteCount(save), archiveTokenBytes = System.Text.Encoding.UTF8.GetByteCount(token),
            worldHash = world.ComputeWorldHash().ToString("X16"), archiveChecksum = token.Substring(token.LastIndexOf('.') + 1),
            saveReload = new { loads = reloaded, sameSaveBytes = reloaded && loaded.SaveString() == save, sameArchiveToken = reloadedToken == token },
            memory = Memory()
        };
    }

    /// <summary>
    /// The Forge evaluator over N archive minds: one decision for every mind per sweep, plus recall for every mind.
    /// Every mind knows the same number of its own memories, so the sweeps differ only in N.
    /// These are evaluator measurements; the actual game world holds three village NPCs, not N.
    /// </summary>
    private static object Minds(int count, NpcArchetypeControllerRegistry registry, int sweeps)
    {
        const int MemoriesPerMind = 16;
        var archive = new MemoryArchive();
        string[] archetypes = { "traveler", "guard" };
        for (int i = 0; i < count; i++) archive.Spawn("mind." + i.ToString("D4"), archetypes[i % archetypes.Length], registry);
        for (int i = 0; i < count * MemoriesPerMind; i++)
        {
            string mind = "mind." + (i % count).ToString("D4");
            var e = archive.Append(MemoryKind.Help, "player", mind, (ulong)i, 5000, false, "provisions", new RelationshipChange(trust: 1, gratitude: 1));
            archive.Learn(mind, e.Id, KnowledgeOrigin.Self, mind, (ulong)i);
        }
        var minds = archive.Minds.ToArray();
        var frames = minds.Select(m => new ObservationFrame(m.Controller.Archetype.AllowedActions,
            new[] { new Observation(Feature.Danger, 10000), new Observation(Feature.Hostility, 5000) })).ToArray();
        ulong now = (ulong)(count * MemoriesPerMind);
        Action sweep = () => { for (int i = 0; i < minds.Length; i++) { UtilityEvaluator.Evaluate(minds[i].Controller, frames[i]); minds[i].Relevant(now); } };
        var timing = Measure("forge-evaluator-" + count + "-minds-sweep", sweeps, 1, sweep);
        long saveStart = Stopwatch.GetTimestamp(); string token = archive.Save(); double saveMs = (Stopwatch.GetTimestamp() - saveStart) * 1000d / Stopwatch.Frequency;
        long loadStart = Stopwatch.GetTimestamp(); bool loads = MemoryArchive.TryLoad(token, registry, out var restored); double loadMs = (Stopwatch.GetTimestamp() - loadStart) * 1000d / Stopwatch.Frequency;
        return new { name = "forge-evaluator-" + count + "-minds", minds = count, memoriesPerMind = MemoriesPerMind, events = archive.Events.Count, acquisitions = archive.Acquisitions.Count,
            note = "evaluator and recall over archive minds; not world NPCs", sweep = timing, perMindMicroseconds = timing.meanMicroseconds / count,
            archiveTokenBytes = System.Text.Encoding.UTF8.GetByteCount(token), saveMilliseconds = saveMs, loadMilliseconds = loadMs,
            reloadSameToken = loads && restored.Save() == token, memory = Memory() };
    }

    private static void Main(string[] args)
    {
        var results = new List<object>(); var registry = ForgeDefaults.CreateRegistry();
        if (args.Contains("--manifest")) { foreach (var c in registry.Versions) Console.WriteLine(c.ArchetypeId + " " + c.Hash); return; }
        string sha = Array.IndexOf(args, "--sha") is int at && at >= 0 && at + 1 < args.Length ? args[at + 1] : "unknown";
        var guard = registry.BindNew("guard");
        var frame = new ObservationFrame(guard.Archetype.AllowedActions, new[] { new Observation(Feature.Danger, 10000), new Observation(Feature.Hostility, 10000) });
        results.Add(Measure("utility-guard-without-formatting", 40, 100, () => UtilityEvaluator.Evaluate(guard, frame)));
        results.Add(Measure("utility-guard-with-decision-trace", 40, 100, () => { var trace = UtilityEvaluator.Evaluate(guard, frame).CanonicalTrace; if (trace.Length == 0) throw new Exception("Missing trace"); }));
        var archive = new MemoryArchive(); archive.Spawn("ilyan", "traveler", registry);
        for (int i = 0; i < 1000; i++) { var e = archive.Append(MemoryKind.Help, "player", "ilyan", (ulong)i, 5000, false, "provisions", new RelationshipChange(trust: 1, gratitude: 1)); archive.Learn("ilyan", e.Id, KnowledgeOrigin.Self, "ilyan", (ulong)i); }
        results.Add(Measure("memory-recall-64-from-1000-events", 40, 50, () => archive.Get("ilyan").Relevant(2000)));
        results.Add(Measure("archive-save-1000-events", 20, 5, () => archive.Save()));
        string saved = archive.Save(); results.Add(Measure("archive-load-1000-events", 20, 5, () => { if (!MemoryArchive.TryLoad(saved, registry, out _)) throw new Exception("Benchmark restore failed"); }));
        var p = CharacterProgression.CreateEntryProfile(); p.EnterVillage(); p.Social.EnableForge(); var world = new ArenaSimulation(71, p, BigInteger.One, LootCatalog.Version1);
        results.Add(Measure("actual-core-village-tick-3-npcs", 40, 100, world.Step));
        results.Add(Measure("actual-core-village-save", 30, 10, () => p.SaveString()));
        string loadBefore = HostLoad();
        var causal = CausalVillage(6107, 10000);
        var minds = new[] { 10, 100, 1000 }.Select(n => Minds(n, ForgeDefaults.CreateRegistry(), n >= 1000 ? 20 : 40)).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            sourceSha = sha, runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), processorCount = Environment.ProcessorCount,
            hostLoadAverage = new { before = loadBefore, after = HostLoad() },
            archiveEvents = archive.Events.Count, archiveTokenBytes = System.Text.Encoding.UTF8.GetByteCount(saved), actualVillageNpcs = world.Npcs.Count,
            measurement = "Managed host measurements from an external harness; batch-mean and per-tick percentiles, not device frame-time evidence",
            results, causalVillage = causal, forgeEvaluator = minds
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
