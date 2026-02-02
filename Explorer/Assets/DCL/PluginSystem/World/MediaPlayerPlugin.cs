using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.FeatureFlags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.WebRequests;
using ECS.LifeCycle;
using RenderHeads.Media.AVProVideo;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Audio;
using DCL.SDKComponents.MediaStream;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class MediaPlayerPlugin : IDCLWorldPlugin<MediaPlayerPlugin.MediaPlayerPluginSettings>
    {
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly ExposedCameraData exposedCameraData;
        private readonly MediaFactoryBuilder mediaFactory;
        private MediaPlayerPluginWrapper mediaPlayerPluginWrapper = null!;

        public MediaPlayerPlugin(
            IPerformanceBudget frameTimeBudget,
            ExposedCameraData exposedCameraData,
            MediaFactoryBuilder mediaFactory)
        {
            this.frameTimeBudget = frameTimeBudget;
            this.exposedCameraData = exposedCameraData;
            this.mediaFactory = mediaFactory;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities _, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners) =>
            mediaPlayerPluginWrapper.InjectToWorld(ref builder, sharedDependencies, finalizeWorldSystems, sceneIsCurrentListeners);

        public UniTask InitializeAsync(MediaPlayerPluginSettings settings, CancellationToken ct)
        {
            mediaPlayerPluginWrapper = new MediaPlayerPluginWrapper(
                frameTimeBudget,
                exposedCameraData,
                settings.FadeSpeed,
                settings.VideoPrioritizationSettings,
                mediaFactory,
                settings.FlipMaterial
            );

            return UniTask.CompletedTask;
        }

        [Serializable]
        public class MediaPlayerPluginSettings : IDCLPluginSettings
        {
            [field: SerializeField] public float FadeSpeed { get; private set; } = 1f;

            [field: SerializeField] public Material FlipMaterial { get; private set; }

            public VideoPrioritizationSettings VideoPrioritizationSettings;
        }
    }
}
