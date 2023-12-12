using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.Unity.AudioSources.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AudioPlugin: IDCLWorldPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public AudioPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies, IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<AudioSource>();
        }
        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InstantiateAudioSourceSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, componentPoolsRegistry, webRequestController);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
