using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioStream.Wrapper;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class AudioStreamPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly AudioStreamPluginWrapper audioStreamPluginWrapper;

        public AudioStreamPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, CacheCleaner cacheCleaner)
        {
            audioStreamPluginWrapper = new AudioStreamPluginWrapper(sharedDependencies.ComponentPoolsRegistry, cacheCleaner);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities _, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            audioStreamPluginWrapper.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, finalizeWorldSystems);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
