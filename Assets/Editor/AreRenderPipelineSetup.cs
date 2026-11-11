#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Creates and assigns the project-owned URP assets. Idempotent: existing assets
    /// (and therefore their GUIDs) are reused, only their settings are re-applied.
    /// Rendering is presentation only; it never touches the deterministic core.
    /// </summary>
    public static class AreRenderPipelineSetup
    {
        public const string Folder = "Assets/Settings/Rendering";
        public const string PipelinePath = "Assets/Settings/Rendering/AreUrpPipeline.asset";
        public const string RendererPath = "Assets/Settings/Rendering/AreUrpRenderer.asset";
        public const string GlobalSettingsPath = "Assets/Settings/Rendering/AreUrpGlobalSettings.asset";
        public const string DefaultVolumeProfilePath = "Assets/Settings/Rendering/AreUrpDefaultVolumeProfile.asset";
        private const string DefaultPostProcessData =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("ARE ARPG/Rendering/Apply Project URP Pipeline")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before changing the render pipeline.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
            }
            if (renderer.postProcessData == null)
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessData);
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            // Tablet-first budget (Tab A9 class GPU); the visual floor is raised by content, not by cost.
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 1;
            pipeline.supportsHDR = true;
            pipeline.shadowDistance = 60f;
            pipeline.shadowCascadeCount = 2;
            // The art base's water graphs sample scene depth and scene colour.
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            var serialized = new SerializedObject(pipeline);
            Set(serialized, "m_MainLightShadowmapResolution", 2048);
            Set(serialized, "m_AdditionalLightsShadowmapResolution", 1024);
            Set(serialized, "m_AdditionalLightsPerObjectLimit", 4);
            var soft = serialized.FindProperty("m_SoftShadowsSupported");
            if (soft == null) throw new InvalidOperationException("Missing URP property: m_SoftShadowsSupported");
            soft.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            // The art base is authored for linear lighting; gamma blows it out to white.
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            // URP generates its global settings into the Assets root; keep them in the project-owned folder.
            MoveIfPresent("Assets/UniversalRenderPipelineGlobalSettings.asset", GlobalSettingsPath);
            MoveIfPresent("Assets/DefaultVolumeProfile.asset", DefaultVolumeProfilePath);
            var global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(GlobalSettingsPath);
            if (global == null)
                throw new InvalidOperationException("Missing project URP global settings: " + GlobalSettingsPath);
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(global);
            var current = QualitySettings.GetQualityLevel();
            try
            {
                for (var level = 0; level < QualitySettings.names.Length; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    QualitySettings.renderPipeline = null; // every level inherits the single project pipeline
                }
            }
            finally { QualitySettings.SetQualityLevel(current, false); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (GraphicsSettings.currentRenderPipeline != pipeline)
                throw new InvalidOperationException("ARE URP pipeline is not the active render pipeline.");
            Debug.Log("ARE ARPG: project URP pipeline active: " + AssetDatabase.GetAssetPath(pipeline));
        }

        private static void MoveIfPresent(string from, string to)
        {
            if (!File.Exists(from) || File.Exists(to)) return;
            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }

        private static void Set(SerializedObject target, string property, int value)
        {
            var found = target.FindProperty(property);
            if (found == null) throw new InvalidOperationException("Missing URP property: " + property);
            found.intValue = value;
        }
    }
}
#endif
