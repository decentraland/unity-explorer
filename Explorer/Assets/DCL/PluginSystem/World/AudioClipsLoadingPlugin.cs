using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World.Dependencies;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.Unity.AudioSources.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AudioClipsLoadingPlugin: IDCLWorldPluginWithoutSettings
    {
        private readonly ECSWorldSingletonSharedDependencies sharedDependencies;
        private readonly IWebRequestController webRequestController;

        public AudioClipsLoadingPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IWebRequestController webRequestController)
        {
            this.sharedDependencies = sharedDependencies;
            this.webRequestController = webRequestController;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            StartAudioClipLoadingSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, 11, new NullBudgetProvider());
            LoadAudioClipSystem.InjectToWorld(ref builder, NoCache<AudioClip, GetAudioClipIntention>.INSTANCE, webRequestController, sharedDependencies.MutexSync);
            // InstantiateAudioSourceSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, componentPoolsRegistry, webRequestController);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
