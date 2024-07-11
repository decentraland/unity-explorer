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
        private readonly IProfilingProvider profilingProvider;
        private readonly AnalyticsConfiguration config;

        private float lastReportTime;

        public PerformanceAnalyticsSystem(World world, IAnalyticsController analytics, IProfilingProvider profilingProvider) : base(world)
        {
            this.profilingProvider = profilingProvider;
            this.analytics = analytics;
            this.config = analytics.Configuration;
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
            analytics.Track(General.PERFORMANCE_REPORT, new JsonObject
            {
                ["total_used_memory"] = profilingProvider.TotalUsedMemoryInBytes * BYTES_TO_MEGABYTES,

                ["gpu_frame_time"] = profilingProvider.LastGPUFrameTimeValueInNS * BYTES_TO_MEGABYTES,

                ["current_frame_time"] = profilingProvider.LastFrameTimeValueInNS * NANOSECONDS_TO_MILLISECONDS,
                ["min_frame_time"] = profilingProvider.MinFrameTimeValueInNS * NANOSECONDS_TO_MILLISECONDS,

                ["average_frame_time"] = profilingProvider.AverageFrameTimeValueInNS * NANOSECONDS_TO_MILLISECONDS,
                ["average_frame_time_samples"] = profilingProvider.AverageFameTimeSamples,

                ["hiccup_count_in_buffer"] = profilingProvider.HiccupCountInBuffer,
                ["hiccup_buffer_size"] = profilingProvider.HiccupCountBufferSize,

                // TODO: include more detailed quality information (renderFeatures, fog, etc). Probably from QualitySettingsAsset.cs
                ["quality_level"] = QualitySettings.names[QualitySettings.GetQualityLevel()],
            });
        }
    }
}
