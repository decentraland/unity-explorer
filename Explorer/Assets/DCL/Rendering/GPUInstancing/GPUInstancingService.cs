using DCL.Roads.GPUInstancing.Playground;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using Utility;

namespace DCL.Roads.GPUInstancing
{
    public class GPUInstancingService : IDisposable
    {
        private const float STREET_MAX_HEIGHT = 10f;
        private static readonly Bounds RENDER_PARAMS_WORLD_BOUNDS =
            new (Vector3.zero, new Vector3(GenesisCityData.EXTENTS.x * ParcelMathHelper.PARCEL_SIZE, STREET_MAX_HEIGHT, GenesisCityData.EXTENTS.y * ParcelMathHelper.PARCEL_SIZE));

        private readonly Dictionary<GPUInstancingLODGroupWithBuffer, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public void Dispose()
        {
            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
                buffers.Dispose();

            candidatesBuffersTable.Clear();
            instancingMaterials.Clear();
        }

        public void RenderIndirect()
        {
            foreach ((GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers) in candidatesBuffersTable)
                RenderCandidateIndirect(candidate, buffers);
        }

        private static void RenderCandidateIndirect(GPUInstancingLODGroupWithBuffer candidate, GPUInstancingBuffers buffers)
        {
            for (var i = 0; i < candidate.LODGroup.CombinedLodsRenderers.Count; i++)
            {
                CombinedLodsRenderer combinedLodRenderer = candidate.LODGroup.CombinedLodsRenderers[i];
                int lodCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

                Graphics.RenderMeshIndirect(combinedLodRenderer.RenderParamsArray[0], combinedLodRenderer.CombinedMesh, buffers.DrawArgs[i], commandCount: lodCount);
            }
        }

        public void AddToIndirect(List<GPUInstancingLODGroupWithBuffer> candidates)
        {
            foreach (GPUInstancingLODGroupWithBuffer candidate in candidates)
                AddToIndirect(candidate);
        }

        private void AddToIndirect(GPUInstancingLODGroupWithBuffer candidate)
        {
            if (!candidatesBuffersTable.TryGetValue(candidate, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(candidate, buffers);
            }

            int _nInstanceCount = candidate.InstancesBuffer.Count;
            int _nLODCount = candidate.LODGroup.LodsScreenSpaceSizes.Length;

            buffers.LODLevels = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, sizeof(uint) * 4);

            {
                buffers.InstanceLookUpAndDither = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount * _nLODCount, sizeof(uint) * 2);
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
                    }
                }
                buffers.InstanceLookUpAndDither.SetData(natArray, 0, 0, natArray.Length);
            }

            // TODO : set flag to Lock
            buffers.PerInstanceMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, _nInstanceCount, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.PerInstanceMatrices.SetData(candidate.InstancesBuffer, 0, 0, _nInstanceCount);

            buffers.DrawArgs = new List<GraphicsBuffer>();
            buffers.DrawArgsCommandData = new List<GraphicsBuffer.IndirectDrawIndexedArgs[]>();
            foreach (var combinedLodRenderer in candidate.LODGroup.CombinedLodsRenderers)
            {
                Mesh combinedMesh = combinedLodRenderer.CombinedMesh;

                if (combinedMesh == null)
                {
                    Debug.LogWarning($"{candidate.Name} has combined lod renderer equal to null for material {combinedLodRenderer.SharedMaterial.name}", candidate.LODGroup.Reference.gameObject);
                    continue;
                }

                var drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, _nLODCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                var drawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[_nLODCount];


                for (var lodLevel = 0; lodLevel < _nLODCount; lodLevel++)
                {
                    if (lodLevel > combinedMesh.subMeshCount - 1)
                    {
                        drawArgsCommandData[lodLevel].instanceCount = 0;
                        continue;
                    }

                    drawArgsCommandData[lodLevel].indexCountPerInstance = combinedMesh.GetIndexCount(lodLevel);
                    drawArgsCommandData[lodLevel].instanceCount = lodLevel == 0 ? (uint)candidate.InstancesBuffer.Count : 0;
                    drawArgsCommandData[lodLevel].startIndex = combinedMesh.GetIndexStart(lodLevel);
                    drawArgsCommandData[lodLevel].baseVertexIndex = combinedMesh.GetBaseVertex(lodLevel);
                    drawArgsCommandData[lodLevel].startInstance = (uint)lodLevel * (uint)candidate.InstancesBuffer.Count;
                }

                drawArgsBuffer.SetData(drawArgsCommandData, 0, 0, count: _nLODCount);

                combinedLodRenderer.InitializeRenderParams(instancingMaterials);
                ref RenderParams rparams = ref combinedLodRenderer.RenderParamsArray[0];

                // rparams.camera = Camera.current;
                rparams.worldBounds = RENDER_PARAMS_WORLD_BOUNDS;
                rparams.matProps = new MaterialPropertyBlock();
                rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.PerInstanceMatrices);
                rparams.matProps.SetBuffer("_PerInstanceLookUpAndDitherBuffer", buffers.InstanceLookUpAndDither);

                buffers.DrawArgs.Add(drawArgsBuffer);
                buffers.DrawArgsCommandData.Add(drawArgsCommandData);
            }
        }

        public void Remove(List<GPUInstancingLODGroupWithBuffer> candidates)
        {
            foreach (GPUInstancingLODGroupWithBuffer candidate in candidates)
                Remove(candidate);
        }

        private void Remove(GPUInstancingLODGroupWithBuffer lodGroup)
        {
            if (lodGroup == null) return;

            if (candidatesBuffersTable.TryGetValue(lodGroup, out GPUInstancingBuffers buffers))
            {
                buffers.Dispose();
                candidatesBuffersTable.Remove(lodGroup);
            }
        }
    }

    public class GPUInstancingBuffers : IDisposable
    {
        public GraphicsBuffer PerInstanceMatrices;
        public List<GraphicsBuffer> DrawArgs;
        public List<GraphicsBuffer.IndirectDrawIndexedArgs[]> DrawArgsCommandData;

        public GraphicsBuffer LODLevels;
        public GraphicsBuffer InstanceLookUpAndDither;

        public void Dispose()
        {
            LODLevels?.Dispose();
            LODLevels = null;

            InstanceLookUpAndDither?.Dispose();
            InstanceLookUpAndDither = null;

            PerInstanceMatrices?.Dispose();
            PerInstanceMatrices = null;

            foreach (GraphicsBuffer drawArg in DrawArgs)
                drawArg?.Dispose();

            DrawArgs.Clear();
            DrawArgs = null;
        }
    }
}
