using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        /// <summary>
        /// Owner request 2026-11-26 (Profile Analyzer): record the CPU along the same route through the village, before and
        /// after a change to the village view, and compare the two captures. The route starts in the village square, walks
        /// a fixed loop for a fixed number of frames with the runtime running, and writes the profiler's binary capture to
        /// <c>Temp/Profiling/village-route-&lt;time&gt;.raw</c>. Load two captures in the Profiler window and pull them into
        /// the Profile Analyzer's Compare view. Explicit: it runs only when asked for by name.
        /// </summary>
        [UnityTest, Explicit("Records a CPU capture for the Profile Analyzer; run it by name"), Category("Profiling"), Timeout(1800000)]
        public IEnumerator ProfiledVillageRoute()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000);
            TravelStory();
            Assert.IsTrue(Game.IsVillage, "the route starts in the village");
            _runtime.enabled = true;
            for (int i = 0; i < 90; i++) yield return null; // let the village art settle before recording

            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "Profiling"));
            Directory.CreateDirectory(folder);
            string capture = System.IO.Path.Combine(folder, "village-route-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Profiler.logFile = capture;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
            foreach (var (x, z) in new[] { (10000, 0), (0, 10000), (-10000, 0), (0, -10000) })
            {
                Game.SetMoveInput(x, z);
                for (int i = 0; i < RouteFramesPerLeg; i++) yield return null;
            }
            Game.SetMoveInput(0, 0);
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = "";
            yield return null;

            string written = File.Exists(capture + ".raw") ? capture + ".raw" : capture;
            Assert.IsTrue(File.Exists(written) && new FileInfo(written).Length > 0, "the route wrote its capture: " + written);
            File.WriteAllText(System.IO.Path.Combine(folder, "latest-village-route.txt"), written);
            Debug.Log("ARE_PROFILED_ROUTE " + written + " " + new FileInfo(written).Length + " bytes");
        }

        private const int RouteFramesPerLeg = 150;
    }
}
