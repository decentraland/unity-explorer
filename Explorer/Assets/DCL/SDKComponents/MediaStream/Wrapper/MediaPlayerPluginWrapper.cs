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
using DCL.FeatureFlags;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings;
using System.Linq;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream.Wrapper
{
    public class MediaPlayerPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IWebRequestController webRequestController;
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly WorldVolumeMacBus worldVolumeMacBus;
        private readonly IExposedCameraData exposedCameraData;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;
        private readonly MediaPlayerReusableHandler mediaPlayerReusableHandler;

        public MediaPlayerPluginWrapper(
            IComponentPoolsRegistry componentPoolsRegistry,
            IWebRequestController webRequestController,
            CacheCleaner cacheCleaner,
            IExtendedObjectPool<Texture2D> videoTexturePool,
            IPerformanceBudget frameTimeBudget,
            MediaPlayer mediaPlayerPrefab,
            WorldVolumeMacBus worldVolumeMacBus,
            IExposedCameraData exposedCameraData,
            VideoPrioritizationSettings videoPrioritizationSettings,
            FeatureFlagsCache featureFlagsCache)
        {
            this.exposedCameraData = exposedCameraData;
            this.videoPrioritizationSettings = videoPrioritizationSettings;

#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.webRequestController = webRequestController;

            this.videoTexturePool = videoTexturePool;
            this.frameTimeBudget = frameTimeBudget;
            this.worldVolumeMacBus = worldVolumeMacBus;
            cacheCleaner.Register(videoTexturePool);

            //TODO: Fix this loopy situation
            Transform mediaPlayerPoolTransform = new GameObject("MEDIA_PLAYER").transform;

            GameObjectPool<MediaPlayer> mediaPlayerPool = componentPoolsRegistry.AddGameObjectPool(
                creationHandler: () =>
                {
                    MediaPlayer? mediaPlayer = Object.Instantiate(mediaPlayerPrefab, mediaPlayerPoolTransform);
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
                    //mediaPlayer.CloseCurrentStream();
                    mediaPlayer.enabled = false;
                });

            cacheCleaner.Register(mediaPlayerPool);
#endif
            mediaPlayerReusableHandler = new MediaPlayerReusableHandler(mediaPlayerPool);

        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ISceneData sceneData, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCrdtWriter, List<IFinalizeWorldSystem> finalizeWorldSystems, FeatureFlagsCache featureFlagsCache)
        {
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX

            CreateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, sceneData, mediaPlayerReusableHandler, sceneStateProvider, frameTimeBudget);
            UpdateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, sceneData, sceneStateProvider, frameTimeBudget, worldVolumeMacBus);

            if(featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.VIDEO_PRIORITIZATION))
                UpdateMediaPlayerPrioritizationSystem.InjectToWorld(ref builder, exposedCameraData, videoPrioritizationSettings);

            VideoEventsSystem.InjectToWorld(ref builder, ecsToCrdtWriter, sceneStateProvider, frameTimeBudget);

            finalizeWorldSystems.Add(CleanUpMediaPlayerSystem.InjectToWorld(ref builder, mediaPlayerReusableHandler, videoTexturePool));
#endif
        }


    }
}
