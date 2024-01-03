using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
using Arch.System;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using ECS.LifeCycle.Components;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITextReleaseSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPoolsRegistry poolsRegistry;

        private UITextReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.poolsRegistry = poolsRegistry;
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleUITextRemovalQuery(World);
            World.Remove<UITextComponent>(in HandleUITextRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBUiText), typeof(DeleteEntityIntention))]
        private void HandleUITextRemoval(ref UITextComponent uiTextComponent) =>
            RemoveLabel(uiTextComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UITextComponent uiTextComponent) =>
            RemoveLabel(uiTextComponent);

        private void RemoveLabel(UITextComponent uiTextComponent)
        {
            if (!poolsRegistry.TryGetPool(typeof(Label), out IComponentPool componentPool))
                return;

            componentPool.Release(uiTextComponent.Label);
        }
    }
}
