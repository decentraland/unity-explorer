using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.SDKComponents.PhysicsImpulse.Systems
{
    public class SDKExternalPhysicsPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;

        public SDKExternalPhysicsPlugin(World globalWorld, Entity globalPlayerEntity)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners) =>
            SDKExternalPhysicsSystems.InjectToWorld(ref builder, globalWorld, globalPlayerEntity, sharedDependencies.SceneStateProvider);
    }
}
