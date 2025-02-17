using DCL.Roads.GPUInstancing.Playground;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace DCL.Roads.GPUInstancingPlayground
{
    [ExecuteAlways]
    public class GPUInstancingRoadPrefabCombinedPlayground : MonoBehaviour
    {
        private static readonly int ComputeVar_nLODCount = Shader.PropertyToID("nLODCount"); // uint
        private static readonly int ComputeVar_PerInstance_LODLevels = Shader.PropertyToID("PerInstance_LODLevels"); // RWStructuredBuffer<uint4>
        private static readonly int ComputeVar_PerInstanceData = Shader.PropertyToID("PerInstanceData"); // RWStructuredBuffer<PerInstance>
        private static readonly int ComputeVar_InstanceLookUpAndDither = Shader.PropertyToID("InstanceLookUpAndDither"); // RWStructuredBuffer<uint2>
        private static readonly int ComputeVar_lodSizes = Shader.PropertyToID("lodSizes"); // float4x4

        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public List<GPUInstancingPrefabData> Prefabs;

        [Min(0)] public int PrefabId;
        [Min(0)] public int CandidateID;
        [Min(0)] public int LodRendererID;
        [Min(0)] public int LodLevel;

        [Space] public bool Run;

        public ComputeShader FrustumCullingAndLODGenComputeShader;

        public ComputeShader IndirectBufferGenerationComputeShader;

        public uint[] LookUpAndDitherDebug;
        private GPUInstancingLODGroupWithBuffer currentCandidate;

        /// ---------
        private GraphicsBuffer perInstanceMatrices;
        private GraphicsBuffer drawArgsBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] drawArgsCommandData;

        private readonly string FrustumCullingAndLODGenComputeShader_KernelName = "CameraCullingAndLODCalculationKernel";
        private int FrustumCullingAndLODGenComputeShader_KernelIDs;

        private GraphicsBuffer cbLODLevels;
        private GraphicsBuffer cbInstanceLookUpAndDither;

        private readonly string IndirectBufferGenerationComputeShader_KernelName = "ComputeLODBufferAccumulation";
        private int IndirectBufferGenerationComputeShader_KernelIDs;

        public void Update()
        {
            if (!Run) return;

            // foreach (var candidate in prefab.IndirectCandidates)
            {
                RenderCandidateIndirect(currentCandidate);
            }
        }

        private void OnEnable()
        {
            int prefabId = Mathf.Min(PrefabId, Prefabs.Count - 1);
            int candidateID = Mathf.Min(CandidateID, Prefabs[prefabId].IndirectCandidates.Count - 1);
            currentCandidate = Prefabs[prefabId].IndirectCandidates[candidateID];

            // AdjustBuffers(currentCandidate);
            PrepareKernels(currentCandidate);
        }

        private void OnDisable()
        {
            // currentNane = string.Empty;
            // DestroyImmediate(originalInstance);
            // originalInstance = null;

            // foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
            {
                perInstanceMatrices?.Dispose();
                perInstanceMatrices = null;

                drawArgsBuffer?.Dispose();
                drawArgsBuffer = null;

                cbLODLevels?.Dispose();
                cbLODLevels = null;

                cbInstanceLookUpAndDither?.Dispose();
                cbInstanceLookUpAndDither = null;
            }

            // candidatesBuffersTable.Clear();
        }

        public void PrepareKernels(GPUInstancingLODGroupWithBuffer candidate)
        {
            int _nInstanceCount = candidate.InstancesBuffer.Count;
            int _nLODCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

            cbLODLevels = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, sizeof(uint) * 4);

            {
                LookUpAndDitherDebug = new uint[_nInstanceCount * 2 * _nLODCount];
                cbInstanceLookUpAndDither = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount * _nLODCount, sizeof(uint) * 2);
                NativeArray<uint> natArray = new NativeArray<uint>(_nInstanceCount * _nLODCount * 2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int lod = 0; lod < _nLODCount; ++lod)
                {
                    int arrayOffset = lod * _nInstanceCount * 2;
                    for (int inst = 0; inst < _nInstanceCount; ++inst)
                    {
                        int arrayPos_LOD_ID = arrayOffset + (inst * 2) + 0;
                        int arrayPos_LOD_Dither = arrayOffset + (inst * 2) + 1;

                        natArray[arrayPos_LOD_ID] = (uint)(inst);
                        natArray[arrayPos_LOD_Dither] = 255;

                        LookUpAndDitherDebug[arrayPos_LOD_ID] = natArray[arrayPos_LOD_ID];
                        LookUpAndDitherDebug[arrayPos_LOD_Dither] = natArray[arrayPos_LOD_Dither];
                    }
                }
                cbInstanceLookUpAndDither.SetData(natArray, 0, 0, natArray.Length);
            }

            // TODO : set flag to Lock
            perInstanceMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            perInstanceMatrices.SetData(candidate.InstancesBuffer, 0, 0, _nInstanceCount);

            FrustumCullingAndLODGenComputeShader_KernelIDs = FrustumCullingAndLODGenComputeShader.FindKernel(FrustumCullingAndLODGenComputeShader_KernelName);
            IndirectBufferGenerationComputeShader_KernelIDs = IndirectBufferGenerationComputeShader.FindKernel(IndirectBufferGenerationComputeShader_KernelName);

            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, _nLODCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            drawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[_nLODCount];

            // for (var lodRendererID = 0; lodRendererID < candidate.LODGroup.CombinedLodsRenderers.Count; lodRendererID++)
            {
                int lodRendererID = Mathf.Min(LodRendererID, candidate.LODGroup.CombinedLodsRenderers.Count - 1);
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[lodRendererID];
                Mesh combinedMesh = combinedLodRenderer.CombinedMesh;

                int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                for (var lodLevel = 0; lodLevel < lodCount; lodLevel++)
                {
                    drawArgsCommandData[lodLevel].indexCountPerInstance = combinedMesh.GetIndexCount(lodLevel);
                    drawArgsCommandData[lodLevel].instanceCount = lodLevel == 0 ? (uint)candidate.InstancesBuffer.Count : 0;
                    drawArgsCommandData[lodLevel].startIndex = combinedMesh.GetIndexStart(lodLevel);
                    drawArgsCommandData[lodLevel].baseVertexIndex = combinedMesh.GetBaseVertex(lodLevel);
                    drawArgsCommandData[lodLevel].startInstance = (uint)lodLevel * (uint)candidate.InstancesBuffer.Count;
                }

                drawArgsBuffer.SetData(drawArgsCommandData, 0, 0, count: lodCount);

                combinedLodRenderer.InitializeRenderParams(instancingMaterials);
                ref RenderParams rparams = ref combinedLodRenderer.RenderParamsArray[0];

                // rparams.camera = Camera.current;
                rparams.matProps = new MaterialPropertyBlock();
                rparams.matProps.SetBuffer("_PerInstanceBuffer", perInstanceMatrices);
                rparams.matProps.SetBuffer("_PerInstanceLookUpAndDitherBuffer", cbInstanceLookUpAndDither);
            }
        }

        private void RenderCandidateIndirect(GPUInstancingLODGroupWithBuffer candidate)
        {
            // foreach (CombinedLodsRenderer combinedLodRenderer in candidate.CombinedLodsRenderers)
            {
                int lodRendererID = Mathf.Min(LodRendererID, candidate.LODGroup.CombinedLodsRenderers.Count - 1);
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[lodRendererID];

                int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                Graphics.RenderMeshIndirect(combinedLodRenderer.RenderParamsArray[0], combinedLodRenderer.CombinedMesh, drawArgsBuffer, commandCount: lodCount);
            }
        }

        public void DispatchFrustumCullingAndLODGenComputeShader(GraphicsBuffer _PerInstanceMatrices, GPUInstancingLODGroupWithBuffer candidate)
        {
            Camera cam = Camera.main;

            Vector3 ComputeVar_vCameraPosition = cam.transform.position;
            float ComputeVar_minCullingDistance = cam.nearClipPlane;
            float ComputeVar_fMaxDistance = cam.farClipPlane;

            Matrix4x4 ComputeVar_matCamera_MVP = cam.projectionMatrix * cam.worldToCameraMatrix;
            float ComputeVar_fCameraHalfAngle = 0.5f * cam.fieldOfView * Mathf.Deg2Rad; // Get Camera Half Angle (vertical FOV/2 in radians)

            // private static readonly int ComputeVar_isFrustumCulling = Shader.PropertyToID("isFrustumCulling"); - true
            // private static readonly int ComputeVar_frustumOffset = Shader.PropertyToID("frustumOffset"); // float

            Vector3 ComputeVar_vBoundsCenter = candidate.LODGroup.Bounds.center;
            Vector3 ComputeVar_vBoundsExtents = candidate.LODGroup.Bounds.extents;
            int ComputeVar_nPerInstanceBufferSize = _PerInstanceMatrices.stride * _PerInstanceMatrices.count;

            int _nLODCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;
            Matrix4x4 _LODSizes = candidate.LODGroup.LODSizesMatrix;

            FrustumCullingAndLODGenComputeShader.SetInt(ComputeVar_nLODCount, _nLODCount);
            FrustumCullingAndLODGenComputeShader.SetMatrix(ComputeVar_lodSizes, _LODSizes);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstanceData, _PerInstanceMatrices);
            FrustumCullingAndLODGenComputeShader.SetBuffer(FrustumCullingAndLODGenComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, cbLODLevels);

            // FrustumCullingAndLODGenComputeShader.Dispatch(FrustumCullingAndLODGenComputeShader_KernelIDs, 512, 0, 0);

            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_PerInstance_LODLevels, cbLODLevels);
            IndirectBufferGenerationComputeShader.SetBuffer(IndirectBufferGenerationComputeShader_KernelIDs, ComputeVar_InstanceLookUpAndDither, cbInstanceLookUpAndDither);
            IndirectBufferGenerationComputeShader.Dispatch(IndirectBufferGenerationComputeShader_KernelIDs, 512, 0, 0);

            // Indirect Draw - bind matrix instance buffer and InstanceLookupAndDither with DrawArgs
        }

        private void AdjustBuffers(GPUInstancingLODGroupWithBuffer candidate)
        {
            // int lodRendererID = Mathf.Min(LodRendererID, candidate.CombinedLodsRenderers.Count - 1);
            // CombinedLodsRenderer mesh = candidate.CombinedLodsRenderers[lodRendererID];

            var totalCommands = 1; //candidate.CombinedLodsRenderers.Count;

            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            drawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            // PerInstanceBuffer[] roadShiftInstancesBuffer = candidate.InstancesBuffer.ToArray();
            // for (var i = 0; i < roadShiftInstancesBuffer.Length; i++)
            // {
            //     PerInstanceBuffer pid = roadShiftInstancesBuffer[i];
            //     pid.instMatrix = baseMatrix * pid.instMatrix;
            //     roadShiftInstancesBuffer[i] = pid;
            // }

            perInstanceMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, candidate.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            perInstanceMatrices.SetData(candidate.InstancesBuffer, 0, 0, candidate.InstancesBuffer.Count);
        }
    }
}
