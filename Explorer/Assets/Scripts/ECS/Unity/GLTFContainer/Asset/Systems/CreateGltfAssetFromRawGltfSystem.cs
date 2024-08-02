using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGltfAssetFromRawGltfSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;

        internal CreateGltfAssetFromRawGltfSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            ConvertFromGLTFDataQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>))]
        private void ConvertFromGLTFData(in Entity entity,
            ref GetGltfContainerAssetIntention assetIntention,
            ref StreamableLoadingResult<GLTFData> gltfDataResult)
        {
            if (assetIntention.CancellationTokenSource.IsCancellationRequested || gltfDataResult.Asset == null)
                return;

            World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(CreateGltfObject(gltfDataResult.Asset)));
        }

        private static GltfContainerAsset CreateGltfObject(GLTFData gltfData)
        {
            // TODO: Create container GameObject, containerTransform, instantiate GLTF GameObject and
            // populate GltfContainerAsset; Check 'CreateGltfAssetFromAssetBundleSystem.CreateGltfObject()'...

            /*var container = new GameObject(gltfData.gltfImportedData.GetSceneName(0));

            // Let the upper layer decide what to do with the root
            container.SetActive(false);
            Transform containerTransform = container.transform;*/

            var result = GltfContainerAsset.Create(gltfData.containerGameObject, gltfData);

            // GameObject? instance = Object.Instantiate(assetBundleData.GetMainAsset<GameObject>(), containerTransform);

            // TODO: Uncommenting this causes all GLTF to not render at all, need to research why
            // Collect all renderers, they are needed for Visibility system
            // using (PoolExtensions.Scope<List<Renderer>> instanceRenderers = GltfContainerAsset.RENDERERS_POOL.AutoScope())
            // {
            //     gltfData.containerGameObject.GetComponentsInChildren(true, instanceRenderers.Value);
            //     result.Renderers.AddRange(instanceRenderers.Value);
            // }

            // Collect all Animations as they are used in Animation System (only for legacy support, as all of them will eventually be converted to Animators)
            using PoolExtensions.Scope<List<Animation>> animationScope = GltfContainerAsset.ANIMATIONS_POOL.AutoScope();
            {
                gltfData.containerGameObject.GetComponentsInChildren(true, animationScope.Value);
                result.Animations.AddRange(animationScope.Value);
            }

            // Collect all Animators as they are used in Animation System
            using PoolExtensions.Scope<List<Animator>> animatorScope = GltfContainerAsset.ANIMATORS_POOL.AutoScope();
            {
                gltfData.containerGameObject.GetComponentsInChildren(true, animatorScope.Value);
                result.Animators.AddRange(animatorScope.Value);
            }

            // Collect colliders from mesh filters
            // Colliders are created/fetched disabled as its layer is controlled by another system
            /*using (PoolExtensions.Scope<List<MeshFilter>> meshFilterScope = GltfContainerAsset.MESH_FILTERS_POOL.AutoScope())
            {
                List<MeshFilter> list = meshFilterScope.Value;
                instance.GetComponentsInChildren(true, list);

                foreach (MeshFilter meshFilter in list)
                {
                    GameObject go = meshFilter.gameObject;

                    // Consider it a visible collider when it has a renderer on it
                    if (go.GetComponent<Renderer>())
                        AddVisibleMeshCollider(result, go, meshFilter.sharedMesh);
                    else
                        // Gather invisible colliders
                        CreateAndAddMeshCollider(result.InvisibleColliders, go, meshFilter.sharedMesh);
                }
            }*/

            // Collect colliders from skinned mesh renderers
            /*using (PoolExtensions.Scope<List<SkinnedMeshRenderer>> instanceRenderers = GltfContainerAsset.SKINNED_RENDERERS_POOL.AutoScope())
            {
                instance.GetComponentsInChildren(true, instanceRenderers.Value);

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in instanceRenderers.Value)
                {
                    GameObject go = skinnedMeshRenderer.gameObject;

                    // Always considered as visible collider
                    AddVisibleMeshCollider(result, go, skinnedMeshRenderer.sharedMesh);
                }
            }*/

            ProcessGameObjects(gltfData.containerGameObject);

            return result;
        }

        private static void ProcessGameObjects(GameObject root)
        {
            var meshFilters = root.GetComponentsInChildren<MeshFilter>();

            foreach (MeshFilter filter in meshFilters)
            {
                if (filter.name.Contains("_collider", StringComparison.OrdinalIgnoreCase))
                    ConfigureColliders(filter.transform, filter);
            }

            var renderers = root.GetComponentsInChildren<Renderer>();

            foreach (Renderer r in renderers)
            {
                if (r.name.Contains("_collider", StringComparison.OrdinalIgnoreCase))
                    UnityObjectUtils.SafeDestroy(r);
            }
        }

        private static void ConfigureColliders(Transform transform, MeshFilter filter)
        {
            if (filter != null)
            {
                Physics.BakeMesh(filter.sharedMesh.GetInstanceID(), false);
                filter.gameObject.AddComponent<MeshCollider>();
                UnityObjectUtils.SafeDestroy(filter.GetComponent<MeshRenderer>());
            }

            foreach (Transform child in transform)
            {
                var f = child.gameObject.GetComponent<MeshFilter>();
                ConfigureColliders(child, f);
            }
        }
    }
}
