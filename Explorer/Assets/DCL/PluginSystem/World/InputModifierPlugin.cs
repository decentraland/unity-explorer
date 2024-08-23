using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.PlayerInputMovement.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class InputModifierPlugin: IDCLWorldPlugin
    {
        private readonly ObjectProxy<Arch.Core.World> globalWorldProxy;
        private readonly ObjectProxy<Entity> playerEntity;

        public InputModifierPlugin(ObjectProxy<Arch.Core.World> globalWorldProxy, ObjectProxy<Entity> playerEntity)
        {
            this.globalWorldProxy = globalWorldProxy;
            this.playerEntity = playerEntity;
        }

        public void Dispose()
        {
            // throw new NotImplementedException();
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) => UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            InputModifierHandlerSystem.InjectToWorld(ref builder, globalWorldProxy, playerEntity);
            ResetDirtyFlagSystem<PBInputModifier>.InjectToWorld(ref builder);
        }
    }
}
