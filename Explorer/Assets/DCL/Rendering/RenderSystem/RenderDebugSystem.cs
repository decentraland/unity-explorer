using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace DCL.Rendering.RenderSystem
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DebugRenderSystem : BaseUnityLoopSystem
    {
        private readonly ElementBinding<bool> residentDrawerBinding;
        private readonly ElementBinding<bool> gpuOcclusionBinding;

        // Track current asset to detect asset changes
        private UniversalRenderPipelineAsset? lastUrpAsset;

        private DebugRenderSystem(World world, IDebugContainerBuilder debugBuilder)
            : base(world)
        {
            // UI drives the state, initially everything is off
            residentDrawerBinding = new ElementBinding<bool>(false);
            gpuOcclusionBinding = new ElementBinding<bool>(false);

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.RENDERING)
                       ?.AddControl(
                             new DebugConstLabelDef("GPU Resident Drawer"),
                             new DebugToggleDef(residentDrawerBinding)
                         )
                        .AddControl(
                             new DebugConstLabelDef("GPU Occlusion (requires GRD)"),
                             new DebugToggleDef(gpuOcclusionBinding)
                         );
        }

        protected override void Update(float t)
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (urpAsset == null)
                return;

            GPUResidentDrawerMode currentGRDmode = residentDrawerBinding.Value
                ? GPUResidentDrawerMode.InstancedDrawing
                : GPUResidentDrawerMode.Disabled;

            if (urpAsset.gpuResidentDrawerMode != currentGRDmode)
                urpAsset.gpuResidentDrawerMode = currentGRDmode;

            bool occlusionEnabled = residentDrawerBinding.Value && gpuOcclusionBinding.Value;

            if (urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras != occlusionEnabled)
                urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras = occlusionEnabled;
        }
    }
}
