using DCL.Optimization.Pools;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Represents an original wearable asset
    /// </summary>
    public class WearableAsset : IDisposable
    {
        internal static readonly ListObjectPool<RendererInfo> RENDERER_INFO_POOL = new (listInstanceDefaultCapacity: 3, defaultCapacity: 500);

        [CanBeNull] private UnityEngine.Object MainAsset { get; }
        private readonly AssetBundleData assetBundleData;
        private readonly List<RendererInfo> rendererInfos;

        private bool disposed;

        public int ReferenceCount { get; private set; }

        public IReadOnlyList<RendererInfo> RendererInfos => rendererInfos;

        public WearableAsset(UnityEngine.Object mainAsset, List<RendererInfo> rendererInfos, AssetBundleData assetBundleData)
        {
            MainAsset = mainAsset;
            this.rendererInfos = rendererInfos;

            this.assetBundleData = assetBundleData;

            if (mainAsset == null)
                ProfilingCounters.EmptyWearablesAssetsAmount.Value++;

            ProfilingCounters.WearablesAssetsAmount.Value++;
        }
        
        /// <summary>
        ///     Currently not called, we don't clean-up assets themselves
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            RENDERER_INFO_POOL.Release(rendererInfos);
            assetBundleData?.Dereference();

            if (ReferenceCount > 0)
                ProfilingCounters.WearablesAssetsReferencedAmount.Value--;

            if (MainAsset == null)
                ProfilingCounters.EmptyWearablesAssetsAmount.Value--;

            ProfilingCounters.WearablesAssetsAmount.Value--;
        }
        
        public T GetMainAsset<T>() where T : UnityEngine.Object
        {
            return MainAsset as T;
        }

        public void AddReference()
        {
            ReferenceCount++;

            if (ReferenceCount == 1)
                ProfilingCounters.WearablesAssetsReferencedAmount.Value++;
        }

        public void Dereference()
        {
            ReferenceCount--;
            Assert.IsTrue(ReferenceCount >= 0, $"Reference count should never be negative, but was {ReferenceCount}");

            if (ReferenceCount == 0)
                ProfilingCounters.WearablesAssetsReferencedAmount.Value--;
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
