using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarAttach.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class AvatarAttachPlugin : IDCLWorldPlugin
    {
        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // ResetDirtyFlagSystem<PBAvatarAttach>.InjectToWorld(ref builder);
            var avatarShapeHandlerSystem = AvatarAttachHandlerSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(avatarShapeHandlerSystem);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
