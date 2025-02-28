using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.Roads.GPUInstancing.Playground;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Roads.GPUInstancing
{
    public class GPUInstancingService : IDisposable
    {
        private readonly GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings settings;

        struct GroupData
        {
            public Matrix4x4 lodSizes;
            public Matrix4x4 matCamera_MVP;
            public Vector3 vCameraPosition;
            public float fShadowDistance;
            public Vector3 vBoundsCenter;
            public float frustumOffset;
            public Vector3 vBoundsExtents;
            public float fCameraHalfAngle;
            public float fMaxDistance;
            public float minCullingDistance;
            public uint nInstBufferSize;
            public uint nMaxLOD_GB;
        };

        private readonly GraphicsBuffer.IndirectDrawIndexedArgs zeroDrawArgs = new()
        {
            indexCountPerInstance = 0,
            instanceCount = 0,
            startIndex = 0,
            baseVertexIndex = 0,
            startInstance = 0,
        };

        private const float STREET_MAX_HEIGHT = 10f;
        private static readonly Bounds RENDER_PARAMS_WORLD_BOUNDS =
            new (Vector3.zero, new Vector3(GenesisCityData.EXTENTS.x * ParcelMathHelper.PARCEL_SIZE, STREET_MAX_HEIGHT, GenesisCityData.EXTENTS.y * ParcelMathHelper.PARCEL_SIZE));

        private readonly Dictionary<GPUInstancingLODGroupWithBuffer, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

          int[] arrLOD = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

        public ComputeShader FrustumCullingAndLODGenComputeShader;
        private string FrustumCullingAndLODGenComputeShader_KernelName = "CameraCullingAndLODCalculationKernel";
        protected static int FrustumCullingAndLODGenComputeShader_KernelIDs;
        protected uint FrustumCullingAndLODGen_ThreadGroupSize_X = 1;
        protected uint FrustumCullingAndLODGen_ThreadGroupSize_Y = 1;
        protected uint FrustumCullingAndLODGen_ThreadGroupSize_Z = 1;

        public ComputeShader IndirectBufferGenerationComputeShader;
        private string IndirectBufferGenerationComputeShader_KernelName = "ComputeLODBufferAccumulation";
        protected static int IndirectBufferGenerationComputeShader_KernelIDs;
        protected uint IndirectBufferGeneration_ThreadGroupSize_X = 1;
        protected uint IndirectBufferGeneration_ThreadGroupSize_Y = 1;
        protected uint IndirectBufferGeneration_ThreadGroupSize_Z = 1;

        public ComputeShader DrawArgsInstanceCountTransferComputeShader;
        private string DrawArgsInstanceCountTransferComputeShader_KernelName = "DrawArgsInstanceCountTransfer";
        protected static int DrawArgsInstanceCountTransferComputeShader_KernelIDs;
        protected uint DrawArgsInstanceCountTransfer_ThreadGroupSize_X = 1;
        protected uint DrawArgsInstanceCountTransfer_ThreadGroupSize_Y = 1;
        protected uint DrawArgsInstanceCountTransfer_ThreadGroupSize_Z = 1;

        private static readonly int ComputeVar_PerInstance_LODLevels  = Shader.PropertyToID("PerInstance_LODLevels"); // RWStructuredBuffer<uint4>
        private static readonly int ComputeVar_PerInstanceData = Shader.PropertyToID("PerInstanceData"); // RWStructuredBuffer<PerInstance>
        private static readonly int ComputeVar_InstanceLookUpAndDither = Shader.PropertyToID("InstanceLookUpAndDitherBuffer"); // RWStructuredBuffer<uint2>
        private static readonly int ComputeVar_GroupDataBuffer = Shader.PropertyToID("GroupDataBuffer"); // RWStructuredBuffer<GroupData> size 196 align 4
        private static readonly int ComputeVar_arrLODCount = Shader.PropertyToID("arrLODCount");
        private static readonly int ComputeVar_IndirectDrawIndexedArgsBuffer = Shader.PropertyToID("IndirectDrawIndexedArgsBuffer");
        private static readonly int ComputeVar_nSubMeshCount = Shader.PropertyToID("nSubMeshCount");

        private Camera renderCamera;

        public LandscapeData LandscapeData { private get; set; }

        public GPUInstancingService(GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings settings)
        {
            this.settings = settings;

            FrustumCullingAndLODGenComputeShader = settings.FrustumCullingAndLODGenComputeShader;
            FrustumCullingAndLODGenComputeShader_KernelIDs = FrustumCullingAndLODGenComputeShader.FindKernel(FrustumCullingAndLODGenComputeShader_KernelName);
            FrustumCullingAndLODGenComputeShader.GetKernelThreadGroupSizes(FrustumCullingAndLODGenComputeShader_KernelIDs,
                out FrustumCullingAndLODGen_ThreadGroupSize_X,
                out FrustumCullingAndLODGen_ThreadGroupSize_Y,
                out FrustumCullingAndLODGen_ThreadGroupSize_Z);

            IndirectBufferGenerationComputeShader = settings.IndirectBufferGenerationComputeShader;
            IndirectBufferGenerationComputeShader_KernelIDs = IndirectBufferGenerationComputeShader.FindKernel(IndirectBufferGenerationComputeShader_KernelName);
            IndirectBufferGenerationComputeShader.GetKernelThreadGroupSizes(IndirectBufferGenerationComputeShader_KernelIDs,
                out IndirectBufferGeneration_ThreadGroupSize_X,
                out IndirectBufferGeneration_ThreadGroupSize_Y,
                out IndirectBufferGeneration_ThreadGroupSize_Z);

            DrawArgsInstanceCountTransferComputeShader = settings.DrawArgsInstanceCountTransferComputeShader;
            DrawArgsInstanceCountTransferComputeShader_KernelIDs = DrawArgsInstanceCountTransferComputeShader.FindKernel(DrawArgsInstanceCountTransferComputeShader_KernelName);
            DrawArgsInstanceCountTransferComputeShader.GetKernelThreadGroupSizes(DrawArgsInstanceCountTransferComputeShader_KernelIDs,
                out DrawArgsInstanceCountTransfer_ThreadGroupSize_X,
                out DrawArgsInstanceCountTransfer_ThreadGroupSize_Y,
                out DrawArgsInstanceCountTransfer_ThreadGroupSize_Z);
        }

        public void Dispose()
        {
            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
                buffers.Dispose();

            candidatesBuffersTable.Clear();
            instancingMaterials.Clear();
        }

        public void SetCamera(Camera camera)
        {
            renderCamera = camera;

            foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers _) in candidatesBuffersTable)
            foreach (var renderer in candidate.LODGroup.CombinedLodsRenderers)
                for (var i = 0; i < renderer.RenderParamsArray.Length; i++)
                    renderer.RenderParamsArray[i].camera = renderCamera;
        }

        public void RenderIndirect()
        {
            if(renderCamera == null) return;

            foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers) in candidatesBuffersTable)
                RenderCandidateIndirect(candidate, buffers, renderCamera);
        }

        private void RenderCandidateIndirect(GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers, Camera cam)
        {
            GroupData groupData = new GroupData
            {
                lodSizes = candidate.LODGroup.LODSizesMatrix,
                matCamera_MVP = cam.projectionMatrix * cam.worldToCameraMatrix,
                vCameraPosition = cam.transform.position,
                fShadowDistance = 0.0f,
                vBoundsCenter = candidate.LODGroup.Bounds.center,
                frustumOffset = 0.0f,
                vBoundsExtents = candidate.LODGroup.Bounds.extents,
                fCameraHalfAngle = 0.5f * cam.fieldOfView * Mathf.Deg2Rad,
                fMaxDistance = LandscapeData.DetailDistance * settings.MaxDistanceScaleFactor,
                minCullingDistance = cam.nearClipPlane,
                nInstBufferSize = (uint)buffers.PerInstanceMatrices.count,
                nMaxLOD_GB = (uint)candidate.LODGroup.LodsScreenSpaceSizes.Length,
            };

            buffers.GroupData.SetData(new[] { groupData }, 0, 0, 1);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_GroupDataBuffer, buffers.GroupData);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstanceData, buffers.PerInstanceMatrices);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, buffers.LODLevels);
            FrustumCullingAndLODGenComputeShader.Dispatch(FrustumCullingAndLODGenComputeShader_KernelIDs, Mathf.CeilToInt((float)buffers.PerInstanceMatrices.count / (int)FrustumCullingAndLODGen_ThreadGroupSize_X), 1, 1);

            buffers.ArrLODCount.SetData(arrLOD, 0, 0, 8);

            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_GroupDataBuffer, buffers.GroupData);
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_arrLODCount, buffers.ArrLODCount); // uint[8]
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, buffers.LODLevels);
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_InstanceLookUpAndDither, buffers.InstanceLookUpAndDither);
            IndirectBufferGenerationComputeShader.Dispatch(IndirectBufferGenerationComputeShader_KernelIDs, Mathf.CeilToInt((float)buffers.PerInstanceMatrices.count / (int)IndirectBufferGeneration_ThreadGroupSize_X), 1, 1);

            // Zero-out draw args - will be calculated by compute shaders
            for (var i = 0; i < buffers.DrawArgsCommandData.Length; i++)
                buffers.DrawArgsCommandData[i].instanceCount = 0;

            buffers.DrawArgs.SetData(buffers.DrawArgsCommandData);

            DrawArgsInstanceCountTransferComputeShader.SetBuffer(DrawArgsInstanceCountTransferComputeShader_KernelIDs, ComputeVar_GroupDataBuffer, buffers.GroupData);
            DrawArgsInstanceCountTransferComputeShader.SetBuffer(DrawArgsInstanceCountTransferComputeShader_KernelIDs, ComputeVar_arrLODCount, buffers.ArrLODCount);
            DrawArgsInstanceCountTransferComputeShader.SetBuffer(DrawArgsInstanceCountTransferComputeShader_KernelIDs, ComputeVar_IndirectDrawIndexedArgsBuffer, buffers.DrawArgs);
            DrawArgsInstanceCountTransferComputeShader.SetInt(ComputeVar_nSubMeshCount, candidate.LODGroup.CombinedLodsRenderers.Count);
            DrawArgsInstanceCountTransferComputeShader.Dispatch(DrawArgsInstanceCountTransferComputeShader_KernelIDs, 1, 1, 1);

            for (var i = 0; i < candidate.LODGroup.CombinedLodsRenderers.Count; i++)
            {
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[i];
                int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                ReportHub.Log(ReportCategory.GPU_INSTANCING, $"{Time.frameCount}: renedering {combinedLodRenderer.CombinedMesh.name} with material {combinedLodRenderer.SharedMaterial.name}");
                Graphics.RenderMeshIndirect(combinedLodRenderer.RenderParamsArray[0], combinedLodRenderer.CombinedMesh, buffers.DrawArgs, commandCount: lodCount, startCommand: i*lodCount);
            }
        }

        public void AddToIndirect(List<GPUInstancingLODGroupWithBuffer> candidates)
        {
            foreach (GPUInstancingLODGroupWithBuffer candidate in candidates)
                AddToIndirect(candidate);
        }

        private void AddToIndirect(GPUInstancingLODGroupWithBuffer candidate)
        {
            if (!candidatesBuffersTable.TryGetValue(candidate, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(candidate, buffers);
            }

            int _nInstanceCount = candidate.InstancesBuffer.Count;
            int _nLODCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

            buffers.LODLevels = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, sizeof(uint) * 4);
            buffers.InstanceLookUpAndDither = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount * _nLODCount, sizeof(uint) * 4);
            buffers.GroupData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 1, 192);
            buffers.ArrLODCount = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 8, sizeof(uint));

            // TODO : set flag to Lock
            buffers.PerInstanceMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.PerInstanceMatrices.SetData(candidate.InstancesBuffer, 0, 0, _nInstanceCount);

            int combinedRenderersCount = candidate.LODGroup.CombinedLodsRenderers.Count;
            buffers.DrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: combinedRenderersCount * _nLODCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[combinedRenderersCount * _nLODCount];

            for (var combinedRendererId = 0; combinedRendererId < combinedRenderersCount; combinedRendererId++)
            {
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[combinedRendererId];
                Mesh combinedMesh = combinedLodRenderer.CombinedMesh;

                if (combinedMesh == null)
                {
                    Debug.LogWarning($"{candidate.Name} has combined lod renderer equal to null for material {combinedLodRenderer.SharedMaterial.name}", candidate.LODGroup.Reference.gameObject);
                    continue;
                }

                for (var lodLevel = 0; lodLevel < _nLODCount; lodLevel++)
                {
                    if (lodLevel < combinedMesh.subMeshCount)
                    {
                        buffers.DrawArgsCommandData[lodLevel + (combinedRendererId *_nLODCount)].instanceCount = 0;
                        buffers.DrawArgsCommandData[lodLevel + (combinedRendererId *_nLODCount)].indexCountPerInstance = combinedMesh.GetIndexCount(lodLevel);
                        buffers.DrawArgsCommandData[lodLevel + (combinedRendererId *_nLODCount)].startIndex = combinedMesh.GetIndexStart(lodLevel);
                        buffers.DrawArgsCommandData[lodLevel + (combinedRendererId *_nLODCount)].baseVertexIndex = combinedMesh.GetBaseVertex(lodLevel);
                        buffers.DrawArgsCommandData[lodLevel + (combinedRendererId *_nLODCount)].startInstance = (uint)lodLevel * (uint)candidate.InstancesBuffer.Count;
                    }
                    else
                    {
                        buffers.DrawArgsCommandData[lodLevel + (combinedRendererId *_nLODCount)] = zeroDrawArgs;
                    }
                }

                buffers.DrawArgs.SetData(buffers.DrawArgsCommandData, 0, 0, count: combinedRenderersCount * _nLODCount);

                Debug.Log($"Inizializing render params for {candidate.Name} with material {combinedLodRenderer.SharedMaterial.name} ", combinedLodRenderer.SharedMaterial);

                combinedLodRenderer.InitializeRenderParams(instancingMaterials);
                ref RenderParams rparams = ref combinedLodRenderer.RenderParamsArray[0];
                rparams.camera = renderCamera;
                rparams.worldBounds = RENDER_PARAMS_WORLD_BOUNDS;
                rparams.matProps = new MaterialPropertyBlock();
                rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.PerInstanceMatrices);
                rparams.matProps.SetBuffer("_PerInstanceLookUpAndDitherBuffer", buffers.InstanceLookUpAndDither);
            }
        }

        public void Remove(List<GPUInstancingLODGroupWithBuffer> candidates)
        {
            foreach (GPUInstancingLODGroupWithBuffer candidate in candidates)
                Remove(candidate);
        }

        private void Remove(GPUInstancingLODGroupWithBuffer lodGroup)
        {
            if (lodGroup == null) return;

            if (candidatesBuffersTable.Remove(lodGroup, out GPUInstancingBuffers buffers))
                buffers.Dispose();
        }

        private void RenderCandidateIndirectViaCommandBuffer(CommandBuffer cmd, GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers)
        {
            for (var i = 0; i < candidate.LODGroup.CombinedLodsRenderers.Count; i++)
            {
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[i];
                int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                cmd.DrawMeshInstancedIndirect(
                    combinedLodRenderer.CombinedMesh,
                    submeshIndex: 0, // TODO (Vit): we need to combine mesh without submeshes for our LOD to work here
                    material: combinedLodRenderer.SharedMaterial,
                    shaderPass: -1, // which pass of the shader to use, or -1 which renders all passes.
                    bufferWithArgs: buffers.DrawArgs,
                    argsOffset: lodCount
                    // ,properties: new MaterialPropertyBlock()
                );
            }
        }
    }

    public class GPUInstancingBuffers : IDisposable
    {
        public GraphicsBuffer LODLevels;
        public GraphicsBuffer InstanceLookUpAndDither;
        public GraphicsBuffer PerInstanceMatrices;
        public GraphicsBuffer GroupData;
        public GraphicsBuffer ArrLODCount;

        public GraphicsBuffer DrawArgs;
        public GraphicsBuffer.IndirectDrawIndexedArgs[] DrawArgsCommandData;

        public void Dispose()
        {
            LODLevels?.Dispose();
            LODLevels = null;

            InstanceLookUpAndDither?.Dispose();
            InstanceLookUpAndDither = null;

            PerInstanceMatrices?.Dispose();
            PerInstanceMatrices = null;

            GroupData?.Dispose();
            GroupData = null;

            ArrLODCount?.Dispose();
            ArrLODCount = null;

            DrawArgs?.Dispose();
            DrawArgs = null;
        }
    }
}
