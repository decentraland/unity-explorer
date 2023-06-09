﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Pool;
using Object = UnityEngine.Object;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    /// <summary>
    ///     Creates <see cref="GltfContainerAsset" /> from the <see cref="StreamableLoadingResult{T}" />
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(GltfContainerGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGltfAssetFromAssetBundleSystem : BaseUnityLoopSystem
    {
        /// <summary>
        ///     TODO Temporary throttler to increase performance before we introduce the proper prioritisation mechanism
        /// </summary>
        private readonly IGltfContainerInstantiationThrottler instantiationThrottler;

        internal CreateGltfAssetFromAssetBundleSystem(World world, IGltfContainerInstantiationThrottler throttler) : base(world)
        {
            instantiationThrottler = throttler;
        }

        public override void BeforeUpdate(in float t)
        {
            instantiationThrottler.Reset();
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
            if (assetIntention.CancellationTokenSource.IsCancellationRequested)

                // Don't care anymore, the entity will be deleted in the system that created this promise
                return;

            if (!assetBundleResult.Succeeded)
            {
                // Just propagate an exception, we can't do anything
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(CreateException(assetBundleResult.Exception)));
                return;
            }

            AssetBundleData assetBundleData = assetBundleResult.Asset;

            // if asset bundle has no game objects we can't process it further but the promise should be resolved
            if (assetBundleData.GameObjectNodes.Count == 0)
            {
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(CreateException(new MissingGltfAssetsException(assetBundleData.AssetBundle.name))));
                return;
            }

            if (!instantiationThrottler.Acquire(assetBundleData.GameObjectNodes.Count))

                // delay to the next frame, it will be grabbed by this system again
                return;

            // Create a new container root
            // It will be cached and pooled

            var container = new GameObject($"AB:{assetBundleData.AssetBundle.name}");

            // Let the upper layer decide what to do with the root
            container.SetActive(false);
            Transform containerTransform = container.transform;

            var result = GltfContainerAsset.Create(container);

            for (var i = 0; i < assetBundleData.GameObjectNodes.Count; i++)
            {
                GameObject go = assetBundleData.GameObjectNodes[i];
                GameObject instance = Object.Instantiate(go, containerTransform);

                // Collect all renderers, they are needed for Visibility system
                using (PoolExtensions.Scope<List<Renderer>> instanceRenderers = GltfContainerAsset.RENDERERS_POOL.AutoScope())
                {
                    instance.GetComponentsInChildren(true, instanceRenderers.Value);
                    result.Renderers.AddRange(instanceRenderers.Value);
                }

                // Collect colliders and mesh filters
                // Colliders are created/fetched disabled as its layer is controlled by another system

                using PoolExtensions.Scope<List<MeshFilter>> meshFilterScope = GltfContainerAsset.MESH_FILTERS_POOL.AutoScope();

                List<MeshFilter> list = meshFilterScope.Value;
                instance.GetComponentsInChildren(true, list);

                for (var j = 0; j < list.Count; j++)
                {
                    MeshFilter meshFilter = list[j];

                    GameObject meshFilterGameObject = meshFilter.gameObject;

                    // gather invisible colliders
                    CreateInvisibleColliders(result.InvisibleColliders, meshFilterGameObject, meshFilter);

                    FilterVisibleColliderCandidate(result.VisibleColliderMeshes, meshFilter);
                }
            }

            World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(result));
        }

        /// <summary>
        ///     Collect mesh filters suitable for becoming a mesh collider as required
        /// </summary>
        private static void FilterVisibleColliderCandidate(List<MeshFilter> results, MeshFilter meshFilter)
        {
            if (meshFilter.sharedMesh && meshFilter.GetComponent<Renderer>())
                results.Add(meshFilter);
        }

        private static void CreateInvisibleColliders(List<Collider> results, GameObject meshFilterGo, MeshFilter meshFilter)
        {
            // Asset Bundle converter creates Colliders during the processing in some cases
            Collider collider = meshFilterGo.GetComponent<Collider>();

            if (collider)
            {
                // Disable it as its activity controlled by another system based on PBGltfContainer component
                collider.enabled = false;

                results.Add(collider);
                return;
            }

            // Compatibility layer for old GLTF importer and GLTFast // TODO do we need it?
            static bool IsCollider(GameObject go)
            {
                const StringComparison IGNORE_CASE = StringComparison.CurrentCultureIgnoreCase;
                const string COLLIDER_SUFFIX = "_collider";

                return go.name.Contains(COLLIDER_SUFFIX, IGNORE_CASE)
                       || go.transform.parent.name.Contains(COLLIDER_SUFFIX, IGNORE_CASE);
            }

            if (!IsCollider(meshFilterGo))
                return;

            MeshCollider newCollider = meshFilterGo.AddComponent<MeshCollider>();
            newCollider.sharedMesh = meshFilter.sharedMesh;
            newCollider.enabled = false;

            results.Add(newCollider);
        }
    }
}
