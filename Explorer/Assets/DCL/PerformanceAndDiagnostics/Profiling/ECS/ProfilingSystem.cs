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

namespace DCL.Profiling.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilingSystem : BaseUnityLoopSystem
    {
        private const float NANO_SECONDS_TO_SECONDS =  1e-9f;

        private readonly IProfilingProvider profilingProvider;
        private readonly FrameTimeCapBudget frameTimeBudget;
        private readonly MemoryBudget memoryBudget;

        private DebugWidgetVisibilityBinding visibilityBinding;
        private DebugWidgetVisibilityBinding memoryVisibilityBinding;

        private ElementBinding<ulong> hiccups;
        private ElementBinding<string> fps;
        private ElementBinding<string> minfps;
        private ElementBinding<string> maxfps;
        private ElementBinding<string> usedMemory;
        private ElementBinding<string> memoryCheckpoints;

        private float lastTimeSinceMetricsUpdate;

        private ProfilingSystem(World world, IProfilingProvider profilingProvider, FrameTimeCapBudget frameTimeCapBudget, MemoryBudget memoryBudget, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.profilingProvider = profilingProvider;
            frameTimeBudget = frameTimeCapBudget;
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
                            .AddMarker("Hiccups last 1k frames:", hiccups = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat);

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

            SetFPS(fps, (long)profilingProvider.AverageFrameTimeValueInNS);
            SetFPS(minfps, profilingProvider.MinFrameTimeValueInNS);
            SetFPS(maxfps, profilingProvider.MaxFrameTimeValueInNS);
            return;

            void SetFPS(ElementBinding<string> elementBinding, long value)
            {
                float averageFrameTimeInSeconds = value * NANO_SECONDS_TO_SECONDS;

                float frameTimeInMS = averageFrameTimeInSeconds * 1e3f;
                float frameRate = 1 / averageFrameTimeInSeconds;

                string fpsColor = frameTimeBudget.TrySpendBudget() ? "green" : "red";
                elementBinding.Value = $"<color={fpsColor}>{frameRate:F1} fps ({frameTimeInMS:F1} ms)</color>";
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
