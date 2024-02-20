using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class AnimatorPlugin : IDCLWorldPlugin
    {
        private readonly IComponentPool<SDKAnimatorComponent> animatorComponentPool;
        private readonly IComponentPool<SDKAnimationState> animationStatePool;

        public AnimatorPlugin(IComponentPoolsRegistry componentsContainerComponentPoolsRegistry)
        {
            animatorComponentPool = componentsContainerComponentPoolsRegistry.AddComponentPool<SDKAnimatorComponent>();
            animationStatePool = componentsContainerComponentPoolsRegistry.AddComponentPool<SDKAnimationState>();
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            ResetDirtyFlagSystem<PBAnimator>.InjectToWorld(ref builder);
            AnimatorLoaderSystem.InjectToWorld(ref builder, animatorComponentPool, animationStatePool);
            AnimatorUpdaterSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, animatorComponentPool, animationStatePool);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }

    }
}
