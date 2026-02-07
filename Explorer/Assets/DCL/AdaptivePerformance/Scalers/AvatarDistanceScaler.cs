// using Arch.Core;
// using DCL.Diagnostics;
// using DCL.Optimization.AdaptivePerformance.Components;
// using ECS.Abstract;
// using UnityEngine;
// using UnityEngine.AdaptivePerformance;
//
// namespace DCL.Optimization.AdaptivePerformance.Scalers
// {
//     /// <summary>
//     /// Custom scaler that adjusts the maximum visibility distance for avatars based on performance.
//     /// Higher performance level = avatars visible at greater distances.
//     /// Level 0 = 20m, Level 4 = 60m.
//     /// </summary>
//     [CreateAssetMenu(fileName = "AvatarDistanceScaler", menuName = "DCL/Adaptive Performance/Avatar Distance Scaler")]
//     public class AvatarDistanceScaler : AdaptivePerformanceScaler
//     {
//         private readonly float[] levels = { 20f, 30f, 40f, 50f, 60f }; // 5 levels (0-4)
//         private readonly World world;
//         private readonly SingleInstanceEntity avatarVisibilityConfig;
//
//         // internal AvatarDistanceScaler(World world)
//         // {
//         //     this.world = world;
//         //     avatarVisibilityConfig = world.CacheAvatarVisibilityConfig();
//         // }
//
//         /// <summary>
//         /// Called by Unity Adaptive Performance Indexer when performance level changes.
//         /// Updates the ECS singleton component that AvatarShapeVisibilitySystem reads.
//         /// </summary>
//         protected override void OnLevel()
//         {
//             // Only apply changes if the scale has actually changed
//             if (!ScaleChanged())
//                 return;
//
//             float maxDistance = levels[Mathf.Clamp(CurrentLevel, 0, levels.Length - 1)];
//
//             // Update ECS singleton
//             ref var config = ref avatarVisibilityConfig.GetAvatarVisibilityConfig(world);
//             config.MaxVisibilityDistance = maxDistance;
//
//             ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"[AvatarDistanceScaler] Level {CurrentLevel}: Max {maxDistance}m distance");
//         }
//     }
// }
