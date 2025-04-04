﻿using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterCamera;
using DCL.FeatureFlags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.SDKComponents.MediaStream.Wrapper;
using DCL.Settings;
using DCL.WebRequests;
using ECS.LifeCycle;
using RenderHeads.Media.AVProVideo;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.World
{
    public class MediaPlayerPlugin : IDCLWorldPlugin<MediaPlayerPlugin.MediaPlayerPluginSettings>
    {
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly IWebRequestController webRequestController;
        private readonly ECSWorldSingletonSharedDependencies sharedDependencies;
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly WorldVolumeMacBus worldVolumeMacBus;
        private MediaPlayer mediaPlayerPrefab;
        private MediaPlayerPluginWrapper mediaPlayerPluginWrapper;
        private ExposedCameraData exposedCameraData;
        private FeatureFlagsCache featureFlagsCache;

        public MediaPlayerPlugin(
            ECSWorldSingletonSharedDependencies sharedDependencies,
            IExtendedObjectPool<Texture2D> videoTexturePool,
            IPerformanceBudget frameTimeBudget,
            IAssetsProvisioner assetsProvisioner,
            IWebRequestController webRequestController,
            CacheCleaner cacheCleaner,
            WorldVolumeMacBus worldVolumeMacBus,
            ExposedCameraData exposedCameraData,
            FeatureFlagsCache featureFlagsCache)
        {
            this.frameTimeBudget = frameTimeBudget;
            this.sharedDependencies = sharedDependencies;
            this.videoTexturePool = videoTexturePool;
            this.assetsProvisioner = assetsProvisioner;
            this.webRequestController = webRequestController;
            this.cacheCleaner = cacheCleaner;
            this.worldVolumeMacBus = worldVolumeMacBus;
            this.exposedCameraData = exposedCameraData;
            this.featureFlagsCache = featureFlagsCache;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities _, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            mediaPlayerPluginWrapper.InjectToWorld(ref builder, sharedDependencies.SceneData, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter, finalizeWorldSystems, featureFlagsCache);
        }

        public async UniTask InitializeAsync(MediaPlayerPluginSettings settings, CancellationToken ct)
        {
            VideoPrioritizationSettings videoPrioritizationSettings = (await assetsProvisioner.ProvideMainAssetAsync(settings.VideoPrioritizationSettings, ct: ct)).Value;
            mediaPlayerPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.MediaPlayerPrefab, ct: ct)).Value.GetComponent<MediaPlayer>();
            mediaPlayerPluginWrapper = new MediaPlayerPluginWrapper(webRequestController, cacheCleaner, videoTexturePool, frameTimeBudget, mediaPlayerPrefab, worldVolumeMacBus, exposedCameraData, videoPrioritizationSettings);

        }

        [Serializable]
        public class MediaPlayerPluginSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceGameObject MediaPlayerPrefab;

            public StaticSettings.VideoPrioritizationSettingsRef VideoPrioritizationSettings;
        }
    }
}
