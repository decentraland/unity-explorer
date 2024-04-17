using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.PluginSystem.World
{
    public class AssetBundlesPlugin : IDCLWorldPluginWithoutSettings, IDCLGlobalPluginWithoutSettings
    {
        public static readonly URLDomain STREAMING_ASSETS_URL =
            URLDomain.FromString(
#if UNITY_EDITOR || UNITY_STANDALONE
                $"file://{Application.streamingAssetsPath}/AssetBundles/"
#else
            $"{Application.streamingAssetsPath}/AssetBundles/"
#endif
            );

        private readonly IReportsHandlingSettings reportsHandlingSettings;

        private readonly AssetBundleCache assetBundleCache;
        private readonly AssetBundleLoadingMutex assetBundleLoadingMutex;

        public AssetBundlesPlugin(IReportsHandlingSettings reportsHandlingSettings, CacheCleaner cacheCleaner)
        {
            this.reportsHandlingSettings = reportsHandlingSettings;
            assetBundleCache = new AssetBundleCache();
            assetBundleLoadingMutex = new AssetBundleLoadingMutex();

            cacheCleaner.Register(assetBundleCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, STREAMING_ASSETS_URL);
            ReportAssetBundleErrorSystem.InjectToWorld(ref builder, reportsHandlingSettings);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, sharedDependencies.MutexSync, assetBundleLoadingMutex);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, dependencies.SceneData, STREAMING_ASSETS_URL);
            ReportAssetBundleErrorSystem.InjectToWorld(ref builder, reportsHandlingSettings);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, dependencies.Mutex, assetBundleLoadingMutex);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // Asset Bundles
            PrepareGlobalAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, STREAMING_ASSETS_URL);
            ReportGlobalAssetBundleErrorSystem.InjectToWorld(ref builder, reportsHandlingSettings);

            // TODO create a runtime ref-counting cache
            LoadGlobalAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, new MutexSync(), assetBundleLoadingMutex);
        }

#region Interface Ambiguity
        UniTask IDCLPlugin.Initialize(IPluginSettingsContainer container, CancellationToken ct) =>

            // Don't even try to retrieve empty settings
            UniTask.CompletedTask;

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose()
        {
            assetBundleCache.Dispose();
        }
#endregion
    }
}
