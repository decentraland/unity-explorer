// using Arch.Core;
// using Arch.SystemGroups;
// using Cysharp.Threading.Tasks;
// using DCL.Diagnostics;
// // using DCL.Optimization.AdaptivePerformance.Scalers;
// using DCL.PluginSystem.Global;
// using ECS.Abstract;
// using System.Threading;
// using UnityEngine;
// using UnityEngine.AdaptivePerformance;
//
// namespace DCL.Optimization.AdaptivePerformance.Systems
// {
//     /// <summary>
//     /// Plugin that initializes Unity Adaptive Performance system.
//     /// Custom scalers are configured via ScriptableObject assets in Project Settings > Adaptive Performance.
//     /// This plugin only ensures the AvatarVisibilityConfigComponent singleton exists for avatar-related scalers.
//     /// </summary>
//     public class AdaptivePerformanceCustomScalersPlugin : IDCLGlobalPlugin<AdaptivePerformanceCustomScalersSettings>
//     {
//         private IAdaptivePerformance? adaptivePerformance;
//         private readonly World ecsWorld;
//
//         public AdaptivePerformanceCustomScalersPlugin(World world)
//         {
//             ecsWorld = world;
//         }
//
//         public UniTask InitializeAsync(AdaptivePerformanceCustomScalersSettings settings, CancellationToken ct)
//         {
//             // Get Unity Adaptive Performance instance
//             adaptivePerformance = Holder.Instance;
//
//             if (adaptivePerformance == null || !adaptivePerformance.Active)
//             {
//                 ReportHub.LogWarning(ReportCategory.ADAPTIVE_PERFORMANCE, "[Adaptive Performance] Not active on this platform - custom scalers will not be registered");
//                 return UniTask.CompletedTask;
//             }
//
//             // Validate settings dependencies
//             if (settings.partitionSettings == null || settings.lightSourceSettings == null ||
//                 settings.landscapeData == null || settings.videoSettings == null)
//             {
//                 ReportHub.LogError(ReportCategory.ADAPTIVE_PERFORMANCE, "[Adaptive Performance] Missing required settings dependencies - cannot initialize custom scalers");
//                 return UniTask.CompletedTask;
//             }
//
//             // Create singleton entity for avatar visibility config if it doesn't exist
//             // This ensures AvatarCountScaler and AvatarDistanceScaler can access it immediately
//             var existingEntity = ecsWorld.GetSingleInstanceEntityOrNull(new QueryDescription().WithAll<Components.AvatarVisibilityConfigComponent>());
//             if (existingEntity.IsNull())
//             {
//                 ecsWorld.Create(new Components.AvatarVisibilityConfigComponent
//                 {
//                     MaxVisibleAvatars = 60,
//                     MaxVisibilityDistance = 60f
//                 });
//             }
//
//             // Custom scalers are configured via ScriptableObject assets in Project Settings > Adaptive Performance
//             // Unity's Indexer system will automatically load and manage scaler instances
//             ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, "[Adaptive Performance] Initialized - custom scalers configured via Project Settings");
//
//             return UniTask.CompletedTask;
//         }
//
//         public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
//         {
//             // No ECS systems needed - Unity Adaptive Performance Indexer handles all performance monitoring and scaling
//         }
//
//         public void Dispose()
//         {
//             // ScriptableObject-based scalers are managed by Unity's Indexer system
//             // No manual cleanup required
//         }
//     }
// }
