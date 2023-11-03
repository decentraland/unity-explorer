using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities.Builders;
using DCL.DebugUtilities.Declarations;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using UnityEngine;

namespace ECS.Profiling.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilingSystem : BaseUnityLoopSystem
    {
        private readonly IProfilingProvider profilingProvider;

        private readonly DebugWidgetVisibilityBinding visibilityBinding;

        private readonly ElementBinding<ulong> hiccups;
        private readonly ElementBinding<string> fps;
        private readonly ElementBinding<string> usedMemory;

        private float lastTimeSinceMetricsUpdate;

        private ProfilingSystem(World world, IProfilingProvider profilingProvider, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.profilingProvider = profilingProvider;

            ElementBinding<string> version;

            debugBuilder.AddWidget("Performance")
                        .SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true))
                        .AddCustomMarker("Version:", version = new ElementBinding<string>(string.Empty))
                        .AddCustomMarker("Total Used Memory:", usedMemory = new ElementBinding<string>(string.Empty))
                        .AddCustomMarker("Frame Rate:", fps = new ElementBinding<string>(string.Empty))
                        .AddMarker("Hiccups last 1000 frames:", hiccups = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat);

            version.Value = Application.version;
        }

        protected override void Update(float t)
        {
            if (visibilityBinding.IsExpanded && lastTimeSinceMetricsUpdate > 0.5f)
            {
                lastTimeSinceMetricsUpdate = 0;
                hiccups.Value = profilingProvider.GetHiccupCountInBuffer();
                usedMemory.Value = $"{profilingProvider.TotalUsedMemoryInMB} MB";

                SetFPS();
            }

            lastTimeSinceMetricsUpdate += t;
            profilingProvider.CheckHiccup();
        }

        private void SetFPS()
        {
            float averageFrameTimeInSeconds = (float)profilingProvider.GetAverageFrameTimeValueInNS() * 1e-9f;

            float frameTimeInMS = averageFrameTimeInSeconds * 1e3f;
            float frameRate = 1 / averageFrameTimeInSeconds;

            fps.Value = $"{frameRate:F1} fps ({frameTimeInMS:F1} ms)";
        }
    }
}
