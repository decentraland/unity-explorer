using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PerformanceBudgeting;
using DCL.Profiling;
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
        private readonly MemoryBudgetProvider memoryBudgetProvider;

        private DebugWidgetVisibilityBinding visibilityBinding;
        private DebugWidgetVisibilityBinding memoryVisibilityBinding;

        private ElementBinding<ulong> hiccups;
        private ElementBinding<string> fps;
        private ElementBinding<string> usedMemory;
        private ElementBinding<string> memoryCheckpoints;

        private float lastTimeSinceMetricsUpdate;

        private ProfilingSystem(World world, IProfilingProvider profilingProvider, MemoryBudgetProvider memoryBudgetProvider, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.profilingProvider = profilingProvider;
            this.memoryBudgetProvider = memoryBudgetProvider;

            CreateView();

            void CreateView()
            {
                var version = new ElementBinding<string>(Application.version);

                DebugWidgetBuilder perfWidget = debugBuilder.AddWidget("Performance")
                                                            .SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true))
                                                            .AddCustomMarker("Version:", version)
                                                            .AddCustomMarker("Frame Rate:", fps = new ElementBinding<string>(string.Empty))
                                                            .AddMarker("Hiccups last 1000 frames:", hiccups = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat);

                if (!Debug.isDebugBuild)
                    perfWidget.AddCustomMarker("Total Used Memory:", usedMemory = new ElementBinding<string>(string.Empty))
                              .AddCustomMarker("Memory Budget Thresholds:", memoryCheckpoints = new ElementBinding<string>(string.Empty));
                else
                    debugBuilder.AddWidget("Memory")
                                .SetVisibilityBinding(memoryVisibilityBinding = new DebugWidgetVisibilityBinding(true))
                                .AddCustomMarker("Total Used Memory:", usedMemory = new ElementBinding<string>(string.Empty))
                                .AddCustomMarker("Memory Budget Thresholds:", memoryCheckpoints = new ElementBinding<string>(string.Empty))
                                .AddSingleButton("Memory NORMAL", () => MemoryBudgetProvider.DebugMode = MemoryUsageStatus.Normal)
                                .AddSingleButton("Memory WARNING", () => MemoryBudgetProvider.DebugMode = MemoryUsageStatus.Warning)
                                .AddSingleButton("Memory FULL", () => MemoryBudgetProvider.DebugMode = MemoryUsageStatus.Full);
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
            usedMemory.Value = $"<color={GetMemoryUsageColor()}>{profilingProvider.TotalUsedMemoryInMB}</color> MB";

            (float warning, float full) memoryRanges = memoryBudgetProvider.GetMemoryRanges();
            memoryCheckpoints.Value = $"<color=green>{memoryRanges.warning}</color> | <color=red>{memoryRanges.full}</color>";

            SetFPS();

            void SetFPS()
            {
                float averageFrameTimeInSeconds = (float)profilingProvider.AverageFrameTimeValueInNS * 1e-9f;

                float frameTimeInMS = averageFrameTimeInSeconds * 1e3f;
                float frameRate = 1 / averageFrameTimeInSeconds;

                fps.Value = $"{frameRate:F1} fps ({frameTimeInMS:F1} ms)";
            }

            string GetMemoryUsageColor()
            {
                return memoryBudgetProvider.GetMemoryUsageStatus() switch
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
