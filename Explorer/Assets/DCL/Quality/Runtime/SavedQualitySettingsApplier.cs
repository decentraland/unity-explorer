using System;
using DCL.Prefs;
using Utility;

namespace DCL.Quality.Runtime
{
    /// <summary>
    ///     Reads saved Custom quality settings from DCLPlayerPrefs.
    /// </summary>
    internal static class SavedQualitySettingsApplier
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
            public GrassPreset Grass;
            public bool SceneLights;
            public bool SceneLightShadows;
            public int MaxSceneLights;
            public ShadowQualityLevel ShadowQuality;
            public ShadowDistanceLevel ShadowDistance;
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
                SceneDistance = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SCENE_DISTANCE_NEW, basePresetData.SceneDistance),
                LandscapeDistance = DCLPlayerPrefs.GetFloat(DCLPrefKeys.PS_LANDSCAPE_DISTANCE, basePresetData.LandscapeDistance),
                Grass = EnumUtils.FromInt<GrassPreset>(DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_GRASS_PRESET, EnumUtils.ToInt(basePresetData.GrassPreset))),
                SceneLights = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SCENE_LIGHTS, basePresetData.SceneLightsEnabled ? 1 : 0) == 1,
                SceneLightShadows = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SCENE_LIGHT_SHADOWS, basePresetData.SceneLightShadowsEnabled ? 1 : 0) == 1,
                MaxSceneLights = DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_MAX_SCENE_LIGHTS, basePresetData.MaxSceneLights),
                ShadowQuality = EnumUtils.FromInt<ShadowQualityLevel>(DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SHADOW_QUALITY, EnumUtils.ToInt(basePresetData.ShadowQuality))),
                ShadowDistance = EnumUtils.FromInt<ShadowDistanceLevel>(DCLPlayerPrefs.GetInt(DCLPrefKeys.PS_SHADOW_DISTANCE, EnumUtils.ToInt(basePresetData.ShadowDistance))),
            };
        }
    }
}
