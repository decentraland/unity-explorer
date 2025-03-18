using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Optimization.PerformanceBudgeting;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using Global.Versioning;
using SceneRunner;
using SceneRunner.Scene;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Diagnostics;
using static DCL.Utilities.ConversionUtils;

namespace DCL.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DebugViewProfilingSystem : BaseUnityLoopSystem
    {
        private const float FRAME_STATS_COOLDOWN = 30; // update each <FRAME_STATS_COOLDOWN> frames (statistic buffer == 1000)

        private readonly IRealmData realmData;
        private readonly IProfiler profiler;
        private readonly MemoryBudget memoryBudget;
        private readonly IScenesCache scenesCache;
        private readonly CurrentSceneInfo currentSceneInfo;

        private readonly PerformanceBottleneckDetector bottleneckDetector = new ();

        private DebugWidgetVisibilityBinding performanceVisibilityBinding;
        private DebugWidgetVisibilityBinding memoryVisibilityBinding;

        private ElementBinding<string> hiccups;
        private ElementBinding<string> fps;
        private ElementBinding<string> minfps;
        private ElementBinding<string> maxfps;

        private ElementBinding<string> gpuFrameTime;
        private ElementBinding<string> cpuFrameTime;
        private ElementBinding<string> cpuMainThreadFrameTime;
        private ElementBinding<string> cpuMainThreadPresentWaitTime;
        private ElementBinding<string> cpuRenderThreadFrameTime;

        private ElementBinding<string> bottleneck;

        private ElementBinding<string> usedMemory;
        private ElementBinding<string> gcUsedMemory;

        private ElementBinding<string> jsHeapUsedSize;
        private ElementBinding<string> jsHeapTotalSize;
        private ElementBinding<string> jsHeapTotalExecutable;
        private ElementBinding<string> jsHeapLimit;

        private ElementBinding<string> jsEnginesCount;

        private ElementBinding<string> memoryCheckpoints;

        private int framesSinceMetricsUpdate;

        private bool frameTimingsEnabled;
        private bool sceneMetricsEnabled;

        internal static ulong allScenesTotalHeapSize { get; private set; }
        internal static ulong allScenesTotalHeapSizeExecutable { get; private set; }
        internal static ulong allScenesTotalPhysicalSize { get; private set; }
        internal static ulong allScenesUsedHeapSize { get; private set; }
        internal static ulong allScenesHeapSizeLimit { get; private set; }
        internal static ulong allScenesTotalExternalSize { get; private set; }
        internal static int activeEngines { get; private set; }
        internal static ulong currentSceneTotalHeapSize { get; private set; }
        internal static ulong currentSceneTotalHeapSizeExecutable { get; private set; }
        internal static ulong currentSceneTotalPhysicalSize { get; private set; }
        internal static ulong currentSceneUsedHeapSize { get; private set; }
        internal static ulong currentSceneHeapSizeLimit { get; private set; }
        internal static ulong currentSceneTotalExternalSize { get; private set; }
        internal static bool currentSceneHasStats { get; private set; }

        private DebugViewProfilingSystem(World world, IRealmData realmData, IProfiler profiler,
            MemoryBudget memoryBudget, IDebugContainerBuilder debugBuilder, IScenesCache scenesCache,
            DCLVersion dclVersion) : base(world)
        {
            this.realmData = realmData;
            this.profiler = profiler;
            this.memoryBudget = memoryBudget;
            this.scenesCache = scenesCache;

            CreateView();
            return;

            void CreateView()
            {
                var version = new ElementBinding<string>(dclVersion.Version);

                debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.PERFORMANCE)
                           ?.SetVisibilityBinding(performanceVisibilityBinding = new DebugWidgetVisibilityBinding(true))
                            .AddCustomMarker("Version:", version)
                            .AddCustomMarker("Frame rate:", fps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Min FPS last 1k frames:", minfps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Max FPS last 1k frames:", maxfps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Hiccups last 1k frames:", hiccups = new ElementBinding<string>(string.Empty))
                            .AddToggleField("Enable Bottleneck detector", evt => frameTimingsEnabled = evt.newValue, frameTimingsEnabled)
                            .AddCustomMarker("GPU:", gpuFrameTime = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("CPU:", cpuFrameTime = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("CPU MainThread:", cpuMainThreadFrameTime = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("CPU RenderThread:", cpuRenderThreadFrameTime = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("CPU MainThread PresentWait:", cpuMainThreadPresentWaitTime = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Bottleneck:", bottleneck = new ElementBinding<string>(string.Empty));

                debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.MEMORY)
                           ?.SetVisibilityBinding(memoryVisibilityBinding = new DebugWidgetVisibilityBinding(true))
                            .AddSingleButton("Resources.UnloadUnusedAssets", () => Resources.UnloadUnusedAssets())
                            .AddSingleButton("GC.Collect", GC.Collect)
                            .AddCustomMarker("System Used Memory [MB]:", usedMemory = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Gc Used Memory [MB]:", gcUsedMemory = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Memory Budget Thresholds [MB]:", memoryCheckpoints = new ElementBinding<string>(string.Empty))
                            .AddSingleButton("Memory NORMAL", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.NORMAL)
                            .AddSingleButton("Memory WARNING", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.WARNING)
                            .AddSingleButton("Memory FULL", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.FULL)
                            .AddToggleField("Enable Scene Metrics", evt => sceneMetricsEnabled = evt.newValue, sceneMetricsEnabled)
                            .AddCustomMarker("Js-Heap Total [MB]:", jsHeapTotalSize = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Js-Heap Used [MB]:", jsHeapUsedSize = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Js-Heap Total Exec [MB]:", jsHeapTotalExecutable = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Js Heap Limit per engine [MB]:", jsHeapLimit = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Js Engines Count:", jsEnginesCount = new ElementBinding<string>(string.Empty));

                debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.CRASH)?
                            .AddSingleButton("FatalError", () => { Utils.ForceCrash(ForcedCrashCategory.FatalError); })
                            .AddSingleButton("Abort", () => { Utils.ForceCrash(ForcedCrashCategory.Abort); })
                            .AddSingleButton("MonoAbort", () => { Utils.ForceCrash(ForcedCrashCategory.MonoAbort); })
                            .AddSingleButton("AccessViolation", () => { Utils.ForceCrash(ForcedCrashCategory.AccessViolation); })
                            .AddSingleButton("PureVirtualFunction", () => { Utils.ForceCrash(ForcedCrashCategory.PureVirtualFunction); });
            }
        }

        protected override void Update(float t)
        {
            SampleJavaScriptProfilerCounters();

            if (!realmData.Configured) return;

            if (memoryVisibilityBinding.IsExpanded)
            {
                UpdateMemoryView(profiler);

                if(sceneMetricsEnabled)
                    UpdateSceneMetrics();
            }

            if (performanceVisibilityBinding.IsExpanded)
            {
                SetFPS(fps, (long)profiler.LastFrameTimeValueNs);

                if (framesSinceMetricsUpdate > FRAME_STATS_COOLDOWN)
                {
                    framesSinceMetricsUpdate = 0;
                    UpdateFrameStatisticsView(profiler);
                }

                if (frameTimingsEnabled && bottleneckDetector.IsFrameTimingSupported && bottleneckDetector.TryCapture())
                    UpdateFrameTimings();

                framesSinceMetricsUpdate++;
            }
        }

        private void SampleJavaScriptProfilerCounters()
        {
            allScenesTotalHeapSize = 0ul;
            allScenesTotalHeapSizeExecutable = 0ul;
            allScenesTotalPhysicalSize = 0ul;
            allScenesUsedHeapSize = 0ul;
            allScenesHeapSizeLimit = 0ul;
            allScenesTotalExternalSize = 0ul;
            activeEngines = 0;

            SampleJavaScriptProfilerCountersQuery(World);

            if (activeEngines > 0)
                allScenesHeapSizeLimit /= (ulong)activeEngines;

            currentSceneHasStats = false;

            if (scenesCache is { CurrentScene: { SceneStateProvider: { IsCurrent: true } } })
            {
                var scene = (SceneFacade)scenesCache.CurrentScene;
                var heapInfo = scene.runtimeInstance.RuntimeHeapInfo;

                if (heapInfo != null)
                {
                    currentSceneTotalHeapSize = heapInfo.TotalHeapSize;
                    currentSceneTotalHeapSizeExecutable = heapInfo.TotalHeapSizeExecutable;
                    currentSceneTotalPhysicalSize = heapInfo.TotalPhysicalSize;
                    currentSceneUsedHeapSize = heapInfo.UsedHeapSize;
                    currentSceneHeapSizeLimit = heapInfo.HeapSizeLimit;
                    currentSceneTotalExternalSize = heapInfo.TotalExternalSize;
                    currentSceneHasStats = true;
                }
            }

#if ENABLE_PROFILER
            JavaScriptProfilerCounters.TOTAL_HEAP_SIZE.Sample(allScenesTotalHeapSize);
            JavaScriptProfilerCounters.TOTAL_HEAP_SIZE_EXECUTABLE.Sample(allScenesTotalHeapSizeExecutable);
            JavaScriptProfilerCounters.TOTAL_PHYSICAL_SIZE.Sample(allScenesTotalPhysicalSize);
            JavaScriptProfilerCounters.USED_HEAP_SIZE.Sample(allScenesUsedHeapSize);
            JavaScriptProfilerCounters.TOTAL_EXTERNAL_SIZE.Sample(allScenesTotalExternalSize);
            JavaScriptProfilerCounters.ACTIVE_ENGINES.Sample(activeEngines);
#endif
        }

        [Query]
        private void SampleJavaScriptProfilerCounters(ISceneFacade scene0)
        {
            var scene = (SceneFacade)scene0;
            var heapInfo = scene.runtimeInstance.RuntimeHeapInfo;

            if (heapInfo != null)
            {
                allScenesTotalHeapSize += heapInfo.TotalHeapSize;
                allScenesTotalHeapSizeExecutable += heapInfo.TotalHeapSizeExecutable;
                allScenesTotalPhysicalSize += heapInfo.TotalPhysicalSize;
                allScenesUsedHeapSize += heapInfo.UsedHeapSize;
                allScenesHeapSizeLimit += heapInfo.HeapSizeLimit;
                allScenesTotalExternalSize += heapInfo.TotalExternalSize;
                activeEngines += 1;
            }
        }

        private void UpdateSceneMetrics()
        {
            jsHeapUsedSize.Value = $"{allScenesUsedHeapSize.ByteToMB():f1} | {(currentSceneHasStats ? currentSceneUsedHeapSize.ByteToMB().ToString("f1") : "N/A")}";
            jsHeapTotalSize.Value = $"{allScenesTotalHeapSize.ByteToMB():f1} | {(currentSceneHasStats ? currentSceneTotalHeapSize.ByteToMB().ToString("f1") : "N/A")}";
            jsHeapTotalExecutable.Value = $"{allScenesTotalHeapSizeExecutable.ByteToMB():f1} | {(currentSceneHasStats ? currentSceneTotalHeapSizeExecutable.ByteToMB().ToString("f1") : "N/A")}";
            jsHeapLimit.Value = $"{allScenesHeapSizeLimit.ByteToMB():f1}";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFrameTimings()
        {
            var frameTiming = bottleneckDetector.FrameTiming;

            bottleneck.Value = bottleneckDetector.DetermineBottleneck().ToString();
            gpuFrameTime.Value = frameTiming.gpuFrameTime.ToString("F2", CultureInfo.InvariantCulture);
            cpuFrameTime.Value = frameTiming.cpuFrameTime.ToString("F2", CultureInfo.InvariantCulture);
            cpuMainThreadFrameTime.Value = frameTiming.cpuMainThreadFrameTime.ToString("F2", CultureInfo.InvariantCulture);
            cpuRenderThreadFrameTime.Value = frameTiming.cpuRenderThreadFrameTime.ToString("F2", CultureInfo.InvariantCulture);
            cpuMainThreadPresentWaitTime.Value = frameTiming.cpuMainThreadPresentWaitTime.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMemoryView(IMemoryProfiler memoryProfiler)
        {
            usedMemory.Value =
                $"<color={GetMemoryUsageColor()}>{(ulong)BytesFormatter.Convert((ulong)memoryProfiler.SystemUsedMemoryInBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte)}</color>";
            gcUsedMemory.Value = BytesFormatter.Convert((ulong)memoryProfiler.GcUsedMemoryInBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte).ToString("F0", CultureInfo.InvariantCulture);

            jsEnginesCount.Value = activeEngines.ToString();

            (float warning, float full) memoryRanges = memoryBudget.GetMemoryRanges();
            memoryCheckpoints.Value = $"<color=green>{memoryRanges.warning}</color> | <color=red>{memoryRanges.full}</color>";
            return;

            string GetMemoryUsageColor()
            {
                return memoryBudget.GetMemoryUsageStatus() switch
                       {
                           MemoryUsageStatus.NORMAL => "green",
                           MemoryUsageStatus.WARNING => "yellow",
                           MemoryUsageStatus.FULL => "red",
                           _ => throw new ArgumentOutOfRangeException(),
                       };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFrameStatisticsView(IProfiler debugProfiler)
        {
            FrameTimeStats? frameTimeStats = debugProfiler.CalculateMainThreadFrameTimesNs();

            if (frameTimeStats.HasValue)
            {
                SetColoredHiccup(hiccups, frameTimeStats.Value.HiccupCount);
                SetFPS(maxfps, frameTimeStats.Value.MinFrameTime);
                SetFPS(minfps, frameTimeStats.Value.MaxFrameTime);
            }
        }

        private static void SetColoredHiccup(ElementBinding<string> elementBinding, long value)
        {
            string color = value switch
                           {
                               < 10 => "green",
                               < 30 => "yellow",
                               < 100 => "orange",
                               _ => "red",
                           };

            elementBinding.Value = $"<color={color}>{value}</color>";
        }

        private static void SetFPS(ElementBinding<string> elementBinding, long value)
        {
            float frameTimeInMS = value * NS_TO_MS;
            float frameRate = 1 / (value * NS_TO_SEC);

            string fpsColor = frameRate switch
                              {
                                  < 20 => "red",
                                  < 30 => "orange",
                                  < 40 => "yellow",
                                  _ => "green",
                              };

            elementBinding.Value = $"<color={fpsColor}>{frameRate:F1} fps ({frameTimeInMS:F1} ms)</color>";
        }
    }

#if ENABLE_PROFILER
    public static class JavaScriptProfilerCounters
    {
        public const string CATEGORY_NAME = "JavaScript";
        public static readonly ProfilerCategory CATEGORY = new (CATEGORY_NAME);

        public static readonly string TOTAL_HEAP_SIZE_NAME = "Total Heap Size";
        public static readonly string TOTAL_HEAP_SIZE_EXECUTABLE_NAME = "Total Executable Heap Size";
        public static readonly string TOTAL_PHYSICAL_SIZE_NAME = "Total Physical Memory Size";
        public static readonly string USED_HEAP_SIZE_NAME = "Used Heap Size";
        public static readonly string TOTAL_EXTERNAL_SIZE_NAME = "Total External Memory Size";
        public static readonly string ACTIVE_ENGINES_NAME = "Active Engines";

        public static readonly ProfilerCounter<ulong> TOTAL_HEAP_SIZE
            = new (CATEGORY, TOTAL_HEAP_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_HEAP_SIZE_EXECUTABLE
            = new (CATEGORY, TOTAL_HEAP_SIZE_EXECUTABLE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_PHYSICAL_SIZE
            = new (CATEGORY, TOTAL_PHYSICAL_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> USED_HEAP_SIZE
            = new (CATEGORY, USED_HEAP_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_EXTERNAL_SIZE
            = new (CATEGORY, TOTAL_EXTERNAL_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<int> ACTIVE_ENGINES
            = new (CATEGORY, ACTIVE_ENGINES_NAME, ProfilerMarkerDataUnit.Count);
    }
#endif
}
