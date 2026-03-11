using DCL.Prefs;
using Utility;

namespace DCL.Quality.Runtime
{
    /// <summary>
    ///     Reads and writes saved quality settings from DCLPlayerPrefs.
    /// </summary>
    public static class SavedQualitySettingsApplier
    {
        public struct SavedValues
        {
            public int FpsLimit;
            public bool VSync;
            public float ResolutionScale;
            public MsaaLevel Msaa;
            public bool Hdr;
            public bool Bloom;
            public bool AvatarOutline;
            public int SceneDistance;
            public float LandscapeDistance;
            public bool SunShadows;
            public bool SceneLights;
            public bool SceneLightShadows;
            public int MaxSceneLights;
            public ShadowQualityLevel SceneShadowQuality;
            public int ShadowDistance;
        }

        public static QualityPresetLevel ReadSavedPreset() =>
            EnumUtils.FromInt<QualityPresetLevel>(DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_QUALITY_PRESET, EnumUtils.ToInt(QualityPresetLevel.Medium)));

        /// <summary>
        ///     Reads persisted Custom quality overrides from player prefs.
        ///     Uses the saved base preset as defaults for any unset keys.
        /// </summary>
        public static SavedValues ReadCustomSettings(QualityPresetsAsset presetsAsset, out QualityPresetData basePresetData)
        {
            var basePreset = EnumUtils.FromInt<QualityPresetLevel>(DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_CUSTOM_BASE_PRESET, EnumUtils.ToInt(QualityPresetLevel.Medium)));
            basePresetData = presetsAsset.GetPreset(basePreset);

            return new SavedValues
            {
                FpsLimit = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_FPS_LIMIT, basePresetData.FpsLimit),
                VSync = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_VSYNC, basePresetData.VSyncEnabled ? 1 : 0) == 1,
                ResolutionScale = DCLPlayerPrefs.GetFloat(DCLPrefKeys.PS_RESOLUTION_SCALE, basePresetData.ResolutionScale),
                Msaa = EnumUtils.FromInt<MsaaLevel>(DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_MSAA, EnumUtils.ToInt(basePresetData.MsaaLevel))),
                Hdr = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_HDR_NEW, basePresetData.HdrEnabled ? 1 : 0) == 1,
                Bloom = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_BLOOM, basePresetData.BloomEnabled ? 1 : 0) == 1,
                AvatarOutline = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_AVATAR_OUTLINE, basePresetData.AvatarOutlineEnabled ? 1 : 0) == 1,
                SceneDistance = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SCENE_DISTANCE, basePresetData.SceneDistance),
                LandscapeDistance = DCLPlayerPrefs.GetFloat(DCLPrefKeys.PS_LANDSCAPE_DISTANCE, basePresetData.LandscapeDistance),
                SunShadows = DCLPlayerPrefs.GetBool(DCLPrefKeys.PS_SUN_SHADOWS, basePresetData.SunShadows),
                SceneLights = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SCENE_LIGHTS, basePresetData.SceneLightsEnabled ? 1 : 0) == 1,
                SceneLightShadows = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SCENE_LIGHT_SHADOWS, basePresetData.SceneLightShadowsEnabled ? 1 : 0) == 1,
                MaxSceneLights = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_MAX_SCENE_LIGHTS, basePresetData.MaxSceneLights),
                SceneShadowQuality = EnumUtils.FromInt<ShadowQualityLevel>(DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SHADOW_QUALITY, EnumUtils.ToInt(basePresetData.ShadowsQualityLevel))),
                ShadowDistance = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SHADOW_DISTANCE, basePresetData.shadowDistance),
            };
        }

        /// <summary>
        ///     Deletes all persisted Custom quality override keys from player prefs.
        /// </summary>
        public static void DeleteCustomSettings()
        {
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_CUSTOM_BASE_PRESET);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_FPS_LIMIT);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_VSYNC);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_RESOLUTION_SCALE);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_MSAA);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_HDR_NEW);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_BLOOM);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_AVATAR_OUTLINE);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SCENE_DISTANCE);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_LANDSCAPE_DISTANCE);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_GRASS_PRESET);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SUN_SHADOWS);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SCENE_LIGHTS);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SCENE_LIGHT_SHADOWS);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_MAX_SCENE_LIGHTS);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SHADOW_QUALITY);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.PS_SHADOW_DISTANCE);
        }

        /// <summary>
        ///     Forces the Low preset by clearing any custom overrides and saving the Low preset level.
        ///     Called before QualitySettingsController is created (e.g. when minimum specs are not met).
        /// </summary>
        public static void EnforceLowPreset()
        {
            DeleteCustomSettings();
            DCLPlayerPrefs.SetInt(DCLPrefKeys.PS_QUALITY_PRESET, EnumUtils.ToInt(QualityPresetLevel.Low), save: true);
        }
    }
}
