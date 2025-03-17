using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.SDKComponents.SceneUI.Systems.UIDropdown
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UIDropdownReleaseSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<UIDropdownComponent> componentPool;

        private UIDropdownReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            componentPool = poolsRegistry.GetReferenceTypePool<UIDropdownComponent>();
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleUIDropdownRemovalQuery(World);
            World.Remove<UIDropdownComponent>(in HandleUIDropdownRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBUiDropdown), typeof(DeleteEntityIntention))]
        private void HandleUIDropdownRemoval(ref UIDropdownComponent uiDropdownComponent) =>
            RemoveDropdownField(uiDropdownComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UIDropdownComponent uiDropdownComponent) =>
            RemoveDropdownField(uiDropdownComponent);

        private void RemoveDropdownField(UIDropdownComponent uiDropdownComponent)
        {
            if (componentPool != null)
                componentPool.Release(uiDropdownComponent);

            uiDropdownComponent.UnregisterDropdownCallbacks();
        }
    }
}
