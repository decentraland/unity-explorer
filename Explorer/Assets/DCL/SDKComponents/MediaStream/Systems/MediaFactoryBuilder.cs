#if !UNITY_WEBGL

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

#if !NO_LIVEKIT_MODE
        private readonly ObjectProxy<IRoomHub> roomHub;
#endif

        private readonly MediaPlayerCustomPool mediaPlayerCustomPool;
        private readonly MediaVolume volumeBus;
        private readonly IWebRequestController webRequestController;
        private readonly IPerformanceBudget performanceBudget;
        private readonly IObjectPool<RenderTexture> videoTexturesPool;

        public MediaFactoryBuilder(

#if !NO_LIVEKIT_MODE
                ObjectProxy<IRoomHub> roomHub, 
#endif

                IWebRequestController webRequestController, 
                MediaVolume volumeBus,
                IPerformanceBudget performanceBudget, 
                MediaPlayer mediaPlayerPrefab, 
                IObjectPool<RenderTexture> videoTexturesPool)
        {

#if !NO_LIVEKIT_MODE
            this.roomHub = roomHub;
#endif

            this.webRequestController = webRequestController;
            this.performanceBudget = performanceBudget;
            this.videoTexturesPool = videoTexturesPool;
            this.volumeBus = volumeBus;

            mediaPlayerCustomPool = new MediaPlayerCustomPool(mediaPlayerPrefab);
        }

        public MediaFactory CreateForScene(World world, in ECSWorldInstanceSharedDependencies sceneDeps) =>
            new (
                    sceneDeps.SceneData, 

#if !NO_LIVEKIT_MODE
                    roomHub.StrictObject.StreamingRoom(), 
#endif

                    mediaPlayerCustomPool, 
                    sceneDeps.SceneStateProvider,
                    volumeBus, 
                    videoTexturesPool, 
                    sceneDeps.EntitiesMap,
                    world, 
                    webRequestController, 
                    performanceBudget);
    }
}

#endif
