using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioSources;
using DCL.WebRequests;
using ECS.ComponentsPooling.Systems;
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
        private readonly FrameTimeCapBudgetProvider frameTimeBudgetProvider;
        private readonly MemoryBudgetProvider memoryBudgetProvider;

        private readonly AudioClipsCache audioClipsCache;

        public AudioSourcesPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IWebRequestController webRequestController, CacheCleaner cacheCleaner)
        {
            this.webRequestController = webRequestController;

            frameTimeBudgetProvider = sharedDependencies.FrameTimeBudgetProvider;
            memoryBudgetProvider = sharedDependencies.MemoryBudgetProvider;

            componentPoolsRegistry = sharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<AudioSource>(onRelease: OnAudioSourceReleased);
            cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<AudioSource>());

            audioClipsCache = new AudioClipsCache();
            cacheCleaner.Register(audioClipsCache);
            return;

            void OnAudioSourceReleased(AudioSource audioSource)
            {
                audioClipsCache.Dereference(audioSource.clip);
                audioSource.clip = null;
            }
        }


        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            StartAudioSourceLoadingSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, frameTimeBudgetProvider);
            LoadAudioClipSystem.InjectToWorld(ref builder, audioClipsCache, webRequestController, sharedDependencies.MutexSync);
            UpdateAudioSourceSystem.InjectToWorld(ref builder, componentPoolsRegistry, frameTimeBudgetProvider, memoryBudgetProvider);
            // CleanUpAudioSourceSystem.InjectToWorld(ref builder, audioClipsCache, componentPoolsRegistry);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<AudioSource, AudioSourceComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
