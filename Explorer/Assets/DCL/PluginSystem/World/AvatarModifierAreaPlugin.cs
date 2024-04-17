using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarModifierArea.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class AvatarModifierAreaPlugin : IDCLWorldPlugin
    {
        private readonly ObjectProxy<Arch.Core.World> globalWorldProxy;

        public AvatarModifierAreaPlugin(ObjectProxy<Arch.Core.World> globalWorldProxy)
        {
            this.globalWorldProxy = globalWorldProxy;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var avatarModifierAreaHandlerSystem = AvatarModifierAreaHandlerSystem.InjectToWorld(ref builder, globalWorldProxy);
            finalizeWorldSystems.Add(avatarModifierAreaHandlerSystem);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
