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
        private readonly AnalyticsConfiguration config;
        private readonly IAnalyticsController analytics;
        private readonly IJsonObjectBuilder jsonObjectBuilder;

        private readonly IRealmData realmData;

        private readonly IAnalyticsReportProfiler profiler;
        private readonly V8ActiveEngines v8ActiveEngines;
        private readonly IScenesCache scenesCache;


        private readonly FrameTimesRecorder mainThreadFrameTimes = new (capacity: 1024);
        private readonly HiccupsCounter mainThreadHiccups = new (hiccupThresholdMs: 50);

        private readonly FrameTimesRecorder gpuFrameTimes = new (capacity: 1024);
        private readonly HiccupsCounter gpuHiccups = new (hiccupThresholdMs: 50);

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

            mainThreadFrameTimes.AddFrameTime(profiler.CurrentFrameTimeValueNs);
            mainThreadHiccups.CheckForHiccup(profiler.CurrentFrameTimeValueNs);

            gpuFrameTimes.AddFrameTime(profiler.CurrentGpuFrameTimeValueNs);
            gpuHiccups.CheckForHiccup(profiler.CurrentGpuFrameTimeValueNs);

            lastReportTime += t;

            if (lastReportTime > config.PerformanceReportInterval)
            {
                ReportPerformanceMetrics();

                lastReportTime = 0;

                mainThreadFrameTimes.Clear();
                mainThreadHiccups.Clear();

                gpuFrameTimes.Clear();
                gpuHiccups.Clear();
            }
        }

        private void ReportPerformanceMetrics()
        {
            // (AnalyticsFrameTimeReport? gpuFrameTimeReport, AnalyticsFrameTimeReport? mainThreadReport, string samplesArray)
            //     = profiler.GetFrameTimesNs(percentiles);
            //
            // if (!mainThreadReport.HasValue || !gpuFrameTimeReport.HasValue)
            //     return;

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
                jsonObjectBuilder.Set("samples", 0);// mainThreadFrameTimes.GetSortedSamplesMs());
                jsonObjectBuilder.Set("samples_amount", mainThreadFrameTimes.SamplesAmount);
                jsonObjectBuilder.Set("total_time", mainThreadFrameTimes.TotalRecordedTime * NS_TO_MS);

                jsonObjectBuilder.Set("hiccups_in_thousand_frames", mainThreadHiccups.Amount);
                jsonObjectBuilder.Set("hiccups_time", mainThreadHiccups.SumTime * NS_TO_MS);
                jsonObjectBuilder.Set("hiccups_min", mainThreadHiccups.Min * NS_TO_MS);
                jsonObjectBuilder.Set("hiccups_max", mainThreadHiccups.Max * NS_TO_MS);
                jsonObjectBuilder.Set("hiccups_avg", mainThreadHiccups.Avg * NS_TO_MS);

                jsonObjectBuilder.Set("min_frame_time", mainThreadFrameTimes.Min * NS_TO_MS);
                jsonObjectBuilder.Set("max_frame_time", mainThreadFrameTimes.Max * NS_TO_MS);
                jsonObjectBuilder.Set("mean_frame_time", mainThreadFrameTimes.Avg * NS_TO_MS);

                jsonObjectBuilder.Set("frame_time_percentile_5", mainThreadFrameTimes.Percentile(5) * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_10", mainThreadFrameTimes.Percentile(10) * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_20", mainThreadFrameTimes.Percentile(20) * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_50", mainThreadFrameTimes.Percentile(50) * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_75", mainThreadFrameTimes.Percentile(75) * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_80", mainThreadFrameTimes.Percentile(80) * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_90", mainThreadFrameTimes.Percentile(90) * NS_TO_MS);
                jsonObjectBuilder.Set("frame_time_percentile_95", mainThreadFrameTimes.Percentile(95) * NS_TO_MS);

                // GPU
                jsonObjectBuilder.Set("gpu_hiccups_in_thousand_frames", gpuHiccups.Amount);
                jsonObjectBuilder.Set("gpu_hiccups_time", gpuHiccups.SumTime * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_hiccups_min", gpuHiccups.Min * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_hiccups_max", gpuHiccups.Max * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_hiccups_avg", gpuHiccups.Avg * NS_TO_MS);

                jsonObjectBuilder.Set("gpu_min_frame_time", gpuFrameTimes.Min * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_max_frame_time", gpuFrameTimes.Max * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_mean_frame_time", gpuFrameTimes.Avg * NS_TO_MS);

                jsonObjectBuilder.Set("gpu_frame_time_percentile_5", gpuFrameTimes.Percentile(5) * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_10", gpuFrameTimes.Percentile(10) * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_20", gpuFrameTimes.Percentile(20) * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_50", gpuFrameTimes.Percentile(50) * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_75", gpuFrameTimes.Percentile(75) * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_80", gpuFrameTimes.Percentile(80) * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_90", gpuFrameTimes.Percentile(90) * NS_TO_MS);
                jsonObjectBuilder.Set("gpu_frame_time_percentile_95", gpuFrameTimes.Percentile(95) * NS_TO_MS);
            }

            using var pooled = jsonObjectBuilder.BuildPooled();
            analytics.Track(General.PERFORMANCE_REPORT, pooled.Json);
        }
    }
}
