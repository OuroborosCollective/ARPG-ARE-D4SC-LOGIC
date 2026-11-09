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

        [MenuItem("ARE ARPG/1. Prepare Demo Scene")]
        public static void PrepareDemoScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("ARE ARPG: Empty scene prepared. ArenaRuntime creates the arena at Play.");
        }

        [MenuItem("ARE ARPG/2. Build Android APK")]
        public static void BuildAndroidApk()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
            PrepareDemoScene();
            PlayerSettings.companyName = "OuroborosCollective";
            PlayerSettings.productName = "ARE Ashen Crypt";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.ouroboroscollective.areashencrypt");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            if (!Directory.Exists("Builds")) Directory.CreateDirectory("Builds");
            BuildReportGuard(BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/ARE-Ashen-Crypt.apk",
                BuildTarget.Android, BuildOptions.None));
        }

        // Fail closed: an emitted APK path alone does not prove that a build succeeded.
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
