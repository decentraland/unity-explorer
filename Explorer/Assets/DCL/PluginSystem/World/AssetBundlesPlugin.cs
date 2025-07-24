using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

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
        private readonly IWebRequestController webRequestController;
        private readonly ArrayPool<byte> buffersPool;
        private readonly IDiskCache<PartialLoadingState> partialsDiskCache;
        private readonly URLDomain assetBundleURL;


        public AssetBundlesPlugin(IReportsHandlingSettings reportsHandlingSettings, CacheCleaner cacheCleaner, IWebRequestController webRequestController, ArrayPool<byte> buffersPool, IDiskCache<PartialLoadingState> partialsDiskCache,
            URLDomain assetBundleURL)
        {
            this.reportsHandlingSettings = reportsHandlingSettings;
            this.webRequestController = webRequestController;
            this.buffersPool = buffersPool;
            this.partialsDiskCache = partialsDiskCache;
            this.assetBundleURL = assetBundleURL;
            assetBundleCache = new AssetBundleCache();
            assetBundleLoadingMutex = new AssetBundleLoadingMutex();

            cacheCleaner.Register(assetBundleCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, STREAMING_ASSETS_URL);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, webRequestController, buffersPool, assetBundleLoadingMutex, partialsDiskCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // Asset Bundles
            PrepareGlobalAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, STREAMING_ASSETS_URL);

            LoadAssetBundleManifestSystem.InjectToWorld(ref builder, new NoCache<SceneAssetBundleManifest, GetAssetBundleManifestIntention>(true, true), assetBundleURL, webRequestController);


            // TODO create a runtime ref-counting cache
            LoadGlobalAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, webRequestController, assetBundleLoadingMutex, buffersPool, partialsDiskCache);
        }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose()
        {
            assetBundleCache.Dispose();
        }
    }
}
