using DCL.Optimization.Pools;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Utility.Primitives;

namespace DCL.AvatarRendering.Loading.Assets
{
    /// <summary>
    /// Facial feature is represented by the main texture and the optional mask
    /// </summary>
    public class AttachmentTextureAsset : AttachmentAssetBase
    {
        public readonly Texture Texture;

        public AttachmentTextureAsset(Texture texture, AssetBundleData assetBundleData)  : base(assetBundleData)
        {
            this.Texture = texture;
        }

        protected override void DisposeInternal()
        {
        }
    }

    public class AttachmentRegularAsset : AttachmentAssetBase
    {
        public static readonly ListObjectPool<RendererInfo> RENDERER_INFO_POOL = new (listInstanceDefaultCapacity: 3, defaultCapacity: 500);

        private readonly List<RendererInfo> rendererInfos;
        private readonly IReadOnlyList<AnimationClip>? animationClips;
        public readonly GameObject MainAsset;

        public IReadOnlyList<RendererInfo> RendererInfos => rendererInfos;
        public IReadOnlyList<AnimationClip>? AnimationClips => animationClips;

        public AttachmentRegularAsset(GameObject mainAsset, List<RendererInfo> rendererInfos, AssetBundleData? assetBundleData, IReadOnlyList<AnimationClip>? animationClips = null)
            : base(assetBundleData)
        {
            MainAsset = mainAsset;
            this.rendererInfos = rendererInfos;
            this.animationClips = animationClips;

            if (mainAsset == null)
                ProfilingCounters.EmptyWearablesAssetsAmount.Value++;

            ProfilingCounters.WearablesAssetsAmount.Value++;
        }

        protected override void DisposeInternal()
        {
            RENDERER_INFO_POOL.Release(rendererInfos);

            if (ReferenceCount > 0)
                ProfilingCounters.WearablesAssetsReferencedAmount.Value--;

            if (MainAsset == null)
                ProfilingCounters.EmptyWearablesAssetsAmount.Value--;

            ProfilingCounters.WearablesAssetsAmount.Value--;
        }

        public readonly struct RendererInfo
        {
            public readonly SkinnedMeshRenderer SkinnedMeshRenderer;
            public readonly Material Material;

            public RendererInfo(SkinnedMeshRenderer skinnedMeshRenderer, Material material)
            {
                SkinnedMeshRenderer = skinnedMeshRenderer;
                Material = material != null ? material : DefaultMaterial.New();
            }
        }

        public string GetInstanceName()
        {
            return assetBundleData != null ? assetBundleData.GetInstanceName() : $"NOT_AB_{MainAsset.name}";
        }
    }

    /// <summary>
    ///     Represents an original wearable asset
    /// </summary>
    public abstract class AttachmentAssetBase : IDisposable
    {
        protected readonly AssetBundleData? assetBundleData;

        private bool disposed;

        protected AttachmentAssetBase(AssetBundleData? assetBundleData)
        {
            this.assetBundleData = assetBundleData;
        }

        public int ReferenceCount { get; private set; }

        public void Dispose()
        {
            if (disposed)
                return;

            assetBundleData?.Dereference();

            DisposeInternal();

            disposed = true;
        }

        protected abstract void DisposeInternal();

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
    }
}
