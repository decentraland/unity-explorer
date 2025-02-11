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
        public List<GPUInstancingCandidate_Old> IndirectCandidates;
        public List<GPUInstancingCandidate_Old> DirectCandidates;

        [field: SerializeField] public List<RoadDescription> RoadDescriptions { get; set; }
        [field: SerializeField] public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }

        IReadOnlyList<RoadDescription> IRoadSettingsAsset.RoadDescriptions => RoadDescriptions;
        IReadOnlyList<AssetReferenceGameObject> IRoadSettingsAsset.RoadAssetsReference => RoadAssetsReference;

#if UNITY_EDITOR
        public void CollectGPUInstancingCandidates(Vector2Int min, Vector2Int max)
        {
            Dictionary<string, GPUInstancingPrefabData_Old> loadedPrefabs = LoadAllPrefabs();

            var tempDirectCandidates = new Dictionary<GPUInstancingCandidate_Old, HashSet<PerInstanceBuffer>>();
            var tempIndirectCandidates = new Dictionary<GPUInstancingCandidate_Old, HashSet<PerInstanceBuffer>>();

            foreach (RoadDescription roadDescription in RoadDescriptions)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;

                if (!loadedPrefabs.TryGetValue(roadDescription.RoadModel, out GPUInstancingPrefabData_Old prefab))
                {
                    Debug.LogWarning($"Can't find prefab {roadDescription.RoadModel}");
                    continue;
                }

                var roadRoot = Matrix4x4.TRS(
                    roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation,
                    roadDescription.Rotation.SelfOrIdentity(),
                    Vector3.one);

                ProcessCandidates(prefab.directCandidates, roadRoot, tempDirectCandidates);
                ProcessCandidates(prefab.indirectCandidates, roadRoot, tempIndirectCandidates);
            }

            DirectCandidates = tempDirectCandidates.Select(kvp => new GPUInstancingCandidate_Old(kvp.Key, kvp.Value)).ToList();
            IndirectCandidates = tempIndirectCandidates.Select(kvp => new GPUInstancingCandidate_Old(kvp.Key, kvp.Value)).ToList();

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            return;

            bool IsOutOfRange(Vector2Int roadCoordinate) =>
                roadCoordinate.x < min.x || roadCoordinate.x > max.x ||
                roadCoordinate.y < min.y || roadCoordinate.y > max.y;
        }

        private Dictionary<string,GPUInstancingPrefabData_Old> LoadAllPrefabs()
        {
            var loadedPrefabs = new Dictionary<string, GPUInstancingPrefabData_Old>();

            foreach (AssetReferenceGameObject assetRef in RoadAssetsReference)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null)
                {
                    Debug.LogError($"Failed to load prefab at path: {assetPath}");
                    continue;
                }

                GPUInstancingPrefabData_Old instanceBehaviour = prefab.GetComponent<GPUInstancingPrefabData_Old>();

                if (instanceBehaviour == null)
                {
                    Debug.LogError($"Prefab {prefab.name} doesn't have PrefabInstanceDataBehaviour component");
                    continue;
                }

                loadedPrefabs[prefab.name] = instanceBehaviour;
            }

            return loadedPrefabs;
        }

        private void ProcessCandidates(List<GPUInstancingCandidate_Old> sourceCandidates, Matrix4x4 roadRoot, Dictionary<GPUInstancingCandidate_Old, HashSet<PerInstanceBuffer>> targetDict)
        {
            foreach (GPUInstancingCandidate_Old candidate in sourceCandidates)
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
