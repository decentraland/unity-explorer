using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Optimization.PerformanceBudgeting;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using SceneRuntime;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using static DCL.Utilities.ConversionUtils;

namespace DCL.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DebugViewProfilingSystem : BaseUnityLoopSystem
    {
        private const float FRAME_STATS_COOLDOWN = 30; // update each <FRAME_STATS_COOLDOWN> frames (statistic buffer == 1000)

        private readonly IRealmData realmData;
        private readonly IDebugViewProfiler profiler;
        private readonly MemoryBudget memoryBudget;
        private readonly V8ActiveEngines v8ActiveEngines;
        private readonly IScenesCache scenesCache;
        private readonly CurrentSceneInfo currentSceneInfo;

        private readonly PerformanceBottleneckDetector bottleneckDetector = new ();

        private DebugWidgetVisibilityBinding visibilityBinding;
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

        private DebugViewProfilingSystem(World world, IRealmData realmData, IDebugViewProfiler profiler, MemoryBudget memoryBudget, IDebugContainerBuilder debugBuilder,
            V8ActiveEngines v8ActiveEngines, IScenesCache scenesCache) : base(world)
        {
            this.realmData = realmData;
            this.profiler = profiler;
            this.memoryBudget = memoryBudget;
            this.v8ActiveEngines = v8ActiveEngines;
            this.scenesCache = scenesCache;

            CreateView();
            return;

            void CreateView()
            {
                var version = new ElementBinding<string>(Application.version);

                debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.PERFORMANCE)
                           ?.SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true))
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
                            .AddCustomMarker("Total Used Memory [MB]:", usedMemory = new ElementBinding<string>(string.Empty))
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
            }
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            if (memoryVisibilityBinding.IsExpanded)
                UpdateMemoryView(profiler);

            if (visibilityBinding.IsExpanded)
            {
                SetFPS(fps, profiler.LastFrameTimeValueNs);

                if (framesSinceMetricsUpdate > FRAME_STATS_COOLDOWN)
                {
                    framesSinceMetricsUpdate = 0;
                    UpdateFrameStatisticsView(profiler);
                }
                
                if(sceneMetricsEnabled)
                    UpdateSceneMetrics();

                if (frameTimingsEnabled && bottleneckDetector.IsFrameTimingSupported && bottleneckDetector.TryCapture())
                    UpdateFrameTimings();

                framesSinceMetricsUpdate++;
            }
        }

        private void UpdateSceneMetrics()
        {
            bool isCurrentScene = scenesCache is { CurrentScene: { SceneStateProvider: { IsCurrent: true } } };
            JsMemorySizeInfo totalJsMemoryData = v8ActiveEngines.GetEnginesSumMemoryData();
            JsMemorySizeInfo currentSceneJsMemoryData = isCurrentScene ? v8ActiveEngines.GetEnginesMemoryDataForScene(scenesCache.CurrentScene.Info) : new JsMemorySizeInfo();

            jsHeapUsedSize.Value = $"{totalJsMemoryData.UsedHeapSizeMB:F1} | {currentSceneJsMemoryData.UsedHeapSizeMB:F1}";
            jsHeapTotalSize.Value = $"{totalJsMemoryData.TotalHeapSizeMB:F1} | {currentSceneJsMemoryData.TotalHeapSizeMB:F1}";
            jsHeapTotalExecutable.Value = $"{totalJsMemoryData.TotalHeapSizeExecutableMB:F1} | {currentSceneJsMemoryData.TotalHeapSizeExecutableMB:F1}";
            jsHeapLimit.Value = $"{totalJsMemoryData.HeapSizeLimitMB:F1}";
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



            jsEnginesCount.Value = v8ActiveEngines.Count.ToString();

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
        private void UpdateFrameStatisticsView(IDebugViewProfiler debugProfiler)
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

            elementBinding.Value = $"<color={color}>{value})</color>";
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
}
