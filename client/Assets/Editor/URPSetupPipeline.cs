using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OutrunAftermath.Client.Editor
{
    public static class URPSetupPipeline
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererAssetPath = "Assets/Settings/URP_HighFidelity_Renderer.asset";
        private const string PipelineAssetPath = "Assets/Settings/URP_HighFidelity.asset";
        private const string VolumeProfilePath = "Assets/DefaultVolumeProfile.asset";

        [MenuItem("Outrun/Setup URP High Fidelity")]
        public static void SetupURP()
        {
            Debug.Log("[URPSetupPipeline] Starting authoritative URP Foundation configuration...");

            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
            }

            // 1. Configure or Create UniversalRendererData
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "URP_HighFidelity_Renderer";
                AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
                Debug.Log($"[URPSetupPipeline] Created new UniversalRendererData at {RendererAssetPath}");
            }

            rendererData.renderingMode = RenderingMode.ForwardPlus;

            // Ensure ScreenSpaceAmbientOcclusion renderer feature is present
            EnsureSSAOFeature(rendererData);
            EditorUtility.SetDirty(rendererData);

            // 2. Configure or Create UniversalRenderPipelineAsset
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                urpAsset.name = "URP_HighFidelity";
                AssetDatabase.CreateAsset(urpAsset, PipelineAssetPath);
                Debug.Log($"[URPSetupPipeline] Created new UniversalRenderPipelineAsset at {PipelineAssetPath}");
            }

            // Bind renderer data to URP asset via SerializedObject
            ConfigureURPAssetSettings(urpAsset, rendererData);

            // 3. Configure DefaultVolumeProfile
            var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (volumeProfile == null)
            {
                volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(volumeProfile, VolumeProfilePath);
                Debug.Log($"[URPSetupPipeline] Created new VolumeProfile at {VolumeProfilePath}");
            }

            ConfigureVolumeProfileComponents(volumeProfile);

            // Link volume profile to URP Asset
            var urpSo = new SerializedObject(urpAsset);
            var volProp = urpSo.FindProperty("m_VolumeProfile");
            if (volProp != null)
            {
                volProp.objectReferenceValue = volumeProfile;
                urpSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 4. Assign to GraphicsSettings (Default Render Pipeline)
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            AssignToGraphicsSettingsAsset(urpAsset);

            // 5. Assign to QualitySettings across all quality tiers
            AssignToQualitySettings(urpAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[URPSetupPipeline] URP Foundation configuration SUCCEEDED.");
            VerifyURPConfiguration();
        }

        private static void EnsureSSAOFeature(UniversalRendererData rendererData)
        {
            bool hasSSAO = false;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature != null && feature is ScreenSpaceAmbientOcclusion)
                {
                    hasSSAO = true;
                    break;
                }
            }

            if (!hasSSAO)
            {
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(ssao, rendererData);
                rendererData.rendererFeatures.Add(ssao);
                Debug.Log("[URPSetupPipeline] Added ScreenSpaceAmbientOcclusion renderer feature to renderer data.");
            }
        }

        private static void ConfigureURPAssetSettings(UniversalRenderPipelineAsset urpAsset, UniversalRendererData rendererData)
        {
            var so = new SerializedObject(urpAsset);

            // Set renderer data list
            var rendererDataListProp = so.FindProperty("m_RendererDataList");
            if (rendererDataListProp != null)
            {
                rendererDataListProp.arraySize = 1;
                rendererDataListProp.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            }

            var defaultRendererProp = so.FindProperty("m_DefaultRendererIndex");
            if (defaultRendererProp != null) defaultRendererProp.intValue = 0;

            // HDR & MSAA
            var hdrProp = so.FindProperty("m_SupportsHDR");
            if (hdrProp != null) hdrProp.boolValue = true;

            var msaaProp = so.FindProperty("m_MSAA");
            if (msaaProp != null) msaaProp.intValue = (int)MsaaQuality._4x;

            // Shadows
            var mainLightShadowsProp = so.FindProperty("m_MainLightShadowsSupported");
            if (mainLightShadowsProp != null) mainLightShadowsProp.boolValue = true;

            var addLightShadowsProp = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (addLightShadowsProp != null) addLightShadowsProp.boolValue = true;

            var softShadowsProp = so.FindProperty("m_SoftShadowsSupported");
            if (softShadowsProp != null) softShadowsProp.boolValue = true;

            var shadowDistProp = so.FindProperty("m_ShadowDistance");
            if (shadowDistProp != null) shadowDistProp.floatValue = 80.0f;

            var cascadeCountProp = so.FindProperty("m_ShadowCascadeCount");
            if (cascadeCountProp != null) cascadeCountProp.intValue = 4;

            var cascade4SplitProp = so.FindProperty("m_Cascade4Split");
            if (cascade4SplitProp != null) cascade4SplitProp.vector3Value = new Vector3(0.067f, 0.2f, 0.467f);

            // Color grading
            var colorGradingProp = so.FindProperty("m_ColorGradingMode");
            if (colorGradingProp != null) colorGradingProp.intValue = (int)ColorGradingMode.HighDynamicRange;

            // SRP Batcher
            var srpBatcherProp = so.FindProperty("m_UseSRPBatcher");
            if (srpBatcherProp != null) srpBatcherProp.boolValue = true;

            // Depth / Opaque textures
            var depthTexProp = so.FindProperty("m_RequireDepthTexture");
            if (depthTexProp != null) depthTexProp.boolValue = true;

            var opaqueTexProp = so.FindProperty("m_RequireOpaqueTexture");
            if (opaqueTexProp != null) opaqueTexProp.boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urpAsset);
        }

        private static void ConfigureVolumeProfileComponents(VolumeProfile volumeProfile)
        {
            // 1. Tonemapping (ACES)
            if (!volumeProfile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = volumeProfile.Add<Tonemapping>(true);
            }
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            // 2. Bloom
            if (!volumeProfile.TryGet<Bloom>(out var bloom))
            {
                bloom = volumeProfile.Add<Bloom>(true);
            }
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.65f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.90f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.70f;

            // 3. ColorAdjustments
            if (!volumeProfile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                colorAdjustments = volumeProfile.Add<ColorAdjustments>(true);
            }
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = 0.20f;
            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.contrast.value = 12.0f;
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = 8.0f;

            // 4. Vignette
            if (!volumeProfile.TryGet<Vignette>(out var vignette))
            {
                vignette = volumeProfile.Add<Vignette>(true);
            }
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.25f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.35f;

            EditorUtility.SetDirty(volumeProfile);
            Debug.Log("[URPSetupPipeline] DefaultVolumeProfile components configured: Tonemapping (ACES), Bloom, ColorAdjustments, Vignette.");
        }

        private static void AssignToGraphicsSettingsAsset(UniversalRenderPipelineAsset urpAsset)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets != null && assets.Length > 0)
            {
                var gsSo = new SerializedObject(assets[0]);
                var customRpProp = gsSo.FindProperty("m_CustomRenderPipeline");
                if (customRpProp != null)
                {
                    customRpProp.objectReferenceValue = urpAsset;
                    gsSo.ApplyModifiedProperties();
                    Debug.Log("[URPSetupPipeline] Assigned URP Asset to GraphicsSettings.asset (m_CustomRenderPipeline).");
                }
            }
        }

        private static void AssignToQualitySettings(UniversalRenderPipelineAsset urpAsset)
        {
            int originalLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urpAsset;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);

            var qsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qsAssets != null && qsAssets.Length > 0)
            {
                var qsSo = new SerializedObject(qsAssets[0]);
                var qsArray = qsSo.FindProperty("m_QualitySettings");
                if (qsArray != null && qsArray.isArray)
                {
                    for (int i = 0; i < qsArray.arraySize; i++)
                    {
                        var element = qsArray.GetArrayElementAtIndex(i);
                        var crpProp = element.FindPropertyRelative("customRenderPipeline");
                        if (crpProp != null)
                        {
                            crpProp.objectReferenceValue = urpAsset;
                        }
                    }
                    qsSo.ApplyModifiedProperties();
                    Debug.Log($"[URPSetupPipeline] Assigned URP Asset across all {qsArray.arraySize} QualitySettings tiers.");
                }
            }
        }

        public static void VerifyURPConfiguration()
        {
            var defaultPipeline = GraphicsSettings.defaultRenderPipeline;
            Debug.Log($"[URP_VERIFY] GraphicsSettings.defaultRenderPipeline: {(defaultPipeline != null ? defaultPipeline.name : "NULL")}");

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                var qRp = QualitySettings.renderPipeline;
                Debug.Log($"[URP_VERIFY] Quality tier [{i}: {QualitySettings.names[i]}]: {(qRp != null ? qRp.name : "NULL")}");
            }

            var vol = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (vol != null)
            {
                Debug.Log($"[URP_VERIFY] VolumeProfile has {vol.components.Count} components.");
                foreach (var comp in vol.components)
                {
                    Debug.Log($"[URP_VERIFY]   - Component: {comp.GetType().Name} (active: {comp.active})");
                }
            }
        }
    }
}
