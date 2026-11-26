using System.Collections;
using System.IO;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        /// <summary>
        /// Owner request 2026-11-26: the performance log of development builds writes one line a second with the area, frame
        /// rate, frame times, the Core's step time, enemies, loot and memory (and the Mali GPU counters on a Mali tablet; the
        /// editor has none, so its header says they are off). Switched on here directly; its file is removed afterwards.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePerformanceLogWritesALineEverySecond()
        {
            typeof(ArenaRuntime).GetField("_performanceOn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_runtime, (bool?)true);
            _runtime.enabled = true;
            float until = Time.realtimeSinceStartup + 3.5f;
            while (Time.realtimeSinceStartup < until) yield return null;
            var log = _runtime.PerformanceLog;
            Assert.IsNotNull(log, "the log is running");
            string path = log.FilePath;
            Assert.GreaterOrEqual(log.Lines, 2, "about one line a second");
            Object.Destroy(log);
            yield return null;
            var lines = File.ReadAllLines(path);
            try
            {
                StringAssert.StartsWith("# ", lines[0]);
                StringAssert.Contains("Mali counters", lines[0]);
                StringAssert.StartsWith("seconds,area,fps,frameMs,worstFrameMs,coreStepMs,enemies,groundLoot,systemMB,gcMB,gfxMB", lines[1]);
                var first = lines[2].Split(',');
                Assert.AreEqual(Game.ChapterName.Replace(',', ' '), first[1], "the line names the area");
                Assert.Greater(float.Parse(first[2], System.Globalization.CultureInfo.InvariantCulture), 0f, "a frame rate is measured");
            }
            finally { File.Delete(path); }
        }
    }
}
