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

namespace DCL.SDKComponents.SceneUI.Systems.UIInput
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UIInputReleaseSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool componentPool;

        private UIInputReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            poolsRegistry.TryGetPool(typeof(UIInputComponent), out componentPool);
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleUIInputRemovalQuery(World);
            World.Remove<UIInputComponent>(in HandleUIInputRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBUiInput), typeof(DeleteEntityIntention))]
        private void HandleUIInputRemoval(ref UIInputComponent uiInputComponent) =>
            RemoveTextField(uiInputComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UIInputComponent uiInputComponent) =>
            RemoveTextField(uiInputComponent);

        private void RemoveTextField(UIInputComponent uiInputComponent)
        {
            if (componentPool != null)
                componentPool.Release(uiInputComponent);

            uiInputComponent.UnregisterInputCallbacks();
        }
    }
}
