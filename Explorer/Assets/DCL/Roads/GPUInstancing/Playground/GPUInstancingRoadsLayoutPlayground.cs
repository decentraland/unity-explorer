using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [ExecuteAlways]
    public class GPUInstancingRoadsLayoutPlayground : MonoBehaviour
    {
        private readonly Dictionary<GPUInstancingCandidate, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public RoadSettingsAsset RoadsConfig;
        public GPUInstancedPrefab[] Prefabs;
        [Space] public List<GPUInstancingCandidate> Candidates;

        [Space] public Transform roadsRoot;
        public Vector2Int ParcelsMin;
        public Vector2Int ParcelsMax;


        [Min(0)] public int LodLevel;
        public bool cached;
        [Space] public bool Run;

        public void Update()
        {
            if (!Run) return;

            if (!cached)
            {
                cached = true;
                TransferFromConfigToService();
            }

            foreach (var candidateWithBuffers in candidatesBuffersTable)
                RenderCandidateIndirect(candidateWithBuffers.Key, candidateWithBuffers.Value);

            foreach (GPUInstancingCandidate directCandidate in RoadsConfig.DirectCandidates)
                RenderCandidateInstanced(directCandidate);
        }

        private void RenderCandidateInstanced(GPUInstancingCandidate candidate)
        {
            foreach (var lod in candidate.Lods)
            foreach (MeshRenderingData meshRendering in lod.MeshRenderingDatas)
            {
                var instancedRenderer = meshRendering.ToGPUInstancedRenderer(instancingMaterials);
                List<Matrix4x4> shiftedInstanceData = new (candidate.InstancesBuffer.Count);
                shiftedInstanceData.AddRange(candidate.InstancesBuffer.Select(matrix => matrix.instMatrix));

                for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], instancedRenderer.Mesh, i, shiftedInstanceData);
            }
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

        private void OnDisable()
        {
            DestroyImmediate(roadsRoot);
            roadsRoot = null;
            cached = false;

            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
            {
                buffers.InstanceBuffer.Dispose();
                buffers.InstanceBuffer = null;

                buffers.DrawArgsBuffer.Dispose();
                buffers.DrawArgsBuffer = null;
            }

            Candidates.Clear();
            candidatesBuffersTable.Clear();
        }

        [ContextMenu("DEBUG - Collect Instances on Roads Config")]
        private void CollectAllMeshInstancesOnRoadsConfig()
        {
            RoadsConfig.CollectGPUInstancingCandidates(ParcelsMin, ParcelsMax);
        }

        [ContextMenu("DEBUG - TransferFromConfigToService")]
        private void TransferFromConfigToService()
        {
            Candidates.Clear();
            candidatesBuffersTable.Clear();

            foreach (GPUInstancingCandidate candidate in RoadsConfig.IndirectCandidates)
                AdjustBuffers(candidate);

            foreach (var candidate in candidatesBuffersTable.Keys)
                Candidates.Add(new GPUInstancingCandidate(candidate));
        }

        private void AdjustBuffers(GPUInstancingCandidate candidate)
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

            foreach (MeshRenderingData mesh in meshes) { totalCommands += mesh.ToGPUInstancedRenderer(instancingMaterials).RenderParamsArray.Length; }

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            buffers.InstanceBuffer?.Release();
            buffers.InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,  candidate.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.InstanceBuffer.SetData( candidate.InstancesBuffer, 0, 0,  candidate.InstancesBuffer.Count);
        }
    }
}
