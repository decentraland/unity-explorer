using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;
#endif

namespace DCL.SDKComponents.MediaStream.Wrapper
{
    public class MediaPlayerPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IWebRequestController webRequestController;
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        public MediaPlayerPluginWrapper(
            IComponentPoolsRegistry componentPoolsRegistry,
            IWebRequestController webRequestController,
            CacheCleaner cacheCleaner,
            IExtendedObjectPool<Texture2D> videoTexturePool,
            IPerformanceBudget frameTimeBudget,
            MediaPlayer mediaPlayerPrefab)
        {
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.webRequestController = webRequestController;

            this.videoTexturePool = videoTexturePool;
            this.frameTimeBudget = frameTimeBudget;
            cacheCleaner.Register(videoTexturePool);

            var parentContainer = new GameObject("MediaPlayerContainer");

            mediaPlayerPool = componentPoolsRegistry.AddGameObjectPool(
                creationHandler: () =>
                {
                    var mediaPlayer = Object.Instantiate(mediaPlayerPrefab);
                    mediaPlayer.PlatformOptionsWindows.audioOutput = Windows.AudioOutput.Unity;
                    mediaPlayer.PlatformOptionsMacOSX.audioMode = MediaPlayer.OptionsApple.AudioMode.Unity;
                    //Add other options if we release on other platforms :D
                    return mediaPlayer;
                },
                onGet: mediaPlayer =>
                {
                    mediaPlayer.transform.SetParent(parentContainer.transform);
                    mediaPlayer.AutoOpen = false;
                    mediaPlayer.enabled = true;
                },
                onRelease: mediaPlayer =>
                {
                    mediaPlayer.CloseCurrentStream();
                    mediaPlayer.enabled = false;
                });

            cacheCleaner.Register(mediaPlayerPool);
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ISceneData sceneData, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCrdtWriter, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX

            CreateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, sceneData, mediaPlayerPool, sceneStateProvider, frameTimeBudget);
            UpdateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, sceneData, sceneStateProvider, frameTimeBudget);
            CleanUpMediaPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, videoTexturePool);

            VideoEventsSystem.InjectToWorld(ref builder, ecsToCrdtWriter, sceneStateProvider, componentPoolsRegistry.GetReferenceTypePool<PBVideoEvent>(), frameTimeBudget);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MediaPlayer, MediaPlayerComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
#endif
        }
    }
}
