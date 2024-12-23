using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;
using BillboardSystem = DCL.Billboard.System.BillboardSystem;

namespace DCL.PluginSystem.World
{
    public class BillboardPlugin : IDCLWorldPlugin
    {
        private readonly IExposedCameraData cameraData;

        public BillboardPlugin(IExposedCameraData cameraData)
        {
            this.cameraData = cameraData;
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            BillboardSystem.InjectToWorld(ref builder, cameraData);
        }
    }
}
