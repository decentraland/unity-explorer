using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Playground;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Roads.GPUInstancing
{
    [ExecuteAlways]
    public class GPUInstancingRoadPrefabPlayground : MonoBehaviour
    {
        public GPUInstancingPrefabData[] originalPrefabs;

        [Min(0)] public int PrefabId;
        [Min(0)] public int CandidateId;
        [Min(0)] public int LodLevel;
        public bool RenderFullPrefab;

        [Space]
        public bool UseIndirect;

        [Space]
        public bool Run;

        // --- INDIRECT ---
        private GraphicsBuffer instanceBuffer;
        private GraphicsBuffer drawArgsBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] drawArgsCommandData;

        public Shader shader;
        private LocalKeyword gpuInstancingKeyword;

        private void OnDisable()
        {
            drawArgsBuffer?.Release();
            drawArgsBuffer = null;

            instanceBuffer?.Release();
            instanceBuffer = null;
        }

        public void Update()
        {
            if (!Run) return;

            if (UseIndirect)
            {
                RenderCandidateIndirect(candidate: originalPrefabs[PrefabId].candidates[CandidateId]);
                return;
            }

            if (RenderFullPrefab)
                foreach (var candidate in originalPrefabs[PrefabId].candidates)
                    RenderCandidateInstanced(candidate);
            else
                RenderCandidateInstanced(candidate: originalPrefabs[PrefabId].candidates[CandidateId]);
        }

        private void RenderCandidateIndirect(GPUInstancingCandidate candidate)
        {
            gpuInstancingKeyword = new LocalKeyword(shader, "_GPU_INSTANCER_BATCHER");

            var totalCommands = 0;
            // foreach (var lod in candidate.Lods)
                MeshRenderingData[] meshes = candidate.Lods[LodLevel].MeshRenderingDatas;
                foreach (MeshRenderingData mesh in meshes)
                    totalCommands += mesh.ToGPUInstancedRenderer().RenderParamsArray.Length;

            drawArgsBuffer?.Release();
            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            drawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            instanceBuffer?.Release();
            instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, candidate.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));

            // PerInstanceBuffer[] shiftedTRS = candidate.InstancesBuffer.ToArray();
            // for (var i = 0; i < shiftedTRS.Length; i++)
            // {
            //     PerInstanceBuffer pid = shiftedTRS[i];
            //     pid.instMatrix = baseMatrix * pid.instMatrix;
            //     shiftedTRS[i] = pid;
            // }

            instanceBuffer.SetData(candidate.InstancesBuffer, 0, 0, candidate.InstancesBuffer.Count);

            var currentCommandIndex = 0;
            // foreach (var lod in candidate.Lods)
            foreach (var mesh in meshes)
            {
                var instancedRenderer = mesh.ToGPUInstancedRenderer();
                int submeshCount = instancedRenderer.RenderParamsArray.Length;

                // Set commands and render
                for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    drawArgsCommandData[currentCommandIndex].indexCountPerInstance = instancedRenderer.Mesh.GetIndexCount(submeshIndex);
                    drawArgsCommandData[currentCommandIndex].instanceCount = (uint)candidate.InstancesBuffer.Count;
                    drawArgsCommandData[currentCommandIndex].startIndex = instancedRenderer.Mesh.GetIndexStart(submeshIndex);
                    drawArgsCommandData[currentCommandIndex].baseVertexIndex = 0;
                    drawArgsCommandData[currentCommandIndex].startInstance = 0;
                    drawArgsBuffer.SetData(drawArgsCommandData, currentCommandIndex, currentCommandIndex, count: 1);

                    RenderParams rparams = instancedRenderer.RenderParamsArray[submeshIndex];
                    // rparams.camera = Camera.current;
                    rparams.matProps = new MaterialPropertyBlock();
                    rparams.matProps.SetBuffer("_PerInstanceBuffer", instanceBuffer);

                    if (rparams.material.shader == shader)
                        rparams.material.EnableKeyword(gpuInstancingKeyword);
                    else
                        Debug.LogWarning($"material {rparams.material.name} has different shader {rparams.material.shader}");

                    Graphics.RenderMeshIndirect(rparams, instancedRenderer.Mesh, drawArgsBuffer, commandCount: 1, currentCommandIndex);
                    currentCommandIndex++;
                }
            }
        }

        private void RenderCandidateInstanced(GPUInstancingCandidate candidate)
        {
            foreach (MeshRenderingData meshRendering in candidate.Lods[LodLevel].MeshRenderingDatas)
            {
                GPUInstancedRenderer instancedRenderer = meshRendering.ToGPUInstancedRenderer();

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
