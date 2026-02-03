// using DCL.Diagnostics;
// using ECS.Prioritization;
// using UnityEngine;
// using UnityEngine.AdaptivePerformance;
//
// namespace DCL.AdaptivePerformance.Scalers
// {
//     /// <summary>
//     /// Custom scaler that adjusts the scene load radius (in parcels) based on performance.
//     /// Higher performance level = more parcels loaded simultaneously.
//     /// Level 0 = 2 parcels, Level 4 = 6 parcels.
//     /// </summary>
//     [CreateAssetMenu(fileName = "SceneLoadRadiusScaler", menuName = "DCL/Adaptive Performance/Parcel Load Distance Scaler")]
//     public class ParcelLoadDistanceScaler : AdaptivePerformanceScaler
//     {
//         private readonly int[] levels = { 2, 3, 4, 5, 6 }; // 5 levels (0-4)
//         private readonly RealmPartitionSettingsAsset partitionSettings;
//
//         internal ParcelLoadDistanceScaler(RealmPartitionSettingsAsset settings)
//         {
//             partitionSettings = settings;
//         }
//
//         protected override void OnLevel()
//         {
//             if (!ScaleChanged())
//                 return;
//
//             int radius = levels[Mathf.Clamp(CurrentLevel, 0, levels.Length - 1)];
//
//             partitionSettings.MaxLoadingDistanceInParcels = radius;
//
//             ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"[SceneLoadRadiusScaler] Level {CurrentLevel}: {radius} parcel radius");
//         }
//     }
// }
