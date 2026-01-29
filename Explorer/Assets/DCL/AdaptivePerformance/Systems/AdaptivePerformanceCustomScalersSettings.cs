using DCL.Landscape.Settings;
using DCL.PluginSystem;
using DCL.SDKComponents.LightSource;
using DCL.SDKComponents.MediaStream.Settings;
using ECS.Prioritization;
using System;
using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    /// <summary>
    /// Settings for the Adaptive Performance Custom Scalers plugin.
    /// Holds references to dependencies required by the custom scalers.
    /// </summary>
    [Serializable]
    public class AdaptivePerformanceCustomScalersSettings : IDCLPluginSettings
    {
        [field: Header("Scaler Dependencies")]
        [field: Tooltip("Scene partition settings (used by SceneLoadRadiusScaler)")]
        [field: SerializeField]
        internal RealmPartitionSettingsAsset partitionSettings { get; private set; } = null!;

        [field: Tooltip("Light source settings (used by DynamicLightCountScaler)")]
        [field: SerializeField]
        internal LightSourceSettings lightSourceSettings { get; private set; } = null!;

        [field: Tooltip("Landscape data (used by GrassDistanceScaler)")]
        [field: SerializeField]
        internal LandscapeData landscapeData { get; private set; } = null!;

        [field: Tooltip("Video prioritization settings (used by VideoStreamScaler)")]
        [field: SerializeField]
        internal VideoPrioritizationSettings videoSettings { get; private set; } = null!;
    }
}
