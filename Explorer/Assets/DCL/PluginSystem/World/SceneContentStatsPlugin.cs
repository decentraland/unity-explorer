using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SceneContentDebug.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class SceneContentStatsPlugin : IDCLWorldPluginWithoutSettings
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            SceneContentStatsSystem.InjectToWorld(ref builder, sharedDependencies.RuntimeMetrics, sharedDependencies.EntitiesMap);
        }
    }
}
