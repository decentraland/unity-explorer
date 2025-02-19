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
            startInstance = 0
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

        private static readonly int ComputeVar_PerInstance_LODLevels  = Shader.PropertyToID("PerInstance_LODLevels"); // RWStructuredBuffer<uint4>
        private static readonly int ComputeVar_PerInstanceData = Shader.PropertyToID("PerInstanceData"); // RWStructuredBuffer<PerInstance>
        private static readonly int ComputeVar_InstanceLookUpAndDither = Shader.PropertyToID("InstanceLookUpAndDither"); // RWStructuredBuffer<uint2>
        private static readonly int ComputeVar_GroupDataBuffer = Shader.PropertyToID("GroupDataBuffer"); // RWStructuredBuffer<GroupData> size 196 align 4
        private static readonly int ComputeVar_arrLODCount = Shader.PropertyToID("arrLODCount");
        private static readonly int ComputeVar_IndirectDrawIndexedArgsBuffer = Shader.PropertyToID("IndirectDrawIndexedArgsBuffer");

        public GPUInstancingService(GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings renderFeatureSettings)
            : this(renderFeatureSettings.FrustumCullingAndLODGenComputeShader, renderFeatureSettings.IndirectBufferGenerationComputeShader)
        {
        }

        public GPUInstancingService(ComputeShader frustumCullingAndLODGenComputeShader, ComputeShader indirectBufferGenerationComputeShader)
        {
            FrustumCullingAndLODGenComputeShader = frustumCullingAndLODGenComputeShader;
            FrustumCullingAndLODGenComputeShader_KernelIDs = FrustumCullingAndLODGenComputeShader.FindKernel(FrustumCullingAndLODGenComputeShader_KernelName);
            FrustumCullingAndLODGenComputeShader.GetKernelThreadGroupSizes(FrustumCullingAndLODGenComputeShader_KernelIDs,
                out FrustumCullingAndLODGen_ThreadGroupSize_X,
                out FrustumCullingAndLODGen_ThreadGroupSize_Y,
                out FrustumCullingAndLODGen_ThreadGroupSize_Z);

            IndirectBufferGenerationComputeShader = indirectBufferGenerationComputeShader;
            IndirectBufferGenerationComputeShader_KernelIDs = IndirectBufferGenerationComputeShader.FindKernel(IndirectBufferGenerationComputeShader_KernelName);
            IndirectBufferGenerationComputeShader.GetKernelThreadGroupSizes(IndirectBufferGenerationComputeShader_KernelIDs,
                out IndirectBufferGeneration_ThreadGroupSize_X,
                out IndirectBufferGeneration_ThreadGroupSize_Y,
                out IndirectBufferGeneration_ThreadGroupSize_Z);
        }

        public void Dispose()
        {
            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
                buffers.Dispose();

            candidatesBuffersTable.Clear();
            instancingMaterials.Clear();
        }

        public void RenderIndirect()
        {
            Camera cam = Camera.main;

            foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers) in candidatesBuffersTable)
                RenderCandidateIndirect(candidate, buffers, cam);
        }

        private void RenderCandidateIndirect(GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers, Camera cam)
        {
            float halfAngle = 0.5f * cam.fieldOfView * Mathf.Deg2Rad;
            Matrix4x4 camMVP = cam.projectionMatrix * cam.worldToCameraMatrix;

            GroupData groupData = new GroupData();
            groupData.lodSizes = candidate.LODGroup.LODSizesMatrix;
            groupData.matCamera_MVP = camMVP;
            groupData.vCameraPosition = cam.transform.position;
            groupData.fShadowDistance = 0.0f;
            groupData.vBoundsCenter = candidate.LODGroup.Bounds.center;
            groupData.frustumOffset = 0.0f;
            groupData.vBoundsExtents = candidate.LODGroup.Bounds.extents;
            groupData.fCameraHalfAngle = halfAngle;
            groupData.fMaxDistance = cam.farClipPlane;
            groupData.minCullingDistance = cam.nearClipPlane;
            groupData.nInstBufferSize = (uint)buffers.PerInstanceMatrices.count;
            groupData.nMaxLOD_GB = (uint)candidate.LODGroup.LodsScreenSpaceSizes.Length;

            List<GroupData> groupDataList = new List<GroupData>();
            groupDataList.Add(groupData);
            buffers.GroupData.SetData(groupDataList, 0, 0, 1);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_GroupDataBuffer, buffers.GroupData);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstanceData, buffers.PerInstanceMatrices);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, buffers.LODLevels);
            FrustumCullingAndLODGenComputeShader.Dispatch(FrustumCullingAndLODGenComputeShader_KernelIDs, Mathf.CeilToInt((float)buffers.PerInstanceMatrices.count / (int)FrustumCullingAndLODGen_ThreadGroupSize_X), 1, 1);

            buffers.ArrLODCount.SetData(arrLOD, 0, 0, 8);

            // Zero-out draw args - will be calculated by compute shaders
            var commands_0 = buffers.DrawArgsCommandDataPerCombinedRenderer[0];
            for (int j = 0; j < commands_0.Length; j++)
                commands_0[j].instanceCount = 0;

            buffers.DrawArgsPerCombinedRenderer[0].SetData(commands_0);

            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_GroupDataBuffer, buffers.GroupData);
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_arrLODCount, buffers.ArrLODCount); // uint[8]
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_IndirectDrawIndexedArgsBuffer, buffers.DrawArgsPerCombinedRenderer[0]);
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, buffers.LODLevels);
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_InstanceLookUpAndDither, buffers.InstanceLookUpAndDither);
            IndirectBufferGenerationComputeShader.Dispatch(IndirectBufferGenerationComputeShader_KernelIDs, Mathf.CeilToInt((float)buffers.PerInstanceMatrices.count / (int)IndirectBufferGeneration_ThreadGroupSize_X), 1, 1);

            for (var i = 1; i < buffers.DrawArgsCommandDataPerCombinedRenderer.Count; i++)
            {
                var array = buffers.DrawArgsCommandDataPerCombinedRenderer[i];
                for (int j = 0; j < array.Length; j++)
                    array[j].instanceCount = commands_0[j].instanceCount;

                buffers.DrawArgsPerCombinedRenderer[i].SetData(array);
            }

            for (var i = 0; i < candidate.LODGroup.CombinedLodsRenderers.Count; i++)
            {
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[i];
                int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                var RenderParams = combinedLodRenderer.RenderParamsArray[0];
                RenderParams.camera = cam;

                Graphics.RenderMeshIndirect(RenderParams, combinedLodRenderer.CombinedMesh, buffers.DrawArgsPerCombinedRenderer[i], commandCount: lodCount);
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
            buffers.InstanceLookUpAndDither = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount * _nLODCount, sizeof(uint) * 2);
            buffers.GroupData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 1, 192);
            buffers.ArrLODCount = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 8, sizeof(uint));

            // TODO : set flag to Lock
            buffers.PerInstanceMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.PerInstanceMatrices.SetData(candidate.InstancesBuffer, 0, 0, _nInstanceCount);

            buffers.DrawArgsPerCombinedRenderer = new List<GraphicsBuffer>();
            buffers.DrawArgsCommandDataPerCombinedRenderer = new List<GraphicsBuffer.IndirectDrawIndexedArgs[]>();

            var cam = Camera.main;
            foreach (var combinedLodRenderer in candidate.LODGroup.CombinedLodsRenderers)
            {
                Mesh combinedMesh = combinedLodRenderer.CombinedMesh;

                if (combinedMesh == null)
                {
                    Debug.LogWarning($"{candidate.Name} has combined lod renderer equal to null for material {combinedLodRenderer.SharedMaterial.name}", candidate.LODGroup.Reference.gameObject);
                    continue;
                }

                var lodLevelsDrawArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count: _nLODCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                var drawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[_nLODCount];

                for (var lodLevel = 0; lodLevel < _nLODCount; lodLevel++)
                {
                    if (lodLevel > combinedMesh.subMeshCount - 1)
                    {
                        drawArgsCommandData[lodLevel].instanceCount = 0;
                        continue;
                    }

                    drawArgsCommandData[lodLevel].indexCountPerInstance = combinedMesh.GetIndexCount(lodLevel);
                    drawArgsCommandData[lodLevel].instanceCount = 0;
                    drawArgsCommandData[lodLevel].startIndex = combinedMesh.GetIndexStart(lodLevel);
                    drawArgsCommandData[lodLevel].baseVertexIndex = combinedMesh.GetBaseVertex(lodLevel);
                    drawArgsCommandData[lodLevel].startInstance = (uint)lodLevel * (uint)candidate.InstancesBuffer.Count;
                }

                lodLevelsDrawArgs.SetData(drawArgsCommandData, 0, 0, count: _nLODCount);

                combinedLodRenderer.InitializeRenderParams(instancingMaterials);
                ref RenderParams rparams = ref combinedLodRenderer.RenderParamsArray[0];
                rparams.camera = cam;
                rparams.worldBounds = RENDER_PARAMS_WORLD_BOUNDS;
                rparams.matProps = new MaterialPropertyBlock();
                rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.PerInstanceMatrices);
                rparams.matProps.SetBuffer("_PerInstanceLookUpAndDitherBuffer", buffers.InstanceLookUpAndDither);

                buffers.DrawArgsPerCombinedRenderer.Add(lodLevelsDrawArgs);
                buffers.DrawArgsCommandDataPerCombinedRenderer.Add(drawArgsCommandData);
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
                    bufferWithArgs: buffers.DrawArgsPerCombinedRenderer[i],
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

        public List<GraphicsBuffer> DrawArgsPerCombinedRenderer;
        public List<GraphicsBuffer.IndirectDrawIndexedArgs[]> DrawArgsCommandDataPerCombinedRenderer;

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

            foreach (GraphicsBuffer drawArg in DrawArgsPerCombinedRenderer)
                drawArg?.Dispose();

            DrawArgsPerCombinedRenderer.Clear();
            DrawArgsPerCombinedRenderer = null;
        }
    }
}
