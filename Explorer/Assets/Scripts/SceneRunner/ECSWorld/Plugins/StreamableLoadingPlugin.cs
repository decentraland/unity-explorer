using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Systems;

namespace SceneRunner.ECSWorld.Plugins
{
    public class StreamableLoadingPlugin : IECSWorldPlugin
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies)
        {
            StartLoadingTextureSystem.InjectToWorld(ref builder);
            ConcludeTextureLoadingSystem.InjectToWorld(ref builder);
            AbortLoadingSystem.InjectToWorld(ref builder);
        }
    }
}
