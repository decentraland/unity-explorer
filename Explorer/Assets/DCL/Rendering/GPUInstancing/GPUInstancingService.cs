using DCL.Roads.GPUInstancing.Playground;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.Roads.GPUInstancing
{
    public class GPUInstancingService : IDisposable
    {
        private readonly Dictionary<GPUInstancingCandidate, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        private readonly List<GPUInstancingCandidate> directCandidates = new ();

        public void RenderIndirect()
        {
            foreach ((GPUInstancingCandidate candidate, GPUInstancingBuffers buffers) in candidatesBuffersTable)
                RenderCandidateIndirect(candidate, buffers);
        }

        private static void RenderCandidateIndirect(GPUInstancingCandidate candidate, GPUInstancingBuffers buffers)
        {
            var currentCommandIndex = 0;

            foreach (GPUInstancingLodLevel lod in candidate.Lods)
            foreach (MeshRenderingData meshData in lod.MeshRenderingDatas)
            {
                int submeshCount = meshData.RenderParamsArray.Length;

                // Set commands and render
                for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    buffers.DrawArgsCommandData[currentCommandIndex].indexCountPerInstance = meshData.SharedMesh.GetIndexCount(submeshIndex);
                    buffers.DrawArgsCommandData[currentCommandIndex].instanceCount = (uint)candidate.InstancesBuffer.Count;
                    buffers.DrawArgsCommandData[currentCommandIndex].startIndex = meshData.SharedMesh.GetIndexStart(submeshIndex);
                    buffers.DrawArgsCommandData[currentCommandIndex].baseVertexIndex = 0;
                    buffers.DrawArgsCommandData[currentCommandIndex].startInstance = 0;
                    buffers.DrawArgsBuffer.SetData(buffers.DrawArgsCommandData, currentCommandIndex, currentCommandIndex, count: 1);

                    RenderParams rparams = meshData.RenderParamsArray[submeshIndex];

                    // rparams.camera = Camera.current;
                    rparams.matProps = new MaterialPropertyBlock();
                    rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.InstanceBuffer);
                    // rparams.matProps.SetMatrix("_LocalShift", meshData.Renderer.localToWorldMatrix);

                    Graphics.RenderMeshIndirect(rparams, meshData.SharedMesh, buffers.DrawArgsBuffer, commandCount: 1, currentCommandIndex);
                    currentCommandIndex++;
                }
            }
        }

        public void AddToIndirect(List<GPUInstancingCandidate> candidates)
        {
            foreach (GPUInstancingCandidate candidate in candidates)
                AddToIndirect(candidate);
        }

        private void AddToIndirect(GPUInstancingCandidate candidate)
        {
            if (!candidatesBuffersTable.TryGetValue(candidate, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(candidate, buffers);
            }

            var totalCommands = 0;

            foreach (GPUInstancingLodLevel lodLevel in candidate.Lods)
            foreach (MeshRenderingData mesh in lodLevel.MeshRenderingDatas)
            {
                mesh.Initialize(instancingMaterials);
                totalCommands += mesh.RenderParamsArray.Length; // i.e. sub-meshes
            }

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            buffers.InstanceBuffer?.Release();
            buffers.InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, candidate.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.InstanceBuffer.SetData(candidate.InstancesBuffer, 0, 0, candidate.InstancesBuffer.Count);
        }

        public void Remove(List<GPUInstancingCandidate> candidates)
        {
            foreach (GPUInstancingCandidate candidate in candidates)
                Remove(candidate);
        }

        private void Remove(GPUInstancingCandidate candidate)
        {
            if (candidate == null) return;

            if (candidatesBuffersTable.TryGetValue(candidate, out GPUInstancingBuffers buffers))
            {
                buffers.Dispose();
                candidatesBuffersTable.Remove(candidate);
            }
        }

        public void RenderDirect()
        {
            foreach (GPUInstancingCandidate candidate in directCandidates)
                RenderCandidateDirect(candidate);
        }

        private static void RenderCandidateDirect(GPUInstancingCandidate candidate)
        {
            foreach (GPUInstancingLodLevel lod in candidate.Lods)
            foreach (MeshRenderingData meshRendering in lod.MeshRenderingDatas)
            {
                for (var i = 0; i < meshRendering.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in meshRendering.RenderParamsArray[i], meshRendering.SharedMesh, i, candidate.InstancesBufferDirect);
            }
        }

        public void AddToDirect(List<GPUInstancingCandidate> candidates)
        {
            directCandidates.AddRange(candidates);

            foreach (GPUInstancingCandidate candidate in candidates)
            {
                candidate.PopulateDirectInstancingBuffer();

                foreach (GPUInstancingLodLevel lodLevel in candidate.Lods)
                foreach (MeshRenderingData mesh in lodLevel.MeshRenderingDatas)
                    mesh.Initialize(instancingMaterials);
            }
        }

        public void Dispose()
        {
            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
                buffers.Dispose();

            candidatesBuffersTable.Clear();
            instancingMaterials.Clear();

            directCandidates.Clear();
        }
    }

    public class GPUInstancingBuffers : IDisposable
    {
        public GraphicsBuffer InstanceBuffer;
        public GraphicsBuffer DrawArgsBuffer;
        public GraphicsBuffer.IndirectDrawIndexedArgs[] DrawArgsCommandData;

        public void Dispose()
        {
            InstanceBuffer?.Release();
            InstanceBuffer?.Dispose();
            InstanceBuffer = null;

            DrawArgsBuffer?.Release();
            DrawArgsBuffer?.Dispose();
            DrawArgsBuffer = null;
        }
    }
}
