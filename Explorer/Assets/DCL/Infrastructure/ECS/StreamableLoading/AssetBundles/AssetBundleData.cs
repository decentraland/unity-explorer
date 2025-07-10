using DCL.Diagnostics;
using DCL.Profiling;
using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData : StreamableRefCountData<AssetBundle>
    {
        public readonly AssetBundleData[] Dependencies;

        public readonly AssetBundleMetrics? Metrics;
        private readonly Object? mainAsset;
        private readonly Type? assetType;

        private readonly string description;

        // Stream the asset bundle that represent a dependency was created from
        // It's `Null` if it was created from the UnityWebRequest or it's not a dependency
        private readonly Stream underlyingStream = Stream.Null;

        private bool unloaded;

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.ABDataAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.ABReferencedAmount;

        internal AssetBundle AssetBundle => Asset;

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, Object mainAsset, Type assetType, AssetBundleData[] dependencies,
            string version = "", string source = "", Stream? underlyingStream = null)
            : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            Metrics = metrics;

            this.mainAsset = mainAsset;
            Dependencies = dependencies;
            this.assetType = assetType;
            this.underlyingStream = underlyingStream ?? Stream.Null;

            description = $"AB:{AssetBundle?.name}_{version}_{source}";
            UnloadAB();
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, AssetBundleData[] dependencies) : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            //Dependencies cant be unloaded, since we dont know who will need them =(
            Metrics = metrics;

            mainAsset = null;
            assetType = null;
            Dependencies = dependencies;
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, GameObject mainAsset, AssetBundleData[] dependencies)
            : this(assetBundle, metrics, mainAsset, typeof(GameObject), dependencies) { }

        /// <summary>
        ///     Constructor for dependencies (with the unknown asset type) used for partial flow
        /// </summary>
        internal AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, AssetBundleData[] dependencies, Stream stream) : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            // Dependencies cant be unloaded, since we don't know who and when will need them =(
            Metrics = metrics;

            mainAsset = null;
            assetType = null;
            Dependencies = dependencies;
            underlyingStream = stream;
        }

        //We immediately unload the asset bundle, as we don't need it anymore.
        //Very hacky, because the asset will remain in cache as AssetBundle == null
        //When DestroyObject is invoked, it will do nothing.
        //When cache in cleaned, the AssetBundleData will be removed from the list. Its there doing nothing
        internal void UnloadAB()
        {
            if (unloaded)
                return;

            unloaded = true;

            if (AssetBundle)
                AssetBundle.UnloadAsync(false);

            underlyingStream.DisposeAsync();
        }

        protected override void DestroyObject()
        {
            foreach (AssetBundleData child in Dependencies)
                child.Dereference();

            if (mainAsset != null)
                Object.DestroyImmediate(mainAsset, true);

            UnloadAB();
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
