using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.MediaStream.Settings;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    public class MediaPlayerPluginWrapper : IDisposable
    {
        // ReSharper disable NotAccessedField.Local -- consumed by InjectToWorld, whose body is compiled out under the Linux defines InspectCode runs with.
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly IExposedCameraData exposedCameraData;
        private readonly float audioFadeSpeed;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;
        private readonly MediaFactoryBuilder mediaFactory;
        private readonly Material flipMaterial;
        private readonly MediaPlayerDebugRegistry debugRegistry;
        // ReSharper restore NotAccessedField.Local

        // Null on platforms where the LiveKit media feature is compiled out (see InjectToWorld guard).
        private readonly AvatarPlaceHolderTextureSource? placeholderSource;

        public MediaPlayerPluginWrapper(
            IPerformanceBudget frameTimeBudget,
            IExposedCameraData exposedCameraData,
            float audioFadeSpeed,
            VideoPrioritizationSettings videoPrioritizationSettings,
            MediaFactoryBuilder mediaFactory,
            Material flipMaterial,
            AvatarPlaceHolderTextureSource? placeholderSource,
            MediaPlayerDebugRegistry debugRegistry)
        {
            this.frameTimeBudget = frameTimeBudget;
            this.exposedCameraData = exposedCameraData;
            this.audioFadeSpeed = audioFadeSpeed;
            this.videoPrioritizationSettings = videoPrioritizationSettings;
            this.mediaFactory = mediaFactory;
            this.flipMaterial = flipMaterial;
            this.placeholderSource = placeholderSource;
            this.debugRegistry = debugRegistry;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sceneDeps, IRoomHub roomHub, List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
#if !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            ReportHub.LogProductionInfo("[MediaPlayerPluginWrapper] Inject");

            MediaFactory sceneMediaFactory = mediaFactory.CreateForScene(builder.World, sceneDeps, roomHub, placeholderSource);

            CreateMediaPlayerSystem.InjectToWorld(ref builder, sceneDeps.SceneStateProvider, sceneMediaFactory);
            sceneIsCurrentListeners.Add(UpdateMediaPlayerSystem.InjectToWorld(ref builder, sceneDeps.SceneData, sceneDeps.SceneStateProvider, frameTimeBudget, sceneMediaFactory, audioFadeSpeed, flipMaterial, videoPrioritizationSettings));

            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.VIDEO_PRIORITIZATION))
                UpdateMediaPlayerPrioritizationSystem.InjectToWorld(ref builder, exposedCameraData, videoPrioritizationSettings);

            VideoEventsSystem.InjectToWorld(ref builder, sceneDeps.EcsToCRDTWriter, sceneDeps.SceneStateProvider, frameTimeBudget);
            GatherMediaStreamDebugSystem.InjectToWorld(ref builder, debugRegistry, sceneDeps.SceneStateProvider, sceneDeps.SceneData);

            finalizeWorldSystems.Add(CleanUpMediaPlayerSystem.InjectToWorld(ref builder));
#else
            ReportHub.LogProductionInfo("[MediaPlayerPluginWrapper] Ignore Inject");
#endif
        }

        public void Dispose()
        {
            placeholderSource?.Dispose();
        }
    }
}
