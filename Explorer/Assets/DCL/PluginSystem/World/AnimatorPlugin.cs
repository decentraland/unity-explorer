using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.Animator.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class AnimatorPlugin : IDCLWorldPluginWithoutSettings
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBAnimator>.InjectToWorld(ref builder);
            SDKAnimatorUpdaterSystem.InjectToWorld(ref builder);
            AnimationPlayerSystem.InjectToWorld(ref builder);
            LegacyAnimationPlayerSystem.InjectToWorld(ref builder);
        }
    }
}
