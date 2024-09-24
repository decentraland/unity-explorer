using DCL.Diagnostics;
using DCL.Profiling;
using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData : StreamableRefCountData<AssetBundle>, IStreamableRefCountData
    {
        private readonly Object? mainAsset;
        private readonly Type? assetType;

        public AssetBundle AssetBundle => Asset;

        public readonly AssetBundleData[] Dependencies;

        public readonly AssetBundleMetrics? Metrics;

        private readonly string version;
        private readonly string source;

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, Object mainAsset, Type assetType, AssetBundleData[] dependencies, string version = "", string source = "")
            : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            Metrics = metrics;

            this.mainAsset = mainAsset;
            Dependencies = dependencies;
            this.assetType = assetType;
            this.version = version;
            this.source = source;
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, AssetBundleData[] dependencies) : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            Metrics = metrics;

            this.mainAsset = null;
            this.assetType = null;
            Dependencies = dependencies;
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, GameObject mainAsset, AssetBundleData[] dependencies)
        : this(assetBundle, metrics, mainAsset, typeof(GameObject), dependencies)
        {
        }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.ABDataAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.ABReferencedAmount;

        protected override void DestroyObject()
        {
            if (AssetBundle != null)
            {
                foreach (AssetBundleData child in Dependencies)
                    child.Dereference();

                AssetBundle.UnloadAsync(unloadAllLoadedObjects: true);
            }
        }

        public T GetMainAsset<T>() where T : Object
        {
            Assert.IsNotNull(assetType, "GetMainAsset can't be called on the Asset Bundle that was not loaded with the asset type specified");

            if (assetType != typeof(T))
                throw new ArgumentException("Asset type mismatch: " + typeof(T) + " != " + assetType);

            return (T)mainAsset!;
        }

        public string GetInstanceName() =>
            $"AB:{AssetBundle.name}_{version}_{source}";
    }
}
