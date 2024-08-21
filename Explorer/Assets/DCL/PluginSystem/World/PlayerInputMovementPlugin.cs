using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class PlayerInputMovementPlugin: IDCLWorldPlugin<PlayerInputMovementPlugin.Settings>
    {
        public class Settings: IDCLPluginSettings
        {

        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            //throw new NotImplementedException();
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            //throw new NotImplementedException();
            await UniTask.Yield();
        }
    }
}
