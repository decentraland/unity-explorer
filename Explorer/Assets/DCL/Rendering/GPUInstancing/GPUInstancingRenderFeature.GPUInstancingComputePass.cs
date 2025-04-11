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
        public class GPUInstancingComputePass : ScriptableRenderPass
        {
            private const string profilerTag = "_DCL.GPUInstancingComputePass";
            private ReportData m_ReportData = new ("_DCL.GPUInstancingRenderPass", ReportHint.SessionStatic);
            private ProfilingSampler m_Sampler = new (profilerTag);

            private GPUInstancingService instancingService;
            private IRealmData realmData;
            private GPUInstancingSettings settings;

            private readonly ComputeShader FrustumCullingAndLODGenComputeShader;
            private static readonly string FrustumCullingAndLODGenComputeShader_KernelName = "CameraCullingAndLODCalculationKernel";
            private int FrustumCullingAndLODGenComputeShader_KernelIDs;
            private uint FrustumCullingAndLODGen_ThreadGroupSize_X = 1;
            private uint FrustumCullingAndLODGen_ThreadGroupSize_Y = 1;
            private uint FrustumCullingAndLODGen_ThreadGroupSize_Z = 1;

            private readonly ComputeShader IndirectBufferGenerationComputeShader;
            private static readonly string IndirectBufferGenerationComputeShader_KernelName = "ComputeLODBufferAccumulation";
            private int IndirectBufferGenerationComputeShader_KernelIDs;
            private uint IndirectBufferGeneration_ThreadGroupSize_X = 1;
            private uint IndirectBufferGeneration_ThreadGroupSize_Y = 1;
            private uint IndirectBufferGeneration_ThreadGroupSize_Z = 1;

            private readonly ComputeShader DrawArgsInstanceCountTransferComputeShader;
            private static readonly string DrawArgsInstanceCountTransferComputeShader_KernelName = "DrawArgsInstanceCountTransfer";
            private int DrawArgsInstanceCountTransferComputeShader_KernelIDs;
            private uint DrawArgsInstanceCountTransfer_ThreadGroupSize_X = 1;
            private uint DrawArgsInstanceCountTransfer_ThreadGroupSize_Y = 1;
            private uint DrawArgsInstanceCountTransfer_ThreadGroupSize_Z = 1;

            private static readonly int ComputeVar_PerInstance_LODLevels  = Shader.PropertyToID("PerInstance_LODLevels"); // RWStructuredBuffer<uint4>
            private static readonly int ComputeVar_PerInstanceData = Shader.PropertyToID("PerInstanceData"); // RWStructuredBuffer<PerInstance>
            private static readonly int ComputeVar_InstanceLookUpAndDither = Shader.PropertyToID("InstanceLookUpAndDitherBuffer"); // RWStructuredBuffer<uint2>
            private static readonly int ComputeVar_GroupDataBuffer = Shader.PropertyToID("GroupDataBuffer"); // RWStructuredBuffer<GroupData> size 196 align 4
            private static readonly int ComputeVar_arrLODCount = Shader.PropertyToID("arrLODCount");
            private static readonly int ComputeVar_IndirectDrawIndexedArgsBuffer = Shader.PropertyToID("IndirectDrawIndexedArgsBuffer");
            private static readonly int ComputeVar_nSubMeshCount = Shader.PropertyToID("nSubMeshCount");


            private readonly int[] arrLOD = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            private int ForwardPass = 0;

            public GPUInstancingComputePass(GPUInstancingService service, GPUInstancingSettings settings)
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
                if(instancingService == null) return;

                CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

                using (new ProfilingScope(cmd, m_Sampler))
                {
                    foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers) in instancingService.candidatesBuffersTable)
                    {

                        cmd.SetComputeBufferParam(settings.FrustumCullingAndLODGenComputeShader, instancingService.FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_GroupDataBuffer, buffers.GroupData);
                        cmd.SetComputeBufferParam(settings.FrustumCullingAndLODGenComputeShader, instancingService.FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstanceData, buffers.PerInstanceMatrices);
                        cmd.SetComputeBufferParam(settings.FrustumCullingAndLODGenComputeShader, instancingService.FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, buffers.LODLevels);
                        cmd.DispatchCompute(settings.FrustumCullingAndLODGenComputeShader,instancingService.FrustumCullingAndLODGenComputeShader_KernelIDs,  Mathf.CeilToInt((float)buffers.PerInstanceMatrices.count / (int)instancingService.FrustumCullingAndLODGen_ThreadGroupSize_X), 1, 1);

                        cmd.SetComputeBufferParam(settings.IndirectBufferGenerationComputeShader, instancingService.IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_GroupDataBuffer, buffers.GroupData);
                        cmd.SetComputeBufferParam(settings.IndirectBufferGenerationComputeShader, instancingService.IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_arrLODCount, buffers.ArrLODCount); // uint[8]
                        cmd.SetComputeBufferParam(settings.IndirectBufferGenerationComputeShader, instancingService.IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, buffers.LODLevels);
                        cmd.SetComputeBufferParam(settings.IndirectBufferGenerationComputeShader, instancingService.IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_InstanceLookUpAndDither, buffers.InstanceLookUpAndDither);
                        cmd.DispatchCompute(settings.IndirectBufferGenerationComputeShader, instancingService.IndirectBufferGenerationComputeShader_KernelIDs, Mathf.CeilToInt((float)buffers.PerInstanceMatrices.count / (int)instancingService.IndirectBufferGeneration_ThreadGroupSize_X), 1, 1);

                        cmd.SetComputeBufferParam(settings.DrawArgsInstanceCountTransferComputeShader, instancingService.DrawArgsInstanceCountTransferComputeShader_KernelIDs, ComputeVar_GroupDataBuffer, buffers.GroupData);
                        cmd.SetComputeBufferParam(settings.DrawArgsInstanceCountTransferComputeShader, instancingService.DrawArgsInstanceCountTransferComputeShader_KernelIDs, ComputeVar_arrLODCount, buffers.ArrLODCount);
                        cmd.SetComputeBufferParam(settings.DrawArgsInstanceCountTransferComputeShader, instancingService.DrawArgsInstanceCountTransferComputeShader_KernelIDs, ComputeVar_IndirectDrawIndexedArgsBuffer, buffers.DrawArgs);
                        cmd.SetComputeIntParam(settings.DrawArgsInstanceCountTransferComputeShader, ComputeVar_nSubMeshCount, candidate.LODGroup.CombinedLodsRenderers.Count);
                        cmd.DispatchCompute(settings.DrawArgsInstanceCountTransferComputeShader, instancingService.DrawArgsInstanceCountTransferComputeShader_KernelIDs, 1, 1, 1);
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
