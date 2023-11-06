using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities.Builders;
using DCL.DebugUtilities.Declarations;
using DCL.DebugUtilities.UIBindings;
using DCL.Profiling;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.Profiling.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilingSystem : BaseUnityLoopSystem
    {
        private readonly IProfilingProvider profilingProvider;

        private DebugWidgetVisibilityBinding visibilityBinding;

        private ElementBinding<ulong> hiccups;
        private ElementBinding<string> fps;
        private ElementBinding<string> usedMemory;

        private float lastTimeSinceMetricsUpdate;

        private ProfilingSystem(World world, IProfilingProvider profilingProvider, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.profilingProvider = profilingProvider;

            CreateView();

            void CreateView()
            {
                var version = new ElementBinding<string>(Application.version);

                debugBuilder.AddWidget("Performance")
                            .SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true))
                            .AddCustomMarker("Version:", version)
                            .AddCustomMarker("Total Used Memory:", usedMemory = new ElementBinding<string>(string.Empty))
                            .AddCustomMarker("Frame Rate:", fps = new ElementBinding<string>(string.Empty))
                            .AddMarker("Hiccups last 1000 frames:", hiccups = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat);
            }
        }

        protected override void Update(float t)
        {
            if (visibilityBinding.IsExpanded && lastTimeSinceMetricsUpdate > 0.5f)
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
            hiccups.Value = profilingProvider.GetHiccupCountInBuffer();
            usedMemory.Value = $"{profilingProvider.TotalUsedMemoryInMB} MB";
            SetFPS();

            void SetFPS()
            {
                float averageFrameTimeInSeconds = (float)profilingProvider.GetAverageFrameTimeValueInNS() * 1e-9f;

                float frameTimeInMS = averageFrameTimeInSeconds * 1e3f;
                float frameRate = 1 / averageFrameTimeInSeconds;

                fps.Value = $"{frameRate:F1} fps ({frameTimeInMS:F1} ms)";
            }
        }
    }
}
