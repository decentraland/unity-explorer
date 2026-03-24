using DCL.Landscape.Settings;
using DCL.PerformanceAndDiagnostics.Analytics;
using System;
using DCL.Prefs;
using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using DCL.Settings.Utils;
using DCL.Utilities;
using ECS.Prioritization;
using Global.AppArgs;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Utility;

namespace DCL.Quality.Runtime
{
    public class QualitySettingsController : IQualitySettingsController
    {
        public event Action<QualityPresetLevel> OnPresetChanged;

        public QualityPresetLevel CurrentPreset { get; private set; }
        public int FpsLimit { get; private set; }
        public bool VSync { get; private set; }
        public float ResolutionScale { get; private set; }
        public MsaaLevel Msaa { get; private set; }
        public bool Hdr { get; private set; }
        public bool Bloom { get; private set; }
        public bool AvatarOutline { get; private set; }
        public int SceneDistance { get; private set; }
        public float LandscapeDistance { get; private set; }
        public bool SunShadows { get; private set; }
        public bool SceneLights { get; private set; }
        public bool SceneLightShadows { get; private set; }
        public int MaxSceneLights { get; private set; }
        public ShadowQualityLevel SceneShadowQuality { get; private set; }
        public int ShadowDistance { get; private set; }

        private readonly QualityPresetsAsset presetsAsset;
        private readonly UpscalingController upscalingController;
        private readonly RealmPartitionSettingsAsset realmPartitionAsset;
        private readonly LandscapeData landscapeData;
        private readonly IRendererFeaturesCache rendererFeaturesCache;
        private readonly IAppArgs appArgs;
        private readonly IAnalyticsController analytics;
        private QualityPresetData? presetData;

        public QualitySettingsController(
            QualityPresetsAsset presetsAsset,
            UpscalingController upscalingController,
            RealmPartitionSettingsAsset realmPartitionAsset,
            LandscapeData landscapeData,
            IRendererFeaturesCache rendererFeaturesCache,
            IAppArgs appArgs,
            IAnalyticsController analytics)
        {
            this.presetsAsset = presetsAsset;
            this.upscalingController = upscalingController;
            this.realmPartitionAsset = realmPartitionAsset;
            this.landscapeData = landscapeData;
            this.rendererFeaturesCache = rendererFeaturesCache;
            this.appArgs = appArgs;
            this.analytics = analytics;

            QualityPresetLevel savedPreset = SavedQualitySettingsApplier.ReadSavedPreset();

            if (savedPreset == QualityPresetLevel.Custom)
            {
                CurrentPreset = QualityPresetLevel.Custom;
                ApplySavedValues(SavedQualitySettingsApplier.ReadCustomSettings(presetsAsset, out presetData));
                ApplyAllSettings();
            }
            else { SetPreset(savedPreset); }
        }

        public void SetPreset(QualityPresetLevel level)
        {
            if (level == QualityPresetLevel.Custom) { throw new ArgumentException("Cannot set custom preset from QualitySettingsController"); }

            QualityPresetData preset = presetsAsset.GetPreset(level);

            if (preset == null)
                return;

            CurrentPreset = level;
            presetData = preset;

            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_QUALITY_PRESET, EnumUtils.ToInt(level));

            DeleteCustomSettings();

            FpsLimit = preset.FpsLimit;
            VSync = preset.VSyncEnabled;
            ResolutionScale = preset.ResolutionScale;
            Msaa = preset.MsaaLevel;
            Hdr = preset.HdrEnabled;
            Bloom = preset.BloomEnabled;
            AvatarOutline = preset.AvatarOutlineEnabled;
            SceneDistance = preset.SceneDistance;
            LandscapeDistance = preset.LandscapeDistance;
            SunShadows = preset.SunShadows;
            SceneLights = preset.SceneLightsEnabled;
            SceneLightShadows = preset.SceneLightShadowsEnabled;
            MaxSceneLights = preset.MaxSceneLights;
            SceneShadowQuality = preset.ShadowsQualityLevel;
            ShadowDistance = preset.ShadowDistance;

            ApplyAllSettings();
            OnPresetChanged?.Invoke(level);
        }

