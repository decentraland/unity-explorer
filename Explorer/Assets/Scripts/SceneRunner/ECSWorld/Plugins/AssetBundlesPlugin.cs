using Arch.Core;
using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using SceneRunner.EmptyScene;
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

        private readonly IReportsHandlingSettings reportsHandlingSettings;

        private readonly AssetBundleCache assetBundleCache;
        private readonly AssetBundleLoadingMutex assetBundleLoadingMutex;

        public AssetBundlesPlugin(IReportsHandlingSettings reportsHandlingSettings)
        {
            this.reportsHandlingSettings = reportsHandlingSettings;
            assetBundleCache = new AssetBundleCache();
            assetBundleLoadingMutex = new AssetBundleLoadingMutex();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, STREAMING_ASSETS_URL);
            ReportAssetBundleErrorSystem.InjectToWorld(ref builder, reportsHandlingSettings);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, sharedDependencies.MutexSync, assetBundleLoadingMutex);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, dependencies.SceneData, STREAMING_ASSETS_URL);
            ReportAssetBundleErrorSystem.InjectToWorld(ref builder, reportsHandlingSettings);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, dependencies.Mutex, assetBundleLoadingMutex);
        }
    }
}
