using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.Skybox.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    /// <summary>
    ///     Scene-world side of the PBSkybox component; the global side lives in <see cref="DCL.SkyBox.SkyboxPlugin" />.
    /// </summary>
    public class SceneSkyboxPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly Arch.Core.World globalWorld;

        public SceneSkyboxPlugin(Arch.Core.World globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in SystemsDependencies systemsDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBSkybox>.InjectToWorld(ref builder);

            SceneSkyboxHandlerSystem handler = SceneSkyboxHandlerSystem.InjectToWorld(ref builder,
                globalWorld,
                persistentEntities.SceneRoot,
                sharedDependencies.SceneData,
                sharedDependencies.ScenePartition,
                sharedDependencies.SceneStateProvider);

            finalizeWorldSystems.Add(handler);
            sceneIsCurrentListeners.Add(handler);
        }
    }
}
