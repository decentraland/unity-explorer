using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderSystem
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class RenderDebugSystem : BaseUnityLoopSystem
    {
        private readonly ElementBinding<bool> residentDrawerBinding;
        private readonly ElementBinding<bool> gpuOcclusionBinding;
        private readonly DebugWidgetVisibilityBinding? visibilityBinding;

        // Track current asset to detect asset changes
        private UniversalRenderPipelineAsset? lastUrpAsset;

        private RenderDebugSystem(World world, IDebugContainerBuilder debugBuilder)
            : base(world)
        {
            // UI drives the state, initially everything is off
            residentDrawerBinding = new ElementBinding<bool>(false);
            gpuOcclusionBinding = new ElementBinding<bool>(false);

            DebugWidgetBuilder? widget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.RENDERING);

            if (widget != null)
            {
                visibilityBinding = new DebugWidgetVisibilityBinding(true);

                widget.SetVisibilityBinding(visibilityBinding)
                      .AddControl(
                           new DebugConstLabelDef("GPU Resident Drawer"),
                           new DebugToggleDef(residentDrawerBinding)
                       )
                      .AddControl(
                           new DebugConstLabelDef("GPU Occlusion (requires GRD)"),
                           new DebugToggleDef(gpuOcclusionBinding)
                       );
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (urpAsset != null)
            {
                urpAsset.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras = false;
            }
        }

        protected override void Update(float t)
        {
            if (visibilityBinding is not { IsConnectedAndExpanded: true })
                return;

            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
                return;

            // Occlusion Culling (works only with GRD enabled)
            if (residentDrawerBinding.Value == false)
                gpuOcclusionBinding.SetAndUpdate(false);

            // Set GRD Mode
            GPUResidentDrawerMode currentGRDmode = residentDrawerBinding.Value
                ? GPUResidentDrawerMode.InstancedDrawing
                : GPUResidentDrawerMode.Disabled;

            if (urpAsset.gpuResidentDrawerMode != currentGRDmode)
                urpAsset.gpuResidentDrawerMode = currentGRDmode;

            // Set Occlusion Culling
            if (urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras != gpuOcclusionBinding.Value)
                urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras = gpuOcclusionBinding.Value;
        }
    }
}
