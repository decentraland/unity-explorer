using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.VideoPlayer.Wrapper;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class VideoPlayerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly VideoPlayerPluginWrapper videoPlayerPluginWrapper;

        public VideoPlayerPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, CacheCleaner cacheCleaner)
        {
            videoPlayerPluginWrapper = new VideoPlayerPluginWrapper(sharedDependencies.ComponentPoolsRegistry, cacheCleaner);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            videoPlayerPluginWrapper.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, finalizeWorldSystems);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
