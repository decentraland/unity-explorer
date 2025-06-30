using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.SkyboxTime.Systems;
using DCL.SkyBox;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.SDKComponents.SkyboxTime
{
    public class SkyboxTimePlugin: IDCLWorldPlugin<SkyboxPlugin.SkyboxSettings>
    {
        private readonly ISceneRestrictionBusController sceneRestrictionController;
        private SkyboxPlugin.SkyboxSettings settings;

        public SkyboxTimePlugin(ISceneRestrictionBusController sceneRestrictionController)
        {
            this.sceneRestrictionController = sceneRestrictionController;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var system = SkyboxTimeHandlerSystem.InjectToWorld(
                    ref builder,
                    settings.SettingsAsset,
                    persistentEntities.SceneRoot,
                    sharedDependencies.SceneStateProvider,
                    sceneRestrictionController);

            finalizeWorldSystems.Add(system);
            sceneIsCurrentListeners.Add(system);
        }

        public UniTask InitializeAsync(SkyboxPlugin.SkyboxSettings pluginSettings, CancellationToken ct)
        {
            this.settings = pluginSettings;
            return UniTask.CompletedTask;
        }
    }
}
