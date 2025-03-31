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
        private readonly GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings settings;

        private readonly DebugWidgetVisibilityBinding visibilityBinding;
        private readonly ElementBinding<float> scaleFactor;
        private readonly float settingsScaleFactor;

        public DebugGPUInstancingSystem(World world, IDebugContainerBuilder debugBuilder, GPUInstancingService service) : base(world)
        {
            this.service = service;
            settings = this.service.Settings;

            settingsScaleFactor = settings.RenderDistScaleFactor;

            visibilityBinding = new DebugWidgetVisibilityBinding(true);
            scaleFactor = new ElementBinding<float>(settings.RenderDistScaleFactor);

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.GPU_INSTANCING)?
                        .SetVisibilityBinding(visibilityBinding)
                        .AddToggleField("Is Enabled", OnIsEnableToggled, service.IsEnabled)
                        .AddFloatSliderField("EnvDist ScaleFactor", scaleFactor, 0, 1);
        }

        protected override void OnDispose()
        {
            settings.RenderDistScaleFactor = settingsScaleFactor;
        }

        private void OnIsEnableToggled(ChangeEvent<bool> evt)
        {
            service.IsEnabled = evt.newValue;
        }

        protected override void Update(float _)
        {
            if (visibilityBinding.IsConnectedAndExpanded)
                settings.RenderDistScaleFactor = scaleFactor.Value;
        }
    }
}
