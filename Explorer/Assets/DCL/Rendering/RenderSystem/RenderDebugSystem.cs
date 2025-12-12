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
            residentDrawerBinding = new ElementBinding<bool>(false, OnResidentDrawerChanged);
            gpuOcclusionBinding = new ElementBinding<bool>(false, OnGpuOcclusionChanged);

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.RENDERING)
                       ?.AddControl(
                             new DebugConstLabelDef("GPU Resident Drawer"),
                             new DebugToggleDef(residentDrawerBinding)
                         )
                        .AddControl(
                             new DebugConstLabelDef("GPU Occlusion (requires GRD)"),
                             new DebugToggleDef(gpuOcclusionBinding)
                         );

            // Apply initial state (off) to current asset
            lastUrpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            ApplyCurrentStateToAsset(lastUrpAsset);
        }

        /// <summary>
        ///     Reactive callback: UI -> Runtime for GPU Resident Drawer
        /// </summary>
        private void OnResidentDrawerChanged(ChangeEvent<bool> evt)
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urpAsset)
                return;

            urpAsset.gpuResidentDrawerMode = evt.newValue
                ? GPUResidentDrawerMode.InstancedDrawing
                : GPUResidentDrawerMode.Disabled;

            // If GRD is disabled, also disable GPU Occlusion
            if (!evt.newValue && gpuOcclusionBinding.Value)
            {
                urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras = false;
                gpuOcclusionBinding.SetAndUpdate(false);
            }
        }

        /// <summary>
        ///     Reactive callback: UI -> Runtime for GPU Occlusion
        /// </summary>
        private void OnGpuOcclusionChanged(ChangeEvent<bool> evt)
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urpAsset)
                return;

            // GPU Occlusion only works when GRD is enabled
            if (urpAsset.gpuResidentDrawerMode == GPUResidentDrawerMode.InstancedDrawing)
                urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras = evt.newValue;
            else if (evt.newValue)
                gpuOcclusionBinding.SetAndUpdate(false); // Reject: GRD is off
        }

        protected override void Update(float t)
        {
            // Check if URP asset changed
            var currentAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (currentAsset != lastUrpAsset)
            {
                lastUrpAsset = currentAsset;
                ApplyCurrentStateToAsset(currentAsset);
            }
        }

        /// <summary>
        ///     Apply current UI state to the given URP asset
        /// </summary>
        private void ApplyCurrentStateToAsset(UniversalRenderPipelineAsset? urpAsset)
        {
            if (urpAsset == null)
                return;

            urpAsset.gpuResidentDrawerMode = residentDrawerBinding.Value
                ? GPUResidentDrawerMode.InstancedDrawing
                : GPUResidentDrawerMode.Disabled;

            urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras =
                residentDrawerBinding.Value && gpuOcclusionBinding.Value;
        }
    }
}
