using CommunityToolkit.HighPerformance;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiling;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Buffers;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;
using Utility.Memory;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData : StreamableRefCountData<AssetBundle>
    {
        /// <summary>
        ///     Represents the ownership over the stream the asset bundle was created from
        /// </summary>
        internal struct MemoryStream : IDisposable
        {
            internal static MemoryStream empty => new () { disposed = true };

            private bool disposed;

            internal readonly MemoryChain memoryChain;

            public MemoryStream(MemoryChain memoryChain)
            {
                this.memoryChain = memoryChain;
                disposed = false;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                memoryChain.Dispose();
            }
        }

        private readonly Object? mainAsset;
        private readonly Type? assetType;

        internal AssetBundle AssetBundle => Asset;

        public readonly AssetBundleData[] Dependencies;

        public readonly AssetBundleMetrics? Metrics;

        private readonly string description;

        private MemoryStream underlyingMemory = MemoryStream.empty;

        private bool unloaded;

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, Object mainAsset, Type assetType, AssetBundleData[] dependencies,
            string version = "", string source = "")
            : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            Metrics = metrics;

            this.mainAsset = mainAsset;
            Dependencies = dependencies;
            this.assetType = assetType;

            description = $"AB:{AssetBundle?.name}_{version}_{source}";
        }

        /// <summary>
        ///     Constructor for dependencies (with the unknown asset type)
        /// </summary>
        internal AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, AssetBundleData[] dependencies, MemoryStream underlyingMemory) : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            // Dependencies cant be unloaded, since we don't know who will need them =(
            Metrics = metrics;

            this.mainAsset = null;
            this.assetType = null;
            Dependencies = dependencies;
            this.underlyingMemory = underlyingMemory;
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, GameObject mainAsset, AssetBundleData[] dependencies)
            : this(assetBundle, metrics, mainAsset, typeof(GameObject), dependencies) { }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.ABDataAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.ABReferencedAmount;

        //We immediately unload the asset bundle, as we don't need it anymore.
        //Very hacky, because the asset will remain in cache as AssetBundle == null
        //When DestroyObject is invoked, it will do nothing.
        //When cache in cleaned, the AssetBundleData will be removed from the list. Its there doing nothing
        internal void UnloadAB(ref MemoryStream ownedStream)
        {
            if (unloaded)
                return;

            unloaded = true;
            AssetBundle?.UnloadAsync(false);
            ownedStream.Dispose();
        }

        protected override void DestroyObject()
        {
            foreach (AssetBundleData child in Dependencies)
                child.Dereference();

            if (mainAsset != null)
                Object.DestroyImmediate(mainAsset, true);

            if (AssetBundle)
                AssetBundle.UnloadAsync(unloadAllLoadedObjects: true);

            // Underlying memory for dependencies is released when the dependency itself is fully disposed of
            underlyingMemory.Dispose();
        }

        public T GetMainAsset<T>() where T: Object
        {
            Assert.IsNotNull(assetType, "GetMainAsset can't be called on the Asset Bundle that was not loaded with the asset type specified");

            if (assetType != typeof(T))
                throw new ArgumentException("Asset type mismatch: " + typeof(T) + " != " + assetType);

            return (T)mainAsset!;
        }

        public string GetInstanceName() =>
            description;
    }
}
