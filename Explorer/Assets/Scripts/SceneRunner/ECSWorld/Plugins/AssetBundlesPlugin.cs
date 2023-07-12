using Arch.Core;
using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.Manifest;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
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

        private readonly AssetBundleManifest localAssetBundleManifest;
        private readonly IReportsHandlingSettings reportsHandlingSettings;
        private readonly IConcurrentBudgetProvider loadingBudgetProvider;

        private readonly AssetBundleCache assetBundleCache;

        public AssetBundlesPlugin(AssetBundleManifest localAssetBundleManifest, IReportsHandlingSettings reportsHandlingSettings, IConcurrentBudgetProvider loadingBudgetProvider)
        {
            this.localAssetBundleManifest = localAssetBundleManifest;
            this.reportsHandlingSettings = reportsHandlingSettings;
            this.loadingBudgetProvider = loadingBudgetProvider;
            assetBundleCache = new AssetBundleCache();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, STREAMING_ASSETS_URL);
            ReportAssetBundleErrorSystem.InjectToWorld(ref builder, reportsHandlingSettings);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, localAssetBundleManifest, sharedDependencies.MutexSync, loadingBudgetProvider);
            AssetBundleDeferredLoadingSystem.InjectToWorld(ref builder, loadingBudgetProvider);
        }
    }
}
