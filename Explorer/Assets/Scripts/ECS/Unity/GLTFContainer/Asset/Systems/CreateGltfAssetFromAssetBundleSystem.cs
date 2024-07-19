using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.SceneBoundsChecker;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using VisibleMeshCollider = ECS.Unity.GLTFContainer.Asset.Components.GltfContainerAsset.VisibleMeshCollider;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    /// <summary>
    ///     Creates <see cref="GltfContainerAsset" /> from the <see cref="StreamableLoadingResult{T}" />
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGltfAssetFromAssetBundleSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;

        internal CreateGltfAssetFromAssetBundleSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            ConvertFromAssetBundleQuery(World);
        }

        /// <summary>
        ///     Called on a separate entity with a promise creates a result with <see cref="GltfContainerAsset" />
        /// </summary>
        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>))]
        private void ConvertFromAssetBundle(in Entity entity, ref GetGltfContainerAssetIntention assetIntention, ref StreamableLoadingResult<AssetBundleData> assetBundleResult)
        {
            if (!instantiationFrameTimeBudget.TrySpendBudget() || !memoryBudget.TrySpendBudget())
                return;

            if (assetIntention.CancellationTokenSource.IsCancellationRequested)

                // Don't care anymore, the entity will be deleted in the system that created this promise
                return;

            if (!assetBundleResult.Succeeded)
            {
                // Just propagate an exception, we can't do anything
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(CreateException(assetBundleResult.Exception)));
                return;
            }

            AssetBundleData assetBundleData = assetBundleResult.Asset!;

            // Create a new container root. It will be cached and pooled
            GltfContainerAsset result = CreateGltfObject(assetBundleData);
            World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(result));
        }

        private static GltfContainerAsset CreateGltfObject(AssetBundleData assetBundleData)
        {
            var container = new GameObject(assetBundleData.GetInstanceName());

            // Let the upper layer decide what to do with the root
            container.SetActive(false);
            Transform containerTransform = container.transform;

            var result = GltfContainerAsset.Create(container, assetBundleData);

            GameObject? instance = Object.Instantiate(assetBundleData.GetMainAsset<GameObject>(), containerTransform);

            // Collect all renderers, they are needed for Visibility system
            using (PoolExtensions.Scope<List<Renderer>> instanceRenderers = GltfContainerAsset.RENDERERS_POOL.AutoScope())
            {
                instance.GetComponentsInChildren(true, instanceRenderers.Value);
                result.Renderers.AddRange(instanceRenderers.Value);
            }

            // Collect all Animations as they are used in Animation System (only for legacy support, as all of them will eventually be converted to Animators)
            using PoolExtensions.Scope<List<Animation>> animationScope = GltfContainerAsset.ANIMATIONS_POOL.AutoScope();
            {
                instance.GetComponentsInChildren(true, animationScope.Value);
                result.Animations.AddRange(animationScope.Value);
            }

            // Collect all Animators as they are used in Animation System
            using PoolExtensions.Scope<List<Animator>> animatorScope = GltfContainerAsset.ANIMATORS_POOL.AutoScope();
            {
                instance.GetComponentsInChildren(true, animatorScope.Value);
                result.Animators.AddRange(animatorScope.Value);
            }

            // Collect colliders from mesh filters
            // Colliders are created/fetched disabled as its layer is controlled by another system
            using (PoolExtensions.Scope<List<MeshFilter>> meshFilterScope = GltfContainerAsset.MESH_FILTERS_POOL.AutoScope())
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
            }

            // Collect colliders from skinned mesh renderers
            using (PoolExtensions.Scope<List<SkinnedMeshRenderer>> instanceRenderers = GltfContainerAsset.SKINNED_RENDERERS_POOL.AutoScope())
            {
                instance.GetComponentsInChildren(true, instanceRenderers.Value);

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in instanceRenderers.Value)
                {
                    GameObject go = skinnedMeshRenderer.gameObject;

                    // Always considered as visible collider
                    AddVisibleMeshCollider(result, go, skinnedMeshRenderer.sharedMesh);
                }
            }

            return result;
        }

        private static void AddVisibleMeshCollider(GltfContainerAsset result, GameObject go, Mesh mesh)
        {
            result.VisibleColliderMeshes.Add(new VisibleMeshCollider
            {
                GameObject = go,
                Mesh = mesh,
            });
        }

        private static void CreateAndAddMeshCollider(List<SDKCollider> results, GameObject go, Mesh mesh)
        {
            // Asset Bundle converter creates Colliders during the processing in some cases
            Collider collider = go.GetComponent<Collider>();

            if (collider)
            {
                // Disable it as its activity controlled by another system based on PBGltfContainer component
                collider.enabled = false;

                results.Add(new SDKCollider(collider));
                return;
            }

            if (!IsNamedAsCollider(go))
                return;

            MeshCollider newCollider = go.AddComponent<MeshCollider>();
            newCollider.sharedMesh = mesh;
            newCollider.enabled = false;

            results.Add(new SDKCollider(newCollider));
            return;

            // Compatibility layer for old GLTF importer and GLTFast
            // TODO do we need it?
            static bool IsNamedAsCollider(GameObject go)
            {
                const StringComparison IGNORE_CASE = StringComparison.CurrentCultureIgnoreCase;
                const string COLLIDER_SUFFIX = "_collider";

                return go.name.Contains(COLLIDER_SUFFIX, IGNORE_CASE)
                       || go.transform.parent.name.Contains(COLLIDER_SUFFIX, IGNORE_CASE);
            }
        }
    }
}
