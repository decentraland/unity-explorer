using System;
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
            if(profile == null)
                return;
            if(profile.TryGet(out Bloom bloom))
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

        public static void ApplyShadows(QualityPresetData.ShadowPresetConfig config, ShadowDistanceLevel distanceLevel)
        {
            ApplyShadows(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset, config, distanceLevel);
        }

        public static void ApplyShadows(UniversalRenderPipelineAsset urpAsset, QualityPresetData.ShadowPresetConfig config, ShadowDistanceLevel distanceLevel)
        {
            urpAsset.mainLightShadowmapResolution = config.mainShadowResolution;
            urpAsset.shadowCascadeCount = config.cascadeCount;
            urpAsset.shadowDistance = GetShadowDistance(distanceLevel);
            urpAsset.maxAdditionalLightsCount = config.perObjectLightLimit;
        }

        public static void ApplyShadowsToAll(QualityPresetData.ShadowPresetConfig config, ShadowDistanceLevel distanceLevel)
        {
            foreach (RenderPipelineAsset pipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ApplyShadows((UniversalRenderPipelineAsset)pipeline, config, distanceLevel);
        }

        public static void ApplyResolution(int width, int height, FullScreenMode screenMode, RefreshRate refreshRate)
        {
            Screen.SetResolution(width, height, screenMode, refreshRate);
        }

        public static void ApplySceneLights(bool sceneLightsEnabled, int maxSceneLights) { }

        public static void ApplyRendererFeature<T>(IRendererFeaturesCache cache, bool enabled) where T : ScriptableRendererFeature
        {
            T feature = cache.GetRendererFeature<T>();
            feature?.SetActive(enabled);
        }

        private static float GetShadowDistance(ShadowDistanceLevel level) =>
            level switch
            {
                ShadowDistanceLevel.Short => 30f,
                ShadowDistanceLevel.Medium => 60f,
                ShadowDistanceLevel.Far => 100f,
                _ => 60f,
            };

        private static VolumeProfile profile;
        public static void InjectVolume(VolumeProfile profile)
        {
            URPSettingsApplier.profile = profile;
        }
    }
}
