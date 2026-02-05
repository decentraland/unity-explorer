using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UIInput
{
    /*
     * As defined in the SDK, UiInput entities composition breakdown:
     * https://github.com/decentraland/js-sdk-toolchain/blob/main/packages/@dcl/react-ecs/src/components/Input/index.tsx#L43-L55
     * - UiInput
     * - (optional, but Explorer queries require it) uiTransform
     * - (optional) uiBackground
     * - (optional) onMouseDown
     * - (optional) onMouseUp
     * - (optional) onMouseEnter
     * - (optional) onMouseLeave
     */

    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIInputInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIInput";

        private readonly IComponentPool<UIInputComponent> inputTextsPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IInputBlock inputBlock;

        public UIInputInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry, IECSToCRDTWriter ecsToCRDTWriter, IInputBlock inputBlock) : base(world)
        {
            inputTextsPool = poolsRegistry.GetReferenceTypePool<UIInputComponent>().EnsureNotNull();
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.inputBlock = inputBlock;
        }

        protected override void Update(float t)
        {
            InstantiateUIInputQuery(World!);
            UpdateUIInputQuery(World!);
            TriggerInputResultsQuery(World!);
        }

        [Query]
        [All(typeof(PBUiInput), typeof(UITransformComponent))]
        [None(typeof(UIInputComponent))]
        private void InstantiateUIInput(in Entity entity, ref PBUiInput sdkModel, ref UITransformComponent uiTransformComponent)
        {
            var newUIInputComponent = inputTextsPool.Get()!;
            newUIInputComponent.Initialize(inputBlock,
                UiElementUtils.BuildElementName(COMPONENT_NAME, entity),
                "dcl-input",
                sdkModel.Value,
                sdkModel.Placeholder,
                sdkModel.GetPlaceholderColor());
            uiTransformComponent.Transform.Add(newUIInputComponent.TextField);

            ConfigureHoverBehaviour(entity, newUIInputComponent.TextField);

            World!.Add(entity, newUIInputComponent);
        }

        private void ConfigureHoverBehaviour(Entity entity, VisualElement targetVisualElement)
        {
            targetVisualElement.RegisterCallback<PointerEnterEvent>((_) =>
            {
                if (!World.TryGet(entity, out UITransformComponent? uiTransformComponent)) return;

                float hoverMultiplier = 0.75f;
                Color borderColor = new Color(
                    uiTransformComponent!.Transform.style.borderTopColor.value.r,
                    uiTransformComponent.Transform.style.borderTopColor.value.g,
                    uiTransformComponent.Transform.style.borderTopColor.value.b,
                    hoverMultiplier * uiTransformComponent.Transform.style.borderTopColor.value.a);
                uiTransformComponent.Transform.style.borderTopColor = new StyleColor(borderColor);

                borderColor = new Color(
                    uiTransformComponent.Transform.style.borderRightColor.value.r,
                    uiTransformComponent.Transform.style.borderRightColor.value.g,
                    uiTransformComponent.Transform.style.borderRightColor.value.b,
                    hoverMultiplier * uiTransformComponent.Transform.style.borderRightColor.value.a);
                uiTransformComponent.Transform.style.borderRightColor = new StyleColor(borderColor);

                borderColor = new Color(
                    uiTransformComponent.Transform.style.borderBottomColor.value.r,
                    uiTransformComponent.Transform.style.borderBottomColor.value.g,
                    uiTransformComponent.Transform.style.borderBottomColor.value.b,
                    hoverMultiplier * uiTransformComponent.Transform.style.borderBottomColor.value.a);
                uiTransformComponent.Transform.style.borderBottomColor = new StyleColor(borderColor);

                borderColor = new Color(
                    uiTransformComponent.Transform.style.borderLeftColor.value.r,
                    uiTransformComponent.Transform.style.borderLeftColor.value.g,
                    uiTransformComponent.Transform.style.borderLeftColor.value.b,
                    hoverMultiplier * uiTransformComponent.Transform.style.borderLeftColor.value.a);
                uiTransformComponent.Transform.style.borderLeftColor = new StyleColor(borderColor);
            });

            targetVisualElement.RegisterCallback<PointerLeaveEvent>((_) =>
            {
                if (!World.TryGet(entity, out UITransformComponent? uiTransformComponent) || !World.TryGet(entity, out PBUiTransform? pbUiTransform ))
                    return;

                uiTransformComponent!.Transform.style.borderTopColor = pbUiTransform!.GetBorderTopColor();
                uiTransformComponent.Transform.style.borderRightColor = pbUiTransform!.GetBorderRightColor();
                uiTransformComponent.Transform.style.borderBottomColor = pbUiTransform!.GetBorderBottomColor();
                uiTransformComponent.Transform.style.borderLeftColor = pbUiTransform!.GetBorderLeftColor();
            });
        }

        [Query]
        private void UpdateUIInput(ref UIInputComponent uiInputComponent, ref PBUiInput sdkModel)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupUIInputComponent(ref uiInputComponent, ref sdkModel);
            sdkModel.IsDirty = false;
        }

        [Query]
        private void TriggerInputResults(ref UIInputComponent uiInputComponent, ref CRDTEntity sdkEntity)
        {
            if (!uiInputComponent.IsOnValueChangedTriggered && !uiInputComponent.IsOnSubmitTriggered)
                return;

            PutMessage(ref sdkEntity, uiInputComponent.IsOnSubmitTriggered, uiInputComponent.TextField.value!);

            if (uiInputComponent.IsOnSubmitTriggered)
                uiInputComponent.TextField.SetValueWithoutNotify(string.Empty);

            uiInputComponent.IsOnValueChangedTriggered = false;
            uiInputComponent.IsOnSubmitTriggered = false;
        }

        private void PutMessage(ref CRDTEntity sdkEntity, bool isSubmit, string value)
        {
            ecsToCRDTWriter.PutMessage<PBUiInputResult, (bool isSubmit, string value)>(static (component, data) =>
            {
                component.IsSubmit = data.isSubmit;
                component.Value = data.value;
            }, sdkEntity, (isSubmit, value));
        }
    }
}
