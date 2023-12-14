using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.Unity.AudioSources.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AudioSourcesPlugin: IDCLWorldPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly FrameTimeCapBudgetProvider frameTimeBudgetProvider;
        private readonly MemoryBudgetProvider memoryBudgetProvider;

        private readonly AudioClipsCache audioClipsCache;

        public AudioSourcesPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IWebRequestController webRequestController, CacheCleaner cacheCleaner)
        {
            this.webRequestController = webRequestController;

            frameTimeBudgetProvider = sharedDependencies.FrameTimeBudgetProvider;
            memoryBudgetProvider = sharedDependencies.MemoryBudgetProvider;

            componentPoolsRegistry = sharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<AudioSource>();

            audioClipsCache = new AudioClipsCache();
            cacheCleaner.Register(audioClipsCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            StartAudioClipLoadingSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, 11, frameTimeBudgetProvider);
            LoadAudioClipSystem.InjectToWorld(ref builder, audioClipsCache, webRequestController, sharedDependencies.MutexSync);
            CreateAudioSourceSystem.InjectToWorld(ref builder, componentPoolsRegistry, frameTimeBudgetProvider, memoryBudgetProvider);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
