using DCL.Optimization.Pools;
using DCL.Profiling;
using ECS.StreamableLoading;
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

        public AttachmentTextureAsset(Texture texture, IStreamableRefCountData streamableData) : base(streamableData)
        {
            this.Texture = texture;
        }

        protected override void DisposeInternal()
        {
        }
    }

    /// <summary>
    ///
    /// </summary>
    public class EmoteOutcomeAnimationPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public EmoteOutcomeAnimationPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    public class AttachmentRegularAsset : AttachmentAssetBase
    {
        public static readonly ListObjectPool<RendererInfo> RENDERER_INFO_POOL = new (listInstanceDefaultCapacity: 3, defaultCapacity: 500);
        public static readonly ListObjectPool<EmoteOutcomeAnimationPose> OUTCOME_ANIMATION_POSE_POOL = new (listInstanceDefaultCapacity: 3, defaultCapacity: 200);

        private readonly List<RendererInfo> rendererInfos;
        public readonly GameObject MainAsset;

        public IReadOnlyList<RendererInfo> RendererInfos => rendererInfos;

        public IReadOnlyList<EmoteOutcomeAnimationPose>? SocialEmoteOutcomeAnimationStartPoses;

        public AttachmentRegularAsset(GameObject mainAsset, List<RendererInfo> rendererInfos, IStreamableRefCountData streamableData, List<EmoteOutcomeAnimationPose>? socialEmoteOutcomeAnimationStartPose) : base(streamableData)
        {
            MainAsset = mainAsset;
            this.rendererInfos = rendererInfos;
            this.SocialEmoteOutcomeAnimationStartPoses = socialEmoteOutcomeAnimationStartPose;

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
            public readonly Material Material;

            public RendererInfo(Material material)
            {
                Material = material != null ? material : DefaultMaterial.New();
            }
        }

        public string GetInstanceName()
        {
            var assetBundleData = assetData as AssetBundleData;
            return assetBundleData != null ? assetBundleData.GetInstanceName() : $"NOT_AB_{MainAsset.name}";
        }
    }

    /// <summary>
    ///     Represents an original wearable asset (raw or asset bundle)
    /// </summary>
    public abstract class AttachmentAssetBase : IDisposable
    {
        internal readonly IStreamableRefCountData assetData;

        private bool disposed;

        protected AttachmentAssetBase(IStreamableRefCountData streamableData)
        {
            this.assetData = streamableData;
        }

        public int ReferenceCount { get; private set; }

        public void Dispose()
        {
            if (disposed)
                return;

            assetData.Dereference();

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
