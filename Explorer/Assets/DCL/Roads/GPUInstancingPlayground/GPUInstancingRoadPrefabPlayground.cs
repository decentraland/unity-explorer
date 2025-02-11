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
        private readonly Dictionary<GPUInstancingCandidate_Old, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public GPUInstancingPrefabData_Old[] originalPrefabs;

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

                foreach (GPUInstancingCandidate_Old candidate in originalPrefabs[prefabId].indirectCandidates)
                    AdjustBuffers(candidate, baseMatrix);
            }

            if (UseIndirect)
            {
                foreach (GPUInstancingCandidate_Old candidate in originalPrefabs[prefabId].indirectCandidates)
                    RenderCandidateIndirect(candidate, candidatesBuffersTable[candidate]);

                foreach (GPUInstancingCandidate_Old candidate in originalPrefabs[prefabId].directCandidates)
                    RenderCandidateInstanced(candidate, baseMatrix);
            }
            else
            {
                if (RenderFullPrefab)
                    foreach (GPUInstancingCandidate_Old candidate in originalPrefabs[prefabId].indirectCandidates)
                        RenderCandidateInstanced(candidate, baseMatrix);
                else
                {
                    int candidateId = Mathf.Min(CandidateId, originalPrefabs[prefabId].indirectCandidates.Count - 1);
                    RenderCandidateInstanced(lodGroup: originalPrefabs[prefabId].indirectCandidates[candidateId], baseMatrix);
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

        private void AdjustBuffers(GPUInstancingCandidate_Old lodGroup, Matrix4x4 baseMatrix)
        {
            if (!candidatesBuffersTable.TryGetValue(lodGroup, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(lodGroup, buffers);
            }

            var totalCommands = 0;

            for (var lodId = 0; lodId < lodGroup.Lods_Old.Count; lodId++)
            {
                if(UseLodLevel) lodId = Mathf.Min(LodLevel, lodGroup.Lods_Old.Count - 1);
                MeshRenderingData_Old[] meshes = lodGroup.Lods_Old[lodId].MeshRenderingDatas;

                foreach (MeshRenderingData_Old mesh in meshes)
                {
                    mesh.Initialize(instancingMaterials);
                    totalCommands += mesh.RenderParamsArray.Length;
                }

                if (UseLodLevel) break;
            }

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            PerInstanceBuffer[] roadShiftInstancesBuffer = lodGroup.InstancesBuffer.ToArray();

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

        private void RenderCandidateIndirect(GPUInstancingCandidate_Old lodGroup, GPUInstancingBuffers buffers)
        {
            var currentCommandIndex = 0;

            foreach (var lod in lodGroup.Lods_Old)
            {
                MeshRenderingData_Old[] meshes = UseLodLevel? lodGroup.Lods_Old[Mathf.Min(LodLevel, lodGroup.Lods_Old.Count - 1)].MeshRenderingDatas : lod.MeshRenderingDatas;

                foreach (MeshRenderingData_Old mesh in meshes)
                {
                    int submeshCount = mesh.RenderParamsArray.Length;

                    // Set commands and render
                    for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                    {
                        buffers.DrawArgsCommandData[currentCommandIndex].indexCountPerInstance = mesh.SharedMesh.GetIndexCount(submeshIndex);
                        buffers.DrawArgsCommandData[currentCommandIndex].instanceCount = (uint)lodGroup.InstancesBuffer.Count;
                        buffers.DrawArgsCommandData[currentCommandIndex].startIndex = mesh.SharedMesh.GetIndexStart(submeshIndex);
                        buffers.DrawArgsCommandData[currentCommandIndex].baseVertexIndex = 0;
                        buffers.DrawArgsCommandData[currentCommandIndex].startInstance = 0;
                        buffers.DrawArgsBuffer.SetData(buffers.DrawArgsCommandData, currentCommandIndex, currentCommandIndex, count: 1);

                        RenderParams rparams = mesh.RenderParamsArray[submeshIndex];

                        // rparams.camera = Camera.current;
                        rparams.matProps = new MaterialPropertyBlock();
                        rparams.matProps.SetBuffer("_PerInstanceBuffer", buffers.InstanceBuffer);

                        Graphics.RenderMeshIndirect(rparams, mesh.SharedMesh, buffers.DrawArgsBuffer, commandCount: 1, currentCommandIndex);
                        currentCommandIndex++;
                    }
                }

                if (UseLodLevel) break;
            }
        }

        private void RenderCandidateInstanced(GPUInstancingCandidate_Old lodGroup, Matrix4x4 baseMatrix)
        {
            for (int lodId = 0; lodId < lodGroup.Lods_Old.Count; lodId++)
            {
                if(UseLodLevel) lodId = Mathf.Min(LodLevel, lodGroup.Lods_Old.Count - 1);
                foreach (MeshRenderingData_Old meshRendering in lodGroup.Lods_Old[lodId].MeshRenderingDatas)
                {
                    meshRendering.Initialize(instancingMaterials);

                    List<Matrix4x4> shiftedInstanceData = new (lodGroup.InstancesBuffer.Count);
                    shiftedInstanceData.AddRange(lodGroup.InstancesBuffer.Select(matrix => baseMatrix * matrix.instMatrix));

                    for (var i = 0; i < meshRendering.RenderParamsArray.Length; i++)
                        Graphics.RenderMeshInstanced(in meshRendering.RenderParamsArray[i], meshRendering.SharedMesh, i, shiftedInstanceData);
                }

                if (UseLodLevel) break;
            }
        }

        [ContextMenu(nameof(PrefabsSelfCollect))]
        private void PrefabsSelfCollect()
        {
            foreach (GPUInstancingPrefabData_Old prefab in originalPrefabs)
            {
                prefab.CollectSelfData();
                prefab.ShowVisuals();

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
#endif
            }
        }

        private void SpawnOriginalPrefab(GPUInstancingPrefabData_Old prefab, Vector2 pos)
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
