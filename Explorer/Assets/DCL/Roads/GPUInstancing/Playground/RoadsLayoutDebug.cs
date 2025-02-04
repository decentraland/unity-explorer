using DCL.Roads.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Utility;

namespace DCL.Roads.GPUInstancing.Playground
{
    [ExecuteAlways]
    public class RoadsLayoutDebug : MonoBehaviour
    {
        private GPUInstancingServiceOld gpuInstancingServiceOld;

        [Space]
        public RoadSettingsAsset RoadsConfig;
        public GPUInstancedPrefab[] Prefabs;

        [Space]
        public bool Run;

        [Header("DEBUG SETTINGS")]
        public int InstanceId;
        public bool HideRoadsVisual;

        public Vector2Int ParcelsMin;
        public Vector2Int ParcelsMax;

        [Header("DEBUG TABLE")]
        public GPUMeshDebug[] MeshesDebug;

        [HideInInspector]
        public Transform debugRoot;

        private void Awake()
        {
            if (Application.isPlaying)
                TransferFromConfigToService();
        }

        public void Update()
        {
            if (Run)
                gpuInstancingServiceOld.RenderInstancedBatched();
        }

        [ContextMenu("DEBUG - Cache Prefabs")]
        private void CachePrefabs()
        {
            var cachedPrefabs = new List<GPUInstancedPrefab>();

            foreach (AssetReferenceGameObject ar in RoadsConfig.RoadAssetsReference)
            {
                AsyncOperationHandle<GameObject> operation = ar.LoadAssetAsync<GameObject>();
                operation.WaitForCompletion();
                GameObject prefab = operation.Result;

                if (prefab != null)
                {
                    GPUInstancedPrefab gpuInstancedPrefabBeh = prefab.GetComponent<GPUInstancedPrefab>();
                    gpuInstancedPrefabBeh.CollectSelfData();

                    if (HideRoadsVisual)
                        gpuInstancedPrefabBeh.GetComponent<GPUInstancingPrefabData>().HideVisuals();
                    else
                        gpuInstancedPrefabBeh.GetComponent<GPUInstancingPrefabData>().ShowVisuals();

                    cachedPrefabs.Add(gpuInstancedPrefabBeh);
                }

                operation.Release();
            }

            Prefabs = cachedPrefabs.ToArray();
        }

        [ContextMenu("DEBUG - Collect Instances on Roads Config")]
        private void CollectAllMeshInstancesOnRoadsConfig()
        {
            // RoadsConfig.CollectGPUInstancingCandidates();
        }

        [ContextMenu("DEBUG - TransferFromConfigToService")]
        private void TransferFromConfigToService()
        {
            gpuInstancingServiceOld = new GPUInstancingServiceOld();
            // gpuInstancingService.AddToInstancingDirectCopy(RoadsConfig.RoadsMeshesGPUInstances);
            gpuInstancingServiceOld.PrepareBatches();
            CollectDebugInfo();
        }

        [ContextMenu("DEBUG - Spawn Roads")]
        private void SpawnRoads()
        {
            debugRoot = new GameObject("RoadsRoot").transform;
            debugRoot.gameObject.SetActive(false);

            foreach (RoadDescription roadDescription in RoadsConfig.RoadDescriptions)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;
                GPUInstancedPrefab gpuInstancedPrefab = Prefabs.FirstOrDefault(op => op.name == roadDescription.RoadModel);

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

        private void CollectDebugInfo()
        {
            MeshesDebug = gpuInstancingServiceOld.gpuInstancingMap.Select(propPair => new GPUMeshDebug
                                               {
                                                   Mesh = propPair.Key.Mesh,
                                                   Material1 = propPair.Key.RenderParamsArray[0].material,
                                                   InstancesCount = propPair.Value.Length,
                                               })
                                              .ToArray();
        }

        private bool IsOutOfRange(Vector2Int roadCoordinate)
        {
            Debug.Log(roadCoordinate.ToString());

            return roadCoordinate.x < ParcelsMin.x || roadCoordinate.x > ParcelsMax.x ||
                   roadCoordinate.y < ParcelsMin.y || roadCoordinate.y > ParcelsMax.y;
        }
    }

    [Serializable]
    public class GPUMeshDebug
    {
        public Mesh Mesh;
        public Material Material1;
        public int InstancesCount;
    }
}
