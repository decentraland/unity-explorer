// using DCL.Diagnostics;
// using DCL.SDKComponents.LightSource;
// using UnityEngine;
// using UnityEngine.AdaptivePerformance;
//
// namespace DCL.AdaptivePerformance.Scalers
// {
//     /// <summary>
//     /// Custom scaler that adjusts the maximum number of dynamic lights and shadow-casting lights based on performance.
//     /// Higher performance level = more lights and shadows allowed.
//     /// Level 0 = 4 lights/0 shadows, Level 4 = 12 lights/4 shadows.
//     /// </summary>
//     [CreateAssetMenu(fileName = "DynamicLightCountScaler", menuName = "DCL/Adaptive Performance/Dynamic Light Count Scaler")]
//     public class DynamicLightCountScaler : AdaptivePerformanceScaler
//     {
//         private readonly int[] maxLightsLevels = { 4, 6, 8, 10, 12 }; // 5 levels (0-4)
//         private readonly int[] maxShadowsLevels = { 0, 1, 2, 3, 4 }; // 5 levels (0-4)
//         private readonly LightSourceSettings lightSettings;
//
//         internal DynamicLightCountScaler(LightSourceSettings settings)
//         {
//             lightSettings = settings;
//         }
//
//         protected override void OnLevel()
//         {
//             if (!ScaleChanged())
//                 return;
//
//             int levelIdx = Mathf.Clamp(CurrentLevel, 0, maxLightsLevels.Length - 1);
//             int maxLights = maxLightsLevels[levelIdx];
//             int maxShadows = maxShadowsLevels[levelIdx];
//
//             // Create new scene limitations with updated counts
//             var sceneLimitations = new LightSourceSettings.SceneLimitationsSettings
//             {
//                 LightsPerParcel = 1f, // Keep existing per-parcel multiplier
//                 HardMaxLightCount = maxLights,
//                 MaxPointLightShadows = maxShadows / 2, // Split shadows between point and spot TODO mihak: Maybe we need a better way
//                 MaxSpotLightShadows = maxShadows / 2
//             };
//
//             lightSettings.ApplyQualitySettings(sceneLimitations, lightSettings.SpotLightsLods, lightSettings.PointLightsLods);
//
//             ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"[DynamicLightCountScaler] Level {CurrentLevel}: {maxLights} lights, {maxShadows} shadows");
//         }
//     }
// }
