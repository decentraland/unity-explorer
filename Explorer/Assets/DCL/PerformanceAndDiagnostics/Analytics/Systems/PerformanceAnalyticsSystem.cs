using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Profiling.ECS;
using ECS;
using ECS.Abstract;
using Segment.Serialization;
using UnityEngine;
using World = Arch.Core.World;
using static DCL.PerformanceAndDiagnostics.Analytics.AnalyticsEvents;
using static DCL.Utilities.ConversionUtils;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(DebugViewProfilingSystem))]
    public partial class PerformanceAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly IAnalyticsController analytics;
        private readonly IRealmData realmData;
        private readonly IAnalyticsReportProfiler profiler;
        private readonly AnalyticsConfiguration config;

        private float lastReportTime;

        public PerformanceAnalyticsSystem(World world, IAnalyticsController analytics, IRealmData realmData, IAnalyticsReportProfiler profiler) : base(world)
        {
            this.realmData = realmData;
            this.profiler = profiler;
            this.analytics = analytics;
            config = analytics.Configuration;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            lastReportTime += t;

            if (lastReportTime > config.PerformanceReportInterval)
            {
                lastReportTime = 0;
                ReportPerformanceMetrics();
            }
        }

        private void ReportPerformanceMetrics()
        {
            int[] percentiles = new[] { 5, 10, 20, 50, 75, 80, 90, 95 };
            var mainThread = profiler.GetMainThreadFramesNs(percentiles);

            if (mainThread.HasValue)
            {
                // ....
            }

            analytics.Track(General.PERFORMANCE_REPORT, new JsonObject
            {
                // TODO: include more detailed quality information (renderFeatures, fog, etc). Probably from QualitySettingsAsset.cs
                ["quality_level"] = QualitySettings.names[QualitySettings.GetQualityLevel()],
                // ["dynamic_resolution_width"] = ScalableBufferManager.widthScaleFactor,
                // ["dynamic_resolution_height"] = ScalableBufferManager.heightScaleFactor,

                // ["PLAYER_COUNT"] //  How many users where nearby the current user
                // ["SAMPLES"] = Total number of frames measured for this event.
                // ["TOTAL_TIME"] = Total length of the performance report.

                ["memory_usage"] = BytesFormatter.Convert((ulong)profiler.TotalUsedMemoryInBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte),

                ["samples"] = mainThread.Value.Samples,
                ["hiccups_in_thousand_frames"] = mainThread.Value.Stats.HiccupCount,

                ["min_frame_time"] = mainThread.Value.Stats.MinFrameTime * NS_TO_MS,
                ["max_frame_time"] = mainThread.Value.Stats.MaxFrameTime * NS_TO_MS,
                ["mean_frame_time"] = mainThread.Value.Average * NS_TO_MS,

                ["frame_time_percentile_5"] = mainThread.Value.Percentiles[0] * NS_TO_MS,
                ["frame_time_percentile_10"] = mainThread.Value.Percentiles[1] * NS_TO_MS,
                ["frame_time_percentile_20"] = mainThread.Value.Percentiles[2] * NS_TO_MS,
                ["frame_time_percentile_50"] = mainThread.Value.Percentiles[3] * NS_TO_MS,
                ["frame_time_percentile_75"] = mainThread.Value.Percentiles[4] * NS_TO_MS,
                ["frame_time_percentile_80"] = mainThread.Value.Percentiles[5] * NS_TO_MS,
                ["frame_time_percentile_90"] = mainThread.Value.Percentiles[6] * NS_TO_MS,
                ["frame_time_percentile_95"] = mainThread.Value.Percentiles[7] * NS_TO_MS,

                // ["gpu_frame_time"] = profilingProvider.LastGPUFrameTimeValueInNS * BYTES_TO_MEGABYTES,
            });
        }
    }
}
