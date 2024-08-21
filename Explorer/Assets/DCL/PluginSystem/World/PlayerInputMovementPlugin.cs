using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.PlayerInputMovement.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class PlayerInputMovementPlugin: IDCLWorldPlugin
    {
        public void Dispose()
        {
            // throw new NotImplementedException();
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) => UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            PlayerInputMovementHandlerSystem.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<PBPlayerInputMovement>.InjectToWorld(ref builder);
        }
    }
}
