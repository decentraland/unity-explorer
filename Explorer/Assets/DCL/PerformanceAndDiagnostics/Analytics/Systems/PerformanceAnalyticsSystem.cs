using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Profiling.ECS;
using ECS.Abstract;
using Segment.Serialization;
using UnityEngine;
using World = Arch.Core.World;
using static DCL.PerformanceAndDiagnostics.Analytics.AnalyticsEvents;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ProfilingSystem))]
    public partial class PerformanceAnalyticsSystem : BaseUnityLoopSystem
    {
        private const float NANOSECONDS_TO_MILLISECONDS = 1e-6f;
        private const float BYTES_TO_MEGABYTES = 1e-6f;

        private readonly IAnalyticsController analytics;
        private readonly IProfiler profiler;
        private readonly AnalyticsConfiguration config;

        private float lastReportTime;

        public PerformanceAnalyticsSystem(World world, IAnalyticsController analytics, IProfiler profiler) : base(world)
        {
            this.profiler = profiler;
            this.analytics = analytics;
            config = analytics.Configuration;
        }

        protected override void Update(float t)
        {
            lastReportTime += t;

            if (lastReportTime > config.PerformanceReportInterval)
            {
                lastReportTime = 0;
                ReportPerformanceMetrics();
            }
        }

        private void ReportPerformanceMetrics()
        {
            // double[]? frameTimePercentilesArray = profilingProvider.GetFrameTimePercentiles(new[] { 1, 5, 10, 20, 50, 75, 80, 90, 95, 99 });

            analytics.Track(General.PERFORMANCE_REPORT, new JsonObject
            {
                // TODO: include more detailed quality information (renderFeatures, fog, etc). Probably from QualitySettingsAsset.cs
                ["quality_level"] = QualitySettings.names[QualitySettings.GetQualityLevel()],

                // ["dynamic_resolution"] = ScalableBufferManager.widthScaleFactor, ScalableBufferManager.heightScaleFactor
                ["memory_usage"] = profiler.TotalUsedMemoryInBytes * BYTES_TO_MEGABYTES,

                // ["samples"] = Total number of frames measured for this event. 🔴
                // ["total_time"] = Total length of the performance report. 🔴


                // ["gpu_frame_time"] = profilingProvider.LastGPUFrameTimeValueInNS * BYTES_TO_MEGABYTES,

                // SAMPLES
                // ["hiccup_buffer_size"] = profilingProvider.HiccupCountBufferSize,
                // ["hiccup_count_in_buffer"] = profilingProvider.HiccupCountInBuffer,

                // ["min_frame_time"] = profilingProvider.MinFrameTimeInNS * NANOSECONDS_TO_MILLISECONDS,
                // ["max_frame_time"] = profilingProvider.MaxFrameTimeInNS * NANOSECONDS_TO_MILLISECONDS,

                // ["average_frame_time_short_term"] = profilingProvider.AverageFrameTimeInNS * NANOSECONDS_TO_MILLISECONDS,
                // ["average_frame_time_samples"] = profilingProvider.AverageFameTimeSamples,

                // ["frame_time_percentile_1"] = frameTimePercentilesArray[0] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_5"] = frameTimePercentilesArray[1] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_10"] = frameTimePercentilesArray[2] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_20"] = frameTimePercentilesArray[3] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_50"] = frameTimePercentilesArray[4] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_75"] = frameTimePercentilesArray[5] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_80"] = frameTimePercentilesArray[6] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_90"] = frameTimePercentilesArray[7] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_95"] = frameTimePercentilesArray[8] * NANOSECONDS_TO_MILLISECONDS,
                // ["frame_time_percentile_99"] = frameTimePercentilesArray[9] * NANOSECONDS_TO_MILLISECONDS,
            });
        }
    }
}
