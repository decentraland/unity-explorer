using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.AdaptivePerformance;

namespace DCL.AdaptivePerformance.Scalers
{
    /// <summary>
    /// Custom scaler that adjusts terrain detail density based on performance.
    /// Higher performance level = higher detail density (more grass/rocks/details per terrain patch).
    /// Level 0 = 0% (disabled), Level 4 = 100% (full density).
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainDetailScaler", menuName = "DCL/Adaptive Performance/Terrain Detail Scaler")]
    public class TerrainDetailDensityScaler : AdaptivePerformanceScaler
    {
        private readonly float[] levels = { 0f, 0.25f, 0.5f, 0.7f, 1.0f }; // 5 levels (0-4)

        protected override void OnLevel()
        {
            if (!ScaleChanged())
                return;

            int idx = Mathf.Clamp(CurrentLevel, 0, levels.Length - 1);
            float detailDensity = levels[idx];

            // Update Unity's terrain detail density (0 = no details, 1 = full density)
            QualitySettings.terrainDetailDensityScale = detailDensity;

            ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"[TerrainDetailScaler] Level {CurrentLevel}: {detailDensity:P0} density");
        }
    }
}
