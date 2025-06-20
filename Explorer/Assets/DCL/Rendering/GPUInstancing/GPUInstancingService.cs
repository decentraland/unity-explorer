using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.Rendering.GPUInstancing.InstancingData;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Rendering.GPUInstancing
{
    public class GPUInstancingService : IDisposable
    {
        private readonly GroupData[] groupDataArray = new GroupData[1];

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

            public void Set(Camera cam, float maxDistance, GPUInstancingLODGroupWithBuffer candidate, uint instancesCount)
            {
                lodSizes = candidate.LODGroup.LODSizesMatrix;
                matCamera_MVP = cam.projectionMatrix * cam.worldToCameraMatrix;
                vCameraPosition = cam.transform.position;
                fShadowDistance = 0.0f;
                vBoundsCenter = candidate.LODGroup.Bounds.center;
                frustumOffset = 0.0f;
                vBoundsExtents = candidate.LODGroup.Bounds.extents;
                fCameraHalfAngle = 0.5f * cam.fieldOfView * Mathf.Deg2Rad;
                fMaxDistance = math.min(maxDistance, cam.farClipPlane);
                minCullingDistance = cam.nearClipPlane;
                nInstBufferSize = instancesCount;
                nMaxLOD_GB = (uint)candidate.LODGroup.LodsScreenSpaceSizes.Length;
            }
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
        private readonly GPUInstancingMaterialsCache instancingMaterials = new ();

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
        private static readonly int MAT_PER_INSTANCE_BUFFER = Shader.PropertyToID("_PerInstanceBuffer");
        private static readonly int PER_INSTANCE_LOOK_UP_AND_DITHER_BUFFER = Shader.PropertyToID("_PerInstanceLookUpAndDitherBuffer");

        private readonly int[] arrLOD = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

        private Camera renderCamera;

        private readonly Vector3[] frustumCorners = new Vector3[4];

        public LandscapeData LandscapeData { private get; set; }

        public bool IsEnabled { get; set; } = true;

        public GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings Settings { get; }

        public GPUInstancingService(GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings settings)
        {
            this.Settings = settings;

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
                buffers?.Dispose();

            candidatesBuffersTable.Clear();
            instancingMaterials.Dispose();
        }

        public void RenderIndirect()
        {
            if (renderCamera == null || !OnDemandRendering.willCurrentFrameRender)
                return;

            Bounds renderBounds = GetRenderBounds();

            foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers) in candidatesBuffersTable)
            {
                groupDataArray[0].Set(renderCamera,
                    Settings.RoadsSceneDistance(LandscapeData.EnvironmentDistance), candidate,
                    (uint)buffers.PerInstanceMatrices.count);

                buffers.GroupData.SetData(groupDataArray, 0, 0, 1);
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

                    combinedLodRenderer.RenderParamsArray.worldBounds = renderBounds;
                    Graphics.RenderMeshIndirect(combinedLodRenderer.RenderParamsArray, combinedLodRenderer.CombinedMesh, buffers.DrawArgs, commandCount: lodCount, startCommand: i * lodCount);
                }
            }
        }

        private Bounds GetRenderBounds()
        {
            Vector3 boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            GetRenderBoundsMinMax(ref boundsMin, ref boundsMax, renderCamera, renderCamera.nearClipPlane);
            GetRenderBoundsMinMax(ref boundsMin, ref boundsMax, renderCamera, renderCamera.farClipPlane);

            return new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
        }

        private void GetRenderBoundsMinMax(ref Vector3 min, ref Vector3 max, Camera camera, float d)
        {
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), d, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

            foreach (var corner in frustumCorners)
            {
                var worldSpaceCorner = camera.transform.TransformPoint(corner);
                min = Vector3.Min(min, worldSpaceCorner);
                max = Vector3.Max(max, worldSpaceCorner);
            }
        }

        public void SetCamera(Camera camera)
        {
            renderCamera = camera;

            foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers _) in candidatesBuffersTable)
            foreach (var renderer in candidate.LODGroup.CombinedLodsRenderers)
                renderer.RenderParamsArray.camera = renderCamera;
        }

        public void AddToIndirect(IReadOnlyList<GPUInstancingLODGroupWithBuffer> candidates)
        {
            foreach (GPUInstancingLODGroupWithBuffer candidate in candidates)
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
                        ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, $"{candidate.Name} has combined lod renderer equal to null for material {combinedLodRenderer.SharedMaterial.name}");
                        continue;
                    }

                    for (var lodLevel = 0; lodLevel < _nLODCount; lodLevel++)
                    {
                        if (lodLevel < combinedMesh.subMeshCount)
                        {
                            buffers.DrawArgsCommandData[lodLevel + (combinedRendererId * _nLODCount)].instanceCount = 0;
                            buffers.DrawArgsCommandData[lodLevel + (combinedRendererId * _nLODCount)].indexCountPerInstance = combinedMesh.GetIndexCount(lodLevel);
                            buffers.DrawArgsCommandData[lodLevel + (combinedRendererId * _nLODCount)].startIndex = combinedMesh.GetIndexStart(lodLevel);
                            buffers.DrawArgsCommandData[lodLevel + (combinedRendererId * _nLODCount)].baseVertexIndex = combinedMesh.GetBaseVertex(lodLevel);
                            buffers.DrawArgsCommandData[lodLevel + (combinedRendererId * _nLODCount)].startInstance = (uint)lodLevel * (uint)candidate.InstancesBuffer.Count;
                        }
                        else { buffers.DrawArgsCommandData[lodLevel + (combinedRendererId * _nLODCount)] = zeroDrawArgs; }
                    }

                    buffers.DrawArgs.SetData(buffers.DrawArgsCommandData, 0, 0, count: combinedRenderersCount * _nLODCount);

                    ReportHub.Log(ReportCategory.GPU_INSTANCING, $"Initializing render params for {candidate.Name} with material {combinedLodRenderer.SharedMaterial.name}");

                    combinedLodRenderer.InitializeRenderParams(instancingMaterials);
                    ref RenderParams rparams = ref combinedLodRenderer.RenderParamsArray;
                    rparams.camera = renderCamera;
                    rparams.matProps = new MaterialPropertyBlock();
                    rparams.matProps.SetBuffer(MAT_PER_INSTANCE_BUFFER, buffers.PerInstanceMatrices);
                    rparams.matProps.SetBuffer(PER_INSTANCE_LOOK_UP_AND_DITHER_BUFFER, buffers.InstanceLookUpAndDither);
                }
            }
        }

        public void Remove(List<GPUInstancingLODGroupWithBuffer> candidates)
        {
            foreach (GPUInstancingLODGroupWithBuffer candidate in candidates)
            {
                if (candidate != null && candidatesBuffersTable.Remove(candidate, out GPUInstancingBuffers buffers))
                    buffers.Dispose();
            }
        }
    }
}
