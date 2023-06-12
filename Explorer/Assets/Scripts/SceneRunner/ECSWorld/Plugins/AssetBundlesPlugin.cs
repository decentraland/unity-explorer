using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.Manifest;
using ECS.StreamableLoading.Cache;
using System.Collections.Generic;
using UnityEngine;

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
            PrepareAssetBundleManifestParametersSystem.InjectToWorld(ref builder, ASSET_BUNDLES_URL);
            LoadAssetBundleManifestSystem.InjectToWorld(ref builder, assetBundlesManifestCache, ASSET_BUNDLES_URL);

            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData);
            LoadAssetBundleSystem.InjectToWorld(ref builder, NoCache<AssetBundle, GetAssetBundleIntention>.INSTANCE);
        }
    }
}
