using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Profiling.ECS;
using ECS;
using ECS.Abstract;
using Microsoft.ClearScript.V8;
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
        private readonly int[] percentiles = { 5, 10, 20, 50, 75, 80, 90, 95 };

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
                ReportPerformanceMetrics();
                lastReportTime = 0;
            }
        }

        private void ReportPerformanceMetrics()
        {
            // V8RuntimeHeapInfo heapInfo = new V8RuntimeHeapInfo();

            var mainThreadReport = profiler.GetMainThreadFramesNs(percentiles);
            var gpuFrameTimeReport = profiler.GetGpuThreadFramesNs(percentiles);

            if (!mainThreadReport.HasValue || !gpuFrameTimeReport.HasValue)
                return;

            analytics.Track(General.PERFORMANCE_REPORT, new JsonObject
            {
                // TODO (Vit): include more detailed quality information (renderFeatures, fog, etc). Probably from QualitySettingsAsset.cs
                ["quality_level"] = QualitySettings.names[QualitySettings.GetQualityLevel()],

                ["player_count"] = 0, // TODO (Vit): How many users where nearby the current user
                ["used_jsheap_size"] = 0, // TODO (Vit): use V8ScriptEngine.GetRuntimeHeapInfo(). Get the ref from V8EngineFactory, but maybe expose it in upper level

                ["memory_usage"] = BytesFormatter.Convert((ulong)profiler.SystemUsedMemoryInBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte),
                ["total_used_memory"] = BytesFormatter.Convert((ulong)profiler.TotalUsedMemoryInBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte),
                ["gc_used_memory"] = BytesFormatter.Convert((ulong)profiler.GcUsedMemoryInBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte),

                // MainThread FrameTime Report
                ["samples"] = mainThreadReport.Value.Samples,
                ["total_time"] = mainThreadReport.Value.SumTime,

                ["hiccups_in_thousand_frames"] = mainThreadReport.Value.Stats.HiccupCount,

                ["min_frame_time"] = mainThreadReport.Value.Stats.MinFrameTime * NS_TO_MS,
                ["max_frame_time"] = mainThreadReport.Value.Stats.MaxFrameTime * NS_TO_MS,
                ["mean_frame_time"] = mainThreadReport.Value.Average * NS_TO_MS,

                ["frame_time_percentile_5"] = mainThreadReport.Value.Percentiles[0] * NS_TO_MS,
                ["frame_time_percentile_10"] = mainThreadReport.Value.Percentiles[1] * NS_TO_MS,
                ["frame_time_percentile_20"] = mainThreadReport.Value.Percentiles[2] * NS_TO_MS,
                ["frame_time_percentile_50"] = mainThreadReport.Value.Percentiles[3] * NS_TO_MS,
                ["frame_time_percentile_75"] = mainThreadReport.Value.Percentiles[4] * NS_TO_MS,
                ["frame_time_percentile_80"] = mainThreadReport.Value.Percentiles[5] * NS_TO_MS,
                ["frame_time_percentile_90"] = mainThreadReport.Value.Percentiles[6] * NS_TO_MS,
                ["frame_time_percentile_95"] = mainThreadReport.Value.Percentiles[7] * NS_TO_MS,

                // GPU FrameTime Report
                ["gpu_hiccups_in_thousand_frames"] = gpuFrameTimeReport.Value.Stats.HiccupCount,

                ["gpu_min_frame_time"] = gpuFrameTimeReport.Value.Stats.MinFrameTime * NS_TO_MS,
                ["gpu_max_frame_time"] = gpuFrameTimeReport.Value.Stats.MaxFrameTime * NS_TO_MS,
                ["gpu_mean_frame_time"] = gpuFrameTimeReport.Value.Average * NS_TO_MS,

                ["gpu_frame_time_percentile_5"] = gpuFrameTimeReport.Value.Percentiles[0] * NS_TO_MS,
                ["gpu_frame_time_percentile_10"] = gpuFrameTimeReport.Value.Percentiles[1] * NS_TO_MS,
                ["gpu_frame_time_percentile_20"] = gpuFrameTimeReport.Value.Percentiles[2] * NS_TO_MS,
                ["gpu_frame_time_percentile_50"] = gpuFrameTimeReport.Value.Percentiles[3] * NS_TO_MS,
                ["gpu_frame_time_percentile_75"] = gpuFrameTimeReport.Value.Percentiles[4] * NS_TO_MS,
                ["gpu_frame_time_percentile_80"] = gpuFrameTimeReport.Value.Percentiles[5] * NS_TO_MS,
                ["gpu_frame_time_percentile_90"] = gpuFrameTimeReport.Value.Percentiles[6] * NS_TO_MS,
                ["gpu_frame_time_percentile_95"] = gpuFrameTimeReport.Value.Percentiles[7] * NS_TO_MS,
            });
        }
    }
}
