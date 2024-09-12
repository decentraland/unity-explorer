﻿using DCL.Optimization.Pools;
using DCL.Profiling;
using ECS.StreamableLoading;
using ECS.Unity.SceneBoundsChecker;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Unity.GLTFContainer.Asset.Components
{
    public class GltfContainerAsset : IDisposable
    {
        public struct VisibleMeshCollider
        {
            public Mesh Mesh;
            public GameObject GameObject;
        }

        internal static readonly ListObjectPool<SDKCollider> COLLIDERS_POOL = new (listInstanceDefaultCapacity: 50);
        internal static readonly ListObjectPool<VisibleMeshCollider> VISIBLE_MESH_COLLIDERS_POOL = new (listInstanceDefaultCapacity: 50);
        internal static readonly ListObjectPool<MeshFilter> MESH_FILTERS_POOL = new (listInstanceDefaultCapacity: 50);
        internal static readonly ListObjectPool<Renderer> RENDERERS_POOL = new (listInstanceDefaultCapacity: 50);
        internal static readonly ListObjectPool<SkinnedMeshRenderer> SKINNED_RENDERERS_POOL = new (listInstanceDefaultCapacity: 50);
        internal static readonly ListObjectPool<Animation> ANIMATIONS_POOL = new (listInstanceDefaultCapacity: 50);
        internal static readonly ListObjectPool<Animator> ANIMATORS_POOL = new (listInstanceDefaultCapacity: 50);

        public readonly GameObject Root;

        // Should be pooled
        public readonly List<SDKCollider> InvisibleColliders;

        /// <summary>
        ///     The list of mesh filters that can act as visible colliders
        /// </summary>
        public readonly List<VisibleMeshCollider> VisibleColliderMeshes;

        /// <summary>
        ///     All types of renderers
        /// </summary>
        public readonly List<Renderer> Renderers;

        /// <summary>
        ///     Animation Components (legacy and will be deprecated in the future)
        /// </summary>
        public readonly List<Animation> Animations;

        public readonly List<Animator> Animators;

        /// <summary>
        ///     Visible meshes colliders are created on demand and then become a part of cached data.
        ///     They are decoded from <see cref="VisibleColliderMeshes" /> that are prepared beforehand.
        /// </summary>
        public List<SDKCollider>? DecodedVisibleSDKColliders;

        private IAssetData assetData;

        private GltfContainerAsset(GameObject root, IAssetData assetData, List<SDKCollider> invisibleColliders,
            List<VisibleMeshCollider> visibleColliderMeshes, List<Renderer> renderers, List<Animation> animations,
            List<Animator> animators)
        {
            this.assetData = assetData;

            Root = root;
            InvisibleColliders = invisibleColliders;
            VisibleColliderMeshes = visibleColliderMeshes;
            Renderers = renderers;
            Animations = animations;
            Animators = animators;

            ProfilingCounters.GltfContainerAssetsAmount.Value++;
        }

        public void Dispose()
        {
            assetData.Dereference();
            assetData.Dispose();
            assetData = null;

            COLLIDERS_POOL.Release(InvisibleColliders);
            VISIBLE_MESH_COLLIDERS_POOL.Release(VisibleColliderMeshes);

            if (DecodedVisibleSDKColliders != null)
                COLLIDERS_POOL.Release(DecodedVisibleSDKColliders);

            ANIMATIONS_POOL.Release(Animations);
            ANIMATORS_POOL.Release(Animators);

            UnityObjectUtils.SafeDestroy(Root);

            ProfilingCounters.GltfContainerAssetsAmount.Value--;
        }

        public static GltfContainerAsset Create(GameObject root, IAssetData assetData) =>
            new (root, assetData, COLLIDERS_POOL.Get(), VISIBLE_MESH_COLLIDERS_POOL.Get(), RENDERERS_POOL.Get(), ANIMATIONS_POOL.Get(), ANIMATORS_POOL.Get());
    }
}
