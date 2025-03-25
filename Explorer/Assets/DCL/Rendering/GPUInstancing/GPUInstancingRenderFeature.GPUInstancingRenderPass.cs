using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.Rendering.GPUInstancing.InstancingData;
using ECS;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.GPUInstancing
{
    public partial class GPUInstancingRenderFeature
    {
        public class GPUInstancingRenderPass : ScriptableRenderPass
        {
            private const string profilerTag = "_DCL.GPUInstancingRenderPass";
            private ReportData m_ReportData = new ("_DCL.GPUInstancingRenderPass", ReportHint.SessionStatic);
            private ProfilingSampler m_Sampler = new (profilerTag);

            private GPUInstancingService instancingService;
            private IRealmData realmData;
            private GPUInstancingSettings settings;

            private static readonly int MAT_PER_INSTANCE_BUFFER = Shader.PropertyToID("_PerInstanceBuffer");
            private static readonly int PER_INSTANCE_LOOK_UP_AND_DITHER_BUFFER = Shader.PropertyToID("_PerInstanceLookUpAndDitherBuffer");

            private readonly int[] arrLOD = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

            public GPUInstancingRenderPass(GPUInstancingService service, GPUInstancingSettings settings)
            {
                this.instancingService = service;
                this.settings = settings;
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
                if(instancingService == null || !realmData.Configured)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

                using (new ProfilingScope(cmd, m_Sampler))
                {
                    foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers) in instancingService.candidatesBuffersTable)
                    {
                        for (var i = 0; i < candidate.LODGroup.CombinedLodsRenderers.Count; i++)
                        {
                            CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[i];
                            int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                            cmd.SetGlobalBuffer(MAT_PER_INSTANCE_BUFFER, buffers.PerInstanceMatrices);
                            cmd.SetGlobalBuffer(PER_INSTANCE_LOOK_UP_AND_DITHER_BUFFER, buffers.InstanceLookUpAndDither);
                            cmd.SetGlobalBuffer("unity_IndirectDrawArgs", buffers.DrawArgs);
                            int ForwardPass = combinedLodRenderer.RenderParamsArray.material.FindPass("ForwardLit");

                            for (int j = 0; j < combinedLodRenderer.CombinedMesh.subMeshCount; j++)
                            {
                                cmd.DrawMeshInstancedIndirect(combinedLodRenderer.CombinedMesh, j, combinedLodRenderer.RenderParamsArray.material, ForwardPass, buffers.DrawArgs, ((i * lodCount) + j) * GraphicsBuffer.IndirectDrawIndexedArgs.size);
                            }
                        }
                    }
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            // Called after executing the render pass, if needed
            public override void OnCameraCleanup(CommandBuffer cmd)
            {

            }

            /// Cleanup any allocated resources that were created during the execution of this render pass.
            public override void FrameCleanup(CommandBuffer cmd)
            {

            }
        }
    }
}
