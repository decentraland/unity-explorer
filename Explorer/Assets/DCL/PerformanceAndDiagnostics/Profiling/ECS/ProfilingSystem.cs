using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Optimization.PerformanceBudgeting;
using ECS;
using ECS.Abstract;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilingSystem : BaseUnityLoopSystem
    {
        private const float NS_TO_SEC = 1e-9f; // nanoseconds to seconds
        private const float NS_TO_MS = 1e-6f; // nanoseconds to milliseconds
        private const float FRAME_STATS_COOLDOWN = 30; // update each <FRAME_STATS_COOLDOWN> frames (statistic buffer == 1000)

        private readonly IRealmData realmData;
        private readonly IDebugViewProfiler profiler;
        private readonly MemoryBudget memoryBudget;

        private DebugWidgetVisibilityBinding visibilityBinding;
        private DebugWidgetVisibilityBinding memoryVisibilityBinding;

        private ElementBinding<string> hiccups;
        private ElementBinding<string> fps;
        private ElementBinding<string> minfps;
        private ElementBinding<string> maxfps;
        private ElementBinding<string> usedMemory;
        private ElementBinding<string> memoryCheckpoints;

        private int framesSinceMetricsUpdate;

        private ProfilingSystem(World world, IRealmData realmData, IDebugViewProfiler profiler, MemoryBudget memoryBudget, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.realmData = realmData;
            this.profiler = profiler;
            this.memoryBudget = memoryBudget;

            CreateView();
            return;

            void CreateView()
            {
                var version = new ElementBinding<string>(Application.version);

                debugBuilder.AddWidget("Performance")
                            .SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true))
                            .AddCustomMarker("Version:", version)
                            .AddCustomMarker("Frame rate:", fps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Min FPS last 1k frames:", minfps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Max FPS last 1k frames:", maxfps = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Hiccups last 1k frames:", hiccups = new ElementBinding<string>(string.Empty));

                debugBuilder.AddWidget("Memory")
                            .SetVisibilityBinding(memoryVisibilityBinding = new DebugWidgetVisibilityBinding(true))
                            .AddSingleButton("Resources.UnloadUnusedAssets", () => Resources.UnloadUnusedAssets())
                            .AddSingleButton("GC.Collect", GC.Collect)
                            .AddCustomMarker("Total Used Memory [MB]:", usedMemory = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Memory Budget Thresholds [MB]:", memoryCheckpoints = new ElementBinding<string>(string.Empty))
                            .AddSingleButton("Memory NORMAL", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.Normal)
                            .AddSingleButton("Memory WARNING", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.Warning)
                            .AddSingleButton("Memory FULL", () => this.memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.Full);
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

                framesSinceMetricsUpdate++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMemoryView(IBudgetProfiler memoryProfiler)
        {
            usedMemory.Value = $"<color={GetMemoryUsageColor()}>{(ulong)BytesFormatter.Convert((ulong)memoryProfiler.TotalUsedMemoryInBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte)}</color>";
            (float warning, float full) memoryRanges = memoryBudget.GetMemoryRanges();
            memoryCheckpoints.Value = $"<color=green>{memoryRanges.warning}</color> | <color=red>{memoryRanges.full}</color>";

            return;

            string GetMemoryUsageColor()
            {
                return memoryBudget.GetMemoryUsageStatus() switch
                       {
                           MemoryUsageStatus.Normal => "green",
                           MemoryUsageStatus.Warning => "yellow",
                           MemoryUsageStatus.Full => "red",
                           _ => throw new ArgumentOutOfRangeException(),
                       };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFrameStatisticsView(IDebugViewProfiler debugProfiler)
        {
            var frameTimeStats = debugProfiler.FrameTimeStatsNs;

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
