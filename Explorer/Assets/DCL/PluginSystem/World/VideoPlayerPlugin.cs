using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.VideoPlayer.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class VideoPlayerPlugin : IDCLWorldPluginWithoutSettings
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            VideoPlayerSystem.InjectToWorld(ref builder);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
