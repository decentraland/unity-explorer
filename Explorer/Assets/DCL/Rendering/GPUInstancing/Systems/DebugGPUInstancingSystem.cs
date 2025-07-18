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
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateAfter(typeof(GPUInstancingRenderSystem))]
    public partial class DebugGPUInstancingSystem : BaseUnityLoopSystem
    {
        private readonly GPUInstancingService service;

        public DebugGPUInstancingSystem(World world, IDebugContainerBuilder debugBuilder, GPUInstancingService service) : base(world)
        {
            this.service = service;

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.GPU_INSTANCING)?
               .AddToggleField("Is Enabled", OnIsEnableToggled, service.IsEnabled);
        }

        private void OnIsEnableToggled(ChangeEvent<bool> evt)
        {
            service.IsEnabled = evt.newValue;
        }

        protected override void Update(float t) { }
    }
}
