using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.AvatarShape.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class AvatarShapePlugin : IDCLWorldPlugin
    {
        private readonly ObjectProxy<Arch.Core.World> globalWorldProxy;

        public AvatarShapePlugin(ObjectProxy<Arch.Core.World> globalWorldProxy)
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
            ResetDirtyFlagSystem<PBAvatarShape>.InjectToWorld(ref builder);
            var avatarShapeHandlerSystem = AvatarShapeHandlerSystem.InjectToWorld(ref builder, globalWorldProxy);
            finalizeWorldSystems.Add(avatarShapeHandlerSystem);
        }
    }
}
