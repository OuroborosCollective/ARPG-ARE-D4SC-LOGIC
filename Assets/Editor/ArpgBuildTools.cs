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
            RaiseBundleSizeWarning();
            ApplyAppIcon();
        }

        /// <summary>
        /// The launcher icon, rendered from the game's own (licensed) art. It lives with the private art, so a checkout
        /// without that art keeps Unity's default icon.
        /// </summary>
        public const string AppIconPath = "Assets/PrivateArt/Branding/AshenCryptIcon.png";

        internal static bool ApplyAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon == null) return false;
#if UNITY_ANDROID
            // Unity 6.6 only builds adaptive icons: background (layer 0) and foreground (layer 1). The opaque icon fills
            // both, so the launcher's mask and parallax always show the game's art; its motif sits in the central two thirds.
            var kind = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(UnityEditor.Build.NamedBuildTarget.Android, kind);
            foreach (PlatformIcon slot in icons) { slot.SetTexture(icon, 0); slot.SetTexture(icon, 1); }
            PlayerSettings.SetPlatformIcons(UnityEditor.Build.NamedBuildTarget.Android, kind, icons);
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Above this size Unity stops a bundle build with a modal question. The owner accepts bundles up to 3 GB
        /// (2026-11-22); the game's data travels in an install-time asset pack, so the base module stays small.
        /// </summary>
        public const int BundleSizeWarningMegabytes = 3072;

        /// <summary>Unity 6.6 keeps this threshold internal, so it is raised through the serialized player settings.</summary>
        internal static int RaiseBundleSizeWarning()
        {
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null || settings.Length == 0) return -1;
            var serialized = new SerializedObject(settings[0]);
            SerializedProperty size = serialized.FindProperty("AndroidAppBundleSizeToValidate");
            if (size == null) return -1;
            if (size.intValue < BundleSizeWarningMegabytes)
            {
                size.intValue = BundleSizeWarningMegabytes;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return size.intValue;
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
            // Windows and macOS file systems ignore case by default, so a differently written path is still the project.
            bool ignoreCase = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor;
            return InsideFolder(path, Directory.GetCurrentDirectory(), ignoreCase);
        }

        /// <summary>Whether a path lies in the folder; the trailing separator keeps sibling folders with the same prefix outside.</summary>
        internal static bool InsideFolder(string path, string folder, bool ignoreCase)
        {
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(root, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>
        /// Seconds since 2026-01-01 UTC: every bundle built later carries a higher version code, as Google Play requires,
        /// also when two builds start in the same minute. Play's largest code (2,100,000,000) is only reached in 2092. The
        /// first two bundles counted minutes (469318, 469396); every code counted in seconds is higher than those.
        /// </summary>
        public static int PlayVersionCode(DateTime utcNow)
        {
            return checked((int)Math.Floor((utcNow - new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds));
        }

        /// <summary>
        /// Signed Android App Bundle for a Google Play testing track (owner decision 2026-11-22). The game's assets go into
        /// an install-time asset pack (split application binary), so the base module stays under Play's size limit. The
        /// upload key and the version code are set for this build only; afterwards the project settings are as before
        /// and hold no password.
        /// </summary>
        [MenuItem("ARE ARPG/3. Build Signed App Bundle for Google Play")]
        public static void BuildAndroidAppBundle()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
            SigningFile signing = ReadSigning();
            PrepareEntryScene();
            ApplyAndroidPlayerSettings();
            bool bundle = EditorUserBuildSettings.buildAppBundle, split = PlayerSettings.Android.splitApplicationBinary;
            bool customKey = PlayerSettings.Android.useCustomKeystore;
            string keystore = PlayerSettings.Android.keystoreName, alias = PlayerSettings.Android.keyaliasName;
            int versionCode = PlayerSettings.Android.bundleVersionCode;
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
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.useCustomKeystore = customKey;
                PlayerSettings.Android.bundleVersionCode = versionCode;
                EditorUserBuildSettings.buildAppBundle = bundle;
                PlayerSettings.Android.splitApplicationBinary = split;
                RemoveKeyPasswords(GradleProject, signing.storePassword, signing.keyPassword);
            }
        }

        /// <summary>Where Unity 6.6 generates the Gradle project of a player build.</summary>
        private const string GradleProject = "Library/Bee/Android/Prj";

        /// <summary>
        /// Unity writes the key passwords as plain text into the generated Gradle build files. After a signed build they
        /// are removed again, whether the build succeeded or not; returns how many files were cleaned. Gradle's own script
        /// cache in the build user's home lies outside the project and is cleaned by the release procedure.
        /// </summary>
        internal static int RemoveKeyPasswords(string folder, params string[] secrets)
        {
            if (!Directory.Exists(folder)) return 0;
            int cleaned = 0;
            foreach (string pattern in new[] { "*.gradle", "*.properties", "*.kts" })
                foreach (string file in Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file), scrubbed = text;
                    foreach (string secret in secrets)
                        if (!string.IsNullOrEmpty(secret)) scrubbed = scrubbed.Replace(secret, "");
                    if (scrubbed == text) continue;
                    File.WriteAllText(file, scrubbed);
                    cleaned++;
                }
            return cleaned;
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
