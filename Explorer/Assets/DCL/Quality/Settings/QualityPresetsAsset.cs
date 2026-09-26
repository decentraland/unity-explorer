using System;
using UnityEngine;

namespace DCL.Quality
{
    [CreateAssetMenu(fileName = "QualityPresetsAsset", menuName = "DCL/Quality/Quality Presets")]
    public class QualityPresetsAsset : ScriptableObject
    {
        [SerializeField] internal QualityPresetData lowPreset;
        [SerializeField] internal QualityPresetData mediumPreset;
        [SerializeField] internal QualityPresetData highPreset;

        [SerializeField] internal ShadowQualityConfig lowShadowQualityConfig;
        [SerializeField] internal ShadowQualityConfig mediumShadowQualityConfig;
        [SerializeField] internal ShadowQualityConfig highShadowQualityConfig;

        public QualityPresetData GetPreset(QualityPresetLevel level) =>
            level switch
            {
                QualityPresetLevel.Low => lowPreset,
                QualityPresetLevel.Medium => mediumPreset,
                QualityPresetLevel.High => highPreset,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
            };

        public ShadowQualityConfig GetShadowConfig(ShadowQualityLevel level) =>
            level switch
            {
                ShadowQualityLevel.Low => lowShadowQualityConfig,
                ShadowQualityLevel.Medium => mediumShadowQualityConfig,
                ShadowQualityLevel.High => highShadowQualityConfig,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
            };
    }
}
