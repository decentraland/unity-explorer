using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITextHandlerSystem : BaseUnityLoopSystem
    {
        private readonly UIDocument canvas;
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IComponentPool<Label> labelsPool;

        private UITextHandlerSystem(World world, UIDocument canvas, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.canvas = canvas;
            this.poolsRegistry = poolsRegistry;
            labelsPool = poolsRegistry.GetReferenceTypePool<Label>();
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingUITextQuery(World);
            TrySetupExistingUITextQuery(World);
            HandleEntityDestructionQuery(World);
            HandleUITextRemovalQuery(World);
            World.Remove<UITextComponent>(in HandleUITextRemoval_QueryDescription);
        }

        [Query]
        [All(typeof(PBUiText))]
        [None(typeof(UITextComponent))]
        private void InstantiateNonExistingUIText(in Entity entity, ref PBUiText sdkComponent)
        {
            var uiTextComponent = new UITextComponent();
            InstantiateLabel(ref uiTextComponent, ref sdkComponent);
            World.Add(entity, uiTextComponent);
        }

        [Query]
        [All(typeof(PBUiText), typeof(UITextComponent))]
        private void TrySetupExistingUIText(ref UITextComponent uiTextComponent, ref PBUiText sdkComponent)
        {
            if (!sdkComponent.IsDirty)
                return;

            if (ReferenceEquals(uiTextComponent.Label, null))
                InstantiateLabel(ref uiTextComponent, ref sdkComponent);
            else
                SetupLabel(uiTextComponent.Label, sdkComponent);

            sdkComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(PBUiText), typeof(DeleteEntityIntention))]
        private void HandleUITextRemoval(ref UITextComponent uiTextComponent) =>
            RemoveLabel(uiTextComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UITextComponent uiTextComponent) =>
            RemoveLabel(uiTextComponent);

        private void InstantiateLabel(ref UITextComponent uiTextComponent, ref PBUiText sdkComponent)
        {
            var label = labelsPool.Get();
            UiElementUtils.SetElementDefaultStyle(label.style);
            canvas.rootVisualElement.Add(label);
            uiTextComponent.Label = label;

            SetupLabel(label, sdkComponent);
        }

        private static void SetupLabel(Label labelToSetup, PBUiText model)
        {
            labelToSetup.pickingMode = PickingMode.Ignore;
            labelToSetup.text = model.Value;
            labelToSetup.style.color = model.GetColor();
            labelToSetup.style.fontSize = model.GetFontSize();
            labelToSetup.style.unityTextAlign = model.GetTextAlign();
            //labelToSetup.style.unityFont = model.GetFont();
        }

        private void RemoveLabel(UITextComponent uiTextComponent)
        {
            if (!poolsRegistry.TryGetPool(typeof(Label), out IComponentPool componentPool))
                return;

            componentPool.Release(uiTextComponent.Label);
        }
    }
}
