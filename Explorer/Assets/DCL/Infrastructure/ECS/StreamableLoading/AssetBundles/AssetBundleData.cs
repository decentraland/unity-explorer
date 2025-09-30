using DCL.Diagnostics;
using DCL.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private Dictionary<string, AssetInfo> assets;
        private readonly string AssetBundleName;

        private bool unloaded;


        public readonly AssetBundleData[] Dependencies;
        public readonly AssetBundleMetrics? Metrics;

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, Object[] loadedAssets, Type assetType, AssetBundleData[] dependencies, string version = "", string source = "")
            : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            Metrics = metrics;

            assets = new Dictionary<string, AssetInfo>();
            for (var i = 0; i < loadedAssets.Length; i++)
                assets[loadedAssets[i].name] = new AssetInfo(loadedAssets[i], assetType, version, source);

            Dependencies = dependencies;
            AssetBundleName = assetBundle.name;
            UnloadAB();
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, AssetBundleData[] dependencies) : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            //Dependencies cant be unloaded, since we dont know who will need them =(
            Metrics = metrics;
            Dependencies = dependencies;
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, Object[] loadedAssets, AssetBundleData[] dependencies)
        : this(assetBundle, metrics, loadedAssets, typeof(GameObject), dependencies)
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
            Asset?.UnloadAsync(false);
        }

        protected override void DestroyObject()
        {
            foreach (AssetBundleData child in Dependencies)
                child.Dereference();

            foreach (AssetInfo assetsValue in assets.Values)
                Object.DestroyImmediate(assetsValue.Asset, true);

            assets = null;

            if (unloaded) return;
            if(Asset && Asset != null) Asset.UnloadAsync(unloadAllLoadedObjects: true);
        }

        public T GetSingleAsset<T>(string assetName = "") where T : Object
        {
            if(assets.Count > 1)
                throw new ArgumentException($"Requested a single asset on a multiple asset Asset Bundle {AssetBundleName}");

            if(assets.Count == 0)
                throw new ArgumentException($"No assets were loaded for Asset Bundle {AssetBundleName}");;

            AssetInfo assetInfo = assets.FirstValueOrDefaultNonAlloc();

            Assert.IsNotNull(assetInfo.AssetType, $"GetMainAsset can't be called on the Asset Bundle that was not loaded with the asset type specified for Asset Bundle {AssetBundleName}");

            if (assetInfo.AssetType != typeof(T))
                throw new ArgumentException($"Asset type mismatch: {typeof(T)} != {assetInfo.AssetType} for Asset Bundle {AssetBundleName}");

            return (T)assetInfo.Asset!;
        }

        public string GetAssetDescription(string assetName = "")
        {
            //validations where done when the asset was requested

            if (string.IsNullOrEmpty(assetName))
            {
                AssetInfo assetInfo = assets.FirstValueOrDefaultNonAlloc();
                return assetInfo.Description;
            }
            else
            {
                AssetInfo assetInfo = assets[assetName];
                return assetInfo.Description;
            }
        }

        public T GetAsset<T>(string name) where T: Object
        {
            bool tryGetAsset = assets.TryGetValue(name, out AssetInfo assetInfo);

            if(!tryGetAsset)
                throw new ArgumentException("Requested an asset that is not part of the asset bundle for Asset Bundle {Asset.name}");;

            if (assetInfo.AssetType != typeof(T))
                throw new ArgumentException($"Asset type mismatch: {typeof(T)} != {assetInfo.AssetType} for Asset Bundle {Asset.name}");;

            return (T)assetInfo.Asset!;
        }

    }
}

public struct AssetInfo
{
    public Object Asset { get; }
    public Type AssetType { get; }
    public string Description { get; }

    public AssetInfo(Object asset, Type assetType, string version, string source)
    {
        Asset = asset;
        AssetType = assetType;
        Description = $"AB:{Asset?.name}_{version}_{source}";
    }
}

public static class DictionaryExtensions
{
    public static TValue FirstValueOrDefaultNonAlloc<TKey, TValue>(this Dictionary<TKey, TValue> dict)
    {
        foreach (var value in dict.Values)
            return value;

        return default;
    }
}
