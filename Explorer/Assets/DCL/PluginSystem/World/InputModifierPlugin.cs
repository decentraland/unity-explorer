using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.PlayerInputMovement.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class InputModifierPlugin: IDCLWorldPlugin
    {
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        public InputModifierPlugin(Arch.Core.World world, Entity playerEntity, ISceneRestrictionBusController sceneRestrictionBusController)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
        }

        public void Dispose()
        {
            // Ignore for now
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) => UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBInputModifier>.InjectToWorld(ref builder);
            var system = InputModifierHandlerSystem.InjectToWorld(ref builder, world, playerEntity, sharedDependencies.SceneStateProvider, sceneRestrictionBusController);
            sceneIsCurrentListeners.Add(system);
            finalizeWorldSystems.Add(system);
        }
    }
}
