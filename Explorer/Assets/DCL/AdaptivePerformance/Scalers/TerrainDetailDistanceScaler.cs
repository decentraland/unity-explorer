// using DCL.Diagnostics;
// using DCL.Landscape.Settings;
// using UnityEngine;
// using UnityEngine.AdaptivePerformance;
//
// namespace DCL.AdaptivePerformance.Scalers
// {
//     /// <summary>
//     /// Custom scaler that adjusts the grass and vegetation rendering distance based on performance.
//     /// Higher performance level = grass visible at greater distances.
//     /// Level 0 = 0m (disabled), Level 5 = 300m.
//     /// </summary>
//     [CreateAssetMenu(fileName = "GrassDistanceScaler", menuName = "DCL/Adaptive Performance/Grass Distance Scaler")]
//     public class TerrainDetailDistanceScaler : AdaptivePerformanceScaler
//     {
//         private readonly float[] levels = { 0f, 50f, 100f, 150f, 200f, 300f }; // 6 levels (0-5)
//         private readonly LandscapeData landscapeData;
//
//         internal TerrainDetailDistanceScaler(LandscapeData landscape)
//         {
//             landscapeData = landscape;
//         }
//
//         protected override void OnLevel()
//         {
//             if (!ScaleChanged())
//                 return;
//
//             float distance = levels[Mathf.Clamp(CurrentLevel, 0, levels.Length - 1)];
//
//             landscapeData.DetailDistance = distance;
//
//             ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"[GrassDistanceScaler] Level {CurrentLevel}: {distance}m distance");
//         }
//     }
// }
