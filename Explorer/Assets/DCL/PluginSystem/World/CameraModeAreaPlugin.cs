using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.CameraModeArea.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class CameraModeAreaPlugin : IDCLWorldPlugin
    {
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly ObjectProxy<Arch.Core.World> globalWorldProxy;

        public CameraModeAreaPlugin(ObjectProxy<Arch.Core.World> globalWorldProxy, ObjectProxy<Entity> cameraEntityProxy)
        {
            this.globalWorldProxy = globalWorldProxy;
            this.cameraEntityProxy = cameraEntityProxy;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBCameraModeArea>.InjectToWorld(ref builder);
            CameraModeAreaHandlerSystem.InjectToWorld(ref builder, globalWorldProxy, cameraEntityProxy);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
