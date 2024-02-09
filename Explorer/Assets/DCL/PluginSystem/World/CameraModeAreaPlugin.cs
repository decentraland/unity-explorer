using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.CameraModeArea.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class CameraModeAreaPlugin : IDCLWorldPlugin
    {
        private readonly MainPlayerTransform mainPlayerTransform;

        public CameraModeAreaPlugin(MainPlayerTransform mainPlayerTransform)
        {
            this.mainPlayerTransform = mainPlayerTransform;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            ResetDirtyFlagSystem<PBCameraModeArea>.InjectToWorld(ref builder);
            var cameraModeAreaHandlerSystem = CameraModeAreaHandlerSystem.InjectToWorld(ref builder, mainPlayerTransform);
            finalizeWorldSystems.Add(cameraModeAreaHandlerSystem);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
