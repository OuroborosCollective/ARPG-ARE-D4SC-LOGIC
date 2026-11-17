#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace AreArpg.EditorTools
{
    /// <summary>Read-only native editor observations; never substitutes for Android device acceptance.</summary>
    public sealed class AndroidQualityLab : EditorWindow
    {
        [Serializable]
        private sealed class TextureObservation
        {
            public string assetPath;
            public long editorBytes;
            public int width, height;
        }

        [Serializable]
        private sealed class Report
        {
            public string schema = "ashen.android-quality-lab.v1";
            public string evidenceKind = "native-editor-observation";
            public string deviceAcceptance = "NOT_MEASURED";
            public string androidBuildAcceptance = "NOT_MEASURED";
            public string ignoredPrivateAssetsAcceptance = "NOT_VERIFIED";
            public string memoryScope = "Whole editor process; loaded asset sizes are not Android residency or build sizes.";
            public string sourceFingerprintScope = "HEAD diff plus nonignored untracked Assets/Packages/ProjectSettings; ignored licensed assets are excluded.";
            public string sourceCommit, sourceDiffSha256, unityVersion, graphicsDevice, buildTarget;
            public bool sourceDirty, isPlaying;
            public long allocatedBytes, reservedBytes, unusedReservedBytes, monoUsedBytes;
            public int loadedProjectTextures, activeLights;
            public TextureObservation[] largestLoadedTextures;
        }

        private string _json = "Noch keine Messung. Beobachtung erfassen, danach Tests und echtes Android-Geraet getrennt pruefen.";
        private Vector2 _scroll;

        /// <summary>Open the lab inside the existing Unity editor, without installing another bridge.</summary>
        [MenuItem("Tools/Ashen Crypt/Android Quality Lab")]
        public static void Open()
        {
            GetWindow<AndroidQualityLab>("Android Quality Lab");
        }

        /// <summary>Capture actual native memory and source provenance; device and APK gates stay unmeasured.</summary>
        public static string CaptureJson()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string head = Git(root, "rev-parse HEAD").Trim();
            if (head.Length != 40 || !head.All(Uri.IsHexDigit))
                throw new InvalidOperationException("No verified Git revision; refusing an unbound quality report.");
            string fingerprint = Fingerprint(root);
            TextureObservation[] textures = Resources.FindObjectsOfTypeAll<Texture>()
                .Select(texture => new { texture, path = AssetDatabase.GetAssetPath(texture) })
                .Where(entry => entry.path.StartsWith("Assets/", StringComparison.Ordinal))
                .Select(entry => new TextureObservation
                {
                    assetPath = entry.path,
                    editorBytes = Profiler.GetRuntimeMemorySizeLong(entry.texture),
                    width = entry.texture.width,
                    height = entry.texture.height
                })
                .OrderByDescending(entry => entry.editorBytes)
                .ThenBy(entry => entry.assetPath, StringComparer.Ordinal).ToArray();
            var report = new Report
            {
                sourceCommit = head,
                sourceDiffSha256 = fingerprint,
                sourceDirty = Git(root, "status --porcelain --untracked-files=normal").Length != 0,
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                isPlaying = EditorApplication.isPlaying,
                allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                reservedBytes = Profiler.GetTotalReservedMemoryLong(),
                unusedReservedBytes = Profiler.GetTotalUnusedReservedMemoryLong(),
                monoUsedBytes = Profiler.GetMonoUsedSizeLong(),
                loadedProjectTextures = textures.Length,
                largestLoadedTextures = textures.Take(20).ToArray(),
                activeLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Count(light => light.enabled)
            };
            if (Git(root, "rev-parse HEAD").Trim() != head || Fingerprint(root) != fingerprint)
                throw new InvalidOperationException("Source changed during capture; repeat the observation.");
            return JsonUtility.ToJson(report, true);
        }

        /// <summary>Batch entry point: store a local report, never upload private asset names automatically.</summary>
        public static void CaptureToLibrary()
        {
            string folder = Path.Combine("Library", "AshenQualityLab");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "editor-observation.json"), CaptureJson(), new UTF8Encoding(false));
        }

        /// <summary>Include uncommitted and new source bytes; HEAD alone cannot identify a dirty workspace.</summary>
        private static string Fingerprint(string root)
        {
            using (var bytes = new MemoryStream())
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                writer.Write(Git(root, "diff --no-ext-diff --binary HEAD -- Assets Packages ProjectSettings"));
                string[] paths = Git(root, "ls-files -z --others --exclude-standard -- Assets Packages ProjectSettings")
                    .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    writer.Write(path);
                    using (var sha = SHA256.Create())
                    using (var file = File.OpenRead(Path.Combine(root, path)))
                        writer.Write(sha.ComputeHash(file));
                }
                writer.Flush();
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>Run fixed read-only Git commands with a timeout and propagate failure rather than inventing a SHA.</summary>
        private static string Git(string root, string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                process.Start();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(15000))
                {
                    process.Kill();
                    throw new TimeoutException("Git provenance read timed out.");
                }
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Git provenance read failed: " + error.GetAwaiter().GetResult());
                return output.GetAwaiter().GetResult();
            }
        }

        /// <summary>Keep measurements, simulation previews, tests and the established build action visibly separate.</summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Ashen Crypt - Android Quality Lab", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Editor-Messung ist kein Tab-A9-Leistungsnachweis. Device Simulator prueft Darstellung/Eingaben, nicht echte CPU/GPU/RAM-Grenzen. Ignorierte private Assets brauchen ihren eigenen Nachweis.", MessageType.Warning);
            if (GUILayout.Button("Native Editor-Beobachtung erfassen"))
            {
                try { _json = CaptureJson(); }
                catch (Exception error) { _json = "BLOCKED: " + error.Message; }
            }
            using (new EditorGUI.DisabledScope(!_json.StartsWith("{", StringComparison.Ordinal)))
            {
                if (GUILayout.Button("Beobachtung lokal exportieren"))
                {
                    string target = EditorUtility.SaveFilePanel("Privater Quality-Lab-Bericht", "", "ashen-editor-observation", "json");
                    if (target.Length > 0) File.WriteAllText(target, _json, new UTF8Encoding(false));
                }
            }
            if (GUILayout.Button("Device Simulator / Touch-Vorschau oeffnen")) OpenMenu("Window/General/Device Simulator");
            if (GUILayout.Button("Unity Profiler oeffnen")) OpenMenu("Window/Analysis/Profiler");
            if (GUILayout.Button("Bestehenden Test Runner oeffnen")) OpenMenu("Window/General/Test Runner");
            EditorGUILayout.HelpBox("Automatisierung: vorhandener Director run_tests (EditMode/PlayMode), danach build_android/inspect_build. Keine neue Build- oder Spiel-Autoritaet. Vor APK-Abnahme Source-SHA, private Asset-Hashes, Test-XML und APK-SHA zusammen pruefen.", MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_json, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Missing optional editor packages are explicit, not silently installed.</summary>
        private static void OpenMenu(string path)
        {
            if (!EditorApplication.ExecuteMenuItem(path))
                EditorUtility.DisplayDialog("Nicht verfuegbar", "Das vorhandene Editor-Werkzeug wurde nicht gefunden: " + path + ". Es wurde nichts installiert oder ersetzt.", "OK");
        }
    }
}
#endif
