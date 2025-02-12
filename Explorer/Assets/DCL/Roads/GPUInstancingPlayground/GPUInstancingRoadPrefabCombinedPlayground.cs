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

        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public GPUInstancingPrefabData prefab;

        [Min(0)] public int CandidateID;
        [Min(0)] public int LodRendererID;
        [Min(0)] public int LodLevel;

        [Space] public bool Run;

        public ComputeShader FrustumCullingAndLODGenComputeShader;

        public ComputeShader IndirectBufferGenerationComputeShader;
        private GPUInstancingLODGroup currentCandidate;

        /// ---------
        private GraphicsBuffer perInstanceMatrices;
        private GraphicsBuffer drawArgsBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] drawArgsCommandData;

        private string FrustumCullingAndLODGenComputeShader_KernelName = "CameraCullingAndLODCalculationKernel";
        private int FrustumCullingAndLODGenComputeShader_KernelIDs;

        private GraphicsBuffer cbLODLevels;
        private GraphicsBuffer cbInstanceLookUpAndDither;

        private string IndirectBufferGenerationComputeShader_KernelName = "ComputeLODBufferAccumulation";
        private int IndirectBufferGenerationComputeShader_KernelIDs;

        private static readonly int ComputeVar_nLODCount = Shader.PropertyToID("nLODCount"); // uint
        private static readonly int ComputeVar_PerInstance_LODLevels  = Shader.PropertyToID("PerInstance_LODLevels"); // RWStructuredBuffer<uint4>
        private static readonly int ComputeVar_PerInstanceData = Shader.PropertyToID("PerInstanceData"); // RWStructuredBuffer<PerInstance>
        private static readonly int ComputeVar_InstanceLookUpAndDither = Shader.PropertyToID("InstanceLookUpAndDither"); // RWStructuredBuffer<uint2>
        private static readonly int ComputeVar_lodSizes = Shader.PropertyToID("lodSizes"); // float4x4

        public int[] LookUp;

        public void PrepareKernels(GPUInstancingLODGroup candidate)
        {
            List<PerInstanceBuffer> _PerInstanceMatrices = candidate.InstancesBuffer;
            int _nInstanceCount = candidate.InstancesBuffer.Count;
            int _nLODCount = candidate.LodsScreenSpaceSizes.Length;

            cbLODLevels = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, sizeof(uint)*4);
            cbInstanceLookUpAndDither = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount * _nLODCount, sizeof(uint)*2);

            // NativeArray<uint> natArray = new NativeArray<uint>(_nInstanceCount * _nLODCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            // for (int inst = 0; inst < _nLODCount; ++inst)
            // {
            //     for (int i = 0 + (_nInstanceCount * inst * 2); i < (_nInstanceCount * 2) + (_nInstanceCount * inst * 2); i += 2)
            //     {
            //         natArray[i] = (uint)(i / 2);
            //         natArray[i + 1] = 0;
            //     }
            // }
            cbInstanceLookUpAndDither.SetData(LookUp, 0,0,4);

            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, _nLODCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            drawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[_nLODCount];

            // TODO : set flag to Lock
            perInstanceMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            perInstanceMatrices.SetData(_PerInstanceMatrices, 0, 0, _nInstanceCount);

            FrustumCullingAndLODGenComputeShader_KernelIDs = FrustumCullingAndLODGenComputeShader.FindKernel(FrustumCullingAndLODGenComputeShader_KernelName);
            IndirectBufferGenerationComputeShader_KernelIDs = IndirectBufferGenerationComputeShader.FindKernel(IndirectBufferGenerationComputeShader_KernelName);

            //
            {
                int lodRendererID = Mathf.Min(LodRendererID, candidate.CombinedLodsRenderers.Count - 1);
                CombinedLodsRenderer combinedLodRenderer = candidate.CombinedLodsRenderers[lodRendererID];
                Mesh combinedMesh = combinedLodRenderer.CombinedMesh;

                int lodCount = candidate.LodsScreenSpaceSizes.Length;

                for (int lodLevel = 0; lodLevel < lodCount; lodLevel++)
                {
                    drawArgsCommandData[lodLevel].indexCountPerInstance = combinedMesh.GetIndexCount(lodLevel);
                    drawArgsCommandData[lodLevel].instanceCount = (uint)candidate.InstancesBuffer.Count;
                    drawArgsCommandData[lodLevel].startIndex = combinedMesh.GetIndexStart(lodLevel);
                    drawArgsCommandData[lodLevel].baseVertexIndex = combinedMesh.GetBaseVertex(lodLevel);
                    drawArgsCommandData[lodLevel].startInstance = (uint)lodLevel * (uint)candidate.InstancesBuffer.Count;
                }

                drawArgsCommandData[0].instanceCount = (uint)candidate.InstancesBuffer.Count;
                drawArgsCommandData[1].instanceCount = 0;
                drawArgsCommandData[2].instanceCount = 0;

                drawArgsBuffer.SetData(drawArgsCommandData, 0, 0, count: lodCount);

                combinedLodRenderer.InitializeRenderParams(instancingMaterials);

                ref RenderParams rparams = ref combinedLodRenderer.RenderParamsArray[0];
                // rparams.camera = Camera.current;
                rparams.matProps = new MaterialPropertyBlock();
                rparams.matProps.SetBuffer("_PerInstanceBuffer", perInstanceMatrices);
                rparams.matProps.SetBuffer("_PerInstanceLookUpAndDitherBuffer", cbInstanceLookUpAndDither);
            }
        }

        public void DispatchFrustumCullingAndLODGenComputeShader(GraphicsBuffer _PerInstanceMatrices, GPUInstancingLODGroup candidate)
        {
            Camera cam = Camera.main;

            var ComputeVar_vCameraPosition = cam.transform.position;
            var ComputeVar_minCullingDistance = cam.nearClipPlane;
            var ComputeVar_fMaxDistance = cam.farClipPlane;

            var ComputeVar_matCamera_MVP = cam.projectionMatrix * cam.worldToCameraMatrix;
            float ComputeVar_fCameraHalfAngle = 0.5f * cam.fieldOfView * Mathf.Deg2Rad;  // Get Camera Half Angle (vertical FOV/2 in radians)

            // private static readonly int ComputeVar_isFrustumCulling = Shader.PropertyToID("isFrustumCulling"); - true
            // private static readonly int ComputeVar_frustumOffset = Shader.PropertyToID("frustumOffset"); // float

            Vector3 ComputeVar_vBoundsCenter = candidate.Bounds.center;
            Vector3 ComputeVar_vBoundsExtents = candidate.Bounds.extents;
            int ComputeVar_nPerInstanceBufferSize = _PerInstanceMatrices.stride * _PerInstanceMatrices.count;

            int _nLODCount = candidate.LodsScreenSpaceSizes.Length;
            Matrix4x4 _LODSizes = candidate.LODSizesMatrix;

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

        private void RenderCandidateIndirect(GPUInstancingLODGroup candidate)
        {
            // foreach (CombinedLodsRenderer combinedLodRenderer in candidate.CombinedLodsRenderers)
            {
                int lodRendererID = Mathf.Min(LodRendererID, candidate.CombinedLodsRenderers.Count - 1);
                CombinedLodsRenderer combinedLodRenderer = candidate.CombinedLodsRenderers[lodRendererID];

                var lodCount = candidate.LodsScreenSpaceSizes.Length;

                Graphics.RenderMeshIndirect(combinedLodRenderer.RenderParamsArray[0], combinedLodRenderer.CombinedMesh, drawArgsBuffer, commandCount: lodCount);
            }
        }

        private void AdjustBuffers(GPUInstancingLODGroup candidate)
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
            int candidateID = Mathf.Min(CandidateID, prefab.IndirectCandidates.Count - 1);
            currentCandidate = prefab.IndirectCandidates[candidateID];

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
    }
}
