using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class MeshInstanceData
    {
        public MeshData MeshData;
        public List<PerInstance> InstancesMatrices;
    }

    [Serializable]
    public struct PerInstance : IEquatable<PerInstance>
    {
        public Matrix4x4 objectToWorld;
        public Color colour;

        public bool Equals(PerInstance other) =>
            objectToWorld.Equals(other.objectToWorld) && colour.Equals(other.colour);

        public override bool Equals(object obj) =>
            obj is PerInstance other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(objectToWorld, colour);

        // private const float EPSILON = 0.0001f;

        // public bool Equals(Matrix4x4 a, Matrix4x4 b) =>
        //     Mathf.Abs(a.m00 - b.m00) < EPSILON &&
        //     Mathf.Abs(a.m01 - b.m01) < EPSILON &&
        //     Mathf.Abs(a.m02 - b.m02) < EPSILON &&
        //     Mathf.Abs(a.m03 - b.m03) < EPSILON &&
        //     Mathf.Abs(a.m10 - b.m10) < EPSILON &&
        //     Mathf.Abs(a.m11 - b.m11) < EPSILON &&
        //     Mathf.Abs(a.m12 - b.m12) < EPSILON &&
        //     Mathf.Abs(a.m13 - b.m13) < EPSILON &&
        //     Mathf.Abs(a.m20 - b.m20) < EPSILON &&
        //     Mathf.Abs(a.m21 - b.m21) < EPSILON &&
        //     Mathf.Abs(a.m22 - b.m22) < EPSILON &&
        //     Mathf.Abs(a.m23 - b.m23) < EPSILON &&
        //     Mathf.Abs(a.m30 - b.m30) < EPSILON &&
        //     Mathf.Abs(a.m31 - b.m31) < EPSILON &&
        //     Mathf.Abs(a.m32 - b.m32) < EPSILON &&
        //     Mathf.Abs(a.m33 - b.m33) < EPSILON;

        // public int GetHashCode(Matrix4x4 matrix)
        // {
        //     unchecked
        //     {
        //         var hash = 17;
        //         hash = (hash * 23) + (int)(matrix.m03 / EPSILON);
        //         hash = (hash * 23) + (int)(matrix.m13 / EPSILON);
        //         hash = (hash * 23) + (int)(matrix.m23 / EPSILON);
        //         return hash;
        //     }
        // }
    }

    public class PrefabInstanceDataBehaviour : MonoBehaviour
    {
        [SerializeField]
        public List<MeshInstanceData> meshInstances;

        public MeshData[] Meshes;
        public LODGroupData[] LODGroups;

        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
#if UNITY_EDITOR
            if (transform.position != Vector3.zero)
                transform.position = Vector3.zero;

            if (transform.rotation != Quaternion.identity)
                transform.rotation = Quaternion.identity;

            if (transform.localScale != Vector3.one)
                transform.localScale = Vector3.one;

            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                CollectDataFromPrefabAsset();
#endif
        }

        public void HideVisuals()
        {
            foreach (MeshData mesh in Meshes)
                mesh.Renderer.enabled = false;

            foreach (LODGroupData lodGroup in LODGroups)
            {
                if (lodGroup.LODs.Length == 0) continue;

                lodGroup.LODGroup.enabled = false;

                foreach (LODEntryMeshData lod in lodGroup.LODs)
                foreach (MeshData mesh in lod.Meshes)
                    mesh.Renderer.enabled = false;
            }
        }

        private void CollectDataFromPrefabAsset()
        {
            var tempMeshToMatrices = new Dictionary<MeshData, HashSet<PerInstance>>();

            Meshes = CollectStandaloneMeshesData(tempMeshToMatrices);
            LODGroups = CollectLODGroupDatas(tempMeshToMatrices);

            meshInstances = new List<MeshInstanceData>(tempMeshToMatrices.Keys.Count);
            foreach (KeyValuePair<MeshData, HashSet<PerInstance>> kvp in tempMeshToMatrices)
                meshInstances.Add(new MeshInstanceData { MeshData = kvp.Key, InstancesMatrices = kvp.Value.ToList() });
        }

        private MeshData[] CollectStandaloneMeshesData(Dictionary<MeshData, HashSet<PerInstance>> tempMeshToMatrices)
        {
            Renderer[] standaloneRenderers = gameObject.GetComponentsInChildren<Renderer>(true)
                                                       .Where(r => !AssignedToLODGroupInPrefabHierarchy(r.transform)).ToArray();

            return CollectMeshData(standaloneRenderers, tempMeshToMatrices).ToArray();
        }

        private LODGroupData[] CollectLODGroupDatas(Dictionary<MeshData, HashSet<PerInstance>> tempMeshToMatrices) =>
            gameObject.GetComponentsInChildren<LODGroup>(true)
                      .Select(group => CollectLODGroupData(group, tempMeshToMatrices))
                      .Where(lodGroupData => lodGroupData.LODs.Length != 0 && lodGroupData.LODs[0].Meshes.Length != 0).ToArray();

        private List<MeshData> CollectMeshData(Renderer[] renderers, Dictionary<MeshData, HashSet<PerInstance>> tempMeshToMatrices)
        {
            var list = new List<MeshData>();

            foreach (Renderer rndr in renderers)
            {
                var meshRenderer = rndr as MeshRenderer;
                if (meshRenderer == null || meshRenderer.sharedMaterials.Length == 0) return list;

                MeshFilter meshFilter = rndr.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) return list;

                MeshData meshData = new MeshData
                {
                    Transform = meshRenderer.transform,
                    SharedMesh = meshFilter.sharedMesh,
                    SharedMaterials = meshRenderer.sharedMaterials,
                    ReceiveShadows = meshRenderer.receiveShadows,
                    ShadowCastingMode = meshRenderer.shadowCastingMode,
                    Renderer = meshRenderer,
                    LocalToRootMatrix = transform.worldToLocalMatrix * rndr.transform.localToWorldMatrix, // root * child
                };

                list.Add(meshData);

                PerInstance data = new PerInstance
                {
                    objectToWorld = meshData.LocalToRootMatrix,
                };

                if (tempMeshToMatrices.TryGetValue(meshData, out var matrices))
                    matrices.Add(data);
                else
                    tempMeshToMatrices[meshData] = new HashSet<PerInstance> { data };
            }

            return list;
        }

        private bool AssignedToLODGroupInPrefabHierarchy(Transform transform)
        {
            Transform current = transform;
            Transform root = this.transform;

            while (current != root && current != null)
            {
                if (current.GetComponent<LODGroup>() != null)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private LODGroupData CollectLODGroupData(LODGroup lodGroup, Dictionary<MeshData, HashSet<PerInstance>> tempMeshToMatrices)
        {
            lodGroup.RecalculateBounds();

            var LODGroupData = new LODGroupData
            {
                LODGroup = lodGroup,
                Transform = lodGroup.transform,
                ObjectSize = lodGroup.size,
                LODBounds = new Bounds(),
                LODs = lodGroup.GetLODs()
                               .Select(lod => new LODEntryMeshData
                                {
                                    Meshes = CollectMeshData(lod.renderers, tempMeshToMatrices).ToArray(),
                                    ScreenRelativeTransitionHeight = lod.screenRelativeTransitionHeight,
                                })
                               .ToArray(),
            };

            CalculateGroupBounds(LODGroupData);

            return LODGroupData;
        }

        private static void CalculateGroupBounds(LODGroupData lodGroup)
        {
            var isInitialized = false;


            foreach (LODEntryMeshData mid in lodGroup.LODs)
            foreach (MeshData data in mid.Meshes)
            {
                if (!isInitialized)
                {
                    lodGroup.LODBounds = data.SharedMesh.bounds;
                    isInitialized = true;
                }
                else lodGroup.LODBounds.Encapsulate(data.SharedMesh.bounds);
            }
        }
    }
}
