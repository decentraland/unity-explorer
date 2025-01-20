using DCL.Roads.Playground;
using DCL.Roads.Settings;
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
        private readonly Dictionary<GPUInstancedRenderer, List<Matrix4x4>> gpuInstancingMap = new ();

        [Space]
        public RoadSettingsAsset RoadsConfig;
        public PrefabInstanceDataBehaviour[] Prefabs;

        [Space]
        public bool Run;

        [Header("DEBUG SETTINGS")]
        public Vector2Int ParcelsMin;
        public Vector2Int ParcelsMax;

        [Header("DEBUG TABLE")]
        public Mesh[] Meshes;
        public Material[] Materials1;
        public int[] InstancesCount;

        public int InstanceId;

        [HideInInspector]
        public Transform debugRoot;

        private void Awake()
        {
            SpawnRoads();
            PrepareInstancesMap();
        }

        public void Update()
        {
            if (!Run) return;

            foreach (KeyValuePair<GPUInstancedRenderer, List<Matrix4x4>> renderInstances in gpuInstancingMap)
            {
                Debug.Log($"{renderInstances.Key.Mesh.name}");

                for (var i = 0; i < renderInstances.Key.RenderParams.Length; i++) // foreach submesh
                {
                    if (InstanceId >= renderInstances.Value.Count) continue;
                    var instanceData = InstanceId < 0 ? renderInstances.Value : new List<Matrix4x4> { renderInstances.Value[InstanceId] };
                    Graphics.RenderMeshInstanced(in renderInstances.Key.RenderParams[i], renderInstances.Key.Mesh, i, instanceData);

                    Debug.Log($"{renderInstances.Key.Mesh.name} - {renderInstances.Key.RenderParams[i].material.name}");

                    foreach (Matrix4x4 value in renderInstances.Value)
                        Debug.Log($"{value}");
                }
            }
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
                    cachedPrefabs.Add(prefabBeh);
                }
            }

            Prefabs = cachedPrefabs.ToArray();
        }

        [ContextMenu("DEBUG - Spawn Roads")]
        private void SpawnRoads()
        {
            // Create debug root
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("LongRoad"))
                DestroyImmediate(obj);

            debugRoot = new GameObject("RoadsRoot").transform;
            debugRoot.tag = "LongRoad";
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

        [ContextMenu("DEBUG - Prepare Instances Table")]
        private void PrepareInstancesMap()
        {
            gpuInstancingMap.Clear();

            foreach (PrefabInstanceDataBehaviour spawnedRoad in debugRoot.GetComponentsInChildren<PrefabInstanceDataBehaviour>())
            {
                Debug.Log($"--- Adding Raod: {spawnedRoad.gameObject.name} from {spawnedRoad.transform.position}");

                // var roadTransform = Matrix4x4.TRS(roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, roadDescription.Rotation.SelfOrIdentity(), Vector3.one);
                AddPrefabDataToInstancingMap(spawnedRoad);
            }

            CollectDebugInfo();
        }

        private void AddPrefabDataToInstancingMap(PrefabInstanceDataBehaviour prefabData)
        {
            Matrix4x4 rootMatrix = prefabData.transform.localToWorldMatrix;
            Debug.Log($"Mesh position from matrix: {prefabData.transform.position}");

            AddMeshDataToInstancingMap(prefabData.PrefabInstance.Meshes, rootMatrix);

            foreach (LODGroupData lodGroup in prefabData.PrefabInstance.LODGroups)
            foreach (LODEntryMeshData lods in lodGroup.LODs)
                AddMeshDataToInstancingMap(lods.Meshes, rootMatrix);
        }

        private void AddMeshDataToInstancingMap(MeshData[] meshes, Matrix4x4 rootMatrix)
        {
            foreach (MeshData meshData in meshes)
            {
                Debug.Log($"-- Adding mesh {meshData.Transform.gameObject.name} from {meshData.Transform.position}");

                // Matrix4x4 finalMatrix = roadTransform * meshData.Transform.localToWorldMatrix;
                Matrix4x4 localMatrix = meshData.Transform.localToWorldMatrix;

                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                Debug.Log($"Adding Render datas of amount {meshData.Transform.localToWorldMatrix}");
                Debug.Log($"Actual transform position: {meshData.Transform.position}");

                if (gpuInstancingMap.TryGetValue(instancedRenderer, out List<Matrix4x4> matrix))
                    matrix.Add(localMatrix);
                else
                    gpuInstancingMap.Add(instancedRenderer, new List<Matrix4x4> { localMatrix });
            }
        }

        private void CollectDebugInfo()
        {
            var meshes = new List<Mesh>();
            var instCount = new List<int>();
            var mat = new List<Material>();

            foreach (KeyValuePair<GPUInstancedRenderer, List<Matrix4x4>> propPair in gpuInstancingMap)
            {
                meshes.Add(propPair.Key.Mesh);
                mat.Add(propPair.Key.RenderParams[0].material);
                instCount.Add(propPair.Value.Count);
            }

            Meshes = meshes.ToArray();
            Materials1 = mat.ToArray();
            InstancesCount = instCount.ToArray();
        }

        private bool IsOutOfRange(Vector2Int roadCoordinate) =>
            roadCoordinate.x < ParcelsMin.x || roadCoordinate.x > ParcelsMax.x ||
            roadCoordinate.y < ParcelsMin.y || roadCoordinate.y > ParcelsMax.y;
    }
}
