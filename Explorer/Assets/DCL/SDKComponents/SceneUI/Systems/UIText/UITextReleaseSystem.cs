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

namespace DCL.SDKComponents.SceneUI.Systems.UIText
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITextReleaseSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool componentPool;

        private UITextReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            poolsRegistry.TryGetPool(typeof(Label), out componentPool);
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
            if (componentPool != null)
                componentPool.Release(uiTextComponent.Label);
        }
    }
}
