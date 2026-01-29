// using DCL.Diagnostics;
// using ECS.Prioritization;
// using UnityEngine;
// using UnityEngine.AdaptivePerformance;
//
// namespace DCL.Optimization.AdaptivePerformance.Scalers
// {
//     /// <summary>
//     /// Custom scaler that adjusts the scene load radius (in parcels) based on performance.
//     /// Higher performance level = more parcels loaded simultaneously.
//     /// Level 0 = 2 parcels, Level 4 = 6 parcels.
//     /// </summary>
//     [CreateAssetMenu(fileName = "SceneLoadRadiusScaler", menuName = "DCL/Adaptive Performance/Scene Load Radius Scaler")]
//     public class SceneLoadRadiusScaler : AdaptivePerformanceScaler
//     {
//         private readonly int[] levels = { 2, 3, 4, 5, 6 }; // 5 levels (0-4)
//         private readonly RealmPartitionSettingsAsset partitionSettings;
//
//         internal SceneLoadRadiusScaler(RealmPartitionSettingsAsset settings)
//         {
//             partitionSettings = settings;
//         }
//
//         /// <summary>
//         /// Called by Unity Adaptive Performance Indexer when performance level changes.
//         /// Updates the maximum loading distance in parcels for the realm partition system.
//         /// </summary>
//         protected override void OnLevel()
//         {
//             // Only apply changes if the scale has actually changed
//             if (!ScaleChanged())
//                 return;
//
//             int radius = levels[Mathf.Clamp(CurrentLevel, 0, levels.Length - 1)];
//
//             // Update partition settings (will trigger OnMaxLoadingDistanceInParcelsChanged event)
//             partitionSettings.MaxLoadingDistanceInParcels = radius;
//
//             ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"[SceneLoadRadiusScaler] Level {CurrentLevel}: {radius} parcel radius");
//         }
//     }
// }
