using DCL.Diagnostics;
using DCL.LOD;
using DCL.Rendering.GPUInstancing.InstancingData;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Roads.Settings
{
    [Serializable]
    [CreateAssetMenu(fileName = "Road Settings", menuName = "DCL/Various/Road Settings")]
    public class RoadSettingsAsset : ScriptableObject, IRoadSettingsAsset
    {
        public List<GPUInstancingLODGroupWithBuffer> IndirectLODGroups;
        public List<GPUInstancingLODGroupWithBuffer> ExtractedLODGroups;
        public List<GPUInstancingLODGroup> PropsAndTiles;

        [field: SerializeField] public List<RoadDescription> RoadDescriptions { get; set; }
        [field: SerializeField] public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }

        IReadOnlyList<RoadDescription> IRoadSettingsAsset.RoadDescriptions => RoadDescriptions;
        IReadOnlyList<AssetReferenceGameObject> IRoadSettingsAsset.RoadAssetsReference => RoadAssetsReference;

#if UNITY_EDITOR
        public void InitializeInstancingKeywords()
        {
            foreach (var candidate in IndirectLODGroups)
            foreach (var combinedLodRenderer in candidate.LODGroup.CombinedLodsRenderers)
            {
                if (combinedLodRenderer.SharedMaterial.parent != null)
                {
                    var keyword = new LocalKeyword(combinedLodRenderer.SharedMaterial.shader, GPUInstancingMaterialsCache.GPU_INSTANCING_KEYWORD);
                    combinedLodRenderer.SharedMaterial.EnableKeyword(keyword);

                    var instancedMat = new Material(combinedLodRenderer.SharedMaterial.parent.shader);
                    instancedMat.CopyPropertiesFromMaterial(combinedLodRenderer.SharedMaterial);

                    combinedLodRenderer.SharedMaterial.DisableKeyword(keyword);
                    instancedMat.EnableKeyword(keyword);
                }
            }
        }

        public void CollectGPUInstancingLODGroups(Vector2Int min, Vector2Int max)
        {

            Dictionary<string, GPUInstancingPrefabData> loadedPrefabs = LoadAllPrefabs();

            var tempIndirectCandidates = new Dictionary<GPUInstancingLODGroupWithBuffer, HashSet<PerInstanceBuffer>>();

            UnityEditor.Undo.RecordObject(this, "Collect GPU Instancing LOD Groups");

            foreach (RoadDescription roadDescription in RoadDescriptions)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;

                if (!loadedPrefabs.TryGetValue(roadDescription.RoadModel, out GPUInstancingPrefabData prefab))
                {
                    ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, $"Can't find prefab {roadDescription.RoadModel}, using default");
                    prefab = loadedPrefabs[RoadAssetsPool.DEFAULT_ROAD_KEY];
                }

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

            IndirectLODGroups = tempIndirectCandidates
                               .Select(kvp => new GPUInstancingLODGroupWithBuffer(kvp.Key.LODGroup, kvp.Value.ToList()))
                               .OrderBy(group => group.LODGroup.Name)
                               .ToList();

            ExtractDuplicateCombinedLodsRenderers();
            IndirectLODGroups.AddRange(ExtractedLODGroups);

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            UnityEditor.AssetDatabase.Refresh();
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
            foreach (GPUInstancingLODGroupWithBuffer myCandidate in sourceCandidates)
            {
                GPUInstancingLODGroupWithBuffer candidate;

                if (myCandidate.Name.StartsWith("RoadTile"))
                {
                    if (roadTileCandidate.LODGroup == null)
                    {
                        roadTileCandidate.LODGroup = myCandidate.LODGroup;
                        roadTileCandidate.LODGroup.Name = "RoadTile";
                        roadTileCandidate.Name = "RoadTile";
                    }

                    roadTileCandidate.InstancesBuffer = myCandidate.InstancesBuffer;
                    candidate = roadTileCandidate;
                }
                else
                    candidate = myCandidate;

                if (!targetDict.TryGetValue(candidate, out HashSet<PerInstanceBuffer> matrices))
                {
                    matrices = new HashSet<PerInstanceBuffer>();
                    targetDict.Add(candidate, matrices);
                }

                foreach (PerInstanceBuffer instanceData in candidate.InstancesBuffer)
                    matrices.Add(new PerInstanceBuffer(roadRoot * instanceData.instMatrix, instanceData.tiling, instanceData.offset));
            }
        }

        private GPUInstancingLODGroupWithBuffer roadTileCandidate = new ();

        private void ExtractDuplicateCombinedLodsRenderers()
        {
            if (ExtractedLODGroups == null)
                ExtractedLODGroups = new List<GPUInstancingLODGroupWithBuffer>();
            else
                ExtractedLODGroups.Clear();

            List<List<(GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index)>> duplicateGroups = FindDuplicateCombinedLodsRenderers();

            foreach (List<(GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index)> duplicateGroup in duplicateGroups) { ExtractDuplicateToNewLODGroup(duplicateGroup); }

            RemoveEmptyIndirectLODGroups();
        }

        private List<List<(GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index)>> FindDuplicateCombinedLodsRenderers()
        {
            var rendererMap = new Dictionary<string, List<(GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index)>>();

            for (var i = 0; i < IndirectLODGroups.Count; i++)
            {
                GPUInstancingLODGroupWithBuffer lodGroup = IndirectLODGroups[i];

                for (var j = 0; j < lodGroup.LODGroup.CombinedLodsRenderers.Count; j++)
                {
                    CombinedLodsRenderer renderer = lodGroup.LODGroup.CombinedLodsRenderers[j];
                    string key = GetCombinedLodsRendererKey(renderer);

                    if (!rendererMap.TryGetValue(key, out List<(GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index)> list))
                    {
                        list = new List<(GPUInstancingLODGroupWithBuffer, CombinedLodsRenderer, int)>();
                        rendererMap[key] = list;
                    }

                    list.Add((lodGroup, renderer, j));
                }
            }

            return rendererMap.Values.Where(list => list.Count > 1).ToList();
        }

        private string GetCombinedLodsRendererKey(CombinedLodsRenderer renderer) =>
            $"{renderer.CombinedMesh.name}_{renderer.SharedMaterial.shader.name}";

        private void ExtractDuplicateToNewLODGroup(List<(GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index)> duplicates)
        {
            (GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index) firstEntry = duplicates[0];
            var extractedName = $"Extracted_{firstEntry.renderer.CombinedMesh.name}_{firstEntry.renderer.SharedMaterial.shader.name}";

            GPUInstancingLODGroup newLODGroup = CreateNewGPUInstancingLODGroup(extractedName, firstEntry.lodGroup.LODGroup);

            newLODGroup.CombinedLodsRenderers = new List<CombinedLodsRenderer> { CloneCombinedLodsRenderer(firstEntry.renderer) };

            var combinedInstancesBuffer = new List<PerInstanceBuffer>();

            foreach ((GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index) in duplicates)
            {
                foreach (PerInstanceBuffer instanceBuffer in lodGroup.InstancesBuffer)
                {
                    Vector4 colorTint = ExtractColorFromMaterial(renderer.SharedMaterial);

                    var modifiedBuffer = new PerInstanceBuffer(instanceBuffer.instMatrix, instanceBuffer.tiling, instanceBuffer.offset)
                    {
                        instColourTint = colorTint,
                    };

                    combinedInstancesBuffer.Add(modifiedBuffer);
                }
            }

            var renderersToRemove = new HashSet<CombinedLodsRenderer>();

            foreach ((GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index) in duplicates) { renderersToRemove.Add(renderer); }

            foreach ((GPUInstancingLODGroupWithBuffer lodGroup, CombinedLodsRenderer renderer, int index) in duplicates) { lodGroup.LODGroup.CombinedLodsRenderers.RemoveAll(r => renderersToRemove.Contains(r)); }

            var extractedLODGroupWithBuffer = new GPUInstancingLODGroupWithBuffer(newLODGroup, combinedInstancesBuffer)
            {
                Name = extractedName,
            };

            newLODGroup.ObjectSize = float.MaxValue;
            ExtractedLODGroups.Add(extractedLODGroupWithBuffer);
        }

        private GPUInstancingLODGroup CreateNewGPUInstancingLODGroup(string name, GPUInstancingLODGroup templateLODGroup)
        {
            if (PropsAndTiles == null || PropsAndTiles.Count == 0)
            {
                ReportHub.LogError(ReportCategory.GPU_INSTANCING, "No PropsAndTiles found to attach new GPUInstancingLODGroup");
                return null;
            }

            GameObject hostGameObject = PropsAndTiles[0].gameObject;
            GPUInstancingLODGroup newLODGroup = hostGameObject.AddComponent<GPUInstancingLODGroup>();

            newLODGroup.Name = name;
            newLODGroup.ObjectSize = templateLODGroup.ObjectSize;
            newLODGroup.Bounds = templateLODGroup.Bounds;
            newLODGroup.LodsScreenSpaceSizes = (float[])templateLODGroup.LodsScreenSpaceSizes.Clone();
            newLODGroup.LODSizesMatrix = templateLODGroup.LODSizesMatrix;
            newLODGroup.whitelistedShaders = templateLODGroup.whitelistedShaders;
            newLODGroup.Reference = templateLODGroup.Reference;
            newLODGroup.Transform = templateLODGroup.Transform;
            newLODGroup.RefRenderers = new List<Renderer>(templateLODGroup.RefRenderers);

            return newLODGroup;
        }

        private CombinedLodsRenderer CloneCombinedLodsRenderer(CombinedLodsRenderer original) =>
            new (
                original.SharedMaterial,
                original.CombinedMesh,
                original.SubMeshId,
                original.RenderParamsSerialized
            );

        private Vector4 ExtractColorFromMaterial(Material material)
        {
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");

            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");

            if (material.HasProperty("_MainColor"))
                return material.GetColor("_MainColor");

            return Vector4.one;
        }

        private void RemoveEmptyIndirectLODGroups()
        {
            for (int i = IndirectLODGroups.Count - 1; i >= 0; i--)
            {
                if (IndirectLODGroups[i].LODGroup.CombinedLodsRenderers.Count == 0) { IndirectLODGroups.RemoveAt(i); }
            }
        }
#endif
    }
}
