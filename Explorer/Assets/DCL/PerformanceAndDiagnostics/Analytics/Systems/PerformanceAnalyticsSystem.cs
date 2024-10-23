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
        private const int FRAMES_SAMPLES_CAPACITY = 1024;
        private const ulong HICCUP_THRESHOLD_MS = 50; // 50 ms ~ 20 FPS

        private readonly AnalyticsConfiguration config;
        private readonly IAnalyticsController analytics;
        private readonly IJsonObjectBuilder jsonObjectBuilder;

        private readonly IRealmData realmData;

        private readonly IProfiler profiler;
        private readonly V8ActiveEngines v8ActiveEngines;

        // private readonly IScenesCache scenesCache;

        private readonly FrameTimesRecorder mainThreadFrameTimes = new (FRAMES_SAMPLES_CAPACITY);
        private readonly FrameTimesRecorder gpuFrameTimes = new (FRAMES_SAMPLES_CAPACITY);

        private float lastReportTime;

        public PerformanceAnalyticsSystem(
            World world,
            IAnalyticsController analytics,
            IRealmData realmData,
            IProfiler profiler,
            V8ActiveEngines v8ActiveEngines,
            IScenesCache scenesCache,
            IJsonObjectBuilder jsonObjectBuilder
        ) : base(world)
        {
            this.realmData = realmData;
            this.profiler = profiler;
            this.v8ActiveEngines = v8ActiveEngines;

            // this.scenesCache = scenesCache;
            this.analytics = analytics;
            this.jsonObjectBuilder = jsonObjectBuilder;
            config = analytics.Configuration;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            mainThreadFrameTimes.AddFrameTime(profiler.LastFrameTimeValueNs);
            gpuFrameTimes.AddFrameTime(profiler.LastGpuFrameTimeValueNs);

            lastReportTime += t;

            if (lastReportTime > config.PerformanceReportInterval)
            {
                ReportPerformanceMetrics(lastReportTime);

                lastReportTime = 0;

                mainThreadFrameTimes.Clear();
                gpuFrameTimes.Clear();
            }
        }

        private void ReportPerformanceMetrics(float measureTime)
        {
            jsonObjectBuilder.Set("total_time", measureTime);

            // TODO (Vit): include more detailed quality information (renderFeatures, fog, etc). Probably from QualitySettingsAsset.cs
            jsonObjectBuilder.Set("quality_level", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            jsonObjectBuilder.Set("player_count", 0); // TODO (Vit): How many users where nearby the current user

            // JS runtime memory
            // bool isCurrentScene = scenesCache is { CurrentScene: { SceneStateProvider: { IsCurrent: true } } };
            // JsMemorySizeInfo totalJsMemoryData = v8ActiveEngines.GetEnginesSumMemoryData();
            // JsMemorySizeInfo currentSceneJsMemoryData = isCurrentScene ? v8ActiveEngines.GetEnginesMemoryDataForScene(scenesCache.CurrentScene.Info) : new JsMemorySizeInfo();
            // jsonObjectBuilder.Set("jsheap_used", totalJsMemoryData.UsedHeapSizeMB);
            // jsonObjectBuilder.Set("jsheap_total", totalJsMemoryData.TotalHeapSizeMB);
            // jsonObjectBuilder.Set("jsheap_total_executable", totalJsMemoryData.TotalHeapSizeExecutableMB);
            // jsonObjectBuilder.Set("jsheap_limit", totalJsMemoryData.HeapSizeLimitMB);
            // jsonObjectBuilder.Set("jsheap_used_current_scene", !isCurrentScene ? -1f : currentSceneJsMemoryData.UsedHeapSizeMB);
            // jsonObjectBuilder.Set("jsheap_total_current_scene", !isCurrentScene ? -1f : currentSceneJsMemoryData.TotalHeapSizeMB);
            // jsonObjectBuilder.Set("jsheap_total_executable_current_scene", !isCurrentScene ? -1f : currentSceneJsMemoryData.TotalHeapSizeExecutableMB);
            jsonObjectBuilder.Set("running_v8_engines", v8ActiveEngines.Count);

            // Memory
            jsonObjectBuilder.Set("total_used_memory", ((ulong)profiler.TotalUsedMemoryInBytes).ByteToMB());
            jsonObjectBuilder.Set("system_used_memory", ((ulong)profiler.SystemUsedMemoryInBytes).ByteToMB());
            jsonObjectBuilder.Set("gc_used_memory", ((ulong)profiler.GcUsedMemoryInBytes).ByteToMB());
            jsonObjectBuilder.Set("total_gc_alloc", ((ulong)profiler.TotalGcAlloc).ByteToMB());

            // MainThread
            (bool hasValue, long count, long sumTime, long min, long max, float avg) hiccups = profiler.CalculateMainThreadHiccups();

            jsonObjectBuilder.Set("hiccups_in_thousand_frames", !hiccups.hasValue? 0 : hiccups.count);
            jsonObjectBuilder.Set("hiccups_time", !hiccups.hasValue? 0 : hiccups.sumTime * NS_TO_MS);
            jsonObjectBuilder.Set("hiccups_min", !hiccups.hasValue? 0 : hiccups.min * NS_TO_MS);
            jsonObjectBuilder.Set("hiccups_max", !hiccups.hasValue? 0 : hiccups.max * NS_TO_MS);
            jsonObjectBuilder.Set("hiccups_avg", !hiccups.hasValue? 0 : hiccups.avg * NS_TO_MS);

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

            jsonObjectBuilder.Set("samples", "<disabled>"); // -> assemble an array of samples (small one from percentiles)
            jsonObjectBuilder.Set("samples_amount", mainThreadFrameTimes.SamplesAmount); // use samples.Count > 10

            // GPU
            hiccups = profiler.CalculateGpuHiccups();
            jsonObjectBuilder.Set("gpu_hiccups_in_thousand_frames", !hiccups.hasValue? 0 : hiccups.count);
            jsonObjectBuilder.Set("gpu_hiccups_time", !hiccups.hasValue? 0 : hiccups.sumTime * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_hiccups_min", !hiccups.hasValue? 0 : hiccups.min * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_hiccups_max", !hiccups.hasValue? 0 : hiccups.max * NS_TO_MS);
            jsonObjectBuilder.Set("gpu_hiccups_avg", !hiccups.hasValue? 0 : hiccups.avg * NS_TO_MS);


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

            using PooledJsonObject pooled = jsonObjectBuilder.BuildPooled();

            analytics.Track(General.PERFORMANCE_REPORT, pooled.Json);
        }
    }
}
