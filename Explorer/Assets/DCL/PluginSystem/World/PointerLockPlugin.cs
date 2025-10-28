using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class PointerLockPlugin : IDCLWorldPlugin
    {
        private readonly Arch.Core.World globalWorld;
        private readonly IExposedCameraData cameraData;

        public PointerLockPlugin(Arch.Core.World globalWorld,
            IExposedCameraData cameraData)
        {
            this.globalWorld = globalWorld;
            this.cameraData = cameraData;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            UpdatePointerLockSystem.InjectToWorld(ref builder, globalWorld, cameraData);
        }
    }
}
