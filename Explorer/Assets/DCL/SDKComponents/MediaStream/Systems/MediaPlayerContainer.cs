using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.CharacterCamera;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem;
using DCL.PluginSystem.World;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.Unity.AssetLoad.Cache;
using DCL.VideoPlayback;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    public class MediaPlayerContainer : DCLGlobalContainer<MediaPlayerContainer.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebRequestController webRequestController;
        private readonly IPerformanceBudget frameBudget;
        private readonly CacheCleaner cacheCleaner;
        private readonly AssetPreLoadCache assetPreLoadCache;
        private readonly IAnalyticsController analyticsController;

        private readonly MediaVolume mediaVolume;

        public MediaPlayerContainer(IAssetsProvisioner assetsProvisioner, IWebRequestController webRequestController, VolumeBus volumeBus, IPerformanceBudget frameBudget,
            CacheCleaner cacheCleaner, AssetPreLoadCache assetPreLoadCache, IAnalyticsController analyticsController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.webRequestController = webRequestController;
            this.frameBudget = frameBudget;
            this.cacheCleaner = cacheCleaner;
            this.assetPreLoadCache = assetPreLoadCache;
            this.analyticsController = analyticsController;

            mediaVolume = new MediaVolume(volumeBus);
        }

        internal MediaFactoryBuilder mediaFactoryBuilder { get; private set; } = null!;

        public MediaPlayerPlugin CreatePlugin(ExposedCameraData exposedCameraData, IDebugContainerBuilder debugBuilder) =>
            new (frameBudget, exposedCameraData, mediaFactoryBuilder, debugBuilder);

        protected override async UniTask InitializeInternalAsync(Settings containerSettings, CancellationToken ct)
        {
            MediaPlayer mediaPlayerPrefab = (await assetsProvisioner.ProvideMainAssetAsync(containerSettings.MediaPlayerPrefab, ct: ct)).Value;

            var videoTexturesPool = new ExtendedObjectPool<RenderTexture>(
                () => new RenderTexture(1, 1, 0, RenderTextureFormat.BGRA32),
                actionOnRelease: rt =>
                {
                    if (rt.IsCreated())
                        rt.Release();

                    rt.width = 1;
                    rt.height = 1;
                },
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                maxSize: 20);

            cacheCleaner.Register(videoTexturesPool);

            mediaFactoryBuilder = new MediaFactoryBuilder(webRequestController, mediaVolume, frameBudget, mediaPlayerPrefab, videoTexturesPool, assetPreLoadCache, analyticsController);
        }

        public override void Dispose() =>
            mediaVolume.Dispose();

        [Serializable]
        public class MediaPlayerReference : ComponentReference<MediaPlayer>
        {
            public MediaPlayerReference(string guid) : base(guid) { }
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public MediaPlayerReference MediaPlayerPrefab { get; private set; } = null!;
        }
    }
}
