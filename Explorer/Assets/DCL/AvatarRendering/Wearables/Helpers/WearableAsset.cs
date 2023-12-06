using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Represents an original wearable asset
    /// </summary>
    public readonly struct WearableAsset : IDisposable
    {
        public readonly struct RendererInfo
        {
            public readonly SkinnedMeshRenderer SkinnedMeshRenderer;
            public readonly Material Material;

            public RendererInfo(SkinnedMeshRenderer skinnedMeshRenderer, Material material)
            {
                SkinnedMeshRenderer = skinnedMeshRenderer;
                Material = material;
            }
        }

        internal static readonly ListObjectPool<RendererInfo> RENDERER_INFO_POOL = new (listInstanceDefaultCapacity: 3, defaultCapacity: 500);

        /// <summary>
        ///     Can be null in case of a texture
        /// </summary>
        [CanBeNull]
        public readonly GameObject GameObject;
        private readonly List<RendererInfo> rendererInfos;

        internal WearableAsset(GameObject gameObject, List<RendererInfo> rendererInfos)
        {
            GameObject = gameObject;
            this.rendererInfos = rendererInfos;
        }

        public IReadOnlyList<RendererInfo> RendererInfos => rendererInfos;

        /// <summary>
        ///     Currently not called, we don't clean-up assets themselves
        /// </summary>
        public void Dispose()
        {
            RENDERER_INFO_POOL.Release(rendererInfos);
        }
    }
}
