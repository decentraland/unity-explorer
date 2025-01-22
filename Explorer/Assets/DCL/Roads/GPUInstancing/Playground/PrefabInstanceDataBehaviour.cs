using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Roads.GPUInstancing.Playground
{
    [System.Serializable]
    public class MeshInstanceData
    {
        public MeshData MeshData;
        public List<Matrix4x4> InstancesMatrices;
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
            var tempMeshToMatrices = new Dictionary<MeshData, List<Matrix4x4>>();

            Meshes = CollectStandaloneMeshesData(tempMeshToMatrices);
            LODGroups = CollectLODGroupDatas(tempMeshToMatrices);

            meshInstances = new List<MeshInstanceData>(tempMeshToMatrices.Keys.Count);
            foreach (var kvp in tempMeshToMatrices)
                meshInstances.Add(new MeshInstanceData { MeshData = kvp.Key, InstancesMatrices = kvp.Value });
        }

        private MeshData[] CollectStandaloneMeshesData(Dictionary<MeshData, List<Matrix4x4>> tempMeshToMatrices)
        {
            Renderer[] standaloneRenderers = gameObject.GetComponentsInChildren<Renderer>(true)
                                                       .Where(r => !AssignedToLODGroupInPrefabHierarchy(r.transform)).ToArray();

            return CollectMeshData(standaloneRenderers, tempMeshToMatrices).ToArray();
        }

        private LODGroupData[] CollectLODGroupDatas(Dictionary<MeshData, List<Matrix4x4>> tempMeshToMatrices) =>
            gameObject.GetComponentsInChildren<LODGroup>(true)
                      .Select(group => CollectLODGroupData(group, tempMeshToMatrices))
                      .Where(lodGroupData => lodGroupData.LODs.Length != 0 && lodGroupData.LODs[0].Meshes.Length != 0).ToArray();

        private List<MeshData> CollectMeshData(Renderer[] renderers, Dictionary<MeshData, List<Matrix4x4>> tempMeshToMatrices)
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

                if (tempMeshToMatrices.TryGetValue(meshData, out var matrices))
                    matrices.Add(meshData.LocalToRootMatrix);
                else
                    tempMeshToMatrices[meshData] = new List<Matrix4x4> { meshData.LocalToRootMatrix };
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

        private LODGroupData CollectLODGroupData(LODGroup lodGroup, Dictionary<MeshData, List<Matrix4x4>> tempMeshToMatrices)
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
