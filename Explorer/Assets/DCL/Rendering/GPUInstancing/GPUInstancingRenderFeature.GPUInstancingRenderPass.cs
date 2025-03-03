using ECS;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.GPUInstancing
{
    public partial class GPUInstancingRenderFeature
    {
        public class GPUInstancingRenderPass : ScriptableRenderPass
        {
            private const string PROFILER_TAG = "_DCL.GPUInstancingRenderPass";

            private GPUInstancingService instancingService;
            private IRealmData realmData;

            public GPUInstancingRenderPass(GPUInstancingService service)
            {
                this.instancingService = service;
            }

            public void SetService(GPUInstancingService service, IRealmData realmData)
            {
                instancingService = service;
                this.realmData = realmData;
            }

            // Called before executing the render pass.
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // Configure camera targets, if needed
            }

            // The actual execution of the pass
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if(instancingService == null || !realmData.Configured) return;

                CommandBuffer cmd = CommandBufferPool.Get(PROFILER_TAG);

                try
                {
                    instancingService.RenderIndirect();
                }
                finally
                {
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }

            // Called after executing the render pass, if needed
            public override void OnCameraCleanup(CommandBuffer cmd) { }

            /// Cleanup any allocated resources that were created during the execution of this render pass.
            public override void FrameCleanup(CommandBuffer cmd) { }
        }
    }
}
