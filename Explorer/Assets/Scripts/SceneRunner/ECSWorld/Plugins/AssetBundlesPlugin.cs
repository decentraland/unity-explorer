using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.Manifest;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins
{
    public class AssetBundlesPlugin : IECSWorldPlugin
    {
        private const string ASSET_BUNDLES_URL = "https://ab-cdn.decentraland.org/";

        private readonly AssetBundlesManifestCache assetBundlesManifestCache;

        public AssetBundlesPlugin()
        {
            assetBundlesManifestCache = new AssetBundlesManifestCache();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Scene Manifest
            LoadAssetBundleManifestFromCacheSystem.InjectToWorld(ref builder, assetBundlesManifestCache);
            StartLoadingAssetBundleManifestSystem.InjectToWorld(ref builder);
            PrepareAssetBundleManifestParametersSystem.InjectToWorld(ref builder, ASSET_BUNDLES_URL);
            ConcludeAssetBundleManifestLoadingSystem.InjectToWorld(ref builder, assetBundlesManifestCache, ASSET_BUNDLES_URL);

            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData);
            StartLoadingAssetBundleSystem.InjectToWorld(ref builder);
            ConcludeAssetBundleLoadingSystem.InjectToWorld(ref builder);
        }
    }
}
