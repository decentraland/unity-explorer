using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using DCL.SDKComponents.Tween.Components;
using TweenCleanUpSystem = DCL.SDKComponents.Tween.TweenCleanUpSystem;
using TweenLoaderSystem = DCL.SDKComponents.Tween.TweenLoaderSystem;
using TweenUpdaterSystem = DCL.SDKComponents.Tween.TweenUpdaterSystem;

namespace DCL.PluginSystem.World
{
    public class TweenPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly TweenerPool tweenerPool;

        public TweenPlugin()
        {
            tweenerPool = new TweenerPool();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBTween>.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<PBTweenSequence>.InjectToWorld(ref builder);
            TweenLoaderSystem.InjectToWorld(ref builder);

            TweenUpdaterSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, tweenerPool, sharedDependencies.SceneStateProvider);

            finalizeWorldSystems.Add(TweenCleanUpSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, tweenerPool));
        }
    }
}
