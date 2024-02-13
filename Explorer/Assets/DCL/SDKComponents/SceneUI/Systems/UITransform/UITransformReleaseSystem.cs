using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UITransformInstantiationSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformReleaseSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool componentPool;

        private UITransformReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            poolsRegistry.TryGetPool(typeof(VisualElement), out componentPool);
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleUITransformRemovalQuery(World);
            World.Remove<UITransformComponent>(in HandleUITransformRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBUiTransform), typeof(DeleteEntityIntention))]
        private void HandleUITransformRemoval(ref UITransformComponent uiTransformComponent) =>
            RemoveVisualElement(ref uiTransformComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UITransformComponent uiTransformComponent) =>
            RemoveVisualElement(ref uiTransformComponent);

        private void RemoveVisualElement(ref UITransformComponent uiTransformComponent)
        {
            if (componentPool != null)
                componentPool.Release(uiTransformComponent);
        }
    }
}
