using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.SDKComponents.PhysicsImpulse.Systems
{
    public class SDKPhysicsImpulsePlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;

        public SDKPhysicsImpulsePlugin(World globalWorld, Entity globalPlayerEntity)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            PhysicsImpulseSystems.InjectToWorld(ref builder, globalWorld, globalPlayerEntity);
        }
    }
}
