using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS.Abstract;
using Plugins.RustSegment.SegmentServerWrap;
using System;
using UnityEngine.UIElements;

namespace DCL.Rendering.GPUInstancing.Systems
{
    /// <summary>
    ///     Not supposed to work in Editor.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DebugGPUInstancingSystem : BaseUnityLoopSystem
    {
        private readonly GPUInstancingService gpuInstancingService;
        private readonly DebugWidgetVisibilityBinding visibilityBinding;

        public DebugGPUInstancingSystem(World world, IDebugContainerBuilder debugBuilder, GPUInstancingService gpuInstancingService) : base(world)
        {
            this.gpuInstancingService = gpuInstancingService;

            visibilityBinding = new DebugWidgetVisibilityBinding(true);

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.GPU_INSTANCING)?
                        .SetVisibilityBinding(visibilityBinding)
                        .AddToggleField("Is Enabled", OnIsEnableToggled, gpuInstancingService.IsEnabled);
        }

        private void OnIsEnableToggled(ChangeEvent<bool> evt)
        {
            gpuInstancingService.IsEnabled = evt.newValue;
        }

        protected override void Update(float t) { }
    }
}
