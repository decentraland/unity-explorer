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
//     /// Custom scaler that adjusts the maximum number of visible avatars based on performance.
//     /// Higher performance level = more avatars visible.
//     /// Level 0 = 10 avatars, Level 5 = 60 avatars.
//     /// </summary>
//     [CreateAssetMenu(fileName = "AvatarCountScaler", menuName = "DCL/Adaptive Performance/Avatar Count Scaler")]
//     public class AvatarCountScaler : AdaptivePerformanceScaler
//     {
//         private readonly int[] levels = { 10, 20, 30, 40, 50, 60 }; // 6 levels (0-5)
//         private readonly World world;
//         private readonly SingleInstanceEntity avatarVisibilityConfig;
//
//         // internal AvatarCountScaler(World world)
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
//             int maxAvatars = levels[Mathf.Clamp(CurrentLevel, 0, levels.Length - 1)];
//
//             // Update ECS singleton
//             ref var config = ref avatarVisibilityConfig.GetAvatarVisibilityConfig(world);
//             config.MaxVisibleAvatars = maxAvatars;
//
//             ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"[AvatarCountScaler] Level {CurrentLevel}: Max {maxAvatars} avatars");
//         }
//     }
// }
