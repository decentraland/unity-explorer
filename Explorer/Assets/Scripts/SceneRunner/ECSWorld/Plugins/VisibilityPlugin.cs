using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.Visibility.Systems;
using SceneRunner.EmptyScene;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins
{
    public class VisibilityPlugin : IECSWorldPlugin
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            PrimitivesVisibilitySystem.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<PBVisibilityComponent>.InjectToWorld(ref builder);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
