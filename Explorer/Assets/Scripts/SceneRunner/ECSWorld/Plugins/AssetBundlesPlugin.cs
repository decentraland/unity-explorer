using Arch.Core;
using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.LifeCycle;
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
        private readonly IReportsHandlingSettings reportsHandlingSettings;

        private readonly AssetBundleCache assetBundleCache;

        public AssetBundlesPlugin(AssetBundleManifest localAssetBundleManifest, IReportsHandlingSettings reportsHandlingSettings)
        {
            this.localAssetBundleManifest = localAssetBundleManifest;
            this.reportsHandlingSettings = reportsHandlingSettings;
            assetBundleCache = new AssetBundleCache();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, STREAMING_ASSETS_URL);
            ReportAssetBundleErrorSystem.InjectToWorld(ref builder, reportsHandlingSettings);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, localAssetBundleManifest, sharedDependencies.MutexSync);
        }
    }
}
