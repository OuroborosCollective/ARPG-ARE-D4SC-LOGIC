using System;
using System.IO;
using Unity.Profiling.Memory;
using UnityEngine.Profiling;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        /// <summary>
        /// Owner request 2026-11-26 (Memory Profiler): compare the Android player's memory between areas and find the
        /// textures and meshes that weigh most after an area change. In a development build (or the editor) with snapshots
        /// switched on, the game takes one memory snapshot a few seconds after every change of area, numbered and named
        /// after the area, into <c>persistentDataPath/MemorySnapshots</c>; the Memory Profiler window opens and compares
        /// them. Switched on by the command-line argument <see cref="SnapshotArgument"/>, the environment variable
        /// <see cref="SnapshotVariable"/> or a file <see cref="SnapshotSwitchFile"/> in persistentDataPath (on a device:
        /// <c>adb shell touch /sdcard/Android/data/&lt;package&gt;/files/memory-snapshots.on</c>, then <c>adb pull</c> the
        /// folder). Release builds never take snapshots.
        /// </summary>
        public const string SnapshotArgument = "-areMemorySnapshots", SnapshotVariable = "ARE_MEMORY_SNAPSHOTS", SnapshotSwitchFile = "memory-snapshots.on";
        private const float SnapshotSettleSeconds = 3f;

        private bool? _snapshotsOn;
        private string _snapshotArea;
        private float _snapshotDue = -1f;
        private int _snapshotCount;
        private bool _snapshotBusy;

        public static string MemorySnapshotFolder => Path.Combine(Application.persistentDataPath, "MemorySnapshots");
        /// <summary>Each session writes into a folder of its own, so a new session never replaces snapshots not yet pulled.</summary>
        private string _snapshotSession;
        public int MemorySnapshotsTaken { get; private set; }

        private bool MemorySnapshotsOn
        {
            get
            {
                if (_snapshotsOn.HasValue) return _snapshotsOn.Value;
                bool on = false;
                if (Debug.isDebugBuild || Application.isEditor)
                {
                    try
                    {
                        on = Array.IndexOf(Environment.GetCommandLineArgs(), SnapshotArgument) >= 0 ||
                            Environment.GetEnvironmentVariable(SnapshotVariable) == "1" ||
                            File.Exists(Path.Combine(Application.persistentDataPath, SnapshotSwitchFile));
                    }
                    catch (Exception) { on = false; }
                }
                _snapshotsOn = on;
                return on;
            }
        }

        private void UpdateMemorySnapshots()
        {
            if (_game == null || !MemorySnapshotsOn) return;
            string area = _game.ChapterName;
            if (area != _snapshotArea) { _snapshotArea = area; _snapshotDue = Time.realtimeSinceStartup + SnapshotSettleSeconds; }
            if (_snapshotDue < 0f || _snapshotBusy || Time.realtimeSinceStartup < _snapshotDue) return;
            _snapshotDue = -1f;
            TakeAreaSnapshot(area);
        }

        private void TakeAreaSnapshot(string area)
        {
            try
            {
                if (_snapshotSession == null)
                    _snapshotSession = Path.Combine(MemorySnapshotFolder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(_snapshotSession);
                var safe = new System.Text.StringBuilder();
                foreach (char c in area) safe.Append(char.IsLetterOrDigit(c) ? c : '_');
                string path = Path.Combine(_snapshotSession, (++_snapshotCount).ToString("00") + "-" + safe + ".snap");
                _snapshotBusy = true;
                MemoryProfiler.TakeSnapshot(path, (file, ok) =>
                {
                    _snapshotBusy = false;
                    if (ok) MemorySnapshotsTaken++;
                    // With a profiler attached the snapshot goes to the connected editor instead of the file, and Unity
                    // reports false here; the editor's Memory Profiler window then holds it.
                    Debug.Log("ARE_MEMORY_SNAPSHOT " + (ok ? "saved " + file : Profiler.enabled ?
                        "sent to the connected editor (" + file + ")" : "failed " + file));
                }, CaptureFlags.ManagedObjects | CaptureFlags.NativeObjects | CaptureFlags.NativeAllocations);
            }
            catch (Exception error)
            {
                _snapshotBusy = false;
                Debug.LogWarning("ARE_MEMORY_SNAPSHOT failed: " + error.Message);
            }
        }
    }
}
