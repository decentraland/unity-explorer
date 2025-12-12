using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.Visibility.Components;
using ECS.Unity.Visibility.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class VisibilityPlugin : IDCLWorldPluginWithoutSettings
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // Inject propagation system (runs first to resolve visibility before render systems)
            VisibilityPropagationSystem.InjectToWorld(ref builder);

            // Reset dirty flags at end of frame
            ResetDirtyFlagSystem<PBVisibilityComponent>.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<ResolvedVisibilityComponent>.InjectToWorld(ref builder);
        }
    }
}
