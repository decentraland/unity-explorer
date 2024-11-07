using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.CharacterCamera;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;
using DCL.ECSComponents;
using DCL.Settings;

namespace DCL.SDKComponents.MediaStream.Wrapper
{
    public class MediaPlayerPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IWebRequestController webRequestController;
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly GameObjectPool<MediaPlayer> mediaPlayerPool;
        private readonly WorldVolumeMacBus worldVolumeMacBus;
        private readonly IExposedCameraData exposedCameraData;

        public MediaPlayerPluginWrapper(
            IComponentPoolsRegistry componentPoolsRegistry,
            IWebRequestController webRequestController,
            CacheCleaner cacheCleaner,
            IExtendedObjectPool<Texture2D> videoTexturePool,
            IPerformanceBudget frameTimeBudget,
            MediaPlayer mediaPlayerPrefab,
            WorldVolumeMacBus worldVolumeMacBus,
            IExposedCameraData exposedCameraData)
        {
            this.exposedCameraData = exposedCameraData;

#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.webRequestController = webRequestController;

            this.videoTexturePool = videoTexturePool;
            this.frameTimeBudget = frameTimeBudget;
            this.worldVolumeMacBus = worldVolumeMacBus;
            cacheCleaner.Register(videoTexturePool);

            mediaPlayerPool = componentPoolsRegistry.AddGameObjectPool(
                creationHandler: () =>
                {
                    var mediaPlayer = Object.Instantiate(mediaPlayerPrefab, mediaPlayerPool!.PoolContainerTransform);
                    mediaPlayer.PlatformOptionsWindows.audioOutput = Windows.AudioOutput.Unity;
                    mediaPlayer.PlatformOptionsMacOSX.audioMode = MediaPlayer.OptionsApple.AudioMode.Unity;
                    //Add other options if we release on other platforms :D
                    return mediaPlayer;
                },
                onGet: mediaPlayer =>
                {
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
            UpdateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, sceneData, sceneStateProvider, frameTimeBudget, worldVolumeMacBus);
            UpdateMediaPlayerVisibilitySystem.InjectToWorld(ref builder, exposedCameraData);
            VideoEventsSystem.InjectToWorld(ref builder, ecsToCrdtWriter, sceneStateProvider, frameTimeBudget);

            finalizeWorldSystems.Add(CleanUpMediaPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, videoTexturePool));
#endif
        }
    }
}
