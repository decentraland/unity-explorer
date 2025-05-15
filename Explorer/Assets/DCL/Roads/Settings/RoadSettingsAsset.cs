using DCL.Diagnostics;
using DCL.LOD;
using DCL.Rendering.GPUInstancing.InstancingData;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [field: SerializeField] 
        [field: HideInInspector] 
        public List<RoadDescription> RoadDescriptions { get; set; }
        [field: SerializeField] public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }

        IReadOnlyList<RoadDescription> IRoadSettingsAsset.RoadDescriptions => RoadDescriptions;
        IReadOnlyList<AssetReferenceGameObject> IRoadSettingsAsset.RoadAssetsReference => RoadAssetsReference;

#if UNITY_EDITOR
        public void CollectGPUInstancingLODGroups(Vector2Int min, Vector2Int max)
        {
            Dictionary<string, GPUInstancingPrefabData> loadedPrefabs = LoadAllPrefabs();

            var tempIndirectCandidates = new Dictionary<GPUInstancingLODGroupWithBuffer, HashSet<PerInstanceBuffer>>();

            UnityEditor.Undo.RecordObject(this, "Collect GPU Instancing LOD Groups");

            foreach (RoadDescription roadDescription in RoadDescriptions)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;
                Debug.Log($"DOING {roadDescription.RoadCoordinate}");
                GPUInstancingPrefabData prefab = loadedPrefabs[RoadAssetsPool.DEFAULT_ROAD_KEY];
                
                var rotation = roadDescription.Rotation;
                if (roadDescription.Rotation is { x: 0, y: 0, z: 0, w: 0 })
                {
                    ReportHub.LogError(ReportCategory.GPU_INSTANCING, $"Road {roadDescription.RoadModel} at {roadDescription.RoadCoordinate} has zero rotation! Change it to {Quaternion.identity}");
                    rotation = Quaternion.identity;
                }

                var roadRoot = Matrix4x4.TRS(
                    roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation,
                    rotation,
                    Vector3.one);

                ProcessCandidates(prefab.IndirectCandidates, roadRoot, tempIndirectCandidates);
            }
            Debug.Log($"JUANI IT FINISHED A");

            IndirectLODGroups = tempIndirectCandidates
                               .Select(kvp => new GPUInstancingLODGroupWithBuffer(kvp.Key.LODGroup, kvp.Value.ToList()))
                               .OrderBy(group => group.LODGroup.Name)
                               .ToList();
            Debug.Log($"JUANI IT FINISHED B");

            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
                        Debug.Log($"JUANI IT FINISHED B");

            return;

            bool IsOutOfRange(Vector2Int roadCoordinate) =>
                roadCoordinate.x < min.x || roadCoordinate.x > max.x ||
                roadCoordinate.y < min.y || roadCoordinate.y > max.y;
        }

        private Dictionary<string, GPUInstancingPrefabData> LoadAllPrefabs()
        {
            var loadedPrefabs = new Dictionary<string, GPUInstancingPrefabData>();

            foreach (AssetReferenceGameObject assetRef in RoadAssetsReference)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null)
                {
                    ReportHub.LogError(ReportCategory.GPU_INSTANCING, $"Failed to load prefab at path: {assetPath}");
                    continue;
                }

                GPUInstancingPrefabData instanceBehaviour = prefab.GetComponent<GPUInstancingPrefabData>();

                if (instanceBehaviour == null)
                {
                    ReportHub.LogError(ReportCategory.GPU_INSTANCING, $"Prefab {prefab.name} doesn't have PrefabInstanceDataBehaviour component");
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
                    matrices.Add(new PerInstanceBuffer(roadRoot * instanceData.instMatrix));
            }
        }
#endif
    }
}
