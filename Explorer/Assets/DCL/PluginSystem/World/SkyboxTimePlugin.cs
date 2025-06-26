using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SkyboxTime.Systems;
using DCL.StylizedSkybox.Scripts.Plugin;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.SDKComponents.SkyboxTime
{
    public class SkyboxTimePlugin: IDCLWorldPlugin<StylizedSkyboxPlugin.StylizedSkyboxSettings>
    {
        private StylizedSkyboxPlugin.StylizedSkyboxSettings settings;

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var system = SkyboxTimeSystem.InjectToWorld(
                    ref builder,
                    settings.SettingsAsset,
                    persistentEntities.SceneRoot,
                    sharedDependencies.SceneStateProvider);

            //finalizeWorldSystems.Add(
            //)

            sceneIsCurrentListeners.Add(system);
        }

        public UniTask InitializeAsync(StylizedSkyboxPlugin.StylizedSkyboxSettings pluginSettings, CancellationToken ct)
        {
            this.settings = pluginSettings;
            return UniTask.CompletedTask;
        }
    }
}
