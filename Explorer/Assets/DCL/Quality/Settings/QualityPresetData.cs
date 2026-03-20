using System;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Sun")]
        [SerializeField] internal bool sunShadows;

        [Header("Scene Lights")]
        [SerializeField] internal bool sceneLightsEnabled;
        [SerializeField] internal bool sceneLightShadowsEnabled;
        [SerializeField] [Range(0, 10)] internal int maxSceneLights = 6;

        [Header("Shadows")]
        [SerializeField] internal ShadowQualityLevel shadowsQualityLevel;
        [SerializeField] internal int shadowDistance;

        public int FpsLimit => fpsLimit;
        public bool VSyncEnabled => vSyncEnabled;
        public float ResolutionScale => resolutionScale;
        public MsaaLevel MsaaLevel => msaaLevel;
        public bool HdrEnabled => hdrEnabled;
        public bool BloomEnabled => bloomEnabled;
        public bool AvatarOutlineEnabled => avatarOutlineEnabled;
        public int SceneDistance => sceneDistance;
        public float LandscapeDistance => landscapeDistance;
        public bool SunShadows => sunShadows;
        public bool SceneLightsEnabled => sceneLightsEnabled;
        public bool SceneLightShadowsEnabled => sceneLightShadowsEnabled;
        public int MaxSceneLights => maxSceneLights;
        public ShadowQualityLevel ShadowsQualityLevel => shadowsQualityLevel;
        public int ShadowDistance => shadowDistance;
    }

    [Serializable]
    public struct ShadowQualityConfig
    {
        [Tooltip("Main directional light shadow map resolution")]
        public int MainShadowResolution;

        [Tooltip("Additional light shadow resolution tier 0")]
        public int ShadowResolutionTier0;

        [Tooltip("Additional light shadow resolution tier 1")]
        public int ShadowResolutionTier1;

        [Tooltip("Additional light shadow resolution tier 2")]
        public int ShadowResolutionTier2;

        [Tooltip("Number of shadow cascades (1, 2, or 4)")]
        [Range(1, 4)]
        public int CascadeCount;

        [Tooltip("Shadow depth bias")]
        public float DepthBias;

        [Tooltip("Shadow normal bias")]
        public float NormalBias;

        [Tooltip("Enable soft shadow filtering")]
        public bool SoftShadows;
    }
}
