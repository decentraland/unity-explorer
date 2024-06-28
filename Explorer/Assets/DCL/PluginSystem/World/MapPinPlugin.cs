using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.MapPins.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class MapPinPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly ObjectProxy<Arch.Core.World> globalWorldProxy;

        public MapPinPlugin(ObjectProxy<Arch.Core.World> globalWorldProxy)
        {
            this.globalWorldProxy = globalWorldProxy;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBMapPin>.InjectToWorld(ref builder);
            MapPinLoaderSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, globalWorldProxy);
        }
    }
}
