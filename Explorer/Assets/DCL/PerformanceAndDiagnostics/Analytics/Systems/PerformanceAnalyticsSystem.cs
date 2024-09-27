using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Profiling.ECS;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRuntime;
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
        private readonly int[] percentiles = { 5, 10, 20, 50, 75, 80, 90, 95 };

        private readonly IAnalyticsController analytics;
        private readonly IRealmData realmData;
        private readonly IAnalyticsReportProfiler profiler;
        private readonly V8ActiveEngines v8ActiveEngines;
        private readonly IScenesCache scenesCache;
        private readonly AnalyticsConfiguration config;
        private readonly IJsonObjectBuilder jsonObjectBuilder;

        private float lastReportTime;

        public PerformanceAnalyticsSystem(
            World world,
            IAnalyticsController analytics,
            IRealmData realmData,
            IAnalyticsReportProfiler profiler,
            V8ActiveEngines v8ActiveEngines,
            IScenesCache scenesCache,
            IJsonObjectBuilder jsonObjectBuilder
        ) : base(world)
        {
            this.realmData = realmData;
            this.profiler = profiler;
            this.v8ActiveEngines = v8ActiveEngines;
            this.scenesCache = scenesCache;
            this.analytics = analytics;
            this.jsonObjectBuilder = jsonObjectBuilder;
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
            (AnalyticsFrameTimeReport? gpuFrameTimeReport, AnalyticsFrameTimeReport? mainThreadReport, string samplesArray)
                = profiler.GetFrameTimesNs(percentiles);

            if (!mainThreadReport.HasValue || !gpuFrameTimeReport.HasValue)
                return;

            bool isCurrentScene = scenesCache is { CurrentScene: { SceneStateProvider: { IsCurrent: true } } };
            JsMemorySizeInfo totalJsMemoryData = v8ActiveEngines.GetEnginesSumMemoryData();
            JsMemorySizeInfo currentSceneJsMemoryData = isCurrentScene ? v8ActiveEngines.GetEnginesMemoryDataForScene(scenesCache.CurrentScene.Info) : new JsMemorySizeInfo();

            {
                // TODO (Vit): include more detailed quality information (renderFeatures, fog, etc). Probably from QualitySettingsAsset.cs
                jsonObjectBuilder.Set("quality_level", QualitySettings.names[QualitySettings.GetQualityLevel()]);
                jsonObjectBuilder.Set("player_count", 0); // TODO (Vit): How many users where nearby the current user

                // JS runtime memory
                jsonObjectBuilder.Set("jsheap_used", totalJsMemoryData.UsedHeapSizeMB);
                jsonObjectBuilder.Set("jsheap_total", totalJsMemoryData.TotalHeapSizeMB);
                jsonObjectBuilder.Set("jsheap_total_executable", totalJsMemoryData.TotalHeapSizeExecutableMB);
                jsonObjectBuilder.Set("jsheap_limit", totalJsMemoryData.HeapSizeLimitMB);
                jsonObjectBuilder.Set("jsheap_used_current_scene", !isCurrentScene ? -1f : currentSceneJsMemoryData.UsedHeapSizeMB);
                jsonObjectBuilder.Set("jsheap_total_current_scene", !isCurrentScene ? -1f : currentSceneJsMemoryData.TotalHeapSizeMB);
                jsonObjectBuilder.Set("jsheap_total_executable_current_scene", !isCurrentScene ? -1f : currentSceneJsMemoryData.TotalHeapSizeExecutableMB);
                jsonObjectBuilder.Set("running_v8_engines", v8ActiveEngines.Count);

                // Memory
                jsonObjectBuilder.Set("total_used_memory", ((ulong)profiler.TotalUsedMemoryInBytes).ByteToMB());
                jsonObjectBuilder.Set("system_used_memory", ((ulong)profiler.SystemUsedMemoryInBytes).ByteToMB());
                jsonObjectBuilder.Set("gc_used_memory", ((ulong)profiler.GcUsedMemoryInBytes).ByteToMB());
                jsonObjectBuilder.Set("total_gc_alloc", ((ulong)profiler.TotalGcAlloc).ByteToMB());

                // MainThread
                jsonObjectBuilder.Set("samples", samplesArray);
                jsonObjectBuilder.Set("samples_amount", mainThreadReport.Value.Samples);
                jsonObjectBuilder.Set("total_time", mainThreadReport.Value.SumTime * NS_TO_MS);
                jsonObjectBuilder.Set("hiccups_in_thousand_frames", mainThreadReport.Value.Stats.HiccupCount);
                jsonObjectBuilder.Set("hiccups_time", mainThreadReport.Value.HiccupsReport.HiccupsTime * NS_TO_MS);
                jsonObjectBuilder.Set("hiccups_avg", mainThreadReport.Value.HiccupsReport.HiccupsAvg * NS_TO_MS);
                jsonObjectBuilder.Set("hiccups_min", mainThreadReport.Value.HiccupsReport.HiccupsMin * NS_TO_MS);
                jsonObjectBuilder.Set("hiccups_max", mainThreadReport.Value.HiccupsReport.HiccupsMax * NS_TO_MS);
                jsonObjectBuilder.Set("min_frame_time", mainThreadReport.Value.Stats.MinFrameTime * NS_TO_MS);
                jsonObjectBuilder.Set("max_frame_time", mainThreadReport.Value.Stats.MaxFrameTime * NS_TO_MS);
                jsonObjectBuilder.Set("mean_frame_time", mainThreadReport.Value.Average * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_5", mainThreadReport.Value.Percentiles[0] * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_10", mainThreadReport.Value.Percentiles[1] * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_20", mainThreadReport.Value.Percentiles[2] * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_50", mainThreadReport.Value.Percentiles[3] * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_75", mainThreadReport.Value.Percentiles[4] * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_80", mainThreadReport.Value.Percentiles[5] * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_90", mainThreadReport.Value.Percentiles[6] * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_95", mainThreadReport.Value.Percentiles[7] * NS_TO_MS);

                // GPU
                jsonObjectBuilder.Set("gpu_hiccups_in_thousand_frames", gpuFrameTimeReport.Value.Stats.HiccupCount);
                jsonObjectBuilder.Set("gpu_hiccups_time", gpuFrameTimeReport.Value.HiccupsReport.HiccupsTime * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_hiccups_avg", gpuFrameTimeReport.Value.HiccupsReport.HiccupsAvg * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_hiccups_min", gpuFrameTimeReport.Value.HiccupsReport.HiccupsMin * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_hiccups_max", gpuFrameTimeReport.Value.HiccupsReport.HiccupsMax * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_min_frame_time", gpuFrameTimeReport.Value.Stats.MinFrameTime * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_max_frame_time", gpuFrameTimeReport.Value.Stats.MaxFrameTime * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_mean_frame_time", gpuFrameTimeReport.Value.Average * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_5", gpuFrameTimeReport.Value.Percentiles[0] * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_10", gpuFrameTimeReport.Value.Percentiles[1] * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_20", gpuFrameTimeReport.Value.Percentiles[2] * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_50", gpuFrameTimeReport.Value.Percentiles[3] * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_75", gpuFrameTimeReport.Value.Percentiles[4] * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_80", gpuFrameTimeReport.Value.Percentiles[5] * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_90", gpuFrameTimeReport.Value.Percentiles[6] * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_95", gpuFrameTimeReport.Value.Percentiles[7] * NS_TO_MS);
            }

            var json = jsonObjectBuilder.Build();
            analytics.Track(General.PERFORMANCE_REPORT, json);
            jsonObjectBuilder.Release(json);
        }
    }
}
