// using DCL.Diagnostics;
// using DCL.SDKComponents.MediaStream.Settings;
// using UnityEngine;
// using UnityEngine.AdaptivePerformance;
//
// namespace DCL.AdaptivePerformance.Scalers
// {
//     /// <summary>
//     /// Custom scaler that adjusts the maximum number of simultaneous video streams based on performance.
//     /// Higher performance level = more video streams can play simultaneously.
//     /// Level 0 = 1 video, Level 4 = 10 videos.
//     /// </summary>
//     [CreateAssetMenu(fileName = "VideoStreamScaler", menuName = "DCL/Adaptive Performance/Video Stream Scaler")]
//     public class VideoStreamScaler : AdaptivePerformanceScaler
//     {
//         private readonly int[] levels = { 1, 2, 3, 5, 10 }; // 5 levels (0-4)
//         private readonly VideoPrioritizationSettings videoSettings;
//
//         internal VideoStreamScaler(VideoPrioritizationSettings settings)
//         {
//             videoSettings = settings;
//         }
//
//         protected override void Awake()
//         {
//             base.Awake();
//             Debug.Log("PACO: VideoStreamScaler Awake");
//         }
//
//         /// <summary>
//         /// Called by Unity Adaptive Performance Indexer when performance level changes.
//         /// Updates the maximum number of simultaneous video streams that can play.
//         /// </summary>
//         protected override void OnLevel()
//         {
//             return;
//             // Only apply changes if the scale has actually changed
//             if (!ScaleChanged())
//                 return;
//
//             int maxVideos = levels[Mathf.Clamp(CurrentLevel, 0, levels.Length - 1)];
//
//             // Update video prioritization settings
//             videoSettings.MaximumSimultaneousVideos = maxVideos;
//
//             ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"[VideoStreamScaler] Level {CurrentLevel}: {maxVideos} videos");
//         }
//     }
// }
