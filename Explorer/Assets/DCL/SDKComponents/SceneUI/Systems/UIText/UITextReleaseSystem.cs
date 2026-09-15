using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.ComponentsPooling.Systems;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UIText
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [UpdateBefore(typeof(ReleasePoolableComponentSystem<Label, UITextComponent>))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITextReleaseSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool componentPool;

        internal UITextReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            poolsRegistry.TryGetPool(typeof(Label), out componentPool);
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleUITextRemovalQuery(World);
            World.Remove<UITextComponent>(in HandleUITextRemoval_QueryDescription);
        }

        public void FinalizeComponents(in Query query) =>
            ReleaseFontsQuery(World);

        [Query]
        [None(typeof(PBUiText), typeof(DeleteEntityIntention))]
        private void HandleUITextRemoval(ref UITextComponent uiTextComponent)
        {
            ReleaseFont(ref uiTextComponent);

            if (componentPool != null)
                componentPool.Release(uiTextComponent.Label);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UITextComponent uiTextComponent, in DeleteEntityIntention deleteEntityIntention)
        {
            if (deleteEntityIntention.DeferDeletion) return;

            ReleaseFont(ref uiTextComponent);
        }

        [Query]
        private void ReleaseFonts(ref UITextComponent uiTextComponent) =>
            ReleaseFont(ref uiTextComponent);

        private void ReleaseFont(ref UITextComponent uiTextComponent)
        {
            uiTextComponent.FontRequest.Release(World);

            if (uiTextComponent.CustomFont == null)
                return;

            uiTextComponent.CustomFont = null;
            UiElementUtils.ClearCustomFont(uiTextComponent.Label);
        }
    }
}
