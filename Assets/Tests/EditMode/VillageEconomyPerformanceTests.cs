using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using AreArpg.Core;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// What the village economy costs: game tick time, memory handed out per tick, and the size of the save, for
    /// the population the game supports today (one resident per finished main quest) and for more. Measured in the
    /// editor's managed runtime on this machine: not an Android device and not a rendered frame. The numbers go to
    /// the test output and, when ARE_ECONOMY_EVIDENCE_DIR is set, into a file there.
    /// </summary>
    public sealed class VillageEconomyPerformanceTests
    {
        const int Day = 14400;

        static ArenaSimulation Village(int residents)
        {
            var character = CharacterProgression.CreateEntryProfile();
            typeof(CharacterProgression).GetMethod("EnterVillage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(character, null);
            character.Social.EnableForge();
            var world = new ArenaSimulation(71, character, 1, LootCatalog.Version1);
            for (int i = 0; i < residents; i++) world.CompleteMainQuest();
            return world;
        }

        [Test]
        public void RecordTickMemoryAndSaveCostsOfTheVillageEconomy()
        {
            var rows = new List<string>
            {
                "Unity " + Application.unityVersion + " / " + SystemInfo.operatingSystem + " / " + SystemInfo.processorType,
                "Editor managed measurements over real game ticks of the village; not an Android device and not a rendered frame.",
                "residents,ticks,mean_us_per_tick,p95_us_per_tick_of_100,worst_us_per_tick_of_100,bytes_per_tick,save_chars,events_in_memory,save_ms,load_ms"
            };
            double baseline = 0, supported = 0;
            foreach (int residents in new[] { 0, 1, 3, 8 })
            {
                ArenaSimulation world = Village(residents);
                for (int warm = 0; warm < 600; warm++) world.Step();
                // Three game days, timed in batches of 100 ticks: the economy looks at the village every tenth tick.
                const int batches = 3 * Day / 100;
                var times = new double[batches];
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int b = 0; b < batches; b++)
                {
                    long start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < 100; i++) world.Step();
                    times[b] = (Stopwatch.GetTimestamp() - start) * 1000000d / Stopwatch.Frequency / 100;
                }
                long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                double mean = times.Average();
                Array.Sort(times);
                CharacterProgression p = world.Progression;
                Assert.AreEqual(residents, p.Economy.Residents.Count);
                Assert.AreEqual(0, p.Economy.Audit().Count, "After three days the ledger still balances");
                string save = null; CharacterProgression loaded = null;
                long saveStart = Stopwatch.GetTimestamp();
                for (int i = 0; i < 5; i++) save = p.SaveString();
                double saveMs = (Stopwatch.GetTimestamp() - saveStart) * 1000d / Stopwatch.Frequency / 5;
                long loadStart = Stopwatch.GetTimestamp();
                for (int i = 0; i < 5; i++) Assert.IsTrue(CharacterProgression.TryLoad(save, out loaded));
                double loadMs = (Stopwatch.GetTimestamp() - loadStart) * 1000d / Stopwatch.Frequency / 5;
                Assert.AreEqual(p.StableHash, loaded.StableHash, "The save of a working village reads back exactly");
                Assert.AreEqual(p.Economy.StableHash, loaded.Economy.StableHash, "... and so does the village's ledger");
                Assert.AreEqual(save, loaded.SaveString(), "Saving what was loaded gives the same save");
                // Some Unity Mono runtimes return zero for the allocation counter. Never call that zero-allocation evidence.
                string bytes = allocatedBefore == 0 && allocatedAfter == 0 ? "unavailable" : ((allocatedAfter - allocatedBefore) / (double)(batches * 100)).ToString("F0", CultureInfo.InvariantCulture);
                rows.Add(string.Join(",", residents.ToString(CultureInfo.InvariantCulture), (batches * 100).ToString(CultureInfo.InvariantCulture),
                    mean.ToString("F2", CultureInfo.InvariantCulture), times[(int)(batches * .95)].ToString("F2", CultureInfo.InvariantCulture),
                    times[batches - 1].ToString("F2", CultureInfo.InvariantCulture), bytes, save.Length.ToString(CultureInfo.InvariantCulture),
                    p.Social.Forge.Archive.Events.Count.ToString(CultureInfo.InvariantCulture),
                    saveMs.ToString("F2", CultureInfo.InvariantCulture), loadMs.ToString("F2", CultureInfo.InvariantCulture)));
                if (residents == 0) baseline = mean;
                if (residents == 1) supported = mean;
                // A game tick is 100 ms long. Even the slowest hundred ticks must stay far inside that on this machine.
                Assert.Less(times[batches - 1], 10000d, residents + " residents: a tick took more than a tenth of its 100 ms");
            }
            rows.Add("economy_cost_per_tick_us_with_one_resident," + (supported - baseline).ToString("F2", CultureInfo.InvariantCulture));
            string report = string.Join("\n", rows) + "\n";
            TestContext.Out.WriteLine(report);
            UnityEngine.Debug.Log("VILLAGE_ECONOMY_COSTS\n" + report);
            string output = Environment.GetEnvironmentVariable("ARE_ECONOMY_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "unity-managed-village-economy-costs.csv"), report); }
        }
    }
}
