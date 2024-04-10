using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.SDK.Systems;
using DCL.PluginSystem.World.Dependencies;
using DCL.Utilities;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class MultiplayerPlugin : IDCLWorldPlugin
    {
        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            var playerIdentityDataSystem = WritePlayerIdentityDataSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);
            // finalizeWorldSystems.Add(playerIdentityDataSystem); // TODO
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
