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
            InstantiateLabel(entity, ref uiTextComponent, ref sdkComponent, ref transform);
            World.Add(entity, uiTextComponent);
        }

        [Query]
        [All(typeof(PBUiText), typeof(UITransformComponent), typeof(UITextComponent))]
        private void TrySetupExistingUIText(in Entity entity, ref UITextComponent uiTextComponent, ref PBUiText sdkComponent, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkComponent.IsDirty)
                return;

            if (ReferenceEquals(uiTextComponent.Label, null))
                InstantiateLabel(entity, ref uiTextComponent, ref sdkComponent, ref uiTransformComponent);
            else
                SetupLabel(ref uiTextComponent.Label, ref sdkComponent, ref uiTransformComponent);

            sdkComponent.IsDirty = false;
        }

        private void InstantiateLabel(in Entity entity, ref UITextComponent uiTextComponent, ref PBUiText sdkComponent, ref UITransformComponent uiTransformComponent)
        {
            var label = labelsPool.Get();
            label.name = $"UIText (Entity {entity.Id})";
            UiElementUtils.SetElementDefaultStyle(label.style);
            uiTransformComponent.Transform.Add(label);
            uiTextComponent.Label = label;

            SetupLabel(ref label, ref sdkComponent, ref uiTransformComponent);
        }

        private static void SetupLabel(ref Label labelToSetup, ref PBUiText model, ref UITransformComponent uiTransformComponent)
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
