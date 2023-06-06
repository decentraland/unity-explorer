using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.Visibility.Systems;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins
{
    public class VisibilityPlugin : IECSWorldPlugin
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            VisibilitySystem.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<PBVisibilityComponent>.InjectToWorld(ref builder);
        }
    }
}
