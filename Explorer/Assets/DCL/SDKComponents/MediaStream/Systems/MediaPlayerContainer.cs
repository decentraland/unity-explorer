using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.CharacterCamera;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem;
using DCL.PluginSystem.World;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.MediaStream.YouTube;
using DCL.Utilities;
using DCL.WebRequests;
using ECS.Unity.AssetLoad.Cache;
using RenderHeads.Media.AVProVideo;
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
        private readonly ObjectProxy<IRoomHub> roomHubProxy;
        private readonly CacheCleaner cacheCleaner;
        private readonly AssetPreLoadCache assetPreLoadCache;
        private readonly IAnalyticsController analyticsController;

        private readonly MediaVolume mediaVolume;

        public MediaPlayerContainer(IAssetsProvisioner assetsProvisioner, IWebRequestController webRequestController, VolumeBus volumeBus, IPerformanceBudget frameBudget, ObjectProxy<IRoomHub> roomHubProxy,
            CacheCleaner cacheCleaner, AssetPreLoadCache assetPreLoadCache, IAnalyticsController analyticsController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.webRequestController = webRequestController;
            this.frameBudget = frameBudget;
            this.roomHubProxy = roomHubProxy;
            this.cacheCleaner = cacheCleaner;
            this.assetPreLoadCache = assetPreLoadCache;
            this.analyticsController = analyticsController;

            mediaVolume = new MediaVolume(volumeBus);
        }

        internal MediaFactoryBuilder mediaFactoryBuilder { get; private set; } = null!;

        public MediaPlayerPlugin CreatePlugin(ExposedCameraData exposedCameraData) =>
            new (frameBudget, exposedCameraData, mediaFactoryBuilder);

        protected override async UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            MediaPlayer mediaPlayerPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.MediaPlayerPrefab, ct: ct)).Value;

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

            mediaFactoryBuilder = new MediaFactoryBuilder(roomHubProxy, webRequestController, mediaVolume, frameBudget, mediaPlayerPrefab, videoTexturesPool, assetPreLoadCache, analyticsController);

            // Pre-warm the YouTube InnerTube session (visitorData + cookies) off the critical
            // path. Without this the first video share pays the warm-up GET synchronously
            // before any player call. The warm-up is idempotent and self-recovers from
            // failures — safe to fire-and-forget. CancellationToken.None because the warm-up
            // is process-global, not tied to this container's init scope.
            InnerTubeClient.PrewarmSessionAsync(CancellationToken.None).Forget();
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
            public MediaPlayerReference MediaPlayerPrefab { get; private set; }
        }
    }
}
