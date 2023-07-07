using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.Prioritization.DeferredLoading;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.Manifest;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins
{
    public class AssetBundlesPlugin : IECSWorldPlugin
    {
        public static readonly string STREAMING_ASSETS_URL =
#if UNITY_EDITOR || UNITY_STANDALONE
            $"file://{Application.streamingAssetsPath}/AssetBundles/";
#else
            return $"{Application.streamingAssetsPath}/AssetBundles/";
#endif

        private readonly AssetBundlesManifestCache assetBundlesManifestCache;
        private readonly AssetBundleManifest localAssetBundleManifest;

        private readonly AssetBundleCache assetBundleCache;
        private readonly ConcurrentLoadingBudgetProvider concurrentLoadingBudgetProvider;

        public AssetBundlesPlugin(AssetBundleManifest localAssetBundleManifest, ConcurrentLoadingBudgetProvider concurrentLoadingBudgetProvider)
        {
            this.localAssetBundleManifest = localAssetBundleManifest;
            this.concurrentLoadingBudgetProvider = concurrentLoadingBudgetProvider;
            assetBundleCache = new AssetBundleCache();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, STREAMING_ASSETS_URL);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, localAssetBundleManifest, sharedDependencies.MutexSync);
            DeferredLoadingSystem<AssetBundleData, GetAssetBundleIntention>.InjectToWorld(ref builder, concurrentLoadingBudgetProvider);
        }
    }
}
