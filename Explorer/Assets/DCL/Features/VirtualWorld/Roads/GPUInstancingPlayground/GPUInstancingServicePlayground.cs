using DCL.Quality;
using DCL.Rendering.GPUInstancing;
using DCL.Rendering.GPUInstancing.InstancingData;
using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Utility;

namespace DCL.Roads.GPUInstancing
{
    [ExecuteAlways]
    public class GPUInstancingServicePlayground : MonoBehaviour
    {
        public Transform roadsRoot;
        public RoadSettingsAsset RoadsConfig;

        public GPUInstancingPrefabData[] originalPrefabs;
        public GameObject[] Prefabs;

        [Min(0)] public int PrefabId;

        public Vector2Int ParcelsMin;
        public Vector2Int ParcelsMax;

        public bool OnePrefabDebug;
        [Space] public bool Run;
        private GPUInstancingService instancingService;

        private int currentPrefabId;

        private void Awake()
        {
            AddRoadsToService();
        }

        public void Update()
        {
            if (!Run) return;

            if (OnePrefabDebug && currentPrefabId != Mathf.Min(PrefabId, originalPrefabs.Length - 1))
                AddPrefabToService();

            instancingService.RenderIndirect();
        }

        private void OnEnable()
        {
            var rendererFeaturesCache = new RendererFeaturesCache();
            GPUInstancingRenderFeature renderFeature = rendererFeaturesCache.GetRendererFeature<GPUInstancingRenderFeature>();
            instancingService = new GPUInstancingService(renderFeature.Settings);
        }

        private void OnDisable()
        {
            instancingService.Dispose();

            DestroyImmediate(roadsRoot);
            roadsRoot = null;
        }

        [ContextMenu(nameof(AddPrefabToService))]
        private void AddPrefabToService()
        {
            instancingService.Dispose();
            currentPrefabId = Mathf.Min(PrefabId, originalPrefabs.Length - 1);
            instancingService.AddToIndirect(originalPrefabs[currentPrefabId].IndirectCandidates);
        }

        [ContextMenu(nameof(RoadConfigCollect))]
        private void RoadConfigCollect()
        {
#if UNITY_EDITOR
            RoadsConfig.CollectGPUInstancingLODGroups(ParcelsMin, ParcelsMax);
#endif
        }

        [ContextMenu(nameof(AddRoadsToService))]
        private void AddRoadsToService()
        {
            instancingService?.Dispose();
            instancingService?.AddToIndirect(RoadsConfig.IndirectLODGroups);
        }

        [ContextMenu(nameof(HideAll))]
        private void HideAll()
        {
            foreach (GPUInstancingLODGroup prop in RoadsConfig.PropsAndTiles)
                prop.HideAll();
        }

        [ContextMenu(nameof(ShowAll))]
        private void ShowAll()
        {
            foreach (GPUInstancingLODGroup prop in RoadsConfig.PropsAndTiles)
                prop.ShowAll();
        }

        [ContextMenu("DEBUG - Cache Prefabs")]
        private void CachePrefabs()
        {
            var cachedPrefabs = new List<GameObject>();

            foreach (AssetReferenceGameObject ar in RoadsConfig.RoadAssetsReference)
            {
                AsyncOperationHandle<GameObject> operation = ar.LoadAssetAsync<GameObject>();
                operation.WaitForCompletion();
                GameObject prefab = operation.Result;

                if (prefab != null)
                    cachedPrefabs.Add(prefab);

                operation.Release();
            }

            Prefabs = cachedPrefabs.ToArray();
        }

        [ContextMenu("DEBUG - Spawn Roads")]
        private void SpawnRoads()
        {
            roadsRoot = new GameObject("RoadsRoot").transform;
            roadsRoot.gameObject.SetActive(false);

            foreach (RoadDescription roadDescription in RoadsConfig.RoadDescriptions)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;
                GameObject gpuInstancedPrefab = Prefabs.FirstOrDefault(op => op.name == roadDescription.RoadModel);

                if (gpuInstancedPrefab == null)
                {
                    Debug.LogWarning($"Can't find prefab {roadDescription.RoadModel}");
                    continue;
                }

                Transform roadAsset =
                    Instantiate(gpuInstancedPrefab, roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, roadDescription.Rotation, roadsRoot)
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
