using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.SDKComponents.SceneUI.Systems.UIBackground
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
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
            // World.Remove<UIBackgroundComponent>(in HandleUIBackgroundRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBUiBackground), typeof(DeleteEntityIntention))]
        private void HandleUIBackgroundRemoval(in Entity entity, ref UIBackgroundComponent uiBackgroundComponent)
        {
            // World.Remove<UIBackgroundComponent>(entity);
            CleanUpDCLImage(ref uiBackgroundComponent);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UIBackgroundComponent uiBackgroundComponent) =>
            CleanUpDCLImage(ref uiBackgroundComponent);

        private void CleanUpDCLImage(ref UIBackgroundComponent uiBackgroundComponent)
        {
            if (uiBackgroundComponent.TexturePromise != null)
            {
                uiBackgroundComponent.TexturePromise.Value.ForgetLoading(World);
                uiBackgroundComponent.TexturePromise = null;
            }

            componentPool?.Release(uiBackgroundComponent.Image);
            uiBackgroundComponent.Image?.Dispose();
        }
    }
}
