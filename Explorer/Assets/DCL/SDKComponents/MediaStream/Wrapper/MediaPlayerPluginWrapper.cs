using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Utilities;
using DCL.WebRequests;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

namespace DCL.SDKComponents.MediaStream.Wrapper
{
    public class MediaPlayerPluginWrapper
    {
        private readonly IWebRequestController webRequestController;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly VolumeBus volumeBus;
        private readonly IExposedCameraData exposedCameraData;
        private readonly float audioFadeSpeed;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;
        private readonly ObjectProxy<IRoomHub> roomHub;
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
        private readonly MediaPlayerCustomPool mediaPlayerCustomPool;
#endif

        public MediaPlayerPluginWrapper(IWebRequestController webRequestController,
            CacheCleaner cacheCleaner,
            IExtendedObjectPool<Texture2D> videoTexturePool,
            IPerformanceBudget frameTimeBudget,
            MediaPlayer mediaPlayerPrefab,
            VolumeBus volumeBus,
            IExposedCameraData exposedCameraData,
            float audioFadeSpeed,
            VideoPrioritizationSettings videoPrioritizationSettings,
            ObjectProxy<IRoomHub> roomHub)
        {
            this.exposedCameraData = exposedCameraData;
            this.audioFadeSpeed = audioFadeSpeed;
            this.videoPrioritizationSettings = videoPrioritizationSettings;
            this.roomHub = roomHub;

#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            this.webRequestController = webRequestController;

            this.frameTimeBudget = frameTimeBudget;
            this.volumeBus = volumeBus;
            cacheCleaner.Register(videoTexturePool);

            mediaPlayerCustomPool = new MediaPlayerCustomPool(mediaPlayerPrefab);
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ISceneData sceneData, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCrdtWriter, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            CreateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, roomHub, sceneData, mediaPlayerCustomPool, sceneStateProvider, frameTimeBudget, volumeBus);
            UpdateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, sceneData, sceneStateProvider, frameTimeBudget, volumeBus, audioFadeSpeed);

            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.VIDEO_PRIORITIZATION))
                UpdateMediaPlayerPrioritizationSystem.InjectToWorld(ref builder, exposedCameraData, videoPrioritizationSettings);

            VideoEventsSystem.InjectToWorld(ref builder, ecsToCrdtWriter, sceneStateProvider, frameTimeBudget);

            UpdateVideoMaterialTextureScaleSystem.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(CleanUpMediaPlayerSystem.InjectToWorld(ref builder));
#endif
        }
    }
}
