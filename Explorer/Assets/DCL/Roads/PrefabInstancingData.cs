using UnityEngine;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class LODInstanceData : IEquatable<LODInstanceData>
{
    public LODGroup OriginalPrefab;     // Reference to the original prefab LODGroup
    public Material Material;            // Shared material for all LODs

    [Header("LODS")]
    public float ObjectSize;            // Size from LODGroup
    public Bounds LODBounds;            // Bounds from LOD0 renderers
    public Mesh[] MeshLOD;              // Meshes for each LOD level
    public float[] DistLOD;             // Distances for each LOD level

    [Header("INSTANCES")]
    public List<Matrix4x4> Matrices = new ();    // Transform matrices for each instance

    public LODInstanceData Copy(LODInstanceData lodInstanceData)
    {
        var copy = new LODInstanceData
        {
            OriginalPrefab = lodInstanceData.OriginalPrefab,
            Material = lodInstanceData.Material,
            ObjectSize = lodInstanceData.ObjectSize,
            LODBounds = lodInstanceData.LODBounds,
            MeshLOD = lodInstanceData.MeshLOD,
            DistLOD = lodInstanceData.DistLOD,
            Matrices = lodInstanceData.Matrices,
        };

        return copy;
    }

    public bool Equals(LODInstanceData other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return MeshLOD[0] == other.MeshLOD[0] &&
               Material == other.Material;
    }

    public override bool Equals(object obj) =>
        Equals(obj as LODInstanceData);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((MeshLOD?[0] != null ? MeshLOD[0].GetHashCode() : 0) * 397) ^
                   (Material != null ? Material.GetHashCode() : 0);
        }
    }
}

public class PrefabInstancingData : MonoBehaviour
{
    public LODInstanceData[] InstancesData;

#if UNITY_EDITOR
    [ContextMenu(nameof(Refresh))]
    private void Refresh()
    {
        var prefabDataMap = CollectUniquePrefabsWithInstances();
        ProcessPrefabsData(prefabDataMap);
        // SortArray();
        // LogResults();
        EditorUtility.SetDirty(this);
    }

    private Dictionary<string, (LODGroup prefab, List<Transform> instances)> CollectUniquePrefabsWithInstances()
    {
        var prefabDataMap = new Dictionary<string, (LODGroup prefab, List<Transform> instances)>();
        var childLODGroups = GetComponentsInChildren<LODGroup>();

        foreach (var lodGroup in childLODGroups)
        {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(lodGroup);
            if (string.IsNullOrEmpty(prefabPath)) continue;

            var originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (originalPrefab == null) continue;

            var originalLODGroup = originalPrefab.GetComponent<LODGroup>();
            if (originalLODGroup == null) continue;

            if (!prefabDataMap.ContainsKey(prefabPath))
            {
                prefabDataMap.Add(prefabPath, (originalLODGroup, new List<Transform>()));
            }
            prefabDataMap[prefabPath].instances.Add(lodGroup.transform);
        }

        return prefabDataMap;
    }

    private void ProcessPrefabsData(Dictionary<string, (LODGroup prefab, List<Transform> instances)> prefabDataMap)
    {
        var instancesDataTmp = new List<LODInstanceData>();

        foreach (var kvp in prefabDataMap)
        {
            var (prefab, instances) = kvp.Value;
            var instanceData = CreateInstanceData(prefab, instances);

            if (instanceData.Material != null && instanceData.MeshLOD[0] != null)
                instancesDataTmp.Add(instanceData);
        }

        InstancesData = instancesDataTmp.ToArray();
    }

    private LODInstanceData CreateInstanceData(LODGroup prefab, List<Transform> instances)
    {
        var instanceData = new LODInstanceData
        {
            OriginalPrefab = prefab
        };

        prefab.RecalculateBounds();
        LOD[] lods = prefab.GetLODs();
        CollectLODData(lods, instanceData);
        CollectSizeAndBounds(prefab, lods, instanceData);

        CollectInstanceMatrices(instances, instanceData);

        return instanceData;
    }

    private void CollectLODData(LOD[] lods, LODInstanceData instanceData)
    {
        int lodCount = lods.Length;
        instanceData.MeshLOD = new Mesh[lodCount];
        instanceData.DistLOD = new float[lodCount];

        for (int i = 0; i < lodCount; i++)
        {
            instanceData.DistLOD[i] = lods[i].screenRelativeTransitionHeight;

            var renderer = lods[i].renderers[0] as MeshRenderer;
            if (renderer == null) continue;

            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null) continue;

            instanceData.MeshLOD[i] = meshFilter.sharedMesh;

            if (instanceData.Material == null && renderer.sharedMaterial != null)
            {
                instanceData.Material = renderer.sharedMaterial;
            }
        }
    }

    private void CollectSizeAndBounds(LODGroup prefab, LOD[] lods, LODInstanceData instanceData)
    {
        instanceData.ObjectSize = prefab.size;

        if (lods.Length > 0 && lods[0].renderers.Length > 0)
        {
            var firstRenderer = lods[0].renderers[0] as MeshRenderer;
            if (firstRenderer != null)
            {
                instanceData.LODBounds = firstRenderer.bounds;

                for (int r = 1; r < lods[0].renderers.Length; r++)
                {
                    if (lods[0].renderers[r] is MeshRenderer renderer)
                    {
                        instanceData.LODBounds.Encapsulate(renderer.bounds);
                    }
                }
            }
        }
    }

    private void CollectInstanceMatrices(List<Transform> instances, LODInstanceData instanceData)
    {
        foreach (var instance in instances)
            instanceData.Matrices.Add(instance.localToWorldMatrix);
    }

    private void SortArray()
    {
        Array.Sort(InstancesData, (a, b) => a.OriginalPrefab.name.CompareTo(b.OriginalPrefab.name));
    }

    private void LogResults()
    {
        Debug.Log($"Collected data for {InstancesData.Length} unique prefabs:");
        foreach (var data in InstancesData)
        {
            Debug.Log($"Prefab: {data.OriginalPrefab.name} has {data.Matrices.Count} instances");
        }
    }
#endif
}
