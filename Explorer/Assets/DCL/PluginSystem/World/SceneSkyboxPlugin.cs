using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.MediaStream;
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
        private readonly MediaFactoryBuilder mediaFactoryBuilder;

        public SceneSkyboxPlugin(Arch.Core.World globalWorld, MediaFactoryBuilder mediaFactoryBuilder)
        {
            this.globalWorld = globalWorld;
            this.mediaFactoryBuilder = mediaFactoryBuilder;
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
                sharedDependencies.SceneStateProvider,
                mediaFactoryBuilder.CreateForScene(builder.World, sharedDependencies, systemsDependencies.RoomHub, placeholderSource: null));

            finalizeWorldSystems.Add(handler);
            sceneIsCurrentListeners.Add(handler);
        }
    }
}
