using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.RealmInfo;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class RealmInfoPlugin : IDCLWorldPluginWithoutSettings
    {
        // public RealmInfoPlugin()

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var writeRealmInfoSystem = WriteRealmInfoSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            // finalizeWorldSystems.Add(writeRealmInfoSystem);
        }
    }
}
