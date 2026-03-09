using System;
using UnityEngine;

namespace DCL.Quality
{
    [CreateAssetMenu(fileName = "QualityPresetData", menuName = "DCL/Quality/Quality Preset Data")]
    public class QualityPresetData : ScriptableObject
    {
        [Header("Display")]
        [SerializeField] internal int fpsLimit;
        [SerializeField] internal bool vSyncEnabled;
        [SerializeField] [Range(0.1f, 1.2f)] internal float resolutionScale = 1f;

        [Header("Post Processing")]
        [SerializeField] internal MsaaLevel msaaLevel = MsaaLevel.Off;
        [SerializeField] internal bool hdrEnabled;
        [SerializeField] internal bool bloomEnabled;
        [SerializeField] internal bool avatarOutlineEnabled;

        [Header("Landscape & Foliage")]
        [SerializeField] internal int sceneDistance = 20;
        [SerializeField] internal float landscapeDistance = 1000f;
        [SerializeField] internal GrassPreset grassPreset;
        [SerializeField] internal GrassConfig grassLow;
        [SerializeField] internal GrassConfig grassMedium;
        [SerializeField] internal GrassConfig grassHigh;

        [Header("Sun & Shadows")]
        [SerializeField] internal SunShadowQuality sunShadows;
        [SerializeField] internal bool sceneLightsEnabled;
        [SerializeField] internal bool sceneLightShadowsEnabled;
        [SerializeField] [Range(0, 10)] internal int maxSceneLights = 6;
        [SerializeField] internal ShadowQualityLevel shadowQuality;
        [SerializeField] internal ShadowDistanceLevel shadowDistance;

        [Header("Shadow Configurations")]
        [SerializeField] internal ShadowPresetConfig shadowLowConfig;
        [SerializeField] internal ShadowPresetConfig shadowMediumConfig;
        [SerializeField] internal ShadowPresetConfig shadowHighConfig;

        public int FpsLimit => fpsLimit;
        public bool VSyncEnabled => vSyncEnabled;
        public float ResolutionScale => resolutionScale;
        public MsaaLevel MsaaLevel => msaaLevel;
        public bool HdrEnabled => hdrEnabled;
        public bool BloomEnabled => bloomEnabled;
        public bool AvatarOutlineEnabled => avatarOutlineEnabled;
        public int SceneDistance => sceneDistance;
        public float LandscapeDistance => landscapeDistance;
        public GrassPreset GrassPreset => grassPreset;
        public SunShadowQuality SunShadows => sunShadows;
        public bool SceneLightsEnabled => sceneLightsEnabled;
        public bool SceneLightShadowsEnabled => sceneLightShadowsEnabled;
        public int MaxSceneLights => maxSceneLights;
        public ShadowQualityLevel ShadowQuality => shadowQuality;
        public ShadowDistanceLevel ShadowDistance => shadowDistance;

        public GrassConfig GetGrassConfig() =>
            grassPreset switch
            {
                Quality.GrassPreset.Low => grassLow,
                Quality.GrassPreset.Medium => grassMedium,
                Quality.GrassPreset.High => grassHigh,
                _ => grassMedium,
            };

        public ShadowPresetConfig GetShadowConfig() =>
            shadowQuality switch
            {
                ShadowQualityLevel.Low => shadowLowConfig,
                ShadowQualityLevel.Medium => shadowMediumConfig,
                ShadowQualityLevel.High => shadowHighConfig,
                _ => shadowMediumConfig,
            };

        [Serializable]
        public struct GrassConfig
        {
            [Tooltip("Maximum distance at which grass is rendered")]
            public float distance;

            [Tooltip("Grass density multiplier (0-1)")]
            [Range(0f, 1f)]
            public float density;
        }

        [Serializable]
        public struct ShadowPresetConfig
        {
            [Tooltip("Main directional light shadow map resolution")]
            public int mainShadowResolution;

            [Tooltip("Additional light shadow resolution tier 0")]
            public int shadowResolutionTier0;

            [Tooltip("Additional light shadow resolution tier 1")]
            public int shadowResolutionTier1;

            [Tooltip("Additional light shadow resolution tier 2")]
            public int shadowResolutionTier2;

            [Tooltip("Maximum shadow rendering distance")]
            public float maxDistance;

            [Tooltip("Number of shadow cascades (1, 2, or 4)")]
            [Range(1, 4)]
            public int cascadeCount;

            [Tooltip("Shadow depth bias")]
            public float depthBias;

            [Tooltip("Shadow normal bias")]
            public float normalBias;

            [Tooltip("Enable soft shadow filtering")]
            public bool softShadows;

            [Tooltip("Maximum number of per-object lights")]
            public int perObjectLightLimit;

            [Tooltip("Allow transparent objects to receive shadows")]
            public bool transparentReceiveShadows;
        }
    }
}