        public void ApplyAllSettings()
        {
            URPSettingsApplier.ApplyVSync(VSync, FpsLimit);
            upscalingController.UpdateUpscaling(ResolutionScale);

            URPSettingsApplier.ApplyMsaa(Msaa);
            URPSettingsApplier.ApplyBloom(Bloom);
            URPSettingsApplier.ApplyHdr(Hdr);
            URPSettingsApplier.ApplyRendererFeature<RendererFeature_AvatarOutline>(rendererFeaturesCache, AvatarOutline);

            realmPartitionAsset.MaxLoadingDistanceInParcels = SceneDistance;
            landscapeData.DetailDistance = LandscapeDistance;

            URPSettingsApplier.ApplySunShadows(SunShadows);
            URPSettingsApplier.ApplySceneLight(SceneLights);
            URPSettingsApplier.ApplyMaxObjectsPerLight(MaxSceneLights);
            URPSettingsApplier.ApplySceneLightsShadows(SceneLightShadows);
            URPSettingsApplier.ApplyShadowQuality(presetsAsset.GetShadowConfig(SceneShadowQuality));
            URPSettingsApplier.ApplyShadowDistance(ShadowDistance);
            TrackQualitySettingsReport();
        }

        public void SetFpsLimit(int fps)
        {
            FpsLimit = fps;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_FPS_LIMIT, fps);
            SwitchToCustom();
            URPSettingsApplier.ApplyVSync(VSync, fps);
            TrackQualitySettingsReport();
        }

        public void SetVSync(bool enabled)
        {
            VSync = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_VSYNC, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplyVSync(enabled, FpsLimit);
            TrackQualitySettingsReport();
        }

        public void SetResolutionScale(float scale)
        {
            ResolutionScale = scale;
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.PS_RESOLUTION_SCALE, scale);
            SwitchToCustom();
            upscalingController.UpdateUpscaling(ResolutionScale);
            TrackQualitySettingsReport();
        }

        public void SetMsaa(MsaaLevel level)
        {
            Msaa = level;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_MSAA, EnumUtils.ToInt(level));
            SwitchToCustom();
            URPSettingsApplier.ApplyMsaa(level);
            TrackQualitySettingsReport();
        }

        public void SetHdr(bool enabled)
        {
            Hdr = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_HDR_NEW, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplyHdr(enabled);
            TrackQualitySettingsReport();
        }

        public void SetBloom(bool enabled)
        {
            Bloom = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_BLOOM, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplyBloom(enabled);
            TrackQualitySettingsReport();
        }

        public void SetAvatarOutline(bool enabled)
        {
            AvatarOutline = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_AVATAR_OUTLINE, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplyRendererFeature<RendererFeature_AvatarOutline>(rendererFeaturesCache, enabled);
            TrackQualitySettingsReport();
        }

        public void SetSceneDistance(int distance)
        {
            SceneDistance = distance;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SCENE_DISTANCE, distance);
            SwitchToCustom();
            realmPartitionAsset.MaxLoadingDistanceInParcels = distance;
            TrackQualitySettingsReport();
        }

        public void SetLandscapeDistance(float distance)
        {
            LandscapeDistance = distance;
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.PS_LANDSCAPE_DISTANCE, distance);
            SwitchToCustom();
            landscapeData.DetailDistance = distance;
            TrackQualitySettingsReport();
        }

        public void SetSunShadows(bool enabled)
        {
            SunShadows = enabled;
            DCLPlayerPrefs.SetBool(DCLPrefKeys.PS_SUN_SHADOWS, enabled);
            SwitchToCustom();
            URPSettingsApplier.ApplySunShadows(enabled);
            TrackQualitySettingsReport();
        }

        public void SetSceneLights(bool enabled)
        {
            SceneLights = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SCENE_LIGHTS, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplySceneLight(enabled);
            TrackQualitySettingsReport();
        }

        public void SetMaxSceneLights(int max)
        {
            MaxSceneLights = max;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_MAX_SCENE_LIGHTS, max);
            SwitchToCustom();
            URPSettingsApplier.ApplyMaxObjectsPerLight(max);
            TrackQualitySettingsReport();
        }

        public void SetSceneLightShadows(bool enabled)
        {
            SceneLightShadows = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SCENE_LIGHT_SHADOWS, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplySceneLightsShadows(enabled);
            TrackQualitySettingsReport();
        }

        public void SetShadowQuality(ShadowQualityLevel level)
        {
            SceneShadowQuality = level;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SHADOW_QUALITY, EnumUtils.ToInt(level));
            SwitchToCustom();
            URPSettingsApplier.ApplyShadowQuality(presetsAsset.GetShadowConfig(level));
            TrackQualitySettingsReport();
        }

        public void SetShadowDistance(int distance)
        {
            ShadowDistance = distance;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SHADOW_DISTANCE, distance);
            SwitchToCustom();
            URPSettingsApplier.ApplyShadowDistance(distance);
            TrackQualitySettingsReport();
        }

        private void SwitchToCustom()
        {
            if (CurrentPreset == QualityPresetLevel.Custom)
                return;

            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_CUSTOM_BASE_PRESET, EnumUtils.ToInt(CurrentPreset));
            CurrentPreset = QualityPresetLevel.Custom;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_QUALITY_PRESET, EnumUtils.ToInt(QualityPresetLevel.Custom));
            OnPresetChanged?.Invoke(QualityPresetLevel.Custom);
        }

        private void ApplySavedValues(SavedQualitySettingsApplier.SavedValues saved)
        {
            FpsLimit = saved.FpsLimit;
            VSync = saved.VSync;
            ResolutionScale = saved.ResolutionScale;
            Msaa = saved.Msaa;
            Hdr = saved.Hdr;
            Bloom = saved.Bloom;
            AvatarOutline = saved.AvatarOutline;
            SceneDistance = saved.SceneDistance;
            LandscapeDistance = saved.LandscapeDistance;
            SunShadows = saved.SunShadows;
            SceneLights = saved.SceneLights;
            SceneLightShadows = saved.SceneLightShadows;
            MaxSceneLights = saved.MaxSceneLights;
            SceneShadowQuality = saved.SceneShadowQuality;
            ShadowDistance = saved.ShadowDistance;
        }

        private static void DeleteCustomSettings()
        {
            SavedQualitySettingsApplier.DeleteCustomSettings();
        }

        private void TrackQualitySettingsReport()
        {
            var properties = new JObject
            {
                ["preset"] = CurrentPreset.ToString(),
            };

            if (CurrentPreset == QualityPresetLevel.Custom)
            {
                var basePreset = EnumUtils.FromInt<QualityPresetLevel>(DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_CUSTOM_BASE_PRESET, EnumUtils.ToInt(QualityPresetLevel.Medium)));
                properties["custom_baseline"] = basePreset.ToString();

                QualityPresetData b = presetData;

                if (FpsLimit != b.FpsLimit) properties["fps_limit"] = FpsLimit;
                if (VSync != b.VSyncEnabled) properties["vsync"] = VSync;
                if (!Mathf.Approximately(ResolutionScale, b.ResolutionScale)) properties["resolution_scale"] = ResolutionScale;
                if (Msaa != b.MsaaLevel) properties["msaa"] = Msaa.ToString();
                if (Hdr != b.HdrEnabled) properties["hdr"] = Hdr;
                if (Bloom != b.BloomEnabled) properties["bloom"] = Bloom;
                if (AvatarOutline != b.AvatarOutlineEnabled) properties["avatar_outline"] = AvatarOutline;
                if (SceneDistance != b.SceneDistance) properties["scene_distance"] = SceneDistance;
                if (!Mathf.Approximately(LandscapeDistance, b.LandscapeDistance)) properties["landscape_distance"] = LandscapeDistance;
                if (SunShadows != b.SunShadows) properties["sun_shadows"] = SunShadows;
                if (SceneLights != b.SceneLightsEnabled) properties["scene_lights"] = SceneLights;
                if (SceneLightShadows != b.SceneLightShadowsEnabled) properties["scene_light_shadows"] = SceneLightShadows;
                if (MaxSceneLights != b.MaxSceneLights) properties["max_scene_lights"] = MaxSceneLights;
                if (SceneShadowQuality != b.ShadowsQualityLevel) properties["shadow_quality"] = SceneShadowQuality.ToString();
                if (ShadowDistance != b.ShadowDistance) properties["shadow_distance"] = ShadowDistance;
            }

            analytics.Track(AnalyticsEvents.Settings.QUALITY_SETTINGS_REPORT, properties);
        }

        public void Dispose() { }
    }
}
