using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class VisibilityPlugin : IDCLWorldPluginWithoutSettings
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBVisibilityComponent>.InjectToWorld(ref builder);
        }
    }
}
