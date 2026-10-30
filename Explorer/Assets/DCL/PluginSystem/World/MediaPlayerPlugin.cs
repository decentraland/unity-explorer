using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.MediaStream.Settings;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.SDKComponents.MediaStream;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class MediaPlayerPlugin : IDCLWorldPlugin<MediaPlayerPlugin.MediaPlayerPluginSettings>
    {
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly ExposedCameraData exposedCameraData;
        private readonly MediaFactoryBuilder mediaFactory;
        private readonly IDebugContainerBuilder debugBuilder;
        private MediaPlayerPluginWrapper mediaPlayerPluginWrapper = null!;
        private MediaPlayerDebugContainer? mediaPlayerDebugContainer;

        public MediaPlayerPlugin(
            IPerformanceBudget frameTimeBudget,
            ExposedCameraData exposedCameraData,
            MediaFactoryBuilder mediaFactory,
            IDebugContainerBuilder debugBuilder)
        {
            this.frameTimeBudget = frameTimeBudget;
            this.exposedCameraData = exposedCameraData;
            this.mediaFactory = mediaFactory;
            this.debugBuilder = debugBuilder;
        }

        public void Dispose()
        {
            mediaPlayerPluginWrapper.Dispose();
            mediaPlayerDebugContainer?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities _, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners) =>
            mediaPlayerPluginWrapper.InjectToWorld(ref builder, sharedDependencies, systemsDependencies.RoomHub, finalizeWorldSystems, sceneIsCurrentListeners);

        public UniTask InitializeAsync(MediaPlayerPluginSettings settings, CancellationToken ct)
        {
            // The placeholder builds an offscreen camera + render texture eagerly, so only create it on
            // platforms where the LiveKit media feature is actually compiled in (see MediaPlayerPluginWrapper).
#if !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            var placeholderSource = new AvatarPlaceHolderTextureSource(settings.CameraOffPlaceholder);
#else
            AvatarPlaceHolderTextureSource? placeholderSource = null;
#endif

            var debugRegistry = new MediaPlayerDebugRegistry();
            mediaPlayerDebugContainer = new MediaPlayerDebugContainer(debugBuilder, debugRegistry);

            mediaPlayerPluginWrapper = new MediaPlayerPluginWrapper(
                frameTimeBudget,
                exposedCameraData,
                settings.FadeSpeed,
                settings.VideoPrioritizationSettings,
                mediaFactory,
                settings.FlipMaterial,
                placeholderSource,
                debugRegistry
            );

            return UniTask.CompletedTask;
        }

        [Serializable]
        public class MediaPlayerPluginSettings : IDCLPluginSettings
        {
            [field: SerializeField] public float FadeSpeed { get; private set; } = 1f;

            [field: SerializeField] public Material FlipMaterial { get; private set; } = null!;

            [field: SerializeField] [field: Tooltip("Shown on LiveKit screens when the streamer turns their camera off. Falls back to black if unset.")]
            public Texture2D CameraOffPlaceholder { get; private set; } = null!;

            public VideoPrioritizationSettings VideoPrioritizationSettings = null!;
        }
    }
}
