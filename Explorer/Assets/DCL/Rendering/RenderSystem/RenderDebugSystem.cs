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
    public partial class DebugRenderSystem : BaseUnityLoopSystem
    {
        private ElementBinding<bool> bEnableResidentDrawer;
        private ElementBinding<bool> bEnableGPUOcclusion;

        private DebugRenderSystem(World world, IDebugContainerBuilder debugBuilder)
            : base(world)
        {
            bEnableResidentDrawer = new ElementBinding<bool>(false);
            bEnableGPUOcclusion = new ElementBinding<bool>(false);

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.RENDERING)
                        .AddControl(
                             new DebugConstLabelDef("Enabled GPU Resident Drawer"),
                             new DebugToggleDef(bEnableResidentDrawer)
                         )
                        .AddControl(
                             new DebugConstLabelDef("Enabled GPU Occlusion - Requires GRD"),
                             new DebugToggleDef(bEnableGPUOcclusion)
                         );
        }

        protected override void Update(float t)
        {
            if (bEnableResidentDrawer != null && bEnableGPUOcclusion != null)
            {
                bool bGRDEnabled = EnableResidentDrawer(bEnableResidentDrawer.Value);
                EnableGPUOcclusion(bGRDEnabled, bEnableGPUOcclusion.Value);
            }
        }

        private bool EnableResidentDrawer(bool _bEnableResidentDrawer)
        {
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            urpAsset.gpuResidentDrawerMode = _bEnableResidentDrawer ? GPUResidentDrawerMode.InstancedDrawing : GPUResidentDrawerMode.Disabled;
            return urpAsset.gpuResidentDrawerMode == GPUResidentDrawerMode.InstancedDrawing;
        }

        private void EnableGPUOcclusion(bool _bGRDEnabled, bool _bEnableGPUOcclusion)
        {
            if (_bGRDEnabled)
            {
                UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                urpAsset.gpuResidentDrawerEnableOcclusionCullingInCameras = _bEnableGPUOcclusion;
            }
        }
    }
}
