using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
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

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            BillboardSystem.InjectToWorld(ref builder, cameraData);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            BillboardSystem.InjectToWorld(ref builder, cameraData);
        }
    }
}
