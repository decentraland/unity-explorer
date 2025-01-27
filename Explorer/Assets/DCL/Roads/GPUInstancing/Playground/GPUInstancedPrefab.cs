using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Roads.GPUInstancing.Playground
{
    public class GPUInstancedPrefab : MonoBehaviour
    {
        [SerializeField]
        public List<GPUInstancedMesh> GPUInstancedMeshes;

        public MeshInstanceData[] Meshes;
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
            foreach (MeshInstanceData mesh in Meshes)
                mesh.Renderer.enabled = false;

            foreach (LODGroupData lodGroup in LODGroups)
            {
                if (lodGroup.LODs.Length == 0) continue;

                lodGroup.LODGroup.enabled = false;

                foreach (GPUInstancedLOD lod in lodGroup.LODs)
                foreach (MeshInstanceData mesh in lod.Meshes)
                    mesh.Renderer.enabled = false;
            }
        }

        private void CollectDataFromPrefabAsset()
        {
            var tempMeshToMatrices = new Dictionary<MeshInstanceData, HashSet<PerInstanceBuffer>>();

            Meshes = CollectStandaloneMeshesData(tempMeshToMatrices);
            LODGroups = CollectLODGroupDatas(tempMeshToMatrices);

            GPUInstancedMeshes = new List<GPUInstancedMesh>(tempMeshToMatrices.Keys.Count);
            foreach (KeyValuePair<MeshInstanceData, HashSet<PerInstanceBuffer>> kvp in tempMeshToMatrices)
                GPUInstancedMeshes.Add(new GPUInstancedMesh { meshInstanceData = kvp.Key, PerInstancesData = kvp.Value.ToArray() });
        }

        private MeshInstanceData[] CollectStandaloneMeshesData(Dictionary<MeshInstanceData, HashSet<PerInstanceBuffer>> tempMeshToMatrices)
        {
            Renderer[] standaloneRenderers = gameObject.GetComponentsInChildren<Renderer>(true)
                                                       .Where(r => !AssignedToLODGroupInPrefabHierarchy(r.transform)).ToArray();

            return CollectMeshData(standaloneRenderers, tempMeshToMatrices).ToArray();
        }

        private LODGroupData[] CollectLODGroupDatas(Dictionary<MeshInstanceData, HashSet<PerInstanceBuffer>> tempMeshToMatrices) =>
            gameObject.GetComponentsInChildren<LODGroup>(true)
                      .Select(group => CollectLODGroupData(group, tempMeshToMatrices))
                      .Where(lodGroupData => lodGroupData.LODs.Length != 0 && lodGroupData.LODs[0].Meshes.Length != 0).ToArray();

        private List<MeshInstanceData> CollectMeshData(Renderer[] renderers, Dictionary<MeshInstanceData, HashSet<PerInstanceBuffer>> tempMeshToMatrices)
        {
            var list = new List<MeshInstanceData>();

            foreach (Renderer rndr in renderers)
            {
                var meshRenderer = rndr as MeshRenderer;
                if (meshRenderer == null || meshRenderer.sharedMaterials.Length == 0) return list;

                MeshFilter meshFilter = rndr.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) return list;

                MeshInstanceData meshInstanceData = new MeshInstanceData
                {
                    Transform = meshRenderer.transform,
                    SharedMesh = meshFilter.sharedMesh,
                    SharedMaterials = meshRenderer.sharedMaterials,
                    ReceiveShadows = meshRenderer.receiveShadows,
                    ShadowCastingMode = meshRenderer.shadowCastingMode,
                    Renderer = meshRenderer,
                    LocalToRootMatrix = transform.worldToLocalMatrix * rndr.transform.localToWorldMatrix, // root * child
                };

                list.Add(meshInstanceData);

                PerInstanceBuffer data = new PerInstanceBuffer
                {
                    instMatrix = meshInstanceData.LocalToRootMatrix,
                };

                if (tempMeshToMatrices.TryGetValue(meshInstanceData, out var matrices))
                    matrices.Add(data);
                else
                    tempMeshToMatrices[meshInstanceData] = new HashSet<PerInstanceBuffer> { data };
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

        private LODGroupData CollectLODGroupData(LODGroup lodGroup, Dictionary<MeshInstanceData, HashSet<PerInstanceBuffer>> tempMeshToMatrices)
        {
            lodGroup.RecalculateBounds();

            var LODGroupData = new LODGroupData
            {
                LODGroup = lodGroup,
                Transform = lodGroup.transform,
                ObjectSize = lodGroup.size,
                LODBounds = new Bounds(),
                LODs = lodGroup.GetLODs()
                               .Select(lod => new GPUInstancedLOD
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

            foreach (GPUInstancedLOD mid in lodGroup.LODs)
            foreach (MeshInstanceData data in mid.Meshes)
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
