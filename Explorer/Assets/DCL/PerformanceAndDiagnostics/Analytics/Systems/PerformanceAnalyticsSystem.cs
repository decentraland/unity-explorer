﻿using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.Multiplayer.Profiles.Tables;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Profiling.ECS;
using DCL.RealmNavigation;
using ECS;
using ECS.Abstract;
using UnityEngine;
using Utility.Json;
using static DCL.PerformanceAndDiagnostics.Analytics.AnalyticsEvents;
using static DCL.Utilities.ConversionUtils;
using World = Arch.Core.World;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(DebugViewProfilingSystem))]
    public partial class PerformanceAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly AnalyticsConfiguration config;
        private readonly IAnalyticsController analytics;
        private readonly ILoadingStatus loadingStatus;
        private readonly JsonObjectBuilder jsonObjectBuilder;

        private readonly IRealmData realmData;
        private readonly IProfiler profiler;

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;

        private float lastReportTime;

        public PerformanceAnalyticsSystem(
            World world,
            IAnalyticsController analytics,
            ILoadingStatus loadingStatus,
            IRealmData realmData,
            IProfiler profiler,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            JsonObjectBuilder jsonObjectBuilder) : base(world)
        {
            this.realmData = realmData;
            this.profiler = profiler;

            this.analytics = analytics;
            this.loadingStatus = loadingStatus;
            this.jsonObjectBuilder = jsonObjectBuilder;
            this.entityParticipantTable = entityParticipantTable;
            config = analytics.Configuration;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured || loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed || !Application.isFocused)
            {
                if (profiler.IsCollectingFrameData) profiler.StopFrameTimeDataCollection();
                return;
            }

            if (!profiler.IsCollectingFrameData)
            {
                profiler.StartFrameTimeDataCollection();
                return; // skip one frame so at least one frame is collected
            }

            profiler.UpdateFrameTimings();

            lastReportTime += t;

            if (lastReportTime > config.PerformanceReportInterval)
            {
                ReportPerformanceMetrics(lastReportTime);
                lastReportTime = 0;

                profiler.ClearFrameTimings();
            }
        }

        private void ReportPerformanceMetrics(float measureTime)
        {
            jsonObjectBuilder.Set("total_time", measureTime);

            // TODO (Vit): include more detailed quality information (renderFeatures, fog, etc). Probably from QualitySettingsAsset.cs
            jsonObjectBuilder.Set("quality_level", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            jsonObjectBuilder.Set("player_count", entityParticipantTable.Count);

            // JS runtime memory
            jsonObjectBuilder.Set("jsheap_used", profiler.AllScenesUsedHeapSize.ByteToMB());
            jsonObjectBuilder.Set("jsheap_total", profiler.AllScenesTotalHeapSize.ByteToMB());
            jsonObjectBuilder.Set("jsheap_total_executable", profiler.AllScenesTotalHeapSizeExecutable.ByteToMB());
            jsonObjectBuilder.Set("jsheap_limit", profiler.AllScenesHeapSizeLimit.ByteToMB());

            jsonObjectBuilder.Set("jsheap_used_current_scene", profiler.CurrentSceneHasStats ? 0 : profiler.CurrentSceneUsedHeapSize.ByteToMB());
            jsonObjectBuilder.Set("jsheap_total_current_scene", profiler.CurrentSceneHasStats ? 0 : profiler.CurrentSceneTotalHeapSize.ByteToMB());
            jsonObjectBuilder.Set("jsheap_total_executable_current_scene", profiler.CurrentSceneHasStats ? 0 : profiler.CurrentSceneTotalHeapSizeExecutable.ByteToMB());

            jsonObjectBuilder.Set("running_v8_engines", profiler.ActiveEngines);

            // Memory
            jsonObjectBuilder.Set("total_used_memory", ((ulong)profiler.TotalUsedMemoryInBytes).ByteToMB());
            jsonObjectBuilder.Set("system_used_memory", ((ulong)profiler.SystemUsedMemoryInBytes).ByteToMB());
            jsonObjectBuilder.Set("gc_used_memory", ((ulong)profiler.GcUsedMemoryInBytes).ByteToMB());
            jsonObjectBuilder.Set("total_gc_alloc", ((ulong)profiler.TotalGcAlloc).ByteToMB());

            // MainThread
            (bool hasValue, long count, long sumTime, long min, long max, float avg) hiccups = profiler.CalculateMainThreadHiccups();
            jsonObjectBuilder.Set("hiccups_in_thousand_frames", !hiccups.hasValue ? 0 : hiccups.count);
            jsonObjectBuilder.Set("hiccups_time", !hiccups.hasValue ? 0 : hiccups.sumTime * NS_TO_MS);
            jsonObjectBuilder.Set("hiccups_min", !hiccups.hasValue ? 0 : hiccups.min * NS_TO_MS);
            jsonObjectBuilder.Set("hiccups_max", !hiccups.hasValue ? 0 : hiccups.max * NS_TO_MS);
            jsonObjectBuilder.Set("hiccups_avg", !hiccups.hasValue ? 0 : hiccups.avg * NS_TO_MS);

            jsonObjectBuilder.Set("min_frame_time", profiler.MainThreadFrameTimes.Min * NS_TO_MS);
            jsonObjectBuilder.Set("max_frame_time", profiler.MainThreadFrameTimes.Max * NS_TO_MS);
            jsonObjectBuilder.Set("mean_frame_time", profiler.MainThreadFrameTimes.Avg * NS_TO_MS);
            jsonObjectBuilder.Set("frame_time_percentile_5", profiler.MainThreadFrameTimes.Percentile(5) * NS_TO_MS);
            jsonObjectBuilder.Set("frame_time_percentile_10", profiler.MainThreadFrameTimes.Percentile(10) * NS_TO_MS);
            jsonObjectBuilder.Set("frame_time_percentile_20", profiler.MainThreadFrameTimes.Percentile(20) * NS_TO_MS);
            jsonObjectBuilder.Set("frame_time_percentile_50", profiler.MainThreadFrameTimes.Percentile(50) * NS_TO_MS);
            jsonObjectBuilder.Set("frame_time_percentile_75", profiler.MainThreadFrameTimes.Percentile(75) * NS_TO_MS);
            jsonObjectBuilder.Set("frame_time_percentile_80", profiler.MainThreadFrameTimes.Percentile(80) * NS_TO_MS);
            jsonObjectBuilder.Set("frame_time_percentile_90", profiler.MainThreadFrameTimes.Percentile(90) * NS_TO_MS);
            jsonObjectBuilder.Set("frame_time_percentile_95", profiler.MainThreadFrameTimes.Percentile(95) * NS_TO_MS);

            jsonObjectBuilder.Set("samples", profiler.MainThreadFrameTimes.GetSamplesArrayAsString());
            jsonObjectBuilder.Set("samples_amount", profiler.MainThreadFrameTimes.SamplesAmount);

            // GPU
            hiccups = profiler.CalculateGpuHiccups();
            jsonObjectBuilder.Set("gpu_hiccups_in_thousand_frames", hiccups.count);
            jsonObjectBuilder.Set("gpu_hiccups_time", hiccups.count == 0 ? 0 : hiccups.sumTime * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_hiccups_min", hiccups.count == 0 ? 0 : hiccups.min * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_hiccups_max", hiccups.count == 0 ? 0 : hiccups.max * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_hiccups_avg", hiccups.count == 0 ? 0 : hiccups.avg * NS_TO_MS);

            jsonObjectBuilder.Set("gpu_min_frame_time", profiler.GpuFrameTimes.Min * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_max_frame_time", profiler.GpuFrameTimes.Max * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_mean_frame_time", profiler.GpuFrameTimes.Avg * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_frame_time_percentile_5", profiler.GpuFrameTimes.Percentile(5) * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_frame_time_percentile_10", profiler.GpuFrameTimes.Percentile(10) * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_frame_time_percentile_20", profiler.GpuFrameTimes.Percentile(20) * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_frame_time_percentile_50", profiler.GpuFrameTimes.Percentile(50) * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_frame_time_percentile_75", profiler.GpuFrameTimes.Percentile(75) * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_frame_time_percentile_80", profiler.GpuFrameTimes.Percentile(80) * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_frame_time_percentile_90", profiler.GpuFrameTimes.Percentile(90) * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_frame_time_percentile_95", profiler.GpuFrameTimes.Percentile(95) * NS_TO_MS);

            using PooledJsonObject pooled = jsonObjectBuilder.BuildPooled();

            analytics.Track(General.PERFORMANCE_REPORT, pooled.Json);
        }
    }
}
