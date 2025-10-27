using DCL.Diagnostics;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData : StreamableRefCountData<AssetBundle>
    {
        private readonly string AssetBundleName;

        public readonly InitialSceneStateMetadata? InitialSceneStateMetadata;
        public bool AssetsDestroyed;

        private bool AssetBundleUnloaded;
        private Dictionary<string, AssetInfo>? Assets;
        private readonly AssetBundleData[] Dependencies;

        public AssetBundleData(AssetBundle assetBundle, InitialSceneStateMetadata? initialSceneState, Object[] loadedAssets, Type? assetType, AssetBundleData[] dependencies, string version = "", string source = "")
            : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            InitialSceneStateMetadata = initialSceneState;

            Assets = new Dictionary<string, AssetInfo>();

            for (var i = 0; i < loadedAssets.Length; i++)
                Assets[loadedAssets[i].name] = new AssetInfo(loadedAssets[i], assetType ?? loadedAssets[i].GetType(), version, source);

            Dependencies = dependencies;

            //Debugging purposes. Test cases may bring a null AB, therefore we need this check
            AssetBundleName = Asset?.name;

            UnloadAB();
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleData[] dependencies) : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            //Dependencies cant be unloaded, since we dont know who will need them =(
            Dependencies = dependencies;
        }

        public AssetBundleData(AssetBundle assetBundle, Object[] loadedAssets, AssetBundleData[] dependencies)
        : this(assetBundle, new InitialSceneStateMetadata(), loadedAssets, typeof(GameObject), dependencies)
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
            if (AssetBundleUnloaded)
                return;

            AssetBundleUnloaded = true;
            Asset?.UnloadAsync(false);
        }

        protected override void DestroyObject()
        {
            foreach (AssetBundleData child in Dependencies)
                child.Dereference();

            if (Assets != null)
            {
                foreach (AssetInfo assetsValue in Assets.Values)
                    Object.DestroyImmediate(assetsValue.Asset, true);

                Assets = null;
            }
            UnloadAB();
            AssetsDestroyed = true;
        }

        /// <summary>
        /// Get an asset loaded from the asset bundle.
        /// </summary>
        /// <param name="assetName">Asset to be requested. If its empty, the first asset loaded will be returned</param>
        /// <typeparam name="T">Type of the asset to load</typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Describes the failling situation</exception>
        public T GetAsset<T>(string assetName = "") where T : Object
        {
            AssetInfo assetInfo;

            if (Assets == null)
                throw new ArgumentException($"No assets were loaded for {AssetBundleName}");

            if (string.IsNullOrEmpty(assetName))
            {
                if (Assets.Count > 1)
                    throw new ArgumentException($"Requested a single asset on a multiple asset Asset Bundle {AssetBundleName}");

                if (Assets.Count == 0)
                    throw new ArgumentException($"No assets were loaded for Asset Bundle {AssetBundleName}");

                assetInfo = Assets.FirstValueOrDefaultNonAlloc();
            }
            else
            {
                if (!Assets.TryGetValue(assetName, out assetInfo))
                    throw new ArgumentException($"No assets were loaded for Asset Bundle {AssetBundleName} with name {assetName}");
            }

            Assert.IsNotNull(assetInfo.AssetType,
                $"GetMainAsset can't be called on the Asset Bundle that was not loaded with the asset type specified for Asset Bundle {AssetBundleName}");

            if (assetInfo.AssetType != typeof(T))
                throw new ArgumentException($"Asset type mismatch: {typeof(T)} != {assetInfo.AssetType} for Asset Bundle {AssetBundleName}");

            return (T)assetInfo.Asset!;
        }

    }
}

public struct AssetInfo
{
    public Object Asset { get; }
    public Type AssetType { get; }

    public AssetInfo(Object asset, Type assetType, string version, string source)
    {
        Asset = asset;
        AssetType = assetType;
        Asset.name = $"AB:{Asset?.name}_{version}_{source}";
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
