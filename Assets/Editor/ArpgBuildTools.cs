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
            PlayerSettings.companyName = "OuroborosCollective";
            PlayerSettings.productName = "ARE Ashen Crypt";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.ouroboroscollective.areashencrypt");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            if (!Directory.Exists("Builds")) Directory.CreateDirectory("Builds");
            BuildReportGuard(BuildPipeline.BuildPlayer(new[] { EntryScenePath, ScenePath }, "Builds/ARE-Ashen-Crypt.apk",
                BuildTarget.Android, BuildOptions.None));
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
    }
}
#endif
