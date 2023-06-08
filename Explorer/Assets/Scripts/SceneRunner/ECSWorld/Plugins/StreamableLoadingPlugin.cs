using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.StreamableLoading.Systems;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins
{
    public class StreamableLoadingPlugin : IECSWorldPlugin
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            StartLoadingTextureSystem.InjectToWorld(ref builder);
            RepeatTextureLoadingSystem.InjectToWorld(ref builder);
            ConcludeTextureLoadingSystem.InjectToWorld(ref builder);
            AbortLoadingSystem.InjectToWorld(ref builder);
        }
    }
}
