using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Pool;

namespace ECS.Unity.GLTFContainer.Asset.Components
{
    public class GltfContainerAsset : IDisposable
    {
        internal static readonly ListObjectPool<Collider> COLLIDERS_POOL = new (listInstanceDefaultCapacity: 50);
        internal static readonly ListObjectPool<MeshFilter> MESH_FILTERS_POOL = new (listInstanceDefaultCapacity: 50);
        internal static readonly ListObjectPool<Renderer> RENDERERS_POOL = new (listInstanceDefaultCapacity: 50);

        public readonly GameObject Root;

        // Should be pooled
        public readonly List<Collider> InvisibleColliders;

        /// <summary>
        ///     The list of mesh filters that can act as visible colliders
        /// </summary>
        public readonly List<MeshFilter> VisibleColliderMeshes;

        /// <summary>
        ///     All types of renderers
        /// </summary>
        public readonly List<Renderer> Renderers;

        /// <summary>
        ///     Visible meshes colliders are created on demand and then become a part of cached data.
        ///     They are decoded from <see cref="VisibleColliderMeshes" /> that are prepared beforehand.
        /// </summary>
        public List<Collider> VisibleMeshesColliders;

        private GltfContainerAsset(GameObject root, List<Collider> invisibleColliders, List<MeshFilter> visibleColliderMeshes, List<Renderer> renderers)
        {
            Root = root;
            InvisibleColliders = invisibleColliders;
            VisibleColliderMeshes = visibleColliderMeshes;
            Renderers = renderers;
        }

        public void Dispose()
        {
            COLLIDERS_POOL.Release(InvisibleColliders);
            MESH_FILTERS_POOL.Release(VisibleColliderMeshes);

            if (VisibleMeshesColliders != null)
                COLLIDERS_POOL.Release(VisibleMeshesColliders);

            UnityObjectUtils.SafeDestroy(Root);
        }

        public static GltfContainerAsset Create(GameObject root) =>
            new (root, COLLIDERS_POOL.Get(), MESH_FILTERS_POOL.Get(), RENDERERS_POOL.Get());
    }
}
