using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.PrimaryPointerInfo.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class PrimaryPointerInfoPlugin : IDCLWorldPlugin<NoExposedPluginSettings>
    {
        private readonly Arch.Core.World globalWorld;
        private readonly ObjectProxy<DCLInput> containerInputProxy;

        public PrimaryPointerInfoPlugin(Arch.Core.World globalWorld, ObjectProxy<DCLInput> containerInputProxy)
        {
            this.globalWorld = globalWorld;
            this.containerInputProxy = containerInputProxy;
        }

        public void Dispose()
        {
            // No cleanup needed
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            PrimaryPointerInfoSystem.InjectToWorld(
                ref builder,
                globalWorld,
                containerInputProxy,
                sharedDependencies.SceneStateProvider,
                sharedDependencies.EcsToCRDTWriter
            );
        }

        public UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
