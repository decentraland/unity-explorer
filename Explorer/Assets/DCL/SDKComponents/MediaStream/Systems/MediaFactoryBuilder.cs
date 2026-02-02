using Arch.Core;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World.Dependencies;
using DCL.Utilities;
using DCL.WebRequests;
using RenderHeads.Media.AVProVideo;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Exists to overcome dependency management issues
    /// </summary>
    public class MediaFactoryBuilder
    {
        private readonly ObjectProxy<IRoomHub> roomHub;
        private readonly MediaPlayerCustomPool mediaPlayerCustomPool;
        private readonly MediaVolume volumeBus;
        private readonly IWebRequestController webRequestController;
        private readonly IPerformanceBudget performanceBudget;
        private readonly IObjectPool<RenderTexture> videoTexturesPool;

        public MediaFactoryBuilder(ObjectProxy<IRoomHub> roomHub, IWebRequestController webRequestController, MediaVolume volumeBus,
            IPerformanceBudget performanceBudget, MediaPlayer mediaPlayerPrefab, IObjectPool<RenderTexture> videoTexturesPool)
        {
            this.roomHub = roomHub;
            this.webRequestController = webRequestController;
            this.performanceBudget = performanceBudget;
            this.videoTexturesPool = videoTexturesPool;
            this.volumeBus = volumeBus;

            mediaPlayerCustomPool = new MediaPlayerCustomPool(mediaPlayerPrefab);
        }

        public MediaFactory CreateForScene(World world, in ECSWorldInstanceSharedDependencies sceneDeps) =>
            new (sceneDeps.SceneData, roomHub.StrictObject.StreamingRoom(), mediaPlayerCustomPool, sceneDeps.SceneStateProvider,
                volumeBus, videoTexturesPool, sceneDeps.EntitiesMap, world, webRequestController, performanceBudget);
    }
}
