using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Systems
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UITextInstantiationSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<Label> labelsPool;

        private UITextInstantiationSystem(World world,IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            labelsPool = poolsRegistry.GetReferenceTypePool<Label>();
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingUITextQuery(World);
            TrySetupExistingUITextQuery(World);
        }

        [Query]
        [All(typeof(PBUiText), typeof(PBUiTransform), typeof(UITransformComponent))]
        [None(typeof(UITextComponent))]
        private void InstantiateNonExistingUIText(in Entity entity, ref PBUiText sdkComponent, ref UITransformComponent transform)
        {
            var uiTextComponent = new UITextComponent();
            InstantiateLabel(ref uiTextComponent, ref sdkComponent, ref transform);
            World.Add(entity, uiTextComponent);
        }

        [Query]
        [All(typeof(PBUiText), typeof(UITransformComponent), typeof(UITextComponent))]
        private void TrySetupExistingUIText(ref UITextComponent uiTextComponent, ref PBUiText sdkComponent, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkComponent.IsDirty)
                return;

            if (ReferenceEquals(uiTextComponent.Label, null))
                InstantiateLabel(ref uiTextComponent, ref sdkComponent, ref uiTransformComponent);
            else
                SetupLabel(uiTextComponent.Label, sdkComponent, uiTransformComponent);

            sdkComponent.IsDirty = false;
        }

        private void InstantiateLabel(ref UITextComponent uiTextComponent, ref PBUiText sdkComponent, ref UITransformComponent uiTransformComponent)
        {
            var label = labelsPool.Get();
            UiElementUtils.SetElementDefaultStyle(label.style);
            uiTransformComponent.Transform.Add(label);
            uiTextComponent.Label = label;

            SetupLabel(label, sdkComponent, uiTransformComponent);
        }

        private static void SetupLabel(Label labelToSetup, PBUiText model, UITransformComponent uiTransformComponent)
        {
            labelToSetup.pickingMode = PickingMode.Ignore;

            if (uiTransformComponent.Transform.style.width.keyword == StyleKeyword.Auto || uiTransformComponent.Transform.style.height.keyword == StyleKeyword.Auto)
                labelToSetup.style.position = new StyleEnum<Position>(Position.Relative);

            labelToSetup.text = model.Value;
            labelToSetup.style.color = model.GetColor();
            labelToSetup.style.fontSize = model.GetFontSize();
            labelToSetup.style.unityTextAlign = model.GetTextAlign();
            //labelToSetup.style.unityFont = model.GetFont();
        }
    }
}
