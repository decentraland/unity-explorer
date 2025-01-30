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
        [SerializeField] public List<GPUInstancedMesh> InstancedMeshes;

        [SerializeField, Tooltip("for RenderMeshIndirect")] public List<GPUInstancedMesh> IndirectInstancedMeshes;
        [SerializeField, Tooltip("for RenderMeshInstanced")] public List<GPUInstancedMesh> DirectInstancedMeshes;

        [SerializeField] private Shader indirectShader;

        public MeshRenderingData[] Meshes;
        public LODGroupData[] LODGroups;

        // Call it not in prefab mode, but from Asset in Project window
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
            foreach (MeshRenderingData mesh in Meshes)
                mesh.Renderer.enabled = false;

            foreach (LODGroupData lodGroup in LODGroups)
            {
                if (lodGroup.LODs.Length == 0) continue;

                lodGroup.LODGroup.enabled = false;

                foreach (GPUInstancedLOD lod in lodGroup.LODs)
                foreach (MeshRenderingData mesh in lod.Meshes)
                    mesh.Renderer.enabled = false;
            }
        }

        private bool IsMyShader(Material[] materials)
        {
            if (indirectShader == null || materials == null) return false;
            return materials.Any(m => m != null && m.shader == indirectShader);
        }

        private void CollectDataFromPrefabAsset()
        {
            var tempMeshToMatrices = new Dictionary<MeshRenderingData, HashSet<PerInstanceBuffer>>();

            Meshes = CollectStandaloneMeshesData(tempMeshToMatrices);
            LODGroups = CollectLODGroupDatas(tempMeshToMatrices);

            InstancedMeshes = new List<GPUInstancedMesh>(tempMeshToMatrices.Keys.Count);
            IndirectInstancedMeshes = new List<GPUInstancedMesh>(tempMeshToMatrices.Keys.Count);
            DirectInstancedMeshes   = new List<GPUInstancedMesh>(tempMeshToMatrices.Keys.Count);

            foreach (KeyValuePair<MeshRenderingData, HashSet<PerInstanceBuffer>> kvp in tempMeshToMatrices)
            {
                var meshInstance = new GPUInstancedMesh
                {
                    meshRenderingData = kvp.Key,
                    PerInstancesData = kvp.Value.ToArray()
                };

                // Optionally, if there is a need for unified view of all GPU-instanced meshes
                InstancedMeshes.Add(meshInstance);

                if (IsMyShader(kvp.Key.Renderer.sharedMaterials))
                    IndirectInstancedMeshes.Add(meshInstance);
                else
                    DirectInstancedMeshes.Add(meshInstance);
            }
        }

        private MeshRenderingData[] CollectStandaloneMeshesData(Dictionary<MeshRenderingData, HashSet<PerInstanceBuffer>> tempMeshToMatrices)
        {
            Renderer[] standaloneRenderers = gameObject.GetComponentsInChildren<Renderer>(true)
                                                       .Where(r => !AssignedToLODGroupInPrefabHierarchy(r.transform)).ToArray();

            return CollectMeshData(standaloneRenderers, tempMeshToMatrices).ToArray();
        }

        private LODGroupData[] CollectLODGroupDatas(Dictionary<MeshRenderingData, HashSet<PerInstanceBuffer>> tempMeshToMatrices) =>
            gameObject.GetComponentsInChildren<LODGroup>(true)
                      .Select(group => CollectLODGroupData(group, tempMeshToMatrices))
                      .Where(lodGroupData => lodGroupData.LODs.Length != 0 && lodGroupData.LODs[0].Meshes.Length != 0).ToArray();

        private List<MeshRenderingData> CollectMeshData(Renderer[] renderers, Dictionary<MeshRenderingData, HashSet<PerInstanceBuffer>> tempMeshToMatrices)
        {
            var list = new List<MeshRenderingData>();

            foreach (Renderer rndr in renderers)
            {
                var meshRenderer = rndr as MeshRenderer;
                if (meshRenderer == null || meshRenderer.sharedMaterials.Length == 0) return list;

                MeshFilter meshFilter = rndr.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) return list;

                MeshRenderingData meshRenderingData = new MeshRenderingData(meshRenderer);
                {
                    // Transform = meshRenderer.transform,
                    // SharedMesh = meshFilter.sharedMesh,
                    // SharedMaterials = meshRenderer.sharedMaterials,
                    // ReceiveShadows = meshRenderer.receiveShadows,
                    // ShadowCastingMode = meshRenderer.shadowCastingMode,
                    // Renderer = meshRenderer,
                    // LocalToRootMatrix = transform.worldToLocalMatrix * rndr.transform.localToWorldMatrix, // root * child
                };

                list.Add(meshRenderingData);

                PerInstanceBuffer data = new PerInstanceBuffer
                {
                    instMatrix = transform.worldToLocalMatrix * rndr.transform.localToWorldMatrix,
                };

                if (tempMeshToMatrices.TryGetValue(meshRenderingData, out var matrices))
                    matrices.Add(data);
                else
                    tempMeshToMatrices[meshRenderingData] = new HashSet<PerInstanceBuffer> { data };
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

        private LODGroupData CollectLODGroupData(LODGroup lodGroup, Dictionary<MeshRenderingData, HashSet<PerInstanceBuffer>> tempMeshToMatrices)
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
            foreach (MeshRenderingData data in mid.Meshes)
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
