using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Utility.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Represents an original wearable asset
    /// </summary>
    public class WearableAsset : IDisposable
    {
        internal static readonly ListObjectPool<RendererInfo> RENDERER_INFO_POOL = new (listInstanceDefaultCapacity: 3, defaultCapacity: 500);

        /// <summary>
        ///     Can be null in case of a texture
        /// </summary>
        [CanBeNull]
        public readonly GameObject GameObject;
        private readonly List<RendererInfo> rendererInfos;
        private readonly AssetBundleData assetBundleData;

        public int ReferenceCount { get; private set; }

        public IReadOnlyList<RendererInfo> RendererInfos => rendererInfos;

        public WearableAsset(GameObject gameObject, List<RendererInfo> rendererInfos, AssetBundleData assetBundleData)
        {
            GameObject = gameObject;
            this.rendererInfos = rendererInfos;
            this.assetBundleData = assetBundleData;

            ProfilingCounters.WearablesAssetsAmount.Value++;
        }

        /// <summary>
        ///     Currently not called, we don't clean-up assets themselves
        /// </summary>
        public void Dispose()
        {
            RENDERER_INFO_POOL.Release(rendererInfos);

            ProfilingCounters.WearablesAssetsAmount.Value--;
        }

        public void AddReference()
        {
            ReferenceCount++;
        }

        public void Dereference()
        {
            ReferenceCount--;
            Assert.IsTrue(ReferenceCount >= 0, "Reference count should never be negative");
        }

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
    }
}
