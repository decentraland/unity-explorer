using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Utility;

namespace DCL.Roads.GPUInstancing
{
    public class GPUInstancingBuffers
    {
        public GraphicsBuffer InstanceBuffer;
        public GraphicsBuffer DrawArgsBuffer;
        public GraphicsBuffer.IndirectDrawIndexedArgs[] DrawArgsCommandData;
    }

    [ExecuteAlways]
    public class GPUInstancingRoadPrefabPlayground : MonoBehaviour
    {
        private readonly Dictionary<GPUInstancingCandidate, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new();

        public GPUInstancingPrefabData[] originalPrefabs;
        public Shader shader;

        [Min(0)] public int PrefabId;
        [Min(0)] public int CandidateId;
        [Min(0)] public int LodLevel;

        [Header("ROADS")]
        public RoadDescription[] Descriptions;

        [Space]
        public bool RenderFullPrefab;
        public bool UseRoadShift;
        public bool UseIndirect;

        [Space]
        public bool Run;

        private string currentNane;

        [ContextMenu(nameof(LogMaterialsAmount))]
        private void LogMaterialsAmount()
        {
            Debug.Log(instancingMaterials.Count);
        }

        public void Update()
        {
            if (!Run) return;

            int prefabId = Mathf.Min(PrefabId, originalPrefabs.Length -1);

            var baseMatrix = UseRoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            if (currentNane != originalPrefabs[prefabId].name)
            {
                currentNane = originalPrefabs[prefabId].name;

                foreach (var candidate in originalPrefabs[prefabId].indirectCandidates)
                    AdjustBuffers(candidate, baseMatrix);
            }

            if (UseIndirect)
            {
                foreach (var candidate in originalPrefabs[prefabId].indirectCandidates)
                    RenderCandidateIndirect(candidate, candidatesBuffersTable[candidate]);

                foreach (var candidate in originalPrefabs[prefabId].directCandidates)
                    RenderCandidateInstanced(candidate, baseMatrix);
            }
            else
            {
                if (RenderFullPrefab)
                    foreach (GPUInstancingCandidate candidate in originalPrefabs[prefabId].indirectCandidates)
                        RenderCandidateInstanced(candidate, baseMatrix);
                else
                {
                    int candidateId = Mathf.Min(CandidateId, originalPrefabs[prefabId].indirectCandidates.Count -1);
                    RenderCandidateInstanced(candidate: originalPrefabs[prefabId].indirectCandidates[candidateId], baseMatrix);
                }
            }
        }

        private void OnDisable()
        {
            currentNane = string.Empty;

            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
            {
                buffers.InstanceBuffer.Dispose();
                buffers.InstanceBuffer = null;

                buffers.DrawArgsBuffer.Dispose();
                buffers.DrawArgsBuffer = null;
            }
        }

        private void AdjustBuffers(GPUInstancingCandidate candidate, Matrix4x4 baseMatrix)
        {
            if (!candidatesBuffersTable.TryGetValue(candidate, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(candidate, buffers);
            }

            var totalCommands = 0;

            // foreach (var lod in candidate.Lods)
            int lodLevel = Mathf.Min(LodLevel, candidate.Lods.Count - 1);
            MeshRenderingData[] meshes = candidate.Lods[lodLevel].MeshRenderingDatas;

            foreach (MeshRenderingData mesh in meshes)
            {
                totalCommands += mesh.ToGPUInstancedRenderer(instancingMaterials).RenderParamsArray.Length;
            }

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            PerInstanceBuffer[] roadShiftInstancesBuffer = candidate.InstancesBuffer.ToArray();
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

        private void RenderCandidateIndirect(GPUInstancingCandidate candidate, GPUInstancingBuffers buffers)
        {
            int lodLevel = Mathf.Min(LodLevel, candidate.Lods.Count - 1);
            MeshRenderingData[] meshes = candidate.Lods[lodLevel].MeshRenderingDatas;
            var currentCommandIndex = 0;

            // foreach (var lod in candidate.Lods)
            foreach (MeshRenderingData mesh in meshes)
            {
                var instancedRenderer = mesh.ToGPUInstancedRenderer(instancingMaterials);
                int submeshCount = instancedRenderer.RenderParamsArray.Length;

                // Set commands and render
                for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    buffers.DrawArgsCommandData[currentCommandIndex].indexCountPerInstance = instancedRenderer.Mesh.GetIndexCount(submeshIndex);
                    buffers.DrawArgsCommandData[currentCommandIndex].instanceCount = (uint)candidate.InstancesBuffer.Count;
                    buffers.DrawArgsCommandData[currentCommandIndex].startIndex = instancedRenderer.Mesh.GetIndexStart(submeshIndex);
                    buffers.DrawArgsCommandData[currentCommandIndex].baseVertexIndex = 0;
                    buffers.DrawArgsCommandData[currentCommandIndex].startInstance = 0;
                    buffers.DrawArgsBuffer.SetData(buffers.DrawArgsCommandData, currentCommandIndex, currentCommandIndex, count: 1);

                    RenderParams rparams = instancedRenderer.RenderParamsArray[submeshIndex];

                    // rparams.camera = Camera.current;
                    rparams.matProps = new MaterialPropertyBlock();
                    rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.InstanceBuffer);

                    Graphics.RenderMeshIndirect(rparams, instancedRenderer.Mesh, buffers.DrawArgsBuffer, commandCount: 1, currentCommandIndex);
                    currentCommandIndex++;
                }
            }
        }

        private void RenderCandidateInstanced(GPUInstancingCandidate candidate, Matrix4x4 baseMatrix)
        {
            int lodLevel = Mathf.Min(LodLevel, candidate.Lods.Count - 1);
            foreach (MeshRenderingData meshRendering in candidate.Lods[lodLevel].MeshRenderingDatas)
            {
                var instancedRenderer = meshRendering.ToGPUInstancedRenderer(instancingMaterials);

                List<Matrix4x4> shiftedInstanceData = new (candidate.InstancesBuffer.Count);
                shiftedInstanceData.AddRange(candidate.InstancesBuffer.Select(matrix => baseMatrix * matrix.instMatrix));

                for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], instancedRenderer.Mesh, i, shiftedInstanceData);
            }
        }
    }
}
