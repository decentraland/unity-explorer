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

        private const float STREET_MAX_HEIGHT = 10f;
        private static readonly Bounds RENDER_PARAMS_WORLD_BOUNDS =
            new (Vector3.zero, new Vector3(GenesisCityData.EXTENTS.x * ParcelMathHelper.PARCEL_SIZE, STREET_MAX_HEIGHT, GenesisCityData.EXTENTS.y * ParcelMathHelper.PARCEL_SIZE));

        private readonly Dictionary<GPUInstancingLODGroupWithBuffer, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        private ComputeShader FrustumCullingAndLODGenComputeShader;
        private string FrustumCullingAndLODGenComputeShader_KernelName = "CameraCullingAndLODCalculationKernel";
        protected static int FrustumCullingAndLODGenComputeShader_KernelIDs;
        // private static readonly int ComputeVar_nInstBufferSize = Shader.PropertyToID("nInstBufferSize"); // uint
        private static readonly int ComputeVar_nMaxLOD = Shader.PropertyToID("nMaxLOD_GB"); // uint
        private static readonly int ComputeVar_PerInstance_LODLevels  = Shader.PropertyToID("PerInstance_LODLevels"); // RWStructuredBuffer<uint4>
        private static readonly int ComputeVar_PerInstanceData = Shader.PropertyToID("PerInstanceData"); // RWStructuredBuffer<PerInstance>
        private static readonly int ComputeVar_InstanceLookUpAndDither = Shader.PropertyToID("InstanceLookUpAndDither"); // RWStructuredBuffer<uint2>
        private static readonly int ComputeVar_GroupDataBuffer = Shader.PropertyToID("GroupDataBuffer"); // RWStructuredBuffer<GroupData> size 196 align 4
        private static readonly int ComputeVar_nPerInstanceBufferSize = Shader.PropertyToID("nPerInstanceBufferSize"); // uint
        private static readonly int ComputeVar_vBoundsCenter = Shader.PropertyToID("vBoundsCenter"); // float3
        private static readonly int ComputeVar_vBoundsExtents = Shader.PropertyToID("vBoundsExtents"); // float3
        private static readonly int ComputeVar_matCamera_MVP = Shader.PropertyToID("matCamera_MVP"); // float4x4
        private static readonly int ComputeVar_vCameraPosition = Shader.PropertyToID("vCameraPosition"); // float3
        private static readonly int ComputeVar_fCameraHalfAngle = Shader.PropertyToID("fCameraHalfAngle"); // float
        private static readonly int ComputeVar_minCullingDistance = Shader.PropertyToID("minCullingDistance"); // float
        private static readonly int ComputeVar_fMaxDistance = Shader.PropertyToID("fMaxDistance"); // float
        private static readonly int ComputeVar_isFrustumCulling = Shader.PropertyToID("isFrustumCulling");
        private static readonly int ComputeVar_frustumOffset = Shader.PropertyToID("frustumOffset"); // float
        private static readonly int ComputeVar_isOcclusionCulling = Shader.PropertyToID("isOcclusionCulling");
        private static readonly int ComputeVar_occlusionOffset = Shader.PropertyToID("occlusionOffset"); // float
        private static readonly int ComputeVar_occlusionAccuracy = Shader.PropertyToID("occlusionAccuracy"); // uint
        private static readonly int ComputeVar_hiZTxtrSize = Shader.PropertyToID("hiZTxtrSize"); // float4
        private static readonly int ComputeVar_hiZMap = Shader.PropertyToID("hiZMap"); // Texture2D<float4>
        private static readonly int ComputeVar_sampler_hiZMap = Shader.PropertyToID("sampler_hiZMap"); // SamplerState
        private static readonly int ComputeVar_cullShadows = Shader.PropertyToID("cullShadows"); // bool
        private static readonly int ComputeVar_fShadowDistance = Shader.PropertyToID("fShadowDistance"); // float
        private static readonly int ComputeVar_shadowLODMap = Shader.PropertyToID("shadowLODMap"); // float4x4
        private static readonly int ComputeVar_lodSizes = Shader.PropertyToID("lodSizes"); // float4x4

        public ComputeShader IndirectBufferGenerationComputeShader;

        private Camera camera;
        private Camera cam => camera ??= Camera.main;

        private string IndirectBufferGenerationComputeShader_KernelName = "ComputeLODBufferAccumulation";
        protected static int IndirectBufferGenerationComputeShader_KernelIDs;
        //private static readonly int ComputeVar_nInstBufferSize = Shader.PropertyToID("nInstBufferSize");
        private static readonly int ComputeVar_nInstBufferSize = Shader.PropertyToID("nInstBufferSize"); // uint
        private static readonly int ComputeVar_nLODCount = Shader.PropertyToID("nLODCount");
        private static readonly int ComputeVar_arrLODCount = Shader.PropertyToID("arrLODCount");
        //private static readonly int ComputeVar_PerInstance_LODLevels = Shader.PropertyToID("PerInstance_LODLevels");
        private static readonly int ComputeVar_IndirectDrawIndexedArgsBuffer = Shader.PropertyToID("IndirectDrawIndexedArgsBuffer");
        //private static readonly int ComputeVar_InstanceLookUpAndDither = Shader.PropertyToID("InstanceLookUpAndDither");

        public GPUInstancingService(GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings renderFeatureSettings)
            : this(renderFeatureSettings.FrustumCullingAndLODGenComputeShader, renderFeatureSettings.IndirectBufferGenerationComputeShader)
        {
        }

        public GPUInstancingService(ComputeShader frustumCullingAndLODGenComputeShader, ComputeShader indirectBufferGenerationComputeShader)
        {
            FrustumCullingAndLODGenComputeShader = frustumCullingAndLODGenComputeShader;
            IndirectBufferGenerationComputeShader = indirectBufferGenerationComputeShader;
            FrustumCullingAndLODGenComputeShader_KernelIDs = FrustumCullingAndLODGenComputeShader.FindKernel(FrustumCullingAndLODGenComputeShader_KernelName);
            IndirectBufferGenerationComputeShader_KernelIDs = IndirectBufferGenerationComputeShader.FindKernel(IndirectBufferGenerationComputeShader_KernelName);
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
            Debug.Log($"VVV Service Render {candidatesBuffersTable.Count}");

            foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers) in candidatesBuffersTable)
                RenderCandidateIndirect(candidate, buffers);
        }

        private void RenderCandidateIndirect(GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers)
        {
             float halfAngle = 0.5f * cam.fieldOfView * Mathf.Deg2Rad;
             Matrix4x4 camMVP = cam.projectionMatrix * cam.worldToCameraMatrix;
             FrustumCullingAndLODGenComputeShader.SetMatrix(ComputeVar_lodSizes, candidate.LODGroup.LODSizesMatrix); // float4x4
             FrustumCullingAndLODGenComputeShader.SetVector(ComputeVar_vBoundsCenter,candidate.LODGroup.Bounds.center); // float3
             FrustumCullingAndLODGenComputeShader.SetVector(ComputeVar_vBoundsExtents,candidate.LODGroup.Bounds.extents); // float3
             //FrustumCullingAndLODGenComputeShader.SetInt(ComputeVar_nMaxLOD,candidate.LODGroup.LodsScreenSpaceSizes.Length); // uint
             FrustumCullingAndLODGenComputeShader.SetInt(ComputeVar_nMaxLOD,(int)5341);
             FrustumCullingAndLODGenComputeShader.SetInt(ComputeVar_nInstBufferSize, buffers.PerInstanceMatrices.count); // uint
             FrustumCullingAndLODGenComputeShader.SetMatrix(ComputeVar_matCamera_MVP, camMVP); // float4x4
             FrustumCullingAndLODGenComputeShader.SetVector( ComputeVar_vCameraPosition, cam.transform.position); // float3
             FrustumCullingAndLODGenComputeShader.SetFloat(ComputeVar_fCameraHalfAngle, halfAngle); // float
             FrustumCullingAndLODGenComputeShader.SetFloat(ComputeVar_minCullingDistance, cam.nearClipPlane); // float
             FrustumCullingAndLODGenComputeShader.SetFloat(ComputeVar_fMaxDistance, cam.farClipPlane); // float
             FrustumCullingAndLODGenComputeShader.SetBool(ComputeVar_isFrustumCulling, true);
             FrustumCullingAndLODGenComputeShader.SetFloat(ComputeVar_frustumOffset, 0.0f); // float

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
             FrustumCullingAndLODGenComputeShader.Dispatch(FrustumCullingAndLODGenComputeShader_KernelIDs, buffers.PerInstanceMatrices.count, 1, 1);

            /////
            /////
             IndirectBufferGenerationComputeShader.SetInt(ComputeVar_nInstBufferSize, buffers.PerInstanceMatrices.count); // uint
             IndirectBufferGenerationComputeShader.SetInt(ComputeVar_nLODCount, candidate.LODGroup.LodsScreenSpaceSizes.Length); // uint
             GraphicsBuffer arrLODCount = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 8, sizeof(uint));
             var arrLOD = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
             arrLODCount.SetData(arrLOD, 0, 0, 8);

             IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_arrLODCount, arrLODCount); // uint[8]
             IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_IndirectDrawIndexedArgsBuffer, buffers.DrawArgs[0]);
             IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, buffers.LODLevels);
             IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_InstanceLookUpAndDither, buffers.InstanceLookUpAndDither);
             IndirectBufferGenerationComputeShader.Dispatch(IndirectBufferGenerationComputeShader_KernelIDs, buffers.PerInstanceMatrices.count, 1, 1);

            for (var i = 0; i < candidate.LODGroup.CombinedLodsRenderers.Count; i++)
            {
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[i];
                int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                Graphics.RenderMeshIndirect(combinedLodRenderer.RenderParamsArray[0], combinedLodRenderer.CombinedMesh, buffers.DrawArgs[i], commandCount: lodCount);
            }
        }

        private void RenderCandidateIndirect(CommandBuffer cmd, GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers)
        {
            for (var i = 0; i < candidate.LODGroup.CombinedLodsRenderers.Count; i++)
            {
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[i];
                int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                cmd.DrawMeshInstancedIndirect(
                    combinedLodRenderer.CombinedMesh,
                    submeshIndex: 0, // TODO (Vit): we need to combine mesh without submeshes for our LOD to work
                    material: combinedLodRenderer.SharedMaterial,
                    shaderPass: -1, // which pass of the shader to use, or -1 which renders all passes.
                    bufferWithArgs: buffers.DrawArgs[i],
                    argsOffset: lodCount
                    // ,properties: new MaterialPropertyBlock()
                );
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

            // TODO : set flag to Lock
            buffers.PerInstanceMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.PerInstanceMatrices.SetData(candidate.InstancesBuffer, 0, 0, _nInstanceCount);

            buffers.DrawArgs = new List<GraphicsBuffer>();
            buffers.DrawArgsCommandData = new List<GraphicsBuffer.IndirectDrawIndexedArgs[]>();
            foreach (var combinedLodRenderer in candidate.LODGroup.CombinedLodsRenderers)
            {
                Mesh combinedMesh = combinedLodRenderer.CombinedMesh;

                if (combinedMesh == null)
                {
                    Debug.LogWarning($"{candidate.Name} has combined lod renderer equal to null for material {combinedLodRenderer.SharedMaterial.name}", candidate.LODGroup.Reference.gameObject);
                    continue;
                }

                var drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, _nLODCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                var drawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[_nLODCount];


                for (var lodLevel = 0; lodLevel < _nLODCount; lodLevel++)
                {
                    if (lodLevel > combinedMesh.subMeshCount - 1)
                    {
                        drawArgsCommandData[lodLevel].instanceCount = 0;
                        continue;
                    }

                    drawArgsCommandData[lodLevel].indexCountPerInstance = combinedMesh.GetIndexCount(lodLevel);
                    drawArgsCommandData[lodLevel].instanceCount = lodLevel == 0 ? (uint)candidate.InstancesBuffer.Count : 0;
                    drawArgsCommandData[lodLevel].startIndex = combinedMesh.GetIndexStart(lodLevel);
                    drawArgsCommandData[lodLevel].baseVertexIndex = combinedMesh.GetBaseVertex(lodLevel);
                    drawArgsCommandData[lodLevel].startInstance = (uint)lodLevel * (uint)candidate.InstancesBuffer.Count;
                }

                drawArgsBuffer.SetData(drawArgsCommandData, 0, 0, count: _nLODCount);

                combinedLodRenderer.InitializeRenderParams(instancingMaterials);
                ref RenderParams rparams = ref combinedLodRenderer.RenderParamsArray[0];

                rparams.camera = camera;
                rparams.worldBounds = RENDER_PARAMS_WORLD_BOUNDS;
                rparams.matProps = new MaterialPropertyBlock();
                rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.PerInstanceMatrices);
                rparams.matProps.SetBuffer("_PerInstanceLookUpAndDitherBuffer", buffers.InstanceLookUpAndDither);

                buffers.DrawArgs.Add(drawArgsBuffer);
                buffers.DrawArgsCommandData.Add(drawArgsCommandData);
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
    }

    public class GPUInstancingBuffers : IDisposable
    {
        public GraphicsBuffer LODLevels;
        public GraphicsBuffer InstanceLookUpAndDither;
        public GraphicsBuffer PerInstanceMatrices;
        public GraphicsBuffer GroupData;

        public List<GraphicsBuffer> DrawArgs;
        public List<GraphicsBuffer.IndirectDrawIndexedArgs[]> DrawArgsCommandData;

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

            foreach (GraphicsBuffer drawArg in DrawArgs)
                drawArg?.Dispose();

            DrawArgs.Clear();
            DrawArgs = null;
        }
    }
}
