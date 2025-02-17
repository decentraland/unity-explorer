using DCL.Roads.GPUInstancing.Playground;
using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.Roads.Settings
{
    [Serializable]
    [CreateAssetMenu(fileName = "Road Settings", menuName = "DCL/Various/Road Settings")]
    public class RoadSettingsAsset : ScriptableObject, IRoadSettingsAsset
    {
        public List<GPUInstancingLODGroupWithBuffer> IndirectLODGroups;
        public List<GPUInstancingLODGroup> PropsAndTiles;

        [field: SerializeField] public List<RoadDescription> RoadDescriptions { get; set; }
        [field: SerializeField] public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }

        IReadOnlyList<RoadDescription> IRoadSettingsAsset.RoadDescriptions => RoadDescriptions;
        IReadOnlyList<AssetReferenceGameObject> IRoadSettingsAsset.RoadAssetsReference => RoadAssetsReference;

#if UNITY_EDITOR
        public void HideAll()
        {
            foreach (GPUInstancingLODGroup prop in PropsAndTiles)
                prop.HideAll();
        }

        public void ShowAll()
        {
            foreach (GPUInstancingLODGroup prop in PropsAndTiles)
                prop.ShowAll();
        }

        public void CollectGPUInstancingLODGroups(Vector2Int min, Vector2Int max)
        {
            Dictionary<string, GPUInstancingPrefabData> loadedPrefabs = LoadAllPrefabs();

            var tempIndirectCandidates = new Dictionary<GPUInstancingLODGroupWithBuffer, HashSet<PerInstanceBuffer>>();

            foreach (RoadDescription roadDescription in RoadDescriptions)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;

                if (!loadedPrefabs.TryGetValue(roadDescription.RoadModel, out GPUInstancingPrefabData prefab))
                {
                    Debug.LogWarning($"Can't find prefab {roadDescription.RoadModel}");
                    continue;
                }

                var roadRoot = Matrix4x4.TRS(
                    roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation,
                    roadDescription.Rotation.SelfOrIdentity(),
                    Vector3.one);

                ProcessCandidates(prefab.IndirectCandidates, roadRoot, tempIndirectCandidates);
            }

            IndirectLODGroups = tempIndirectCandidates
                               .Select(kvp => new GPUInstancingLODGroupWithBuffer(kvp.Key.LODGroup, kvp.Value.ToList()))
                               .OrderBy(group => group.LODGroup.Name)
                               .ToList();

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            return;

            bool IsOutOfRange(Vector2Int roadCoordinate) =>
                roadCoordinate.x < min.x || roadCoordinate.x > max.x ||
                roadCoordinate.y < min.y || roadCoordinate.y > max.y;
        }

        private Dictionary<string,GPUInstancingPrefabData> LoadAllPrefabs()
        {
            var loadedPrefabs = new Dictionary<string, GPUInstancingPrefabData>();

            foreach (AssetReferenceGameObject assetRef in RoadAssetsReference)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null)
                {
                    Debug.LogError($"Failed to load prefab at path: {assetPath}");
                    continue;
                }

                GPUInstancingPrefabData instanceBehaviour = prefab.GetComponent<GPUInstancingPrefabData>();

                if (instanceBehaviour == null)
                {
                    Debug.LogError($"Prefab {prefab.name} doesn't have PrefabInstanceDataBehaviour component");
                    continue;
                }

                loadedPrefabs[prefab.name] = instanceBehaviour;
            }

            return loadedPrefabs;
        }

        private void ProcessCandidates(List<GPUInstancingLODGroupWithBuffer> sourceCandidates, Matrix4x4 roadRoot, Dictionary<GPUInstancingLODGroupWithBuffer, HashSet<PerInstanceBuffer>> targetDict)
        {
            foreach (GPUInstancingLODGroupWithBuffer candidate in sourceCandidates)
            {
                if (!targetDict.TryGetValue(candidate, out HashSet<PerInstanceBuffer> matrices))
                {
                    matrices = new HashSet<PerInstanceBuffer>();
                    targetDict.Add(candidate, matrices);
                }

                foreach (PerInstanceBuffer instanceData in candidate.InstancesBuffer)
                    matrices.Add(new PerInstanceBuffer { instMatrix = roadRoot * instanceData.instMatrix });
            }
        }
#endif
    }
}
