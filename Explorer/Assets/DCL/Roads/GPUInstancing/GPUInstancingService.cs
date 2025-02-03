using DCL.Roads.GPUInstancing.Playground;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.Roads.GPUInstancing
{
    public class GPUInstancingService
    {
        private readonly Dictionary<GPUInstancingCandidate, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public void RenderIndirect()
        {
            foreach ((GPUInstancingCandidate candidate, GPUInstancingBuffers buffers) in candidatesBuffersTable)
                RenderCandidateIndirect(candidate, buffers);
        }

        private void RenderCandidateIndirect(GPUInstancingCandidate candidate, GPUInstancingBuffers buffers)
        {
            var currentCommandIndex = 0;

            foreach (GPUInstancingLodLevel lod in candidate.Lods)
            foreach (MeshRenderingData mesh in lod.MeshRenderingDatas)
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

        public void Add(List<GPUInstancingCandidate> candidates)
        {
            foreach (GPUInstancingCandidate candidate in candidates)
                Add(candidate);
        }

        private void Add(GPUInstancingCandidate candidate)
        {
            if (!candidatesBuffersTable.TryGetValue(candidate, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(candidate, buffers);
            }

            var totalCommands = 0;

            foreach (GPUInstancingLodLevel lodLevel in candidate.Lods)
            foreach (MeshRenderingData mesh in lodLevel.MeshRenderingDatas)
                totalCommands += mesh.ToGPUInstancedRenderer(instancingMaterials).RenderParamsArray.Length;

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            buffers.InstanceBuffer?.Release();
            buffers.InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, candidate.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.InstanceBuffer.SetData(candidate.InstancesBuffer, 0, 0, candidate.InstancesBuffer.Count);
        }

        public void Clear()
        {
            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
            {
                buffers.InstanceBuffer.Dispose();
                buffers.InstanceBuffer = null;

                buffers.DrawArgsBuffer.Dispose();
                buffers.DrawArgsBuffer = null;
            }

            candidatesBuffersTable.Clear();
        }
    }

    public class GPUInstancingBuffers
    {
        public GraphicsBuffer InstanceBuffer;
        public GraphicsBuffer DrawArgsBuffer;
        public GraphicsBuffer.IndirectDrawIndexedArgs[] DrawArgsCommandData;
    }
}
