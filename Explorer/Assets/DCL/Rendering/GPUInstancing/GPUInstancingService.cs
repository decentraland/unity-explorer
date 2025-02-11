using DCL.Roads.GPUInstancing.Playground;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Utility;

namespace DCL.Roads.GPUInstancing
{
    public class GPUInstancingService : IDisposable
    {
        private const float STREET_MAX_HEIGHT = 10f;
        private static readonly Bounds RENDER_PARAMS_WORLD_BOUNDS =
            new (Vector3.zero, new Vector3(GenesisCityData.EXTENTS.x * ParcelMathHelper.PARCEL_SIZE, STREET_MAX_HEIGHT, GenesisCityData.EXTENTS.y * ParcelMathHelper.PARCEL_SIZE));

        private readonly Dictionary<GPUInstancingCandidate_Old, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        private readonly List<GPUInstancingCandidate_Old> directCandidates = new ();

        public void Dispose()
        {
            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
                buffers.Dispose();

            candidatesBuffersTable.Clear();
            instancingMaterials.Clear();

            directCandidates.Clear();
        }

        public void RenderIndirect()
        {
            foreach ((GPUInstancingCandidate_Old candidate, GPUInstancingBuffers buffers) in candidatesBuffersTable)
                RenderCandidateIndirect(candidate, buffers);
        }

        private static void RenderCandidateIndirect(GPUInstancingCandidate_Old lodGroup, GPUInstancingBuffers buffers)
        {
            var currentCommandIndex = 0;

            foreach (GPUInstancingLodLevel_Old lod in lodGroup.Lods_Old)
            foreach (MeshRenderingData_Old meshData in lod.MeshRenderingDatas)
            {
                int submeshCount = meshData.RenderParamsArray.Length;

                // Set commands and render
                for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    buffers.DrawArgsCommandData[currentCommandIndex].indexCountPerInstance = meshData.SharedMesh.GetIndexCount(submeshIndex);
                    buffers.DrawArgsCommandData[currentCommandIndex].instanceCount = (uint)lodGroup.InstancesBuffer.Count;
                    buffers.DrawArgsCommandData[currentCommandIndex].startIndex = meshData.SharedMesh.GetIndexStart(submeshIndex);
                    buffers.DrawArgsCommandData[currentCommandIndex].baseVertexIndex = 0;
                    buffers.DrawArgsCommandData[currentCommandIndex].startInstance = 0;
                    buffers.DrawArgsBuffer.SetData(buffers.DrawArgsCommandData, currentCommandIndex, currentCommandIndex, count: 1);

                    RenderParams rparams = meshData.RenderParamsArray[submeshIndex];

                    rparams.matProps = new MaterialPropertyBlock();
                    rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.InstanceBuffer);
                    rparams.worldBounds = RENDER_PARAMS_WORLD_BOUNDS;
                    // rparams.camera = Camera.current;

                    // rparams.matProps.SetMatrix("_LocalShift", meshData.Renderer.localToWorldMatrix);
                    Graphics.RenderMeshIndirect(rparams, meshData.SharedMesh, buffers.DrawArgsBuffer, commandCount: 1, currentCommandIndex);
                    currentCommandIndex++;
                }
            }
        }

        public void AddToIndirect(List<GPUInstancingCandidate_Old> candidates)
        {
            foreach (GPUInstancingCandidate_Old candidate in candidates)
                AddToIndirect(candidate);
        }

        private void AddToIndirect(GPUInstancingCandidate_Old lodGroup)
        {
            if (!candidatesBuffersTable.TryGetValue(lodGroup, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(lodGroup, buffers);
            }

            var totalCommands = 0;

            foreach (GPUInstancingLodLevel_Old lodLevel in lodGroup.Lods_Old)
            foreach (MeshRenderingData_Old mesh in lodLevel.MeshRenderingDatas)
            {
                mesh.Initialize(instancingMaterials);
                totalCommands += mesh.RenderParamsArray.Length; // i.e. sub-meshes
            }

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            buffers.InstanceBuffer?.Release();
            buffers.InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, lodGroup.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.InstanceBuffer.SetData(lodGroup.InstancesBuffer, 0, 0, lodGroup.InstancesBuffer.Count);
        }

        public void Remove(List<GPUInstancingCandidate_Old> candidates)
        {
            foreach (GPUInstancingCandidate_Old candidate in candidates)
                Remove(candidate);
        }

        private void Remove(GPUInstancingCandidate_Old lodGroup)
        {
            if (lodGroup == null) return;

            if (candidatesBuffersTable.TryGetValue(lodGroup, out GPUInstancingBuffers buffers))
            {
                buffers.Dispose();
                candidatesBuffersTable.Remove(lodGroup);
            }
        }

        public void RenderDirect()
        {
            foreach (GPUInstancingCandidate_Old candidate in directCandidates)
                RenderCandidateDirect(candidate);
        }

        private static void RenderCandidateDirect(GPUInstancingCandidate_Old lodGroup)
        {
            foreach (GPUInstancingLodLevel_Old lod in lodGroup.Lods_Old)
            foreach (MeshRenderingData_Old meshRendering in lod.MeshRenderingDatas)
            {
                for (var i = 0; i < meshRendering.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in meshRendering.RenderParamsArray[i], meshRendering.SharedMesh, i, lodGroup.InstancesBufferDirect);
            }
        }

        public void AddToDirect(List<GPUInstancingCandidate_Old> candidates)
        {
            directCandidates.AddRange(candidates);

            foreach (GPUInstancingCandidate_Old candidate in candidates)
            {
                candidate.PopulateDirectInstancingBuffer();

                foreach (GPUInstancingLodLevel_Old lodLevel in candidate.Lods_Old)
                foreach (MeshRenderingData_Old mesh in lodLevel.MeshRenderingDatas)
                    mesh.Initialize(instancingMaterials);
            }
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
