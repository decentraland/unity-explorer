using DCL.Roads.GPUInstancing.Playground;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using Utility;

namespace DCL.Roads.Settings
{
    [Serializable]
    [CreateAssetMenu(fileName = "Road Settings", menuName = "DCL/Various/Road Settings")]
    public class RoadSettingsAsset : ScriptableObject, IRoadSettingsAsset
    {
        [SerializeField] public List<MeshInstanceData> RoadsMeshesGPUInstances;

        [field: SerializeField] public List<RoadDescription> RoadDescriptions { get; set; }
        [field: SerializeField] public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }

        IReadOnlyList<RoadDescription> IRoadSettingsAsset.RoadDescriptions => RoadDescriptions;
        IReadOnlyList<AssetReferenceGameObject> IRoadSettingsAsset.RoadAssetsReference => RoadAssetsReference;

#if UNITY_EDITOR
        public void CollectAllMeshInstances()
        {
            Dictionary<string, PrefabInstanceDataBehaviour> loadedPrefabs = LoadAllPrefabs();

            Dictionary<MeshData, HashSet<PerInstance>> tempMeshToMatrices = CollectInstancesMap(loadedPrefabs);

            RoadsMeshesGPUInstances = tempMeshToMatrices.Select(kvp => new MeshInstanceData { MeshData = kvp.Key, InstancesMatrices = kvp.Value.ToList() }).ToList();

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private Dictionary<MeshData, HashSet<PerInstance>> CollectInstancesMap(Dictionary<string, PrefabInstanceDataBehaviour> loadedPrefabs)
        {
            var tempMeshToMatrices = new Dictionary<MeshData, HashSet<PerInstance>>();

            foreach (RoadDescription roadDescription in RoadDescriptions)
            {
                if (!loadedPrefabs.TryGetValue(roadDescription.RoadModel, out PrefabInstanceDataBehaviour prefab))
                {
                    Debug.LogWarning($"Can't find prefab {roadDescription.RoadModel}");
                    continue;
                }

                var roadRoot = Matrix4x4.TRS(roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, roadDescription.Rotation.SelfOrIdentity(), Vector3.one);

                foreach (MeshInstanceData meshInstance in prefab.meshInstances)
                {
                    if (!tempMeshToMatrices.TryGetValue(meshInstance.MeshData, out HashSet<PerInstance> matrices))
                    {
                        matrices = new HashSet<PerInstance>();
                        tempMeshToMatrices.Add(meshInstance.MeshData, matrices);
                    }

                    foreach (PerInstance instanceData in meshInstance.InstancesMatrices)
                        matrices.Add(new PerInstance { objectToWorld = roadRoot * instanceData.objectToWorld });
                }
            }

            return tempMeshToMatrices;
        }

        private Dictionary<string, PrefabInstanceDataBehaviour> LoadAllPrefabs()
        {
            var loadedPrefabs = new Dictionary<string, PrefabInstanceDataBehaviour>();

            foreach (AssetReferenceGameObject assetRef in RoadAssetsReference)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null)
                {
                    Debug.LogError($"Failed to load prefab at path: {assetPath}");
                    continue;
                }

                PrefabInstanceDataBehaviour instanceBehaviour = prefab.GetComponent<PrefabInstanceDataBehaviour>();

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
