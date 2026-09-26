using System.Reflection;
using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Quality.Runtime
{
    /// <summary>
    ///     Applies quality settings onto URP render pipeline assets and Unity APIs
    /// </summary>
    internal static class URPSettingsApplier
    {
        private static FieldInfo? mainLightRenderingMode;
        private static FieldInfo? supportsSoftShadows;
        private static FieldInfo? additionalLightsRenderingMode;
        private static FieldInfo? additionalLightShadowsSupported;
        private static FieldInfo? additionalLightsShadowResolutionTierLow;
        private static FieldInfo? additionalLightsShadowResolutionTierMedium;
        private static FieldInfo? additionalLightsShadowResolutionTierHigh;

        private static VolumeProfile? profile;

        public static void ApplyMsaa(MsaaLevel level)
        {
            var urpAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            int sampleCount = ClampToSupportedSampleCount(level.ToSampleCount());

            urpAsset.msaaSampleCount = sampleCount > 0 ? sampleCount : (int)MsaaQuality.Disabled;
        }

        private static int ClampToSupportedSampleCount(int sampleCount)
        {
            if (sampleCount <= 1)
                return 0;

            var descriptor = new RenderTextureDescriptor(Mathf.Max(Screen.width, 1), Mathf.Max(Screen.height, 1), RenderTextureFormat.Default, 24)
            {
                msaaSamples = sampleCount,
            };

            int supported = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            return supported > 1 ? supported : 0;
        }

        public static void ApplyHdr(bool enabled)
        {
            var urpAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            urpAsset.supportsHDR = enabled;
        }

        public static void ApplyBloom(bool enabled)
        {
            if (profile == null)
                return;

            if (profile.TryGet(out Bloom bloom))
                bloom.active = enabled;
        }

        public static void ApplyUpscaling(float renderScale, UpscalingFilterSelection filterSelection)
        {
            var urpAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            urpAsset.renderScale = renderScale;
            urpAsset.upscalingFilter = filterSelection;
        }

        public static void ApplyVSync(bool enabled, int fpsLimit)
        {
            if (enabled)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = 0;
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = fpsLimit > 0 ? fpsLimit : -1;
            }
        }

        private static void EnsureReflectionProperties() // Disgusting, but Unity doesn't provide setters for some properties -_-: https://discussions.unity.com/t/change-shadow-resolution-from-script/767158/70
        {
            if (mainLightRenderingMode == null)
                mainLightRenderingMode = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightRenderingMode", BindingFlags.Instance | BindingFlags.NonPublic);
            if (supportsSoftShadows == null)
                supportsSoftShadows = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
            if (additionalLightShadowsSupported == null)
                additionalLightShadowsSupported = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
            if (additionalLightsShadowResolutionTierLow == null)
                additionalLightsShadowResolutionTierLow = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsShadowResolutionTierLow", BindingFlags.Instance | BindingFlags.NonPublic);
            if (additionalLightsShadowResolutionTierMedium == null)
                additionalLightsShadowResolutionTierMedium = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsShadowResolutionTierMedium", BindingFlags.Instance | BindingFlags.NonPublic);
            if (additionalLightsShadowResolutionTierHigh == null)
                additionalLightsShadowResolutionTierHigh = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsShadowResolutionTierHigh", BindingFlags.Instance | BindingFlags.NonPublic);
            if (additionalLightsRenderingMode == null)
                additionalLightsRenderingMode = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsRenderingMode", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static bool TrySetReflectionField(FieldInfo? field, object target, object value, string fieldName)
        {
            if (field == null)
            {
                ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"{fieldName} field not found via reflection — skipping.");
                return false;
            }

            field.SetValue(target, value);
            return true;
        }

        public static void ApplySunLensFlare(bool enabled)
        {
            Light? sun = RenderSettings.sun;
            if (sun == null) return;
            var lensFlare = sun.GetComponent<LensFlareComponentSRP>();
            if (lensFlare != null)
                lensFlare.enabled = enabled;
        }

        public static void ApplySunShadows(bool enabled)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (UniversalRenderPipelineAsset) GraphicsSettings.currentRenderPipeline;
            TrySetReflectionField(mainLightRenderingMode, renderPipelineAsset, enabled ? LightRenderingMode.PerPixel : LightRenderingMode.Disabled, nameof(mainLightRenderingMode));
        }

        public static void ApplySceneLight(bool enabled)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (UniversalRenderPipelineAsset) GraphicsSettings.currentRenderPipeline;
            TrySetReflectionField(additionalLightsRenderingMode, renderPipelineAsset, enabled ? LightRenderingMode.PerPixel : LightRenderingMode.Disabled, nameof(additionalLightsRenderingMode));
        }

        public static void ApplyMaxObjectsPerLight(int maxLights)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (UniversalRenderPipelineAsset) GraphicsSettings.currentRenderPipeline;
            renderPipelineAsset.maxAdditionalLightsCount = maxLights;
        }

        public static void ApplySceneLightsShadows(bool enabled)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (UniversalRenderPipelineAsset) GraphicsSettings.currentRenderPipeline;
            TrySetReflectionField(additionalLightShadowsSupported, renderPipelineAsset, enabled, nameof(additionalLightShadowsSupported));
        }

        public static void ApplyShadowQuality(ShadowQualityConfig shadowQualityConfig)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (UniversalRenderPipelineAsset) GraphicsSettings.currentRenderPipeline;
            renderPipelineAsset.mainLightShadowmapResolution = shadowQualityConfig.MainShadowResolution;
            TrySetReflectionField(additionalLightsShadowResolutionTierLow, renderPipelineAsset, shadowQualityConfig.ShadowResolutionTier0, nameof(additionalLightsShadowResolutionTierLow));
            TrySetReflectionField(additionalLightsShadowResolutionTierMedium, renderPipelineAsset, shadowQualityConfig.ShadowResolutionTier1, nameof(additionalLightsShadowResolutionTierMedium));
            TrySetReflectionField(additionalLightsShadowResolutionTierHigh, renderPipelineAsset, shadowQualityConfig.ShadowResolutionTier2, nameof(additionalLightsShadowResolutionTierHigh));
            renderPipelineAsset.shadowCascadeCount = shadowQualityConfig.CascadeCount;
            renderPipelineAsset.shadowDepthBias = shadowQualityConfig.DepthBias;
            renderPipelineAsset.shadowNormalBias = shadowQualityConfig.NormalBias;
            TrySetReflectionField(supportsSoftShadows, renderPipelineAsset, shadowQualityConfig.SoftShadows, nameof(supportsSoftShadows));
        }

        public static void ApplyShadowDistance(float distance)
        {
            var renderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (renderPipelineAsset != null)
                renderPipelineAsset.shadowDistance = distance;
        }

        public static void ApplyResolution(int width, int height, FullScreenMode screenMode, RefreshRate refreshRate)
        {
            Screen.SetResolution(width, height, screenMode, refreshRate);
        }

        public static void ApplyRendererFeature<T>(IRendererFeaturesCache cache, bool enabled) where T: ScriptableRendererFeature
        {
            T? feature = cache.GetRendererFeature<T>();
            feature?.SetActive(enabled);
        }

        public static void InjectVolume(VolumeProfile vp) =>
            profile = vp;
    }
}
