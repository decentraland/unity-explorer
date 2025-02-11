using DCL.Roads.GPUInstancing;
using DCL.Roads.GPUInstancing.Playground;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.Roads.GPUInstancingPlayground
{
    [ExecuteAlways]
    public class GPUInstancingRoadPrefabCombinedPlayground : MonoBehaviour
    {
        private readonly Dictionary<GPUInstancingLODGroup, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public GPUInstancingPrefabData prefab;

        [Min(0)] public int CandidateID;
        [Min(0)] public int LodRendererID;
        [Min(0)] public int LodLevel;

        [Space] public bool Run;

        public void Update()
        {
            if (!Run) return;

            foreach (var candidate in prefab.IndirectCandidates)
            {
                AdjustBuffers(candidate);
                RenderCandidateIndirect(candidate, candidatesBuffersTable[candidate]);
            }
        }

        private void RenderCandidateIndirect(GPUInstancingLODGroup candidate, GPUInstancingBuffers buffers)
        {
            var currentCommandIndex = 0;

                foreach (var combinedLodRenderer in candidate.CombinedLodsRenderers)
                {
                    // CombinedLodsRenderer combinedLodRenderer = lodGroup.CombinedLodsRenderers[LodRendererID];
                    var combinedMesh = combinedLodRenderer.CombinedMesh;

                    {
                        buffers.DrawArgsCommandData[currentCommandIndex].indexCountPerInstance = combinedMesh.GetIndexCount(LodLevel);
                        buffers.DrawArgsCommandData[currentCommandIndex].instanceCount = (uint)candidate.InstancesBuffer.Count;
                        buffers.DrawArgsCommandData[currentCommandIndex].startIndex =combinedMesh.GetIndexStart(LodLevel);
                        buffers.DrawArgsCommandData[currentCommandIndex].baseVertexIndex = combinedMesh.GetBaseVertex(LodLevel);;
                        buffers.DrawArgsCommandData[currentCommandIndex].startInstance = 0;
                        buffers.DrawArgsBuffer.SetData(buffers.DrawArgsCommandData, currentCommandIndex, currentCommandIndex, count: 1);

                        combinedLodRenderer.Initialize(instancingMaterials);
                        RenderParams rparams =  combinedLodRenderer.RenderParamsArray[0];
                        // rparams.camera = Camera.current;
                        rparams.matProps = new MaterialPropertyBlock();
                        rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.InstanceBuffer);

                        Graphics.RenderMeshIndirect(rparams, combinedMesh, buffers.DrawArgsBuffer, commandCount: 1, currentCommandIndex);
                        currentCommandIndex++;
                    }
                }
        }

        private void AdjustBuffers(GPUInstancingLODGroup lodGroup)
        {
            if (!candidatesBuffersTable.TryGetValue(lodGroup, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(lodGroup, buffers);
            }

            var totalCommands = lodGroup.CombinedLodsRenderers.Count;
            var mesh = lodGroup.CombinedLodsRenderers[LodRendererID];

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            // PerInstanceBuffer[] roadShiftInstancesBuffer = lodGroup.InstancesBuffer.ToArray();
            // for (var i = 0; i < roadShiftInstancesBuffer.Length; i++)
            // {
            //     PerInstanceBuffer pid = roadShiftInstancesBuffer[i];
            //     pid.instMatrix = baseMatrix * pid.instMatrix;
            //     roadShiftInstancesBuffer[i] = pid;
            // }

            buffers.InstanceBuffer?.Release();
            buffers.InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, lodGroup.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.InstanceBuffer.SetData(lodGroup.InstancesBuffer, 0, 0, lodGroup.InstancesBuffer.Count);
        }

        private void OnDisable()
        {
            // currentNane = string.Empty;
            // DestroyImmediate(originalInstance);
            // originalInstance = null;

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
}
