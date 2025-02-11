using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.AvatarShape.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class AvatarShapePlugin : IDCLWorldPlugin
    {
        private readonly Arch.Core.World globalWorld;

        public AvatarShapePlugin(Arch.Core.World globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBAvatarShape>.InjectToWorld(ref builder);
            var avatarShapeHandlerSystem = AvatarShapeHandlerSystem.InjectToWorld(ref builder, globalWorld);
            finalizeWorldSystems.Add(avatarShapeHandlerSystem);
            UpdateAvatarShapeTargetPositionSystem.InjectToWorld(ref builder, globalWorld);
        }
    }
}
