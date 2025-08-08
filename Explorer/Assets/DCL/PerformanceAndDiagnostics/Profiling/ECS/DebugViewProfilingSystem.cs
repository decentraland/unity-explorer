using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Optimization.AdaptivePerformance.Systems;
using DCL.Optimization.PerformanceBudgeting;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle.IncreasingRadius;
using Global.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.UIElements;
using Utility.Types;
using static DCL.Utilities.ConversionUtils;

namespace DCL.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(UpdateProfilerSystem))]
    public partial class DebugViewProfilingSystem : BaseUnityLoopSystem
    {
        private const float FRAME_STATS_COOLDOWN = 30; // update each <FRAME_STATS_COOLDOWN> frames (statistic buffer == 1000)

        private readonly IRealmData realmData;
        private readonly IProfiler profiler;
        private readonly MemoryBudget memoryBudget;
        private readonly SceneLoadingLimit sceneLoadingLimit;
        private readonly PerformanceBottleneckDetector bottleneckDetector = new ();

        private DebugWidgetVisibilityBinding performanceVisibilityBinding;
        private DebugWidgetVisibilityBinding memoryVisibilityBinding;
        private DebugWidgetVisibilityBinding memoryLimitsVisibilityBinding;


        private ElementBinding<string> hiccups;
        private ElementBinding<string> fps;
        private ElementBinding<float> avgFrameTimeNs;
        private ElementBinding<string> minfps;
        private ElementBinding<string> maxfps;

        private ElementBinding<string> gpuFrameTime;
        private ElementBinding<string> cpuFrameTime;
        private ElementBinding<string> physicsDeltaTime;
        private ElementBinding<string> physicsSimulate;
        private ElementBinding<string> cpuMainThreadFrameTime;
        private ElementBinding<string> cpuMainThreadPresentWaitTime;
        private ElementBinding<string> cpuRenderThreadFrameTime;

        private ElementBinding<string> bottleneck;

        private ElementBinding<string> usedMemory;
        private ElementBinding<string> gcUsedMemory;
        private ElementBinding<string> isInAbundance;


        private ElementBinding<string> jsHeapUsedSize;
        private ElementBinding<string> jsHeapTotalSize;
        private ElementBinding<string> jsHeapTotalExecutable;
        private ElementBinding<string> jsHeapLimit;

        private ElementBinding<string> jsEnginesCount;

        private ElementBinding<string> memoryCheckpoints;

        private ElementBinding<string> maxAmountOfScenesThatCanLoadInMB;
        private ElementBinding<string> maxAmountOfReductedLODsThatCanLoadInMB;


        private int framesSinceMetricsUpdate;

        // Rolling window for average FPS over the last N frames
        private const int AVG_WINDOW_FRAMES = 100;
        private readonly ulong[] avgFrameWindow = new ulong[AVG_WINDOW_FRAMES];
        private int avgFrameWindowIndex;
        private int avgFrameWindowCount;
        private ulong avgFrameWindowSumNs;

        private bool frameTimingsEnabled;
        private bool sceneMetricsEnabled;
        private bool memoryLimitsEnabled;

        private DebugViewProfilingSystem(World world, IRealmData realmData, IProfiler profiler,
            MemoryBudget memoryBudget, IDebugContainerBuilder debugBuilder, DCLVersion dclVersion, AdaptivePhysicsSettings adaptivePhysicsSettings, SceneLoadingLimit sceneLoadingLimit)
            : base(world)
        {
            this.realmData = realmData;
            this.profiler = profiler;
            this.memoryBudget = memoryBudget;
            this.sceneLoadingLimit = sceneLoadingLimit;

            CreateView();
            return;

            void CreateView()
            {
                var version = new ElementBinding<string>(dclVersion.Version);

                debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.PERFORMANCE)
                           ?.SetVisibilityBinding(performanceVisibilityBinding = new DebugWidgetVisibilityBinding(true))
                            .AddControl(new AverageFpsBannerDef(avgFrameTimeNs = new ElementBinding<float>(0f), 30f, 20f), null)
                            .AddCustomMarker("Version:", version)
                            .AddCustomMarker("Frame rate:", fps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Min FPS last 1k frames:", minfps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Max FPS last 1k frames:", maxfps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Hiccups last 1k frames:", hiccups = new ElementBinding<string>(string.Empty))

                            .AddControl(new DebugDropdownDef(CreatePhysicsModeBinding(adaptivePhysicsSettings), "Physics Mode"), null)
                            .AddCustomMarker("Physics deltaTime:", physicsDeltaTime = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Physics Simulations in 10 frames", physicsSimulate = new ElementBinding<string>(string.Empty))

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
                            .AddCustomMarker("Is In Abundances:", isInAbundance = new ElementBinding<string>("YES"))
                            .AddSingleButton("Memory NORMAL", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.NORMAL)
                            .AddSingleButton("Memory WARNING", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.WARNING)
                            .AddSingleButton("Memory FULL", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.FULL)
                            .AddSingleButton("Toggle Abundance", () => this.memoryBudget.SimulateLackOfAbundance = !this.memoryBudget.SimulateLackOfAbundance)
                            .AddToggleField("Enable Scene Metrics", evt => sceneMetricsEnabled = evt.newValue, sceneMetricsEnabled)
                            .AddCustomMarker("Js-Heap Total [MB]:", jsHeapTotalSize = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Js-Heap Used [MB]:", jsHeapUsedSize = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Js-Heap Total Exec [MB]:", jsHeapTotalExecutable = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Js Heap Limit per engine [MB]:", jsHeapLimit = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Js Engines Count:", jsEnginesCount = new ElementBinding<string>(string.Empty));

                DebugWidgetBuilder? memoryLimitsBuilder = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.MEMORY_LIMITS);

                if (memoryLimitsBuilder != null)
                {
                    memoryLimitsBuilder.SetVisibilityBinding(memoryLimitsVisibilityBinding = new DebugWidgetVisibilityBinding(true))
                                       .AddCustomMarker("Upper scene limit [MB]:", maxAmountOfScenesThatCanLoadInMB = new ElementBinding<string>(string.Empty))
                                       .AddCustomMarker("Upperd Reducted LOD [MB]:", maxAmountOfReductedLODsThatCanLoadInMB = new ElementBinding<string>(string.Empty));

                    memoryLimitsEnabled = true;
                }


                debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.CRASH)?
                            .AddSingleButton("FatalError", () => { Utils.ForceCrash(ForcedCrashCategory.FatalError); })
                            .AddSingleButton("Abort", () => { Utils.ForceCrash(ForcedCrashCategory.Abort); })
                            .AddSingleButton("MonoAbort", () => { Utils.ForceCrash(ForcedCrashCategory.MonoAbort); })
                            .AddSingleButton("AccessViolation", () => { Utils.ForceCrash(ForcedCrashCategory.AccessViolation); })
                            .AddSingleButton("PureVirtualFunction", () => { Utils.ForceCrash(ForcedCrashCategory.PureVirtualFunction); });
            }
        }

        private static IndexedElementBinding CreatePhysicsModeBinding(AdaptivePhysicsSettings adpativePhysicsSettings)
        {
            const float UNITY_DEFAULT_FIXED_DELTA_TIME = 0.02f;

            return new IndexedElementBinding(
                Enum.GetNames(typeof(PhysSimulationMode)).ToList(),
                adpativePhysicsSettings.Mode.ToString(),
                evt =>
                {
                    if (Enum.TryParse(evt.value, out PhysSimulationMode mode))
                        switch (mode)
                        {
                            case PhysSimulationMode.DEFAULT:
                            case PhysSimulationMode.ADAPTIVE:
                                adpativePhysicsSettings.Mode = mode;
                                Physics.simulationMode = SimulationMode.FixedUpdate;
                                UnityEngine.Time.fixedDeltaTime = UNITY_DEFAULT_FIXED_DELTA_TIME;
                                break;
                            case PhysSimulationMode.MANUAL:
                                adpativePhysicsSettings.Mode = mode;
                                Physics.simulationMode = SimulationMode.Script;
                                break;
                            default: throw new ArgumentOutOfRangeException();
                        }
                }
            );
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            // Always update our average window irrespective of UI expansion
            ulong lastFrameNs = profiler.LastFrameTimeValueNs;
            if (lastFrameNs > 0)
            {
                if (avgFrameWindowCount < AVG_WINDOW_FRAMES)
                {
                    avgFrameWindow[avgFrameWindowIndex] = lastFrameNs;
                    avgFrameWindowSumNs += lastFrameNs;
                    avgFrameWindowIndex = (avgFrameWindowIndex + 1) % AVG_WINDOW_FRAMES;
                    avgFrameWindowCount++;
                }
                else
                {
                    ulong replaced = avgFrameWindow[avgFrameWindowIndex];
                    avgFrameWindow[avgFrameWindowIndex] = lastFrameNs;
                    avgFrameWindowSumNs = avgFrameWindowSumNs - replaced + lastFrameNs;
                    avgFrameWindowIndex = (avgFrameWindowIndex + 1) % AVG_WINDOW_FRAMES;
                }
            }

            if (memoryVisibilityBinding.IsExpanded)
            {
                UpdateMemoryView(profiler);

                if(sceneMetricsEnabled)
                    UpdateSceneMetrics();
            }

            if (memoryLimitsEnabled && memoryLimitsVisibilityBinding.IsExpanded)
            {
                maxAmountOfScenesThatCanLoadInMB.Value = sceneLoadingLimit.currentSceneLimits.SceneMaxAmountOfUsableMemoryInMB.ToString("F");
                maxAmountOfReductedLODsThatCanLoadInMB.Value = sceneLoadingLimit.currentSceneLimits.QualityReductedLODMaxAmountOfUsableMemoryInMB.ToString("F");
            }

            if (performanceVisibilityBinding.IsExpanded)
            {
                SetFPS(fps, (long)profiler.LastFrameTimeValueNs);

                physicsDeltaTime.Value = (UnityEngine.Time.fixedDeltaTime / MILISEC_TO_SEC).ToString("F2", CultureInfo.InvariantCulture);
                physicsSimulate.Value = profiler.PhysicsSimulationsAvgInTenFrames.ToString("F2", CultureInfo.InvariantCulture);

                if (framesSinceMetricsUpdate > FRAME_STATS_COOLDOWN)
                {
                    framesSinceMetricsUpdate = 0;
                    UpdateFrameStatisticsView(profiler);
                }

                if (frameTimingsEnabled && bottleneckDetector.IsFrameTimingSupported && bottleneckDetector.TryCapture())
                    UpdateFrameTimings();

                // Update Average based on the last 100 frames
                float averageNs = avgFrameWindowCount > 0 ? (float)(avgFrameWindowSumNs / (double)avgFrameWindowCount) : 0f;
                avgFrameTimeNs.SetAndUpdate(averageNs);
                framesSinceMetricsUpdate++;
            }
        }

        private void UpdateSceneMetrics()
        {
            jsHeapUsedSize.Value = $"{profiler.AllScenesUsedHeapSize.ByteToMB():f1} | {(profiler.CurrentSceneHasStats ? profiler.CurrentSceneUsedHeapSize.ByteToMB().ToString("f1") : "N/A")}";
            jsHeapTotalSize.Value = $"{profiler.AllScenesTotalHeapSize.ByteToMB():f1} | {(profiler.CurrentSceneHasStats ? profiler.CurrentSceneTotalHeapSize.ByteToMB().ToString("f1") : "N/A")}";
            jsHeapTotalExecutable.Value = $"{profiler.AllScenesTotalHeapSizeExecutable.ByteToMB():f1} | {(profiler.CurrentSceneHasStats ? profiler.CurrentSceneTotalHeapSizeExecutable.ByteToMB().ToString("f1") : "N/A")}";
            jsHeapLimit.Value = $"{profiler.AllScenesHeapSizeLimit.ByteToMB():f1}";
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

            jsEnginesCount.Value = profiler.ActiveEngines.ToString();

            (float warning, float full) memoryRanges = memoryBudget.GetMemoryRanges();
            memoryCheckpoints.Value = $"<color=green>{memoryRanges.warning}</color> | <color=red>{memoryRanges.full}</color>";
            isInAbundance.Value = memoryBudget.IsInAbundance() ? "YES" : "NO";
            return;

            string GetMemoryUsageColor()
            {
                if (memoryBudget.IsMemoryNormal())
                    return "green";

                if (memoryBudget.IsMemoryFull())
                    return "red";

                return "yellow";
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

            elementBinding.Value = frameTimeInMS == 0 ? "collecting.." : $"<color={fpsColor}>{frameRate:F1} fps ({frameTimeInMS:F1} ms)</color>";
        }
    }
}
