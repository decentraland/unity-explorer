using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Utility;

namespace DCL.Roads.GPUInstancing
{
    [ExecuteAlways]
    public class GPUInstancingRoadPrefabPlayground : MonoBehaviour
    {
        private readonly Dictionary<GPUInstancingCandidate, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public GPUInstancingPrefabData[] originalPrefabs;

        [Min(0)] public int PrefabId;
        [Min(0)] public int CandidateId;
        [Min(0)] public int LodLevel;

        [Header("ROADS")]
        public RoadDescription[] Descriptions;
        public Vector2 comparisonShift;

        [Space]
        public bool RenderFullPrefab;
        public bool DisableMeshRenderers;
        public bool UseRoadShift;
        public bool UseLodLevel;
        public bool UseIndirect;

        [Space]
        public bool Run;

        private string currentNane;
        private GameObject originalInstance;

        public void Update()
        {
            if (!Run) return;

            int prefabId = Mathf.Min(PrefabId, originalPrefabs.Length - 1);

            Matrix4x4 baseMatrix = UseRoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            if (currentNane != originalPrefabs[prefabId].name)
            {
                currentNane = originalPrefabs[prefabId].name;
                SpawnOriginalPrefab(originalPrefabs[prefabId], comparisonShift);

                foreach (GPUInstancingCandidate candidate in originalPrefabs[prefabId].indirectCandidates)
                    AdjustBuffers(candidate, baseMatrix);
            }

            if (UseIndirect)
            {
                foreach (GPUInstancingCandidate candidate in originalPrefabs[prefabId].indirectCandidates)
                    RenderCandidateIndirect(candidate, candidatesBuffersTable[candidate]);

                foreach (GPUInstancingCandidate candidate in originalPrefabs[prefabId].directCandidates)
                    RenderCandidateInstanced(candidate, baseMatrix);
            }
            else
            {
                if (RenderFullPrefab)
                    foreach (GPUInstancingCandidate candidate in originalPrefabs[prefabId].indirectCandidates)
                        RenderCandidateInstanced(candidate, baseMatrix);
                else
                {
                    int candidateId = Mathf.Min(CandidateId, originalPrefabs[prefabId].indirectCandidates.Count - 1);
                    RenderCandidateInstanced(candidate: originalPrefabs[prefabId].indirectCandidates[candidateId], baseMatrix);
                }
            }
        }

        private void OnDisable()
        {
            currentNane = string.Empty;

            DestroyImmediate(originalInstance);
            originalInstance = null;

            foreach (GPUInstancingBuffers buffers in candidatesBuffersTable.Values)
            {
                buffers.InstanceBuffer.Dispose();
                buffers.InstanceBuffer = null;

                buffers.DrawArgsBuffer.Dispose();
                buffers.DrawArgsBuffer = null;
            }

            candidatesBuffersTable.Clear();
        }

        [ContextMenu(nameof(LogMaterialsAmount))]
        private void LogMaterialsAmount()
        {
            Debug.Log(instancingMaterials.Count);
        }

        private void AdjustBuffers(GPUInstancingCandidate candidate, Matrix4x4 baseMatrix)
        {
            if (!candidatesBuffersTable.TryGetValue(candidate, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(candidate, buffers);
            }

            var totalCommands = 0;

            for (var lodId = 0; lodId < candidate.Lods.Count; lodId++)
            {
                if(UseLodLevel) lodId = Mathf.Min(LodLevel, candidate.Lods.Count - 1);
                MeshRenderingData[] meshes = candidate.Lods[lodId].MeshRenderingDatas;

                foreach (MeshRenderingData mesh in meshes) { totalCommands += mesh.ToGPUInstancedRenderer(instancingMaterials).RenderParamsArray.Length; }
                if (UseLodLevel) break;
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
            var currentCommandIndex = 0;

            foreach (var lod in candidate.Lods)
            {
                MeshRenderingData[] meshes = UseLodLevel? candidate.Lods[Mathf.Min(LodLevel, candidate.Lods.Count - 1)].MeshRenderingDatas : lod.MeshRenderingDatas;

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

                if (UseLodLevel) break;
            }
        }

        private void RenderCandidateInstanced(GPUInstancingCandidate candidate, Matrix4x4 baseMatrix)
        {
            for (int lodId = 0; lodId < candidate.Lods.Count; lodId++)
            {
                if(UseLodLevel) lodId = Mathf.Min(LodLevel, candidate.Lods.Count - 1);
                foreach (MeshRenderingData meshRendering in candidate.Lods[lodId].MeshRenderingDatas)
                {
                    var instancedRenderer = meshRendering.ToGPUInstancedRenderer(instancingMaterials);

                    List<Matrix4x4> shiftedInstanceData = new (candidate.InstancesBuffer.Count);
                    shiftedInstanceData.AddRange(candidate.InstancesBuffer.Select(matrix => baseMatrix * matrix.instMatrix));

                    for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                        Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], instancedRenderer.Mesh, i, shiftedInstanceData);
                }

                if (UseLodLevel) break;
            }
        }

        [ContextMenu(nameof(PrefabsSelfCollect))]
        private void PrefabsSelfCollect()
        {
            foreach (GPUInstancingPrefabData prefab in originalPrefabs)
            {
                prefab.CollectSelfData();
                prefab.ShowVisuals();

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
#endif
            }
        }

        private void SpawnOriginalPrefab(GPUInstancingPrefabData prefab, Vector2 pos)
        {
            if (DisableMeshRenderers)
                prefab.HideVisuals();
            else
                prefab.ShowVisuals();

            if (originalInstance == null || originalInstance.name != prefab.name)
            {
                if (originalInstance != null)
                    DestroyImmediate(originalInstance);

                originalInstance = Instantiate(prefab.gameObject);
                originalInstance.name = prefab.name;
                originalInstance.transform.Translate(pos.x, 0, pos.y);
            }

            prefab.CollectSelfData();
        }
    }
}
