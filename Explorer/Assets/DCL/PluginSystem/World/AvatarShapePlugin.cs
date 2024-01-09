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
        private WorldProxy globalWorld;

        public AvatarShapePlugin(WorldProxy globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void Dispose()
        {
            //ignore
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            ResetDirtyFlagSystem<PBAvatarShape>.InjectToWorld(ref builder);
            AvatarShapeLoaderSystem.InjectToWorld(ref builder, globalWorld);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
