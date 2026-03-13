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
        public static void ApplyMsaa(UniversalRenderPipelineAsset urpAsset, MsaaLevel level)
        {
            urpAsset.msaaSampleCount = level.ToSampleCount();
        }

        public static void ApplyMsaa(MsaaLevel level)
        {
            foreach (RenderPipelineAsset pipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ApplyMsaa((UniversalRenderPipelineAsset)pipeline, level);
        }

        public static void ApplyHdr(UniversalRenderPipelineAsset urpAsset, bool enabled)
        {
            urpAsset.supportsHDR = enabled;
        }

        public static void ApplyHdr(bool enabled)
        {
            foreach (RenderPipelineAsset pipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ApplyHdr((UniversalRenderPipelineAsset)pipeline, enabled);
        }

        public static void ApplyBloom(bool enabled)
        {
            if (profile == null)
                return;

            if (profile.TryGet(out Bloom bloom))
                bloom.active = enabled;
        }

        public static void ApplyResolutionScale(UniversalRenderPipelineAsset urpAsset, float scale)
        {
            urpAsset.renderScale = scale;
        }

        public static void ApplyResolutionScale(float scale)
        {
            foreach (RenderPipelineAsset pipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ApplyResolutionScale((UniversalRenderPipelineAsset)pipeline, scale);
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

        private static FieldInfo? MainLightRenderingMode;
        private static FieldInfo? SupportsSoftShadows;
        private static FieldInfo? AdditionalLightsRenderingMode;
        private static FieldInfo? AdditionalLightShadowsSupported;
        private static FieldInfo? AdditionalLightsShadowResolutionTierLow;
        private static FieldInfo? AdditionalLightsShadowResolutionTierMedium;
        private static FieldInfo? AdditionalLightsShadowResolutionTierHigh;

        private static void EnsureReflectionProperties() // Disgusting, but Unity doesn't provide setters for some properties -_-: https://discussions.unity.com/t/change-shadow-resolution-from-script/767158/70
        {
            if (MainLightRenderingMode == null)
                MainLightRenderingMode = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightRenderingMode", BindingFlags.Instance | BindingFlags.NonPublic);
            if (SupportsSoftShadows == null)
                SupportsSoftShadows = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
            if (AdditionalLightShadowsSupported == null)
                AdditionalLightShadowsSupported = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
            if (AdditionalLightsShadowResolutionTierLow == null)
                AdditionalLightsShadowResolutionTierLow = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsShadowResolutionTierLow", BindingFlags.Instance | BindingFlags.NonPublic);
            if (AdditionalLightsShadowResolutionTierMedium == null)
                AdditionalLightsShadowResolutionTierMedium = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsShadowResolutionTierMedium", BindingFlags.Instance | BindingFlags.NonPublic);
            if (AdditionalLightsShadowResolutionTierHigh == null)
                AdditionalLightsShadowResolutionTierHigh = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsShadowResolutionTierHigh", BindingFlags.Instance | BindingFlags.NonPublic);
            if (AdditionalLightsRenderingMode == null)
                AdditionalLightsRenderingMode = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsRenderingMode", BindingFlags.Instance | BindingFlags.NonPublic);
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

        public static void ApplySunShadows(bool enabled)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            TrySetReflectionField(MainLightRenderingMode, renderPipelineAsset, enabled ? LightRenderingMode.PerPixel : LightRenderingMode.Disabled, nameof(MainLightRenderingMode));
        }

        public static void ApplySceneLight(bool enabled)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            TrySetReflectionField(AdditionalLightsRenderingMode, renderPipelineAsset, enabled ? LightRenderingMode.PerPixel : LightRenderingMode.Disabled, nameof(AdditionalLightsRenderingMode));
        }

        public static void ApplyMaxObjectsPerLight(int maxLights)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            renderPipelineAsset.maxAdditionalLightsCount = maxLights;
        }

        public static void ApplySceneLightsShadows(bool enabled)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            TrySetReflectionField(AdditionalLightShadowsSupported, renderPipelineAsset, enabled, nameof(AdditionalLightShadowsSupported));
        }

        public static void ApplyShadowQuality(ShadowQualityConfig shadowQualityConfig)
        {
            EnsureReflectionProperties();
            var renderPipelineAsset = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            renderPipelineAsset.mainLightShadowmapResolution = shadowQualityConfig.MainShadowResolution;
            TrySetReflectionField(AdditionalLightsShadowResolutionTierLow, renderPipelineAsset, shadowQualityConfig.ShadowResolutionTier0, nameof(AdditionalLightsShadowResolutionTierLow));
            TrySetReflectionField(AdditionalLightsShadowResolutionTierMedium, renderPipelineAsset, shadowQualityConfig.ShadowResolutionTier1, nameof(AdditionalLightsShadowResolutionTierMedium));
            TrySetReflectionField(AdditionalLightsShadowResolutionTierHigh, renderPipelineAsset, shadowQualityConfig.ShadowResolutionTier2, nameof(AdditionalLightsShadowResolutionTierHigh));
            renderPipelineAsset.shadowCascadeCount = shadowQualityConfig.CascadeCount;
            renderPipelineAsset.shadowDepthBias = shadowQualityConfig.DepthBias;
            renderPipelineAsset.shadowNormalBias = shadowQualityConfig.NormalBias;
            TrySetReflectionField(SupportsSoftShadows, renderPipelineAsset, shadowQualityConfig.SoftShadows, nameof(SupportsSoftShadows));
        }

        public static void ApplyShadowDistance(float distance)
        {
            var renderPipelineAsset = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            renderPipelineAsset.shadowDistance = distance;
        }

        public static void ApplyResolution(int width, int height, FullScreenMode screenMode, RefreshRate refreshRate)
        {
            Screen.SetResolution(width, height, screenMode, refreshRate);
        }

        public static void ApplyRendererFeature<T>(IRendererFeaturesCache cache, bool enabled) where T: ScriptableRendererFeature
        {
            T feature = cache.GetRendererFeature<T>();
            feature?.SetActive(enabled);
        }

        private static VolumeProfile profile;

        public static void InjectVolume(VolumeProfile profile)
        {
            URPSettingsApplier.profile = profile;
        }
    }
}
