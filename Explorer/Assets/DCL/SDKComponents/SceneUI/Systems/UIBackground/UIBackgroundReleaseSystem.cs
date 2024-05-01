using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Groups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.SDKComponents.SceneUI.Systems.UIBackground
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UIBackgroundReleaseSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool componentPool;

        private UIBackgroundReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            poolsRegistry.TryGetPool(typeof(DCLImage), out componentPool);
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleUIBackgroundRemovalQuery(World);
            World.Remove<UIBackgroundComponent>(in HandleUIBackgroundRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBUiBackground), typeof(DeleteEntityIntention))]
        private void HandleUIBackgroundRemoval(ref UIBackgroundComponent uiBackgroundComponent) =>
            RemoveDCLImage(ref uiBackgroundComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UIBackgroundComponent uiBackgroundComponent) =>
            RemoveDCLImage(ref uiBackgroundComponent);

        private void RemoveDCLImage(ref UIBackgroundComponent uiBackgroundComponent)
        {
            if (uiBackgroundComponent.TexturePromise != null)
            {
                uiBackgroundComponent.TexturePromise.Value.ForgetLoading(World);
                uiBackgroundComponent.TexturePromise = null;
            }

            if (componentPool != null)
                componentPool.Release(uiBackgroundComponent.Image);
        }
    }
}
