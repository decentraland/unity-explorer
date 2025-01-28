using DCL.Roads.GPUInstancing.Playground;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.Roads.Settings
{
    [Serializable]
    [CreateAssetMenu(fileName = "Road Settings", menuName = "DCL/Various/Road Settings")]
    public class RoadSettingsAsset : ScriptableObject, IRoadSettingsAsset
    {
        [SerializeField] public List<GPUInstancedMesh> RoadsMeshesGPUInstances;

        [field: SerializeField] public List<RoadDescription> RoadDescriptions { get; set; }
        [field: SerializeField] public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }

        IReadOnlyList<RoadDescription> IRoadSettingsAsset.RoadDescriptions => RoadDescriptions;
        IReadOnlyList<AssetReferenceGameObject> IRoadSettingsAsset.RoadAssetsReference => RoadAssetsReference;

#if UNITY_EDITOR
        public void CollectAllMeshInstances()
        {
            Dictionary<string, GPUInstancedPrefab> loadedPrefabs = LoadAllPrefabs();

            Dictionary<MeshInstanceData, HashSet<PerInstanceBuffer>> tempMeshToMatrices = CollectInstancesMap(loadedPrefabs);

            RoadsMeshesGPUInstances = tempMeshToMatrices.Select(kvp => new GPUInstancedMesh { meshInstanceData = kvp.Key, PerInstancesData = kvp.Value.ToArray() }).ToList();

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private Dictionary<MeshInstanceData, HashSet<PerInstanceBuffer>> CollectInstancesMap(Dictionary<string, GPUInstancedPrefab> loadedPrefabs)
        {
            var tempMeshToMatrices = new Dictionary<MeshInstanceData, HashSet<PerInstanceBuffer>>();

            foreach (RoadDescription roadDescription in RoadDescriptions)
            {
                if (!loadedPrefabs.TryGetValue(roadDescription.RoadModel, out GPUInstancedPrefab prefab))
                {
                    Debug.LogWarning($"Can't find prefab {roadDescription.RoadModel}");
                    continue;
                }

                var roadRoot = Matrix4x4.TRS(roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, roadDescription.Rotation.SelfOrIdentity(), Vector3.one);

                foreach (GPUInstancedMesh meshInstance in prefab.InstancedMeshes)
                {
                    if (!tempMeshToMatrices.TryGetValue(meshInstance.meshInstanceData, out HashSet<PerInstanceBuffer> matrices))
                    {
                        matrices = new HashSet<PerInstanceBuffer>();
                        tempMeshToMatrices.Add(meshInstance.meshInstanceData, matrices);
                    }

                    foreach (PerInstanceBuffer instanceData in meshInstance.PerInstancesData)
                        matrices.Add(new PerInstanceBuffer { instMatrix = roadRoot * instanceData.instMatrix });
                }
            }

            return tempMeshToMatrices;
        }

        private Dictionary<string, GPUInstancedPrefab> LoadAllPrefabs()
        {
            var loadedPrefabs = new Dictionary<string, GPUInstancedPrefab>();

            foreach (AssetReferenceGameObject assetRef in RoadAssetsReference)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null)
                {
                    Debug.LogError($"Failed to load prefab at path: {assetPath}");
                    continue;
                }

                GPUInstancedPrefab instanceBehaviour = prefab.GetComponent<GPUInstancedPrefab>();

                if (instanceBehaviour == null)
                {
                    Debug.LogError($"Prefab {prefab.name} doesn't have PrefabInstanceDataBehaviour component");
                    continue;
                }

                loadedPrefabs[prefab.name] = instanceBehaviour;
            }

            return loadedPrefabs;
        }
#endif
    }
}
