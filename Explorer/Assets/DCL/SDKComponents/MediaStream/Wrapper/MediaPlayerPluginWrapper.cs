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
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings;
using DCL.Utilities;

namespace DCL.SDKComponents.MediaStream.Wrapper
{
    public class MediaPlayerPluginWrapper
    {
        private readonly IWebRequestController webRequestController;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly WorldVolumeMacBus worldVolumeMacBus;
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
            WorldVolumeMacBus worldVolumeMacBus,
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
            this.worldVolumeMacBus = worldVolumeMacBus;
            cacheCleaner.Register(videoTexturePool);

            mediaPlayerCustomPool = new MediaPlayerCustomPool(mediaPlayerPrefab);
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ISceneData sceneData, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCrdtWriter, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            CreateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, roomHub, sceneData, mediaPlayerCustomPool, sceneStateProvider, frameTimeBudget);
            UpdateMediaPlayerSystem.InjectToWorld(ref builder, webRequestController, sceneData, sceneStateProvider, frameTimeBudget, worldVolumeMacBus, audioFadeSpeed);

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.VIDEO_PRIORITIZATION))
                UpdateMediaPlayerPrioritizationSystem.InjectToWorld(ref builder, exposedCameraData, videoPrioritizationSettings);

            VideoEventsSystem.InjectToWorld(ref builder, ecsToCrdtWriter, sceneStateProvider, frameTimeBudget);

            InitializeVideoPlayerMaterialsSystem.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(CleanUpMediaPlayerSystem.InjectToWorld(ref builder));
#endif
        }
    }
}
