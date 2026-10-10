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

    public class AttachmentRegularAsset : AttachmentAssetBase
    {
        public static readonly ListObjectPool<RendererInfo> RENDERER_INFO_POOL = new (listInstanceDefaultCapacity: 3, defaultCapacity: 500);

        private readonly List<RendererInfo> rendererInfos;
        private readonly HashSet<EntityId> tangentsRecalculatedMeshes = new ();
        public readonly GameObject MainAsset;

        public IReadOnlyList<RendererInfo> RendererInfos => rendererInfos;

        public AttachmentRegularAsset(GameObject mainAsset, List<RendererInfo> rendererInfos, IStreamableRefCountData streamableData) : base(streamableData)
        {
            MainAsset = mainAsset;
            this.rendererInfos = rendererInfos;

            if (mainAsset == null)
                ProfilingCounters.EmptyWearablesAssetsAmount.Value++;

            ProfilingCounters.WearablesAssetsAmount.Value++;
        }

        protected override void DisposeInternal()
        {
            RENDERER_INFO_POOL.Release(rendererInfos);
            tangentsRecalculatedMeshes.Clear();

            if (ReferenceCount > 0)
                ProfilingCounters.WearablesAssetsReferencedAmount.Value--;

            if (MainAsset == null)
                ProfilingCounters.EmptyWearablesAssetsAmount.Value--;

            ProfilingCounters.WearablesAssetsAmount.Value--;
        }

        /// <summary>
        ///     Marks the tangents of <paramref name="mesh" /> as recalculated, returning true only the first time
        ///     the mesh is passed in. The marks are kept per asset because the meshes belong to the streamable data
        ///     this asset references: they are dropped on disposal, before that data is destroyed and the entity ids
        ///     of its meshes become free for Unity to hand out to newly loaded ones.
        /// </summary>
        public bool TryMarkTangentsRecalculated(Mesh mesh) =>
            tangentsRecalculatedMeshes.Add(mesh.GetEntityId());

        public readonly struct RendererInfo
        {
            public readonly Material Material;

            public RendererInfo(Material material)
            {
                Material = material != null ? material : DefaultMaterial.New();
            }
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
