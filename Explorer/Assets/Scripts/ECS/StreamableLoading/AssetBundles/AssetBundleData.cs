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
    public class AssetBundleData : StreamableRefCountData<AssetBundle>
    {
        private readonly Object? mainAsset;
        private readonly Type? assetType;

        internal AssetBundle AssetBundle => Asset;

        public readonly AssetBundleData[] Dependencies;

        public readonly AssetBundleMetrics? Metrics;

        private readonly string description;


        public bool unloaded;

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, Object mainAsset, Type assetType, AssetBundleData[] dependencies, string version = "", string source = "")
            : base(assetBundle, ReportCategory.ASSET_BUNDLES)
        {
            Metrics = metrics;

            this.mainAsset = mainAsset;
            Dependencies = dependencies;
            this.assetType = assetType;

            if (mainAsset != null)
            {
                description = $"AB:{AssetBundle.name}_{version}_{source}";
                ForceUnload();

                foreach (AssetBundleData child in Dependencies)
                {
                    if(!child.unloaded && !child.Asset.name.Contains("ignore"))
                        child.ForceUnload();
                }
            }
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

        
        public void ForceUnload()
        {
            //We immediately unload the asset bundle, as we don't need it anymore.
            //Very hacky, because the asset will remain in cache as AssetBundle == null
            //When DestroyObject is invoked, it will do nothing.
            //When cache in cleaned, the AssetBundleData will be removed from the list. Its there doing nothing
            //Also, this allows dependencies (the shader) to stay in the cache since we dont dereference it
            if (unloaded)
                return;
            unloaded = true;
            AssetBundle.UnloadAsync(false);
        }
        
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

        public string GetInstanceName() => description;
            
    }
}
