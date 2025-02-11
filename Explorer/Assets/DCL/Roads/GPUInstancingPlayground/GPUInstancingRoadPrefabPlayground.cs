using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Utility;

namespace DCL.Roads.GPUInstancing
{
    [ExecuteAlways]
    public class GPUInstancingRoadPrefabPlayground : MonoBehaviour
    {
        private readonly Dictionary<GPUInstancingCandidate_Old, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public GPUInstancingPrefabData_Old[] originalPrefabs;

        [Min(0)] public int PrefabId;
        [Min(0)] public int CandidateId;
        [Min(0)] public int LodLevel;

        private GraphicsBuffer.IndirectDrawIndexedArgs[] drawargscommands;
        public ComputeShader FrustumCullingAndLODGenComputeShader;
        private string FrustumCullingAndLODGenComputeShader_KernelName = "CameraCullingAndLODCalculationKernel";
        protected static int FrustumCullingAndLODGenComputeShader_KernelIDs;
        private static readonly int ComputeVar_nInstBufferSize = Shader.PropertyToID("nInstBufferSize"); // uint
        private static readonly int ComputeVar_nLODCount = Shader.PropertyToID("nLODCount"); // uint
        private static readonly int ComputeVar_PerInstance_LODLevels  = Shader.PropertyToID("PerInstance_LODLevels"); // RWStructuredBuffer<uint4>
        private static readonly int ComputeVar_PerInstanceData = Shader.PropertyToID("PerInstanceData"); // RWStructuredBuffer<PerInstance>
        private static readonly int ComputeVar_InstanceLookUpAndDither = Shader.PropertyToID("InstanceLookUpAndDither"); // RWStructuredBuffer<uint2>
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
        private static readonly int ComputeVar_nLodCount = Shader.PropertyToID("nLodCount"); // uint
        private static readonly int ComputeVar_deltaTime = Shader.PropertyToID("deltaTime"); // float

        private ComputeBuffer cbLODLevels;
        private ComputeBuffer cbInstanceLookUpAndDither;

        public ComputeShader IndirectBufferGenerationComputeShader;
        private string IndirectBufferGenerationComputeShader_KernelName = "ComputeLODBufferAccumulation";
        protected static int IndirectBufferGenerationComputeShader_KernelIDs;

        [Header("ROADS")]
        public RoadDescription[] Descriptions;
        public Vector2 comparisonShift;

        [Space]
        public bool RenderFullPrefab;
        public bool DisableMeshRenderers;
        public bool UseRoadShift;
        public bool UseLodLevel;
        public bool UseIndirect;

        [Space]
        public bool Run;

        private string currentNane;
        private GameObject originalInstance;

        // PerInstanceMatrix - Matrix4x4, Colour
        // PerInstanceLODLevels - UINT4 - LOD_A, LOD_B, LOD_Dither, LOD_Shadow (0-8 LOD_ID, 0-8 LOD_ID, 0-8 LOD_ID, 0-255 dither value) e.g. (LOD2 current lod, LOD3 next lod, LOD2 shadow lod, 124 dither amount)
        // PerInstanceLookUpAndDither - UINT2 (InstanceID - lookup into PerInstanceMatrix, Dither 0-255)

        // 10x instances and 3x LODs
        // [0,0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2] Representation of Array Sections
        // [0,1,3,5,*,*,*,*,*,2,4,6,*,*,*,*,*,*,*,7,8,9,10,*,*,*,*,*] Actual data
        // DrawArgs[3] - [0].instanceCount == 4, [1].instanceCount == 3, [2].instanceCount == 4
        // DrawArgs[3] - [0].startInstance == 0, [1].instanceCount == 10, [2].instanceCount == 20

        public void PrepareKernels(ComputeBuffer _PerInstanceMatrices, int _nInstanceCount, int _nLODCount)
        {
            cbLODLevels = new ComputeBuffer(_nInstanceCount, sizeof(uint)*4);
            cbInstanceLookUpAndDither = new ComputeBuffer(_nInstanceCount * _nLODCount, sizeof(uint)*2);
            drawargscommands = new GraphicsBuffer.IndirectDrawIndexedArgs[_nLODCount];
            FrustumCullingAndLODGenComputeShader_KernelIDs = FrustumCullingAndLODGenComputeShader.FindKernel(FrustumCullingAndLODGenComputeShader_KernelName);
            IndirectBufferGenerationComputeShader_KernelIDs = IndirectBufferGenerationComputeShader.FindKernel(IndirectBufferGenerationComputeShader_KernelName);
        }

