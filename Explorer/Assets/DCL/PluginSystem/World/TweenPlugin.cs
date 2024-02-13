using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.Tween.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class TweenPlugin : IDCLWorldPlugin
    {
        private readonly WorldProxy globalWorld;
        private WriteTweenDataSystem system;

        public TweenPlugin(WorldProxy globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            ResetDirtyFlagSystem<PBTween>.InjectToWorld(ref builder);
            var tweenHandlerSystem = TweenLoaderSystem.InjectToWorld(ref builder);
            var tweenUpdaterSystem = TweenUpdaterSystem.InjectToWorld(ref builder);
            WriteTweenDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            finalizeWorldSystems.Add(tweenHandlerSystem);
            finalizeWorldSystems.Add(tweenUpdaterSystem);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
