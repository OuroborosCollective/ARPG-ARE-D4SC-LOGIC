#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreArpg.EditorTools
{
    public static class ArpgBuildTools
    {
        private const string ScenePath = "Assets/Scenes/Arena.unity";
        private const string EntryScenePath = "Assets/Scenes/AshenEntry.unity";

        /// <summary>Open the explicit two-act profile without replacing the legacy scene or its saves.</summary>
        [MenuItem("ARE ARPG/1. Open Ashen Crypt Entry")]
        public static void PrepareEntryScene()
        {
            if (!File.Exists(EntryScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, EntryScenePath);
            }
            else EditorSceneManager.OpenScene(EntryScenePath, OpenSceneMode.Single);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(EntryScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Explicitly rebuild the legacy demo scene when that editor command is chosen.</summary>
        [MenuItem("ARE ARPG/1. Prepare Demo Scene")]
        public static void PrepareDemoScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("ARE ARPG: Empty scene prepared. ArenaRuntime creates the arena at Play.");
        }

        /// <summary>Build both entry and legacy scenes using the configured Android target and inspect the real build result.</summary>
        [MenuItem("ARE ARPG/2. Build Android APK")]
        public static void BuildAndroidApk()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
            PrepareEntryScene();
            ApplyAndroidPlayerSettings();
            if (!Directory.Exists("Builds")) Directory.CreateDirectory("Builds");
            BuildReportGuard(BuildPipeline.BuildPlayer(new[] { EntryScenePath, ScenePath }, "Builds/ARE-Ashen-Crypt.apk",
                BuildTarget.Android, BuildOptions.None));
        }

        private static void ApplyAndroidPlayerSettings()
        {
            PlayerSettings.companyName = "OuroborosCollective";
            PlayerSettings.productName = "ARE Ashen Crypt";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PlayPackageName);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        }

        public const string PlayPackageName = "com.ouroboroscollective.areashencrypt";
        /// <summary>Environment variable naming the signing file of the Google Play upload key (outside the project).</summary>
        public const string SigningVariable = "ARE_ANDROID_SIGNING";

        [Serializable]
        private sealed class SigningFile { public string keystore = ""; public string alias = ""; public string storePassword = ""; public string keyPassword = ""; }

        /// <summary>
        /// The upload key and its passwords come from the file named by ARE_ANDROID_SIGNING. Both the file and the key
        /// must lie outside the project, so neither can be committed, and nothing of them is ever logged.
        /// </summary>
        private static SigningFile ReadSigning()
        {
            string path = Environment.GetEnvironmentVariable(SigningVariable);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new InvalidOperationException(SigningVariable + " must name the signing file of the Google Play upload key.");
            if (InsideProject(path)) throw new InvalidOperationException("The signing file must not lie inside the project.");
            SigningFile signing = JsonUtility.FromJson<SigningFile>(File.ReadAllText(path));
            if (signing == null || string.IsNullOrEmpty(signing.keystore) || !File.Exists(signing.keystore) || string.IsNullOrEmpty(signing.alias) ||
                string.IsNullOrEmpty(signing.storePassword) || string.IsNullOrEmpty(signing.keyPassword))
                throw new InvalidOperationException("The signing file of the Google Play upload key is incomplete.");
            if (InsideProject(signing.keystore)) throw new InvalidOperationException("The upload key must not lie inside the project.");
            return signing;
        }

        private static bool InsideProject(string path)
        {
            string project = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(project, StringComparison.Ordinal);
        }

        /// <summary>Minutes since 2026-01-01 UTC: every bundle built later carries a higher version code, as Google Play requires.</summary>
        public static int PlayVersionCode(DateTime utcNow)
        {
            return checked((int)Math.Floor((utcNow - new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMinutes));
        }

        /// <summary>
        /// Signed Android App Bundle for a Google Play testing track (owner decision 2026-11-22). The game's assets go into
        /// an install-time asset pack (split application binary), so the base module stays under Play's size limit. The
        /// upload key is set for this build only and removed from the project settings afterwards.
        /// </summary>
        [MenuItem("ARE ARPG/3. Build Signed App Bundle for Google Play")]
        public static void BuildAndroidAppBundle()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
            SigningFile signing = ReadSigning();
            PrepareEntryScene();
            ApplyAndroidPlayerSettings();
            bool bundle = EditorUserBuildSettings.buildAppBundle, split = PlayerSettings.Android.splitApplicationBinary;
            try
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = signing.keystore;
                PlayerSettings.Android.keystorePass = signing.storePassword;
                PlayerSettings.Android.keyaliasName = signing.alias;
                PlayerSettings.Android.keyaliasPass = signing.keyPassword;
                PlayerSettings.Android.bundleVersionCode = PlayVersionCode(DateTime.UtcNow);
                EditorUserBuildSettings.buildAppBundle = true;
                PlayerSettings.Android.splitApplicationBinary = true;
                if (!Directory.Exists("Builds")) Directory.CreateDirectory("Builds");
                BuildReportGuard(BuildPipeline.BuildPlayer(new[] { EntryScenePath, ScenePath }, "Builds/ARE-Ashen-Crypt.aab",
                    BuildTarget.Android, BuildOptions.None));
            }
            finally
            {
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = bundle;
                PlayerSettings.Android.splitApplicationBinary = split;
            }
        }

        // Fail closed: an emitted APK path alone does not prove that a build succeeded.
        /// <summary>Fail the batch build unless Unity reports a successful player build.</summary>
        private static void BuildReportGuard(UnityEditor.Build.Reporting.BuildReport report)
        {
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Android build failed: " + report.summary.result);
            Debug.Log("ARE ARPG Android build succeeded: " + report.summary.outputPath);
        }

        // In a licensed Unity CI runner: -batchmode -quit -executeMethod AreArpg.EditorTools.ArpgBuildTools.BuildAndroidApk
        // Google Play: ARE_ANDROID_SIGNING=<signing file outside the project> ... -executeMethod AreArpg.EditorTools.ArpgBuildTools.BuildAndroidAppBundle
    }
}
#endif
