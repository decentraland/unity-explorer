using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Profiling.ECS;
using ECS.Abstract;
using Segment.Serialization;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ProfilingSystem))]
    public partial class PerformanceAnalyticsSystem: BaseUnityLoopSystem
    {
        private const float NANOSECONDS_TO_MILLISECONDS = 1e-6f;
        private const float BYTES_TO_MEGABYTES = 1e-6f;

        private readonly AnalyticsController analytics;
        private readonly AnalyticsConfiguration config;
        private readonly IProfilingProvider profilingProvider;
        private float lastReportTime;

        public PerformanceAnalyticsSystem(World world, AnalyticsController analytics, AnalyticsConfiguration config, IProfilingProvider profilingProvider) : base(world)
        {
            this.analytics = analytics;
            this.config = config;
            this.profilingProvider = profilingProvider;
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
            analytics.Track("performance_report", new Dictionary<string, JsonElement>
            {
                ["current_frame_time"] = profilingProvider.CurrentFrameTimeValueInNS * NANOSECONDS_TO_MILLISECONDS,
                ["min_frame_time"] = profilingProvider.MinFrameTimeValueInNS * NANOSECONDS_TO_MILLISECONDS,
                ["average_frame_time"] = profilingProvider.AverageFrameTimeValueInNS * NANOSECONDS_TO_MILLISECONDS,
                ["total_used_memory"] = profilingProvider.TotalUsedMemoryInBytes * BYTES_TO_MEGABYTES,

                ["hiccupCountInBuffer"] = profilingProvider.HiccupCountInBuffer,

                // ["device_model"] = SystemInfo.deviceModel, // "XPS 17 9720 (Dell Inc.)"
                // ["operating_system"] = SystemInfo.operatingSystem, // "Windows 11  (10.0.22631) 64bit"
                // ["system_memory_size"] = SystemInfo.systemMemorySize, // 65220 in [MB]
                // ["processor_type"] = SystemInfo.processorType, // "12th Gen Intel(R) Core(TM) i7-12700H"
                // ["graphics_device_name"] = SystemInfo.graphicsDeviceName, // "NVIDIA GeForce RTX 3050 Laptop GPU"
                // ["graphics_device_version"] = SystemInfo.graphicsDeviceVersion, // "Direct3D 11.0 [level 11.1]"
            });
        }
    }
}
