using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilingSystem : BaseUnityLoopSystem
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly FrameTimeCapBudgetProvider frameTimeBudget;
        private readonly MemoryBudgetProvider memoryBudget;

        private DebugWidgetVisibilityBinding visibilityBinding;
        private DebugWidgetVisibilityBinding memoryVisibilityBinding;

        private ElementBinding<ulong> hiccups;
        private ElementBinding<string> fps;
        private ElementBinding<string> usedMemory;
        private ElementBinding<string> memoryCheckpoints;

        private float lastTimeSinceMetricsUpdate;

        private ProfilingSystem(World world, IProfilingProvider profilingProvider, FrameTimeCapBudgetProvider frameTimeCapBudgetProvider, MemoryBudgetProvider memoryBudgetProvider, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.profilingProvider = profilingProvider;
            frameTimeBudget = frameTimeCapBudgetProvider;
            memoryBudget = memoryBudgetProvider;

            CreateView();
            return;

            void CreateView()
            {
                var version = new ElementBinding<string>(Application.version);

                debugBuilder.AddWidget("Performance")
                            .SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true))
                            .AddCustomMarker("Version:", version)
                            .AddCustomMarker("Frame Rate:", fps = new ElementBinding<string>(string.Empty))
                            .AddMarker("Hiccups last 1000 frames:", hiccups = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat)
                            .AddSingleButton("FrameTime CAPPED", () => frameTimeBudget.SimulateCappedFrameTime = true)
                            .AddSingleButton("FrameTime NORMAL", () => frameTimeBudget.SimulateCappedFrameTime = false);

                debugBuilder.AddWidget("Memory")
                            .SetVisibilityBinding(memoryVisibilityBinding = new DebugWidgetVisibilityBinding(true))
                            .AddSingleButton("Resources.UnloadUnusedAssets", () => Resources.UnloadUnusedAssets())
                            .AddSingleButton("GC.Collect", GC.Collect)
                            .AddCustomMarker("Total Used Memory [MB]:", usedMemory = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Memory Budget Thresholds [MB]:", memoryCheckpoints = new ElementBinding<string>(string.Empty))
                            .AddSingleButton("Memory NORMAL", () => memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.Normal)
                            .AddSingleButton("Memory WARNING", () => memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.Warning)
                            .AddSingleButton("Memory FULL", () => memoryBudget.SimulatedMemoryUsage = MemoryUsageStatus.Full);
            }
        }

        protected override void Update(float t)
        {
            if ((visibilityBinding.IsExpanded || memoryVisibilityBinding.IsExpanded) && lastTimeSinceMetricsUpdate > 0.5f)
            {
                lastTimeSinceMetricsUpdate = 0;
                UpdateView();
            }

            lastTimeSinceMetricsUpdate += t;
            profilingProvider.CheckHiccup();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateView()
        {
            hiccups.Value = profilingProvider.HiccupCountInBuffer;
            usedMemory.Value = $"<color={GetMemoryUsageColor()}>{(ulong)BytesFormatter.Convert(profilingProvider.TotalUsedMemoryInBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte)}</color>";

            (float warning, float full) memoryRanges = memoryBudget.GetMemoryRanges();
            memoryCheckpoints.Value = $"<color=green>{memoryRanges.warning}</color> | <color=red>{memoryRanges.full}</color>";

            SetFPS();
            return;

            void SetFPS()
            {
                float averageFrameTimeInSeconds = (float)profilingProvider.AverageFrameTimeValueInNS * 1e-9f;

                float frameTimeInMS = averageFrameTimeInSeconds * 1e3f;
                float frameRate = 1 / averageFrameTimeInSeconds;

                string fpsColor = frameTimeBudget.TrySpendBudget() ? "green" : "red";
                fps.Value = $"<color={fpsColor}>{frameRate:F1} fps ({frameTimeInMS:F1} ms)</color>";
            }

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
    }
}
