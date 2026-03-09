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

        public QualityPresetData LowPreset => lowPreset;
        public QualityPresetData MediumPreset => mediumPreset;
        public QualityPresetData HighPreset => highPreset;

        public QualityPresetData GetPreset(QualityPresetLevel level) =>
            level switch
            {
                QualityPresetLevel.Low => lowPreset,
                QualityPresetLevel.Medium => mediumPreset,
                QualityPresetLevel.High => highPreset,
                QualityPresetLevel.Custom => null,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
            };
    }
}
