using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioSources;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.AudioClips;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AudioSourcesPlugin: IDCLWorldPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly FrameTimeCapBudget frameTimeBudgetProvider;
        private readonly MemoryBudget memoryBudgetProvider;

        internal readonly AudioClipsCache audioClipsCache;

        public AudioSourcesPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IWebRequestController webRequestController, CacheCleaner cacheCleaner)
        {
            this.webRequestController = webRequestController;

            frameTimeBudgetProvider = sharedDependencies.FrameTimeBudget;
            memoryBudgetProvider = sharedDependencies.MemoryBudget;

            componentPoolsRegistry = sharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<AudioSource>(onRelease: audioSource => audioSource.clip = null);
            cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<AudioSource>());

            audioClipsCache = new AudioClipsCache();
            cacheCleaner.Register(audioClipsCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            StartAudioSourceLoadingSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, frameTimeBudgetProvider);
            LoadAudioClipSystem.InjectToWorld(ref builder, audioClipsCache, webRequestController, sharedDependencies.MutexSync);
            UpdateAudioSourceSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, sharedDependencies.SceneStateProvider, audioClipsCache, componentPoolsRegistry, frameTimeBudgetProvider, memoryBudgetProvider);

            finalizeWorldSystems.Add(CleanUpAudioSourceSystem.InjectToWorld(ref builder, audioClipsCache, componentPoolsRegistry));
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
