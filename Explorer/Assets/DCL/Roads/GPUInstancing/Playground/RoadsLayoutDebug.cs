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
        private readonly GPUInstancingService gpuInstancingService = new ();

        [Space]
        public RoadSettingsAsset RoadsConfig;
        public PrefabInstanceDataBehaviour[] Prefabs;

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
                PrepareInstancesMap();
        }

        public void Update()
        {
            if (Run) gpuInstancingService.RenderInstanced(InstanceId);
        }

        [ContextMenu("DEBUG - Prepare Instances Table")]
        private void PrepareInstancesMap()
        {
            gpuInstancingService.Clear();
            gpuInstancingService.AddToInstancing(debugRoot.GetComponentsInChildren<PrefabInstanceDataBehaviour>());
            CollectDebugInfo();
        }

        [ContextMenu("DEBUG - Cache Prefabs")]
        private void CachePrefabs()
        {
            var cachedPrefabs = new List<PrefabInstanceDataBehaviour>();

            foreach (AssetReferenceGameObject ar in RoadsConfig.RoadAssetsReference)
            {
                AsyncOperationHandle<GameObject> operation = ar.LoadAssetAsync<GameObject>();
                operation.WaitForCompletion();
                GameObject prefab = operation.Result;

                if (prefab != null)
                {
                    PrefabInstanceDataBehaviour prefabBeh = prefab.GetComponent<PrefabInstanceDataBehaviour>();
                    prefabBeh.CollectSelfData();

                    if (HideRoadsVisual)
                        prefabBeh.HideVisuals();

                    cachedPrefabs.Add(prefabBeh);
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

            // Spawn roadss
            foreach (RoadDescription roadDescription in RoadsConfig.RoadDescriptions)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;
                PrefabInstanceDataBehaviour prefab = Prefabs.FirstOrDefault(op => op.name == roadDescription.RoadModel);

                if (prefab != null)
                {
                    PrefabInstanceDataBehaviour roadAsset = Instantiate(prefab);

                    roadAsset.transform.localPosition = roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation;
                    roadAsset.transform.localRotation = roadDescription.Rotation;
                    roadAsset.gameObject.SetActive(true);

                    roadAsset.transform.parent = debugRoot;
                }
            }
        }

        private void CollectDebugInfo()
        {
            MeshesDebug = gpuInstancingService.gpuInstancingMap.Select(propPair => new GPUMeshDebug
                                               {
                                                   Mesh = propPair.Key.Mesh,
                                                   Material1 = propPair.Key.RenderParams[0].material,
                                                   InstancesCount = propPair.Value.Count,
                                               })
                                              .ToArray();
        }

        private bool IsOutOfRange(Vector2Int roadCoordinate) =>
            roadCoordinate.x < ParcelsMin.x || roadCoordinate.x > ParcelsMax.x ||
            roadCoordinate.y < ParcelsMin.y || roadCoordinate.y > ParcelsMax.y;
    }

    [Serializable]
    public class GPUMeshDebug
    {
        public Mesh Mesh;
        public Material Material1;
        public int InstancesCount;
    }
}
