using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        /// <summary>
        /// Owner request 2026-11-26 (Profiling Core): the game's own values in the Profiler and its captures, so a hitch can be
        /// told apart: the Core's step time per frame, the enemies alive and the loot on the ground. They cost nothing while
        /// no profiler listens.
        /// </summary>
        private static readonly ProfilerCounterValue<long> CoreStepCounter = new ProfilerCounterValue<long>(ProfilerCategory.Scripts,
            "Ashen Core Step", ProfilerMarkerDataUnit.TimeNanoseconds, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);
        private static readonly ProfilerCounterValue<int> EnemyCounter = new ProfilerCounterValue<int>(ProfilerCategory.Scripts,
            "Ashen Enemies Alive", ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);
        private static readonly ProfilerCounterValue<int> LootCounter = new ProfilerCounterValue<int>(ProfilerCategory.Scripts,
            "Ashen Ground Loot", ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        /// <summary>
        /// The performance log (<see cref="PerformanceLog"/>) runs in development builds and the editor when switched on: by
        /// the command-line argument <see cref="PerformanceArgument"/>, the environment variable
        /// <see cref="PerformanceVariable"/>, or a file <see cref="PerformanceSwitchFile"/> in persistentDataPath (on the tablet:
        /// <c>adb shell touch /sdcard/Android/data/&lt;package&gt;/files/performance-log.on</c>). Development builds made for
        /// the owner's tests write it from the start (<see cref="PerformanceAlwaysInDevelopment"/>).
        /// </summary>
        public const string PerformanceArgument = "-arePerformanceLog", PerformanceVariable = "ARE_PERFORMANCE_LOG", PerformanceSwitchFile = "performance-log.on";
        public const bool PerformanceAlwaysInDevelopment = true;

        private long _stepTicksThisFrame;
        private (long x, long z)? _scriptedMove;

        /// <summary>
        /// A fixed movement in world basis points that replaces touch and keyboard while set, for profiling routes and
        /// tests that walk with the runtime running; null gives the input back.
        /// </summary>
        public void ScriptMove((long x, long z)? move) { _scriptedMove = move; }
        private bool? _performanceOn;
        private PerformanceLog _performanceLog;
        public PerformanceLog PerformanceLog => _performanceLog;

        /// <summary>Called around the Core's step: only the time is taken, nothing in the Core changes.</summary>
        private long BeginStepTiming() => System.Diagnostics.Stopwatch.GetTimestamp();
        private void EndStepTiming(long start) => _stepTicksThisFrame += System.Diagnostics.Stopwatch.GetTimestamp() - start;

        private bool PerformanceLogOn
        {
            get
            {
                if (_performanceOn.HasValue) return _performanceOn.Value;
                bool on = false;
                if (Debug.isDebugBuild || Application.isEditor)
                {
                    try
                    {
                        on = (!Application.isEditor && PerformanceAlwaysInDevelopment) ||
                            Array.IndexOf(Environment.GetCommandLineArgs(), PerformanceArgument) >= 0 ||
                            Environment.GetEnvironmentVariable(PerformanceVariable) == "1" ||
                            File.Exists(Path.Combine(Application.persistentDataPath, PerformanceSwitchFile));
                    }
                    catch (Exception) { on = false; }
                }
                _performanceOn = on;
                return on;
            }
        }

        private void UpdatePerformanceProbes()
        {
            double stepMs = _stepTicksThisFrame * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            CoreStepCounter.Value = (long)(_stepTicksThisFrame * (1e9 / System.Diagnostics.Stopwatch.Frequency));
            _stepTicksThisFrame = 0;
            if (_game == null) return;
            int enemies = 0;
            foreach (var monster in _game.Monsters) if (!monster.IsDead) enemies++;
            EnemyCounter.Value = enemies;
            LootCounter.Value = _game.GroundLoot.Count;
            if (!PerformanceLogOn) return;
            if (_performanceLog == null) _performanceLog = gameObject.AddComponent<PerformanceLog>();
            _performanceLog.Sample(_game.ChapterName, stepMs, enemies, _game.GroundLoot.Count);
        }
    }
}
