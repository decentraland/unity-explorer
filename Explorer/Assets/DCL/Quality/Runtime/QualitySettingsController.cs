using System;
using DCL.Prefs;
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
        public bool Msaa { get; private set; }
        public bool Hdr { get; private set; }
        public bool Bloom { get; private set; }
        public bool AvatarOutline { get; private set; }
        public int SceneDistance { get; private set; }
        public float LandscapeDistance { get; private set; }
        public GrassPreset Grass { get; private set; }
        public bool SceneLights { get; private set; }
        public bool SceneLightShadows { get; private set; }
        public int MaxSceneLights { get; private set; }
        public ShadowQualityLevel ShadowQuality { get; private set; }
        public ShadowDistanceLevel ShadowDistance { get; private set; }

        private readonly QualityPresetsAsset presetsAsset;
        private QualityPresetData presetData;

        public QualitySettingsController(QualityPresetsAsset presetsAsset)
        {
            this.presetsAsset = presetsAsset;

            QualityPresetLevel savedPreset = SavedQualitySettingsApplier.ReadSavedPreset();

            if (savedPreset == QualityPresetLevel.Custom)
            {
                CurrentPreset = QualityPresetLevel.Custom;
                ApplySavedValues(SavedQualitySettingsApplier.ReadCustomSettings(presetsAsset, out presetData));
                ApplyAllSettings();
            }
            else
            {
                SetPreset(savedPreset);
            }
        }

        public void SetPreset(QualityPresetLevel level)
        {
            if (level == QualityPresetLevel.Custom)
            {
                throw new ArgumentException("Cannot set custom preset from QualitySettingsController");
            }

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
            Msaa = preset.MsaaEnabled;
            Hdr = preset.HdrEnabled;
            Bloom = preset.BloomEnabled;
            AvatarOutline = preset.AvatarOutlineEnabled;
            SceneDistance = preset.SceneDistance;
            LandscapeDistance = preset.LandscapeDistance;
            Grass = preset.GrassPreset;
            SceneLights = preset.SceneLightsEnabled;
            SceneLightShadows = preset.SceneLightShadowsEnabled;
            MaxSceneLights = preset.MaxSceneLights;
            ShadowQuality = preset.ShadowQuality;
            ShadowDistance = preset.ShadowDistance;

            ApplyAllSettings();
            OnPresetChanged?.Invoke(level);
        }

        public void ApplyAllSettings()
        {
            URPSettingsApplier.ApplyVSync(VSync, FpsLimit);
            URPSettingsApplier.ApplyResolutionScale(ResolutionScale);
            URPSettingsApplier.ApplyMsaa(Msaa);
            URPSettingsApplier.ApplyHdr(Hdr);
            URPSettingsApplier.ApplySceneLights(SceneLights, MaxSceneLights);

            QualityPresetData.ShadowPresetConfig shadowConfig = GetCurrentShadowConfig();
        }

        public void SetFpsLimit(int fps)
        {
            FpsLimit = fps;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_FPS_LIMIT, fps);
            SwitchToCustom();
            URPSettingsApplier.ApplyVSync(VSync, fps);
        }

        public void SetVSync(bool enabled)
        {
            VSync = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_VSYNC, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplyVSync(enabled, FpsLimit);
        }

        public void SetResolutionScale(float scale)
        {
            ResolutionScale = scale;
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.PS_RESOLUTION_SCALE, scale);
            SwitchToCustom();
            URPSettingsApplier.ApplyResolutionScale(scale);
        }

        public void SetMsaa(bool enabled)
        {
            Msaa = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_MSAA, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplyMsaa(enabled);
        }

        public void SetHdr(bool enabled)
        {
            Hdr = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_HDR_NEW, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplyHdr(enabled);
        }

        public void SetBloom(bool enabled)
        {
            Bloom = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_BLOOM, enabled ? 1 : 0);
            SwitchToCustom();
        }

        public void SetAvatarOutline(bool enabled)
        {
            AvatarOutline = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_AVATAR_OUTLINE, enabled ? 1 : 0);
            SwitchToCustom();
        }

        public void SetSceneDistance(int distance)
        {
            SceneDistance = distance;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SCENE_DISTANCE_NEW, distance);
            SwitchToCustom();
        }

        public void SetLandscapeDistance(float distance)
        {
            LandscapeDistance = distance;
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.PS_LANDSCAPE_DISTANCE, distance);
            SwitchToCustom();
        }

        public void SetGrassPreset(GrassPreset preset)
        {
            Grass = preset;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_GRASS_PRESET, EnumUtils.ToInt(preset));
            SwitchToCustom();
        }

        public void SetSceneLights(bool enabled)
        {
            SceneLights = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SCENE_LIGHTS, enabled ? 1 : 0);
            SwitchToCustom();
            URPSettingsApplier.ApplySceneLights(enabled, MaxSceneLights);
        }

        public void SetSceneLightShadows(bool enabled)
        {
            SceneLightShadows = enabled;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SCENE_LIGHT_SHADOWS, enabled ? 1 : 0);
            SwitchToCustom();
        }

        public void SetMaxSceneLights(int max)
        {
            MaxSceneLights = max;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_MAX_SCENE_LIGHTS, max);
            SwitchToCustom();
            URPSettingsApplier.ApplySceneLights(SceneLights, max);
        }

        public void SetShadowQuality(ShadowQualityLevel level)
        {
            ShadowQuality = level;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SHADOW_QUALITY, EnumUtils.ToInt(level));
            SwitchToCustom();
            URPSettingsApplier.ApplyShadows(GetCurrentShadowConfig(), ShadowDistance);
        }

        public void SetShadowDistance(ShadowDistanceLevel level)
        {
            ShadowDistance = level;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_SHADOW_DISTANCE, EnumUtils.ToInt(level));
            SwitchToCustom();
            URPSettingsApplier.ApplyShadows(GetCurrentShadowConfig(), ShadowDistance);
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
            Grass = saved.Grass;
            SceneLights = saved.SceneLights;
            SceneLightShadows = saved.SceneLightShadows;
            MaxSceneLights = saved.MaxSceneLights;
            ShadowQuality = saved.ShadowQuality;
            ShadowDistance = saved.ShadowDistance;
        }

        private static void DeleteCustomSettings()
        {
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_CUSTOM_BASE_PRESET);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_FPS_LIMIT);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_VSYNC);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_RESOLUTION_SCALE);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_MSAA);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_HDR_NEW);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_BLOOM);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_AVATAR_OUTLINE);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SCENE_DISTANCE_NEW);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_LANDSCAPE_DISTANCE);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_GRASS_PRESET);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SCENE_LIGHTS);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SCENE_LIGHT_SHADOWS);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_MAX_SCENE_LIGHTS);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SHADOW_QUALITY);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SHADOW_DISTANCE);
        }

        private QualityPresetData.ShadowPresetConfig GetCurrentShadowConfig() =>
            ShadowQuality switch
            {
                ShadowQualityLevel.Low => presetData.shadowLowConfig,
                ShadowQualityLevel.Medium => presetData.shadowMediumConfig,
                ShadowQualityLevel.High => presetData.shadowHighConfig,
                _ => presetData.shadowMediumConfig,
            };

        public void Dispose() { }
    }
}
