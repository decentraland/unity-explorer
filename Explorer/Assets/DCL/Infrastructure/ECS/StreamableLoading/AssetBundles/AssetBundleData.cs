using DCL.Diagnostics;
using DCL.Profiling;
using System;
using System.Collections.Generic;
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
        public readonly Object?[] assets;
        private readonly Type? assetType;

        internal AssetBundle AssetBundle => Asset;

        public readonly AssetBundleData[] Dependencies;

        public readonly AssetBundleMetrics? Metrics;

        private readonly string description;

        private bool unloaded;

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, Object?[] assets, Type assetType, AssetBundleData[] dependencies, string version = "", string source = "")
            : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            Metrics = metrics;

            this.assets = assets;
            Dependencies = dependencies;
            this.assetType = assetType;

            description = $"AB:{AssetBundle?.name}_{version}_{source}";
            UnloadAB();
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, AssetBundleData[] dependencies) : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            //Dependencies cant be unloaded, since we dont know who will need them =(
            Metrics = metrics;

            this.assets = Array.Empty<Object>();
            this.assetType = null;
            Dependencies = dependencies;
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, GameObject mainAsset, AssetBundleData[] dependencies)
        : this(assetBundle, metrics, new Object?[]{mainAsset}, typeof(GameObject), dependencies)
        {
        }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.ABDataAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.ABReferencedAmount;


        private void UnloadAB()
        {
            //We immediately unload the asset bundle, as we don't need it anymore.
            //Very hacky, because the asset will remain in cache as AssetBundle == null
            //When DestroyObject is invoked, it will do nothing.
            //When cache in cleaned, the AssetBundleData will be removed from the list. Its there doing nothing
            if (unloaded)
                return;
            unloaded = true;
            AssetBundle?.UnloadAsync(false);
        }

        protected override void DestroyObject()
        {
            foreach (AssetBundleData child in Dependencies)
                child.Dereference();

            foreach (Object asset in assets)
                Object.DestroyImmediate(asset, true);

            if (unloaded) return;
            if(AssetBundle && AssetBundle != null) AssetBundle.UnloadAsync(unloadAllLoadedObjects: true);
        }

        public T GetMainAsset<T>() where T : Object
        {
            Assert.IsNotNull(assetType, "GetMainAsset can't be called on the Asset Bundle that was not loaded with the asset type specified");

            if (assetType != typeof(T))
                throw new ArgumentException("Asset type mismatch: " + typeof(T) + " != " + assetType);
            return (T)assets[0]!;
        }

        //TODO (JUANI) : OPTIMIZE
        public T GetAsset<T>(string name) where T : Object
        {
            Assert.IsNotNull(assetType, "GetMainAsset can't be called on the Asset Bundle that was not loaded with the asset type specified");

            if (assetType != typeof(T))
                throw new ArgumentException("Asset type mismatch: " + typeof(T) + " != " + assetType);

            //TODO (JUANI): Handle name missing issue
            T objectToReturn = (T)assets[0]!;
            foreach (Object asset in assets)
            {
                if (asset.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return (T)asset;
            }
            return objectToReturn;
        }

        public string GetInstanceName() => description;

        public bool HasMultipleAssets() => assets.Length > 1;

    }
}