        public void DispatchFrustumCullingAndLODGenComputeShader(ComputeBuffer _PerInstanceMatrices, int _nInstanceCount, int _nLODCount, Matrix4x4 _LODSizes)
        {
            // private static readonly int ComputeVar_lodSizes = Shader.PropertyToID("lodSizes"); // float4x4
            // private static readonly int ComputeVar_nLodCount = Shader.PropertyToID("nLodCount"); // uint
            // private static readonly int ComputeVar_vBoundsCenter = Shader.PropertyToID("vBoundsCenter"); // float3
            // private static readonly int ComputeVar_vBoundsExtents = Shader.PropertyToID("vBoundsExtents"); // float3
            // private static readonly int ComputeVar_nLODCount = Shader.PropertyToID("nLODCount"); // uint
            // private static readonly int ComputeVar_nPerInstanceBufferSize = Shader.PropertyToID("nPerInstanceBufferSize"); // uint
            // private static readonly int ComputeVar_matCamera_MVP = Shader.PropertyToID("matCamera_MVP"); // float4x4
            // private static readonly int ComputeVar_vCameraPosition = Shader.PropertyToID("vCameraPosition"); // float3
            // private static readonly int ComputeVar_fCameraHalfAngle = Shader.PropertyToID("fCameraHalfAngle"); // float
            // private static readonly int ComputeVar_minCullingDistance = Shader.PropertyToID("minCullingDistance"); // float
            // private static readonly int ComputeVar_fMaxDistance = Shader.PropertyToID("fMaxDistance"); // float
            // private static readonly int ComputeVar_isFrustumCulling = Shader.PropertyToID("isFrustumCulling");
            // private static readonly int ComputeVar_frustumOffset = Shader.PropertyToID("frustumOffset"); // float

            FrustumCullingAndLODGenComputeShader.SetInt(ComputeVar_nLODCount, _nLODCount);
            FrustumCullingAndLODGenComputeShader.SetMatrix(ComputeVar_lodSizes, _LODSizes);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstanceData, _PerInstanceMatrices);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, cbLODLevels);
            FrustumCullingAndLODGenComputeShader.Dispatch(FrustumCullingAndLODGenComputeShader_KernelIDs, 512, 0, 0);


            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, cbLODLevels);
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_InstanceLookUpAndDither, cbInstanceLookUpAndDither);
            IndirectBufferGenerationComputeShader.Dispatch(IndirectBufferGenerationComputeShader_KernelIDs, 512, 0, 0);

            // Indirect Draw - bind matrix instance buffer and InstanceLookupAndDither with DrawArgs
        }

        public void Update()
        {
            if (!Run) return;

            int prefabId = Mathf.Min(PrefabId, originalPrefabs.Length - 1);

            Matrix4x4 baseMatrix = UseRoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            if (currentNane != originalPrefabs[prefabId].name)
            {
                currentNane = originalPrefabs[prefabId].name;
                SpawnOriginalPrefab(originalPrefabs[prefabId], comparisonShift);

                foreach (GPUInstancingCandidate_Old candidate in originalPrefabs[prefabId].indirectCandidates)
                    AdjustBuffers(candidate, baseMatrix);
            }

            if (UseIndirect)
            {
                foreach (GPUInstancingCandidate_Old candidate in originalPrefabs[prefabId].indirectCandidates)
                    RenderCandidateIndirect(candidate, candidatesBuffersTable[candidate]);

                foreach (GPUInstancingCandidate_Old candidate in originalPrefabs[prefabId].directCandidates)
                    RenderCandidateInstanced(candidate, baseMatrix);
            }
            else
            {
                if (RenderFullPrefab)
                    foreach (GPUInstancingCandidate_Old candidate in originalPrefabs[prefabId].indirectCandidates)
                        RenderCandidateInstanced(candidate, baseMatrix);
                else
                {
                    int candidateId = Mathf.Min(CandidateId, originalPrefabs[prefabId].indirectCandidates.Count - 1);
                    RenderCandidateInstanced(lodGroup: originalPrefabs[prefabId].indirectCandidates[candidateId], baseMatrix);
                }
            }
        }

        private void OnDisable()
        {
            currentNane = string.Empty;

            DestroyImmediate(originalInstance);
            originalInstance = null;

            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
            {
                buffers.InstanceBuffer.Dispose();
                buffers.InstanceBuffer = null;

                buffers.DrawArgsBuffer.Dispose();
                buffers.DrawArgsBuffer = null;
            }

            candidatesBuffersTable.Clear();
        }

        [ContextMenu(nameof(LogMaterialsAmount))]
        private void LogMaterialsAmount()
        {
            Debug.Log(instancingMaterials.Count);
        }

        private void AdjustBuffers(GPUInstancingCandidate_Old lodGroup, Matrix4x4 baseMatrix)
        {
            if (!candidatesBuffersTable.TryGetValue(lodGroup, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(lodGroup, buffers);
            }

            var totalCommands = 0;

            for (var lodId = 0; lodId < lodGroup.Lods_Old.Count; lodId++)
            {
                if(UseLodLevel) lodId = Mathf.Min(LodLevel, lodGroup.Lods_Old.Count - 1);
                MeshRenderingData_Old[] meshes = lodGroup.Lods_Old[lodId].MeshRenderingDatas;

                foreach (MeshRenderingData_Old mesh in meshes)
                {
                    mesh.Initialize(instancingMaterials);
                    totalCommands += mesh.RenderParamsArray.Length;
                }

                if (UseLodLevel) break;
            }

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            PerInstanceBuffer[] roadShiftInstancesBuffer = lodGroup.InstancesBuffer.ToArray();

            for (var i = 0; i < roadShiftInstancesBuffer.Length; i++)
            {
                PerInstanceBuffer pid = roadShiftInstancesBuffer[i];
                pid.instMatrix = baseMatrix * pid.instMatrix;
                roadShiftInstancesBuffer[i] = pid;
            }

            buffers.InstanceBuffer?.Release();
            buffers.InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, roadShiftInstancesBuffer.Length, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.InstanceBuffer.SetData(roadShiftInstancesBuffer, 0, 0, roadShiftInstancesBuffer.Length);
        }

        private void RenderCandidateIndirect(GPUInstancingCandidate_Old lodGroup, GPUInstancingBuffers buffers)
        {
            var currentCommandIndex = 0;

            foreach (var lod in lodGroup.Lods_Old)
            {
                MeshRenderingData_Old[] meshes = UseLodLevel? lodGroup.Lods_Old[Mathf.Min(LodLevel, lodGroup.Lods_Old.Count - 1)].MeshRenderingDatas : lod.MeshRenderingDatas;

                foreach (MeshRenderingData_Old mesh in meshes)
                {
                    int submeshCount = mesh.RenderParamsArray.Length;

                    // Set commands and render
                    for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                    {
                        buffers.DrawArgsCommandData[currentCommandIndex].indexCountPerInstance = mesh.SharedMesh.GetIndexCount(submeshIndex);
                        buffers.DrawArgsCommandData[currentCommandIndex].instanceCount = (uint)lodGroup.InstancesBuffer.Count;
                        buffers.DrawArgsCommandData[currentCommandIndex].startIndex = mesh.SharedMesh.GetIndexStart(submeshIndex);
                        buffers.DrawArgsCommandData[currentCommandIndex].baseVertexIndex = 0;
                        buffers.DrawArgsCommandData[currentCommandIndex].startInstance = 0;
                        buffers.DrawArgsBuffer.SetData(buffers.DrawArgsCommandData, currentCommandIndex, currentCommandIndex, count: 1);

                        RenderParams rparams = mesh.RenderParamsArray[submeshIndex];

                        // rparams.camera = Camera.current;
                        rparams.matProps = new MaterialPropertyBlock();
                        rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.InstanceBuffer);

                        Graphics.RenderMeshIndirect(rparams, mesh.SharedMesh, buffers.DrawArgsBuffer, commandCount: 1, currentCommandIndex);
                        currentCommandIndex++;
                    }
                }

                if (UseLodLevel) break;
            }
        }

        private void RenderCandidateInstanced(GPUInstancingCandidate_Old lodGroup, Matrix4x4 baseMatrix)
        {
            for (int lodId = 0; lodId < lodGroup.Lods_Old.Count; lodId++)
            {
                if(UseLodLevel) lodId = Mathf.Min(LodLevel, lodGroup.Lods_Old.Count - 1);
                foreach (MeshRenderingData_Old meshRendering in lodGroup.Lods_Old[lodId].MeshRenderingDatas)
                {
                    meshRendering.Initialize(instancingMaterials);

                    List<Matrix4x4> shiftedInstanceData = new (lodGroup.InstancesBuffer.Count);
                    shiftedInstanceData.AddRange(lodGroup.InstancesBuffer.Select(matrix => baseMatrix * matrix.instMatrix));

                    for (var i = 0; i < meshRendering.RenderParamsArray.Length; i++)
                        Graphics.RenderMeshInstanced(in meshRendering.RenderParamsArray[i], meshRendering.SharedMesh, i, shiftedInstanceData);
                }

                if (UseLodLevel) break;
            }
        }

        [ContextMenu(nameof(PrefabsSelfCollect))]
        private void PrefabsSelfCollect()
        {
            foreach (GPUInstancingPrefabData_Old prefab in originalPrefabs)
            {
                prefab.CollectSelfData();
                prefab.ShowVisuals();

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
#endif
            }
        }

        private void SpawnOriginalPrefab(GPUInstancingPrefabData_Old prefab, Vector2 pos)
        {
            if (DisableMeshRenderers)
                prefab.HideVisuals();
            else
                prefab.ShowVisuals();

            if (originalInstance == null || originalInstance.name != prefab.name)
            {
                if (originalInstance != null)
                    DestroyImmediate(originalInstance);

                originalInstance = Instantiate(prefab.gameObject);
                originalInstance.name = prefab.name;
                originalInstance.transform.Translate(pos.x, 0, pos.y);
            }

            prefab.CollectSelfData();
        }
    }
}
