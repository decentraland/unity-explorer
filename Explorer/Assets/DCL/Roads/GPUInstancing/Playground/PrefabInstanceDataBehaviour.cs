using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Roads.GPUInstancing.Playground
{
    public class PrefabInstanceDataBehaviour : MonoBehaviour
    {
        public PrefabInstanceData PrefabInstance;

        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
#if UNITY_EDITOR
            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);

            if (isPrefabAsset)
            {
                CollectDataFromPrefabAsset();
                return;
            }
#endif

            CollectDataFromInstance();
        }

        private void CollectDataFromInstance()
        {
            PrefabInstance = new PrefabInstanceData
            {
                Meshes = CollectMeshesNotIncludedInLOD(),
                LODGroups = CollectLODGroupsData(),
            };
        }

        private LODGroupData[] CollectLODGroupsData() =>
            (from lodGroup in GetComponentsInChildren<LODGroup>(includeInactive: false)
                where lodGroup.enabled && lodGroup.gameObject.activeInHierarchy
                select CollectLODGroupData(lodGroup)).ToArray();

        private MeshData[] CollectMeshesNotIncludedInLOD() =>
            (from renderer in GetComponentsInChildren<MeshRenderer>(includeInactive: false)
                where renderer.enabled && renderer.gameObject.activeInHierarchy
                where !HasLODGroupInHierarchy(renderer.transform)
                let meshFilter = renderer.GetComponent<MeshFilter>()
                where meshFilter != null && meshFilter.sharedMesh != null
                select new MeshData
                {
                    Transform = renderer.transform,
                    SharedMesh = meshFilter.sharedMesh,
                    SharedMaterials = renderer.sharedMaterials,
                    ReceiveShadows = renderer.receiveShadows,
                    ShadowCastingMode = renderer.shadowCastingMode,
                    Renderer = renderer,
                }).ToArray();

        private static LODGroupData CollectLODGroupData(LODGroup lodGroup)
        {
            lodGroup.RecalculateBounds();

            var LODGroupData = new LODGroupData
            {
                LODGroup = lodGroup,
                Transform = lodGroup.transform,
                ObjectSize = lodGroup.size,
                LODBounds = new Bounds(),
                LODs = lodGroup.GetLODs().Select(lod => new LODEntryMeshData
                                {
                                    Meshes = CollectLODMeshData(lod).ToArray(),
                                    Distance = lod.screenRelativeTransitionHeight,
                                })
                               .ToArray(),
            };

            CalculateGroupBounds(LODGroupData);

            return LODGroupData;
        }

        private static List<MeshData> CollectLODMeshData(UnityEngine.LOD lod)
        {
            var list = new List<MeshData>();

            foreach (Renderer renderer in lod.renderers)
            {
                if (renderer is SkinnedMeshRenderer) continue;

                var meshRenderer = renderer as MeshRenderer;
                if (meshRenderer == null || meshRenderer.sharedMaterials.Length == 0) return list;

                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) return list;

                list.Add(new MeshData
                {
                    Transform = meshRenderer.transform,
                    SharedMesh = meshFilter.sharedMesh,
                    SharedMaterials = meshRenderer.sharedMaterials,
                    ReceiveShadows = meshRenderer.receiveShadows,
                    ShadowCastingMode = meshRenderer.shadowCastingMode,
                    Renderer = meshRenderer,
                });
            }

            return list;
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

        private bool HasLODGroupInHierarchy(Transform transform)
        {
            while (transform != this.transform)
            {
                if (transform.GetComponent<LODGroup>() != null)
                    return true;

                transform = transform.parent;
            }

            return false;
        }

#if UNITY_EDITOR
        private void CollectDataFromPrefabAsset()
        {
            // Get all components regardless of their enabled state when working with prefab asset
            MeshRenderer[] allRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
            LODGroup[] allLODGroups = gameObject.GetComponentsInChildren<LODGroup>(true);

            PrefabInstance = new PrefabInstanceData
            {
                Meshes = (from renderer in allRenderers
                    where !HasLODGroupInPrefabHierarchy(renderer.transform)
                    let meshFilter = renderer.GetComponent<MeshFilter>()
                    where meshFilter != null && meshFilter.sharedMesh != null
                    select new MeshData
                    {
                        Transform = renderer.transform,
                        SharedMesh = meshFilter.sharedMesh,
                        SharedMaterials = renderer.sharedMaterials,
                        ReceiveShadows = renderer.receiveShadows,
                        ShadowCastingMode = renderer.shadowCastingMode,
                        Renderer = renderer,
                    }).ToArray(),

                LODGroups = (from lodGroup in allLODGroups
                    select CollectLODGroupData(lodGroup)).ToArray(),
            };
        }

        private bool HasLODGroupInPrefabHierarchy(Transform transform)
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
#endif
    }
}
