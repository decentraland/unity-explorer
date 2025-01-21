﻿using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
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

        public AssetBundlesPlugin(IReportsHandlingSettings reportsHandlingSettings, CacheCleaner cacheCleaner, IWebRequestController webRequestController, ArrayPool<byte> buffersPool)
        {
            this.reportsHandlingSettings = reportsHandlingSettings;
            this.webRequestController = webRequestController;
            this.buffersPool = buffersPool;
            assetBundleCache = new AssetBundleCache();
            assetBundleLoadingMutex = new AssetBundleLoadingMutex();

            cacheCleaner.Register(assetBundleCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, STREAMING_ASSETS_URL);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, webRequestController, buffersPool, assetBundleLoadingMutex);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // Asset Bundles
            PrepareGlobalAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, STREAMING_ASSETS_URL);

            // TODO create a runtime ref-counting cache
            LoadGlobalAssetBundleSystem.InjectToWorld(ref builder, assetBundleCache, webRequestController, assetBundleLoadingMutex, buffersPool);
        }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose()
        {
            assetBundleCache.Dispose();
        }
    }
}
