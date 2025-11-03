using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SkyboxTime.Systems;
using DCL.SkyBox;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.SDKComponents.SkyboxTime
{
    public class SkyboxTimePlugin : IDCLWorldPlugin<SkyboxTimePlugin.SkyboxTimeSettings>
    {
        private SkyboxSettingsAsset? skyboxSettings;

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var system = SkyboxTimeHandlerSystem.InjectToWorld(
                ref builder,
                skyboxSettings,
                persistentEntities.SceneRoot,
                sharedDependencies.SceneStateProvider);

            sceneIsCurrentListeners.Add(system);
        }

        public UniTask InitializeAsync(SkyboxTimeSettings pluginSettings, CancellationToken ct)
        {
            skyboxSettings = pluginSettings.Settings;
            return UniTask.CompletedTask;
        }

        public class SkyboxTimeSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public SkyboxSettingsAsset Settings { get; private set; }
        }
    }
}
