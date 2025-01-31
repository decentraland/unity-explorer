using DCL.Roads.GPUInstancing.Playground;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

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
        // --- INDIRECT ---
        private readonly Dictionary<GPUInstancingCandidate, GPUInstancingBuffers> candidatesBuffers = new ();

        public GPUInstancingPrefabData[] originalPrefabs;

        [Min(0)] public int PrefabId;
        [Min(0)] public int CandidateId;
        [Min(0)] public int LodLevel;
        public bool RenderFullPrefab;

        [Space]
        public bool UseIndirect;

        [Space]
        public bool Run;

        public Shader shader;

        private string currentNane;

        public void Update()
        {
            if (!Run) return;

            int prefabId = Mathf.Min(PrefabId, originalPrefabs.Length -1);

            if (currentNane != originalPrefabs[prefabId].name)
            {
                currentNane = originalPrefabs[prefabId].name;

                foreach (var candidate in originalPrefabs[prefabId].indirectCandidates)
                    AdjustBuffers(candidate);
            }

            if (UseIndirect)
            {
                foreach (var candidate in originalPrefabs[prefabId].indirectCandidates)
                    RenderCandidateIndirect(candidate, candidatesBuffers[candidate]);

                foreach (var candidate in originalPrefabs[prefabId].directCandidates)
                    RenderCandidateInstanced(candidate);
            }
            else
            {
                if (RenderFullPrefab)
                    foreach (GPUInstancingCandidate candidate in originalPrefabs[prefabId].indirectCandidates)
                        RenderCandidateInstanced(candidate);
                else
                {
                    int candidateId = Mathf.Min(CandidateId, originalPrefabs[prefabId].indirectCandidates.Count -1);
                    RenderCandidateInstanced(candidate: originalPrefabs[prefabId].indirectCandidates[candidateId]);
                }
            }
        }

        private void OnDisable()
        {
            currentNane = string.Empty;

            foreach (GPUInstancingBuffers buffers in candidatesBuffers.Values)
            {
                buffers.InstanceBuffer.Dispose();
                buffers.InstanceBuffer = null;

                buffers.DrawArgsBuffer.Dispose();
                buffers.DrawArgsBuffer = null;
            }
        }

        private void AdjustBuffers(GPUInstancingCandidate candidate)
        {
            if (!candidatesBuffers.TryGetValue(candidate, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffers.Add(candidate, buffers);
            }

            var totalCommands = 0;

            // foreach (var lod in candidate.Lods)
            int lodLevel = Mathf.Min(LodLevel, candidate.Lods.Count - 1);
            MeshRenderingData[] meshes = candidate.Lods[lodLevel].MeshRenderingDatas;

            foreach (MeshRenderingData mesh in meshes)
                totalCommands += mesh.ToGPUInstancedRenderer().RenderParamsArray.Length;

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            buffers.InstanceBuffer?.Release();
            buffers.InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, candidate.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));

            // PerInstanceBuffer[] shiftedTRS = candidate.InstancesBuffer.ToArray();
            // for (var i = 0; i < shiftedTRS.Length; i++)
            // {
            //     PerInstanceBuffer pid = shiftedTRS[i];
            //     pid.instMatrix = baseMatrix * pid.instMatrix;
            //     shiftedTRS[i] = pid;
            // }

            buffers.InstanceBuffer.SetData(candidate.InstancesBuffer, 0, 0, candidate.InstancesBuffer.Count);
        }

        private void RenderCandidateIndirect(GPUInstancingCandidate candidate, GPUInstancingBuffers buffers)
        {
            int lodLevel = Mathf.Min(LodLevel, candidate.Lods.Count - 1);
            MeshRenderingData[] meshes = candidate.Lods[lodLevel].MeshRenderingDatas;
            var currentCommandIndex = 0;

            // foreach (var lod in candidate.Lods)
            foreach (MeshRenderingData mesh in meshes)
            {
                var instancedRenderer = mesh.ToGPUInstancedRenderer();
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

                    if (rparams.material.shader == shader)
                        rparams.material.EnableKeyword(new LocalKeyword(shader, "_GPU_INSTANCER_BATCHER"));
                    else
                        Debug.LogWarning($"material {rparams.material.name} has different shader {rparams.material.shader}");

                    Graphics.RenderMeshIndirect(rparams, instancedRenderer.Mesh, buffers.DrawArgsBuffer, commandCount: 1, currentCommandIndex);
                    currentCommandIndex++;
                }
            }
        }

        private void RenderCandidateInstanced(GPUInstancingCandidate candidate)
        {
            int lodLevel = Mathf.Min(LodLevel, candidate.Lods.Count - 1);
            foreach (MeshRenderingData meshRendering in candidate.Lods[lodLevel].MeshRenderingDatas)
            {
                var instancedRenderer = meshRendering.ToGPUInstancedRenderer();

                List<Matrix4x4> shiftedInstanceData = new (candidate.InstancesBuffer.Count);

                shiftedInstanceData.AddRange(

                    // RoadShift ? instancedMesh.PerInstancesData.Select(matrix => baseMatrix * matrix.instMatrix) :
                    candidate.InstancesBuffer.Select(matrix => matrix.instMatrix));

                for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], instancedRenderer.Mesh, i, shiftedInstanceData);
            }
        }
    }
}
