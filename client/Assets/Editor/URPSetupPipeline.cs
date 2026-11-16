using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OutrunAftermath.Client.Editor
{
    /// <summary>
    /// Phase B.1 Authoritative Multi-Tier URP & Quality Profile Pipeline.
    /// Configures 6 distinct Universal Render Pipeline configurations for:
    /// 0: Very Low, 1: Low, 2: Medium, 3: High, 4: Very High, 5: Ultra.
    /// Balances mobile/low-end scalability with high-end desktop visual fidelity.
    /// </summary>
    public static class URPSetupPipeline
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string VolumeProfilePath = "Assets/DefaultVolumeProfile.asset";

        public struct QualityTierConfig
        {
            public string Name;
            public RenderingMode RenderMode;
            public bool EnableSSAO;
            public bool SupportsHDR;
            public MsaaQuality MSAA;
            public float RenderScale;
            public float ShadowDistance;
            public int ShadowCascades;
            public Vector3 CascadeSplit; // For 4 cascades
            public float Cascade2Split;  // For 2 cascades
            public Vector2 Cascade3Split; // For 3 cascades
            public bool SoftShadows;
            public bool MainLightShadows;
            public bool AdditionalLightShadows;
            public float LodBias;
            public int MasterTextureLimit;
            public int ParticleRaycastBudget;
        }

        private static readonly QualityTierConfig[] TierConfigs = new QualityTierConfig[]
        {
            // Tier 0: Very Low (Budget Mobile / Low-end Hardware)
            new QualityTierConfig
            {
                Name = "VeryLow",
                RenderMode = RenderingMode.Forward,
                EnableSSAO = false,
                SupportsHDR = false,
                MSAA = MsaaQuality.Disabled,
                RenderScale = 0.75f,
                ShadowDistance = 20.0f,
                ShadowCascades = 1,
                CascadeSplit = Vector3.zero,
                Cascade2Split = 0f,
                Cascade3Split = Vector2.zero,
                SoftShadows = false,
                MainLightShadows = true,
                AdditionalLightShadows = false,
                LodBias = 0.4f,
                MasterTextureLimit = 3,
                ParticleRaycastBudget = 256
            },
            // Tier 1: Low (Standard Mobile / Entry-level PC)
            new QualityTierConfig
            {
                Name = "Low",
                RenderMode = RenderingMode.Forward,
                EnableSSAO = false,
                SupportsHDR = false,
                MSAA = MsaaQuality._2x,
                RenderScale = 0.85f,
                ShadowDistance = 35.0f,
                ShadowCascades = 2,
                CascadeSplit = Vector3.zero,
                Cascade2Split = 0.25f,
                Cascade3Split = Vector2.zero,
                SoftShadows = false,
                MainLightShadows = true,
                AdditionalLightShadows = false,
                LodBias = 0.7f,
                MasterTextureLimit = 2,
                ParticleRaycastBudget = 512
            },
            // Tier 2: Medium (Mid-tier PC / High-end Mobile)
            new QualityTierConfig
            {
                Name = "Medium",
                RenderMode = RenderingMode.ForwardPlus,
                EnableSSAO = true,
                SupportsHDR = true,
                MSAA = MsaaQuality._2x,
                RenderScale = 1.0f,
                ShadowDistance = 50.0f,
                ShadowCascades = 3,
                CascadeSplit = Vector3.zero,
                Cascade2Split = 0f,
                Cascade3Split = new Vector2(0.15f, 0.40f),
                SoftShadows = true,
                MainLightShadows = true,
                AdditionalLightShadows = true,
                LodBias = 1.0f,
                MasterTextureLimit = 1,
                ParticleRaycastBudget = 1024
            },
            // Tier 3: High (Standard Desktop Gaming)
            new QualityTierConfig
            {
                Name = "High",
                RenderMode = RenderingMode.ForwardPlus,
                EnableSSAO = true,
                SupportsHDR = true,
                MSAA = MsaaQuality._4x,
                RenderScale = 1.0f,
                ShadowDistance = 75.0f,
                ShadowCascades = 4,
                CascadeSplit = new Vector3(0.08f, 0.22f, 0.50f),
                Cascade2Split = 0f,
                Cascade3Split = Vector2.zero,
                SoftShadows = true,
                MainLightShadows = true,
                AdditionalLightShadows = true,
                LodBias = 1.2f,
                MasterTextureLimit = 0,
                ParticleRaycastBudget = 2048
            },
            // Tier 4: Very High (Enthusiast Gaming PC)
            new QualityTierConfig
            {
                Name = "VeryHigh",
                RenderMode = RenderingMode.ForwardPlus,
                EnableSSAO = true,
                SupportsHDR = true,
                MSAA = MsaaQuality._4x,
                RenderScale = 1.0f,
                ShadowDistance = 95.0f,
                ShadowCascades = 4,
                CascadeSplit = new Vector3(0.067f, 0.20f, 0.467f),
                Cascade2Split = 0f,
                Cascade3Split = Vector2.zero,
                SoftShadows = true,
                MainLightShadows = true,
                AdditionalLightShadows = true,
                LodBias = 1.5f,
                MasterTextureLimit = 0,
                ParticleRaycastBudget = 4096
            },
            // Tier 5: Ultra (Cinematic Quality PC)
            new QualityTierConfig
            {
                Name = "Ultra",
                RenderMode = RenderingMode.ForwardPlus,
                EnableSSAO = true,
                SupportsHDR = true,
                MSAA = MsaaQuality._8x,
                RenderScale = 1.0f,
                ShadowDistance = 120.0f,
                ShadowCascades = 4,
                CascadeSplit = new Vector3(0.05f, 0.15f, 0.40f),
                Cascade2Split = 0f,
                Cascade3Split = Vector2.zero,
                SoftShadows = true,
                MainLightShadows = true,
                AdditionalLightShadows = true,
                LodBias = 2.0f,
                MasterTextureLimit = 0,
                ParticleRaycastBudget = 8192
            }
        };

        [MenuItem("Outrun/Setup URP High Fidelity")]
        public static void SetupURP()
        {
            Debug.Log("[URPSetupPipeline] Starting authoritative Multi-Tier URP & Quality Profile configuration...");

            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
            }

            // 1. Configure or Create DefaultVolumeProfile
            var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (volumeProfile == null)
            {
                volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(volumeProfile, VolumeProfilePath);
                Debug.Log($"[URPSetupPipeline] Created new VolumeProfile at {VolumeProfilePath}");
            }
            ConfigureVolumeProfileComponents(volumeProfile);

            var createdUrpAssets = new UniversalRenderPipelineAsset[TierConfigs.Length];

            // 2. Author each Quality Tier's RendererData and PipelineAsset
            for (int i = 0; i < TierConfigs.Length; i++)
            {
                var cfg = TierConfigs[i];
                string rendererPath = $"{SettingsFolder}/URP_Renderer_{cfg.Name}.asset";
                string pipelinePath = $"{SettingsFolder}/URP_Quality_{cfg.Name}.asset";

                // UniversalRendererData
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    rendererData.name = $"URP_Renderer_{cfg.Name}";
                    AssetDatabase.CreateAsset(rendererData, rendererPath);
                }

                rendererData.renderingMode = cfg.RenderMode;
                ConfigureSSAOFeature(rendererData, cfg.EnableSSAO);
                EditorUtility.SetDirty(rendererData);

                // UniversalRenderPipelineAsset
                var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
                if (urpAsset == null)
                {
                    urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                    urpAsset.name = $"URP_Quality_{cfg.Name}";
                    AssetDatabase.CreateAsset(urpAsset, pipelinePath);
                }

                ConfigureURPAssetSettings(urpAsset, rendererData, cfg, volumeProfile);
                createdUrpAssets[i] = urpAsset;
            }

            // Also maintain legacy URP_HighFidelity pointing to High/VeryHigh for backward compatibility
            var legacyHighFidelity = createdUrpAssets[3]; // High tier
            GraphicsSettings.defaultRenderPipeline = legacyHighFidelity;
            AssignToGraphicsSettingsAsset(legacyHighFidelity);

            // 3. Assign Tiered Pipeline Assets & Parameters to QualitySettings
            AssignTieredQualitySettings(createdUrpAssets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[URPSetupPipeline] Multi-Tier URP & Quality configuration SUCCEEDED.");
            VerifyURPConfiguration();
        }

        private static void ConfigureSSAOFeature(UniversalRendererData rendererData, bool enable)
        {
            ScreenSpaceAmbientOcclusion ssao = null;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion found)
                {
                    ssao = found;
                    break;
                }
            }

            if (enable)
            {
                if (ssao == null)
                {
                    ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                    ssao.name = "ScreenSpaceAmbientOcclusion";
                    AssetDatabase.AddObjectToAsset(ssao, rendererData);
                    rendererData.rendererFeatures.Add(ssao);
                }
                ssao.SetActive(true);
            }
            else
            {
                if (ssao != null)
                {
                    ssao.SetActive(false);
                }
            }
        }

        private static void ConfigureURPAssetSettings(UniversalRenderPipelineAsset urpAsset, UniversalRendererData rendererData, QualityTierConfig cfg, VolumeProfile volumeProfile)
        {
            var so = new SerializedObject(urpAsset);

            var rendererDataListProp = so.FindProperty("m_RendererDataList");
            if (rendererDataListProp != null)
            {
                rendererDataListProp.arraySize = 1;
                rendererDataListProp.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            }

            var defaultRendererProp = so.FindProperty("m_DefaultRendererIndex");
            if (defaultRendererProp != null) defaultRendererProp.intValue = 0;

            var hdrProp = so.FindProperty("m_SupportsHDR");
            if (hdrProp != null) hdrProp.boolValue = cfg.SupportsHDR;

            var msaaProp = so.FindProperty("m_MSAA");
            if (msaaProp != null) msaaProp.intValue = (int)cfg.MSAA;

            var renderScaleProp = so.FindProperty("m_RenderScale");
            if (renderScaleProp != null) renderScaleProp.floatValue = cfg.RenderScale;

            var mainLightShadowsProp = so.FindProperty("m_MainLightShadowsSupported");
            if (mainLightShadowsProp != null) mainLightShadowsProp.boolValue = cfg.MainLightShadows;

            var addLightShadowsProp = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (addLightShadowsProp != null) addLightShadowsProp.boolValue = cfg.AdditionalLightShadows;

            var softShadowsProp = so.FindProperty("m_SoftShadowsSupported");
            if (softShadowsProp != null) softShadowsProp.boolValue = cfg.SoftShadows;

            var shadowDistProp = so.FindProperty("m_ShadowDistance");
            if (shadowDistProp != null) shadowDistProp.floatValue = cfg.ShadowDistance;

            var cascadeCountProp = so.FindProperty("m_ShadowCascadeCount");
            if (cascadeCountProp != null) cascadeCountProp.intValue = cfg.ShadowCascades;

            if (cfg.ShadowCascades == 2)
            {
                var cascade2SplitProp = so.FindProperty("m_Cascade2Split");
                if (cascade2SplitProp != null) cascade2SplitProp.floatValue = cfg.Cascade2Split;
            }
            else if (cfg.ShadowCascades == 3)
            {
                var cascade3SplitProp = so.FindProperty("m_Cascade3Split");
                if (cascade3SplitProp != null) cascade3SplitProp.vector2Value = cfg.Cascade3Split;
            }
            else if (cfg.ShadowCascades == 4)
            {
                var cascade4SplitProp = so.FindProperty("m_Cascade4Split");
                if (cascade4SplitProp != null) cascade4SplitProp.vector3Value = cfg.CascadeSplit;
            }

            var colorGradingProp = so.FindProperty("m_ColorGradingMode");
            if (colorGradingProp != null) colorGradingProp.intValue = (int)ColorGradingMode.HighDynamicRange;

            var srpBatcherProp = so.FindProperty("m_UseSRPBatcher");
            if (srpBatcherProp != null) srpBatcherProp.boolValue = true;

            var depthTexProp = so.FindProperty("m_RequireDepthTexture");
            if (depthTexProp != null) depthTexProp.boolValue = true;

            var opaqueTexProp = so.FindProperty("m_RequireOpaqueTexture");
            if (opaqueTexProp != null) opaqueTexProp.boolValue = cfg.SupportsHDR;

            var volProp = so.FindProperty("m_VolumeProfile");
            if (volProp != null) volProp.objectReferenceValue = volumeProfile;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urpAsset);
        }

        private static void ConfigureVolumeProfileComponents(VolumeProfile volumeProfile)
        {
            if (!volumeProfile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = volumeProfile.Add<Tonemapping>(true);
            }
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

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
                    Debug.Log($"[URPSetupPipeline] Assigned {urpAsset.name} to GraphicsSettings.asset (m_CustomRenderPipeline).");
                }
            }
        }

        private static void AssignTieredQualitySettings(UniversalRenderPipelineAsset[] tierAssets)
        {
            int originalLevel = QualitySettings.GetQualityLevel();

            for (int i = 0; i < QualitySettings.names.Length && i < tierAssets.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = tierAssets[i];
                QualitySettings.lodBias = TierConfigs[i].LodBias;
                QualitySettings.globalTextureMipmapLimit = TierConfigs[i].MasterTextureLimit;
                QualitySettings.particleRaycastBudget = TierConfigs[i].ParticleRaycastBudget;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);

            var qsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qsAssets != null && qsAssets.Length > 0)
            {
                var qsSo = new SerializedObject(qsAssets[0]);
                var qsArray = qsSo.FindProperty("m_QualitySettings");
                if (qsArray != null && qsArray.isArray)
                {
                    for (int i = 0; i < qsArray.arraySize && i < tierAssets.Length; i++)
                    {
                        var element = qsArray.GetArrayElementAtIndex(i);
                        var crpProp = element.FindPropertyRelative("customRenderPipeline");
                        if (crpProp != null)
                        {
                            crpProp.objectReferenceValue = tierAssets[i];
                        }
                    }
                    qsSo.ApplyModifiedProperties();
                    Debug.Log($"[URPSetupPipeline] Assigned distinct URP Assets across all {qsArray.arraySize} QualitySettings tiers.");
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
                Debug.Log($"[URP_VERIFY] Quality tier [{i}: {QualitySettings.names[i]}] => Asset: {(qRp != null ? qRp.name : "NULL")}, LODBias: {QualitySettings.lodBias:F2}, MipmapLimit: {QualitySettings.globalTextureMipmapLimit}, ParticleBudget: {QualitySettings.particleRaycastBudget}");
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
