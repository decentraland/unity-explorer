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
            var container = new GameObject($"AB:{assetBundleData.AssetBundle.name}");

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

            // Collect colliders and mesh filters
            // Colliders are created/fetched disabled as its layer is controlled by another system
            using PoolExtensions.Scope<List<MeshFilter>> meshFilterScope = GltfContainerAsset.MESH_FILTERS_POOL.AutoScope();

            List<MeshFilter> list = meshFilterScope.Value;
            instance.GetComponentsInChildren(true, list);

            foreach (MeshFilter meshFilter in list)
            {
                GameObject meshFilterGameObject = meshFilter.gameObject;

                // gather invisible colliders
                CreateInvisibleColliders(result.InvisibleColliders, meshFilterGameObject, meshFilter);

                FilterVisibleColliderCandidate(result.VisibleColliderMeshes, meshFilter);
            }

            return result;
        }

        /// <summary>
        ///     Collect mesh filters suitable for becoming a mesh collider as required
        /// </summary>
        private static void FilterVisibleColliderCandidate(List<MeshFilter> results, MeshFilter meshFilter)
        {
            if (meshFilter.sharedMesh && meshFilter.GetComponent<Renderer>())
                results.Add(meshFilter);
        }

        private static void CreateInvisibleColliders(List<SDKCollider> results, GameObject meshFilterGo, MeshFilter meshFilter)
        {
            // Asset Bundle converter creates Colliders during the processing in some cases
            Collider collider = meshFilterGo.GetComponent<Collider>();

            if (collider)
            {
                // Disable it as its activity controlled by another system based on PBGltfContainer component
                collider.enabled = false;

                results.Add(new SDKCollider(collider));
                return;
            }

            if (!IsCollider(meshFilterGo))
                return;

            MeshCollider newCollider = meshFilterGo.AddComponent<MeshCollider>();
            newCollider.sharedMesh = meshFilter.sharedMesh;
            newCollider.enabled = false;

            results.Add(new SDKCollider(newCollider));
            return;

            // Compatibility layer for old GLTF importer and GLTFast
            // TODO do we need it?
            static bool IsCollider(GameObject go)
            {
                const StringComparison IGNORE_CASE = StringComparison.CurrentCultureIgnoreCase;
                const string COLLIDER_SUFFIX = "_collider";

                return go.name.Contains(COLLIDER_SUFFIX, IGNORE_CASE)
                       || go.transform.parent.name.Contains(COLLIDER_SUFFIX, IGNORE_CASE);
            }
        }
    }
}
