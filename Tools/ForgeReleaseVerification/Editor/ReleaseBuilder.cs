using System;
using System.IO;
using AshenCrypt.MemoryForge.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AshenCrypt.ForgeVerification.Editor
{
    public static class ReleaseBuilder
    {
        public static void ConfigureAndAuthor()
        {
            const string folder="Assets/Verification/Settings";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(folder+"/Renderer.asset");
            if(renderer==null){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,folder+"/Renderer.asset");}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(folder+"/Pipeline.asset");
            if(pipeline==null){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,folder+"/Pipeline.asset");}
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            ForgePackageBuilder.BuildDemo();AssetDatabase.SaveAssets();
        }
        public static void BuildLinuxEvidence()
        {
            ConfigureAndAuthor();new GameObject("Verification-only driver").AddComponent<PlayerEvidence>();
            const string scene="Assets/Verification/Verification.unity";EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
            PlayerSettings.companyName="OuroborosCollective";PlayerSettings.productName="MemoryForgeVerification";
            string output=Environment.GetEnvironmentVariable("FORGE_PLAYER_OUTPUT");if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Missing FORGE_PLAYER_OUTPUT");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            Require(BuildPipeline.BuildPlayer(new[]{scene},output,BuildTarget.StandaloneLinux64,BuildOptions.Development));
        }
        public static void BuildAndroidExample()
        {
            ConfigureAndAuthor();PlayerSettings.companyName="OuroborosCollective";PlayerSettings.productName="MemoryForgeExample";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.ouroboroscollective.memoryforge.example");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            string output=Environment.GetEnvironmentVariable("FORGE_ANDROID_OUTPUT");if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Missing FORGE_ANDROID_OUTPUT");
            Directory.CreateDirectory(Path.GetDirectoryName(output));Require(BuildPipeline.BuildPlayer(new[]{ForgePackageBuilder.DemoPath},output,BuildTarget.Android,BuildOptions.None));
        }
        private static void Require(BuildReport report){if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Actual player build failed: "+report.summary.result);Debug.Log("FORGE_PLAYER_BUILD_SUCCEEDED "+report.summary.outputPath);}
    }
}
