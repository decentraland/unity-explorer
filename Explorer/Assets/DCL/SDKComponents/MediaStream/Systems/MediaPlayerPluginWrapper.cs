using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.WebRequests;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    public class MediaPlayerPluginWrapper : IDisposable
    {
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly IExposedCameraData exposedCameraData;
        private readonly float audioFadeSpeed;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;
        private readonly MediaFactoryBuilder mediaFactory;
        private readonly Material flipMaterial;

        // Null on platforms where the LiveKit media feature is compiled out (see InjectToWorld guard).
        private readonly AvatarPlaceHolderTextureSource? placeholderSource;

        public MediaPlayerPluginWrapper(
            IPerformanceBudget frameTimeBudget,
            IExposedCameraData exposedCameraData,
            float audioFadeSpeed,
            VideoPrioritizationSettings videoPrioritizationSettings,
            MediaFactoryBuilder mediaFactory,
            Material flipMaterial,
            AvatarPlaceHolderTextureSource? placeholderSource)
        {
            this.exposedCameraData = exposedCameraData;
            this.audioFadeSpeed = audioFadeSpeed;
            this.videoPrioritizationSettings = videoPrioritizationSettings;
            this.mediaFactory = mediaFactory;
            this.flipMaterial = flipMaterial;
            this.placeholderSource = placeholderSource;

#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            this.frameTimeBudget = frameTimeBudget;
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sceneDeps, IRoomHub roomHub, List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            MediaFactory mediaFactory = this.mediaFactory.CreateForScene(builder.World, sceneDeps, roomHub, placeholderSource);

            CreateMediaPlayerSystem.InjectToWorld(ref builder, sceneDeps.SceneStateProvider, mediaFactory);
            sceneIsCurrentListeners.Add(UpdateMediaPlayerSystem.InjectToWorld(ref builder, sceneDeps.SceneData, sceneDeps.SceneStateProvider, frameTimeBudget, mediaFactory, audioFadeSpeed, flipMaterial, videoPrioritizationSettings));

            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.VIDEO_PRIORITIZATION))
                UpdateMediaPlayerPrioritizationSystem.InjectToWorld(ref builder, exposedCameraData, videoPrioritizationSettings);

            VideoEventsSystem.InjectToWorld(ref builder, sceneDeps.EcsToCRDTWriter, sceneDeps.SceneStateProvider, frameTimeBudget);

            finalizeWorldSystems.Add(CleanUpMediaPlayerSystem.InjectToWorld(ref builder));
#endif
        }

        public void Dispose()
        {
            placeholderSource?.Dispose();
        }
    }
}
