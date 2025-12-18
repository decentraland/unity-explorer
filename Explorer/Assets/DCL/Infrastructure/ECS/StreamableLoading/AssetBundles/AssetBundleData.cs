using DCL.Diagnostics;
using DCL.Profiling;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData : StreamableRefCountData<AssetBundle>
    {
        /// <summary>
        /// Flag to enable/disable material sharing across instances.
        /// When enabled, materials are cached on first instantiation and reused for subsequent instances.
        /// Set to false to revert to the old behavior where each instance gets its own material copies.
        /// </summary>
        public static bool ENABLE_MATERIAL_SHARING = true;

        private readonly string AssetBundleName;

        public readonly InitialSceneStateMetadata? InitialSceneStateMetadata;

        private bool AssetBundleUnloaded;
        private Dictionary<string, AssetInfo>? Assets;
        private readonly AssetBundleData[] Dependencies;

        /// <summary>
        /// Cache for original shared materials per asset, keyed by asset name.
        /// Used to restore shared materials after instantiation to avoid material duplication.
        /// </summary>
        private Dictionary<string, Material[][]>? cachedMaterials;

        public AssetBundleData(AssetBundle assetBundle, InitialSceneStateMetadata? initialSceneState, Object[] loadedAssets, Type? assetType, AssetBundleData[] dependencies, string version = "", string source = "")
            : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            InitialSceneStateMetadata = initialSceneState;

            Assets = new Dictionary<string, AssetInfo>();

            for (var i = 0; i < loadedAssets.Length; i++)
                Assets[loadedAssets[i].name] = new AssetInfo(loadedAssets[i], assetType ?? loadedAssets[i].GetType(), version, source, InitialSceneStateMetadata.HasValue);

            Dependencies = dependencies;

            //Debugging purposes. Test cases may bring a null AB, therefore we need this check
            AssetBundleName = Asset?.name;

            //We cannot unload an AB if its an ISS (Initial Scene State AB). It may be a dependency for dynamically isntanced AB
            if (!InitialSceneStateMetadata.HasValue)
                UnloadAB();
        }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleData[] dependencies) : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            //Dependencies cant be unloaded, since we dont know who will need them =(
            Dependencies = dependencies;
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

            //Needed for quitting, since Unity destroy the Asset out of our control
            if (Asset != null && Asset)
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
        }

        /// <summary>
        /// Try to get an asset loaded from the asset bundle without throwing exceptions.
        /// </summary>
        /// <param name="assetName">Asset to be requested. If its empty, the first asset loaded will be returned</param>
        /// <param name="asset">The retrieved asset if successful</param>
        /// <typeparam name="T">Type of the asset to load</typeparam>
        /// <returns>True if the asset was successfully retrieved, false otherwise</returns>
        public bool TryGetAsset<T>(out T asset, string assetName = "") where T: Object
        {
            asset = null;

            if (Assets == null || Assets.Count == 0)
            {
                ReportHub.LogWarning($"No assets were loaded for {AssetBundleName}", ReportCategory.ASSET_BUNDLES);
                return false;
            }

            AssetInfo assetInfo;

            if (string.IsNullOrEmpty(assetName))
            {
                if (Assets.Count > 1)
                    ReportHub.LogWarning($"Requested an asset by type when there is more than one in the AB {AssetBundleName}, the first one will be returned", ReportCategory.ASSET_BUNDLES);

                assetInfo = Assets.FirstValueOrDefaultNonAlloc();
            }
            else
            {
                if (!Assets.TryGetValue(assetName, out assetInfo))
                {
                    ReportHub.LogWarning($"No assets were loaded for Asset Bundle {AssetBundleName} with name {assetName}", ReportCategory.ASSET_BUNDLES);
                    return false;
                }
            }

            if (assetInfo.AssetType != typeof(T))
            {
                ReportHub.LogWarning($"Asset type mismatch: {typeof(T)} != {assetInfo.AssetType} for Asset Bundle {AssetBundleName}", ReportCategory.ASSET_BUNDLES);
                return false;
            }

            asset = (T)assetInfo.Asset!;
            return true;
        }

        /// <summary>
        /// Gets or caches the original shared materials from the prefab's renderers.
        /// Materials are extracted once on first call and reused for subsequent instantiations.
        /// This prevents Unity from cloning materials on each Object.Instantiate call.
        /// </summary>
        /// <param name="assetName">The asset name/hash used as cache key</param>
        /// <param name="prefab">The original prefab GameObject from the asset bundle</param>
        /// <returns>Array of material arrays, one per renderer in hierarchy order</returns>
        public Material[][] GetOrCacheOriginalMaterials(string assetName, GameObject prefab)
        {
            cachedMaterials ??= new Dictionary<string, Material[][]>();

            if (!cachedMaterials.TryGetValue(assetName, out Material[][] materials))
            {
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                materials = new Material[renderers.Length][];

                for (int i = 0; i < renderers.Length; i++)
                    materials[i] = renderers[i].sharedMaterials;

                cachedMaterials[assetName] = materials;
            }

            return materials;
        }

    }

}

public struct AssetInfo
{
    public Object Asset { get; }
    public Type AssetType { get; }

    public AssetInfo(Object asset, Type assetType, string version, string source, bool isISS)
    {
        Asset = asset;
        AssetType = assetType;
        Asset.name = isISS ? $"AB:{Asset?.name}_{version}_{source}_ISS" : $"AB:{Asset?.name}_{version}_{source}_NoISS";
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

public struct InitialSceneStateMetadata
{
    public List<string> assetHash;
    public List<Vector3> positions;
    public List<Quaternion> rotations;
    public List<Vector3> scales;
}
