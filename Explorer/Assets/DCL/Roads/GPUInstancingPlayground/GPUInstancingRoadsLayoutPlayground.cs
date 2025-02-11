using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Utility;

namespace DCL.Roads.GPUInstancing.Playground
{
    [ExecuteAlways]
    public class GPUInstancingRoadsLayoutPlayground : MonoBehaviour
    {
        private readonly Dictionary<GPUInstancingCandidate_Old, GPUInstancingBuffers> candidatesBuffersTable = new ();
        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public RoadSettingsAsset RoadsConfig;
        public GPUInstancingPrefabData_Old[] Prefabs;

        [Space] public List<GPUInstancingCandidate_Old> Candidates;

        [Space] public Transform roadsRoot;
        public bool HideRoadsVisual;

        public Vector2Int ParcelsMin;
        public Vector2Int ParcelsMax;

        [HideInInspector]
        public Transform debugRoot;


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

            foreach (GPUInstancingCandidate_Old directCandidate in RoadsConfig.DirectCandidates)
                RenderCandidateInstanced(directCandidate);
        }

        private void RenderCandidateInstanced(GPUInstancingCandidate_Old lodGroup)
        {
            foreach (var lod in lodGroup.Lods_Old)
            foreach (MeshRenderingData_Old meshRendering in lod.MeshRenderingDatas)
            {
                meshRendering.Initialize(instancingMaterials);

                List<Matrix4x4> shiftedInstanceData = new (lodGroup.InstancesBuffer.Count);
                shiftedInstanceData.AddRange(lodGroup.InstancesBuffer.Select(matrix => matrix.instMatrix));

                for (var i = 0; i < meshRendering.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in meshRendering.RenderParamsArray[i], meshRendering.SharedMesh, i, shiftedInstanceData);
            }
        }

        private void RenderCandidateIndirect(GPUInstancingCandidate_Old lodGroup, GPUInstancingBuffers buffers)
        {
            int lodLevel = Mathf.Min(LodLevel, lodGroup.Lods_Old.Count - 1);
            MeshRenderingData_Old[] meshes = lodGroup.Lods_Old[lodLevel].MeshRenderingDatas;
            var currentCommandIndex = 0;

            // foreach (var lod in candidate.Lods)
            foreach (MeshRenderingData_Old mesh in meshes)
            {
                mesh.Initialize(instancingMaterials);

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

        [ContextMenu("DEBUG - TransferFromConfigToService")]
        private void TransferFromConfigToService()
        {
            Candidates.Clear();
            candidatesBuffersTable.Clear();

            foreach (GPUInstancingCandidate_Old candidate in RoadsConfig.IndirectCandidates)
                AdjustBuffers(candidate);

            foreach (var candidate in candidatesBuffersTable.Keys)
                Candidates.Add(new GPUInstancingCandidate_Old(candidate));
        }

        private void AdjustBuffers(GPUInstancingCandidate_Old lodGroup)
        {
            if (!candidatesBuffersTable.TryGetValue(lodGroup, out GPUInstancingBuffers buffers))
            {
                buffers = new GPUInstancingBuffers();
                candidatesBuffersTable.Add(lodGroup, buffers);
            }

            var totalCommands = 0;

            // foreach (var lod in candidate.Lods)
            int lodLevel = Mathf.Min(LodLevel, lodGroup.Lods_Old.Count - 1);
            MeshRenderingData_Old[] meshes = lodGroup.Lods_Old[lodLevel].MeshRenderingDatas;

            foreach (MeshRenderingData_Old mesh in meshes)
            {
                mesh.Initialize(instancingMaterials);
                totalCommands += mesh.RenderParamsArray.Length;
            }

            buffers.DrawArgsBuffer?.Release();
            buffers.DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            buffers.DrawArgsCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            buffers.InstanceBuffer?.Release();
            buffers.InstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,  lodGroup.InstancesBuffer.Count, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            buffers.InstanceBuffer.SetData( lodGroup.InstancesBuffer, 0, 0,  lodGroup.InstancesBuffer.Count);
        }

        [ContextMenu("DEBUG - Cache Prefabs")]
        private void CachePrefabs()
        {
            var cachedPrefabs = new List<GPUInstancingPrefabData_Old>();

            foreach (AssetReferenceGameObject ar in RoadsConfig.RoadAssetsReference)
            {
                AsyncOperationHandle<GameObject> operation = ar.LoadAssetAsync<GameObject>();
                operation.WaitForCompletion();
                GameObject prefab = operation.Result;

                if (prefab != null)
                {
                    GPUInstancingPrefabData_Old gpuInstancedPrefabBeh = prefab.GetComponent<GPUInstancingPrefabData_Old>();
                    gpuInstancedPrefabBeh.CollectSelfData();

                    if (HideRoadsVisual)
                        gpuInstancedPrefabBeh.HideVisuals();
                    else
                        gpuInstancedPrefabBeh.ShowVisuals();

                    cachedPrefabs.Add(gpuInstancedPrefabBeh);
                }

                operation.Release();
            }

            Prefabs = cachedPrefabs.ToArray();
        }

        [ContextMenu("DEBUG - Spawn Roads")]
        private void SpawnRoads()
        {
            debugRoot = new GameObject("RoadsRoot").transform;
            debugRoot.gameObject.SetActive(false);

            foreach (RoadDescription roadDescription in RoadsConfig.RoadDescriptions)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;
                GPUInstancingPrefabData_Old gpuInstancedPrefab = Prefabs.FirstOrDefault(op => op.name == roadDescription.RoadModel);

                if (gpuInstancedPrefab == null)
                {
                    Debug.LogWarning($"Can't find prefab {roadDescription.RoadModel}");
                    continue;
                }

                Transform roadAsset =
                    Instantiate(gpuInstancedPrefab, roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, roadDescription.Rotation, debugRoot)
                       .transform;

                roadAsset.gameObject.SetActive(true);
            }
        }

        private bool IsOutOfRange(Vector2Int roadCoordinate)
        {
            Debug.Log(roadCoordinate.ToString());

            return roadCoordinate.x < ParcelsMin.x || roadCoordinate.x > ParcelsMax.x ||
                   roadCoordinate.y < ParcelsMin.y || roadCoordinate.y > ParcelsMax.y;
        }
    }
}
