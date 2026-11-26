using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Profiling.SystemMetrics;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Owner request 2026-11-26 (System Metrics Mali, Profiling Core): a performance log for development builds, made for
    /// the target tablet (Galaxy Tab A9, Mali-G57). Once a second it writes one line: the area, frame rate, mean and worst
    /// frame time, the Core's own step time, enemies and loot on the ground, the memory in use, and the Mali GPU counters
    /// (active cycles, fragment and vertex load, shader arithmetic, texture and load/store load) where the GPU has them.
    /// Written to <c>persistentDataPath/PerformanceLog/perf-&lt;time&gt;.csv</c>; the owner sends the file after playing.
    /// </summary>
    public sealed class PerformanceLog : MonoBehaviour
    {
        private readonly List<(string name, ProfilerRecorder recorder)> _gpu = new List<(string, ProfilerRecorder)>();
        private ProfilerRecorder _systemMemory, _gcMemory, _gfxMemory;
        private StreamWriter _file;
        private float _windowStart;
        private int _frames;
        private double _frameSum, _frameWorst, _stepSum;
        public string FilePath { get; private set; }
        public int Lines { get; private set; }

        private void OnEnable()
        {
            _frames = 0; _frameSum = _frameWorst = _stepSum = 0;
            _systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _gcMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _gfxMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
            var mali = SystemMetricsMali.Instance;
            if (mali != null && mali.Active)
            {
                Gpu("GpuActiveCycles", mali.GpuCycles);
                Gpu("GpuFragmentUtilization", mali.GpuFragmentUtilization);
                Gpu("GpuVertexComputeUtilization", mali.GpuVertexAndComputeUtilization);
                Gpu("ShaderArithmeticUtilization", mali.GpuShaderArithmeticUtilization);
                Gpu("ShaderTextureUtilization", mali.GpuShaderTextureUtilization);
                Gpu("ShaderLoadStoreUtilization", mali.GpuShaderLoadStoreUtilization);
            }
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "PerformanceLog");
                Directory.CreateDirectory(folder);
                FilePath = Path.Combine(folder, "perf-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv");
                _file = new StreamWriter(FilePath, false, new UTF8Encoding(false));
                _file.WriteLine("# " + SystemInfo.deviceModel + " | " + SystemInfo.graphicsDeviceName + " | " + Application.version +
                    " | Mali counters " + (_gpu.Count > 0 ? "on" : "off"));
                var header = new StringBuilder("seconds,area,fps,frameMs,worstFrameMs,coreStepMs,enemies,groundLoot,systemMB,gcMB,gfxMB");
                foreach (var (name, _) in _gpu) header.Append(',').Append(name);
                _file.WriteLine(header);
                _file.Flush();
            }
            catch (Exception error)
            {
                Debug.LogWarning("ARE_PERFORMANCE_LOG could not open its file: " + error.Message);
                CloseFile();
            }
            _windowStart = Time.realtimeSinceStartup;
        }

        private void Gpu(string name, ProfilerRecorderHandle handle)
        {
            if (!handle.Valid) return;
            var recorder = new ProfilerRecorder(handle);
            recorder.Start();
            _gpu.Add((name, recorder));
        }

        /// <summary>One frame: its time and the Core's step time in it; a line is written once a second.</summary>
        public void Sample(string area, double coreStepMs, int enemies, int groundLoot)
        {
            if (_file == null) return;
            double frameMs = Time.unscaledDeltaTime * 1000.0;
            _frames++; _frameSum += frameMs; _stepSum += coreStepMs;
            if (frameMs > _frameWorst) _frameWorst = frameMs;
            float now = Time.realtimeSinceStartup;
            if (now - _windowStart < 1f) return;
            var c = CultureInfo.InvariantCulture;
            var line = new StringBuilder();
            line.Append(now.ToString("0.0", c)).Append(',').Append((area ?? "").Replace(',', ' '))
                .Append(',').Append((_frames / (now - _windowStart)).ToString("0.0", c))
                .Append(',').Append((_frameSum / _frames).ToString("0.00", c)).Append(',').Append(_frameWorst.ToString("0.00", c))
                .Append(',').Append((_stepSum / _frames).ToString("0.000", c)).Append(',').Append(enemies).Append(',').Append(groundLoot)
                .Append(',').Append(Mb(_systemMemory)).Append(',').Append(Mb(_gcMemory)).Append(',').Append(Mb(_gfxMemory));
            foreach (var (_, recorder) in _gpu) line.Append(',').Append(recorder.LastValue.ToString(c));
            try { _file.WriteLine(line); _file.Flush(); Lines++; }
            catch (Exception error) { Debug.LogWarning("ARE_PERFORMANCE_LOG write failed: " + error.Message); CloseFile(); }
            _windowStart = now; _frames = 0; _frameSum = _frameWorst = _stepSum = 0;
        }

        private static string Mb(ProfilerRecorder recorder) =>
            recorder.Valid ? (recorder.LastValue / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) : "";

        private void OnDisable()
        {
            foreach (var (_, recorder) in _gpu) recorder.Dispose();
            _gpu.Clear();
            _systemMemory.Dispose(); _gcMemory.Dispose(); _gfxMemory.Dispose();
            CloseFile();
        }

        private void CloseFile()
        {
            try { _file?.Dispose(); } catch (Exception) { }
            _file = null;
        }
    }
}
