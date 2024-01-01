using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Unity.ColorComponent;
using ECS.Unity.Groups;
using ECS.Unity.Utils;
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
        private readonly IComponentPool<Label> labelsPool;

        private UITextHandlerSystem(World world, UIDocument canvas, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.canvas = canvas;
            labelsPool = poolsRegistry.GetReferenceTypePool<Label>();
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingUITextQuery(World);
            TrySetupExistingUITextQuery(World);
        }

        [Query]
        [All(typeof(PBUiText), typeof(PBUiTransform))]
        [None(typeof(UITextComponent))]
        private void InstantiateNonExistingUIText(in Entity entity, ref PBUiText sdkComponent)
        {
            var uiTextComponent = new UITextComponent();
            InstantiateLabel(ref uiTextComponent, ref sdkComponent);
            World.Add(entity, uiTextComponent);
        }

        [Query]
        [All(typeof(PBUiText), typeof(PBUiTransform), typeof(UITextComponent))]
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

        private void InstantiateLabel(ref UITextComponent uiTextComponent, ref PBUiText sdkComponent)
        {
            var label = labelsPool.Get();
            UiElementUtils.SetElementDefaultStyle(label.style);
            canvas.rootVisualElement.Add(label);
            uiTextComponent.Label = label;

            SetupLabel(label, sdkComponent);
        }

        private static void SetupLabel(Label labelToSetup, PBUiText pbModel)
        {
            labelToSetup.text = pbModel.Value;
            labelToSetup.style.color = pbModel.GetColor();
            labelToSetup.style.fontSize = pbModel.GetFontSize();
            labelToSetup.style.unityTextAlign = pbModel.GetTextAlign();
            //labelToSetup.style.unityFont = pbModel.GetFont();
        }
    }
}
