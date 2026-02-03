using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using UnityEngine.UIElements;
using Color = UnityEngine.Color;

namespace DCL.SDKComponents.SceneUI.Systems.UIDropdown
{
    /*
     * As defined in the SDK, UiDropdown entities composition breakdown:
     * https://github.com/decentraland/js-sdk-toolchain/blob/main/packages/@dcl/react-ecs/src/components/Dropdown/index.tsx#L41-L53
     *  - UiDropdown
     * - (optional, but Explorer queries require it) uiTransform
     * - (optional) uiBackground
     * - (optional) onMouseDown
     * - (optional) onMouseUp
     * - (optional) onMouseEnter
     * - (optional) onMouseLeave
     */

    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIDropdownInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIDropdown";

        private readonly IComponentPool<UIDropdownComponent> dropdownsPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public UIDropdownInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            dropdownsPool = poolsRegistry.GetReferenceTypePool<UIDropdownComponent>();
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            InstantiateUIDropdownQuery(World);
            UpdateUIDropdownQuery(World);
            TriggerDropdownResultsQuery(World);
        }

        [Query]
        [All(typeof(PBUiDropdown), typeof(UITransformComponent))]
        [None(typeof(UIDropdownComponent))]
        private void InstantiateUIDropdown(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            var newDropdown = dropdownsPool.Get();
            newDropdown.Initialize(UiElementUtils.BuildElementName(COMPONENT_NAME, entity), "dcl-dropdown", "unity-base-popup-field__text");
            uiTransformComponent.Transform.Add(newDropdown.DropdownField);

            ConfigureHoverBehaviour(entity, newDropdown.DropdownField);

            World.Add(entity, newDropdown);
        }

        private void ConfigureHoverBehaviour(Entity entity, VisualElement targetVisualElement)
        {
            targetVisualElement.RegisterCallback<PointerEnterEvent>((_) =>
            {
                if (!World.TryGet(entity, out UITransformComponent? uiComponent)) return;

                float hoverBorderAlphaMultiplier = 0.75f;
                Color borderColor = new Color(
                    uiComponent!.Transform.style.borderTopColor.value.r,
                    uiComponent.Transform.style.borderTopColor.value.g,
                    uiComponent.Transform.style.borderTopColor.value.b,
                    hoverBorderAlphaMultiplier * uiComponent.Transform.style.borderTopColor.value.a);
                uiComponent.Transform.style.borderTopColor = new StyleColor(borderColor);

                borderColor = new Color(
                    uiComponent.Transform.style.borderRightColor.value.r,
                    uiComponent.Transform.style.borderRightColor.value.g,
                    uiComponent.Transform.style.borderRightColor.value.b,
                    hoverBorderAlphaMultiplier * uiComponent.Transform.style.borderRightColor.value.a);
                uiComponent.Transform.style.borderRightColor = new StyleColor(borderColor);

                borderColor = new Color(
                    uiComponent.Transform.style.borderBottomColor.value.r,
                    uiComponent.Transform.style.borderBottomColor.value.g,
                    uiComponent.Transform.style.borderBottomColor.value.b,
                    hoverBorderAlphaMultiplier * uiComponent.Transform.style.borderBottomColor.value.a);
                uiComponent.Transform.style.borderBottomColor = new StyleColor(borderColor);

                borderColor = new Color(
                    uiComponent.Transform.style.borderLeftColor.value.r,
                    uiComponent.Transform.style.borderLeftColor.value.g,
                    uiComponent.Transform.style.borderLeftColor.value.b,
                    hoverBorderAlphaMultiplier * uiComponent.Transform.style.borderLeftColor.value.a);
                uiComponent.Transform.style.borderLeftColor = new StyleColor(borderColor);

                if (!World.TryGet(entity, out PBUiBackground? pbUiBackground))
                    return;
                float darkenFactor = 0.1f;
                uiComponent.Transform.style.backgroundColor = Color.Lerp(pbUiBackground!.GetColor(), Color.black, darkenFactor);
            });

            // targetVisualElement.RegisterCallback<PointerCaptureEvent>((_) =>
            /*targetVisualElement.RegisterCallback<PointerDownEvent>((_) =>
            {
                if (!World.TryGet(entity, out UITransformComponent? uiComponent) || !World.TryGet(entity, out PBUiBackground? pbUiBackground))
                    return;
                float darkenFactor = 0.15f;
                uiComponent!.Transform.style.backgroundColor = Color.Lerp(pbUiBackground!.GetColor(), Color.black, darkenFactor);
            });*/

            targetVisualElement.RegisterCallback<PointerLeaveEvent>((_) =>
            {
                if (!World.TryGet(entity, out UITransformComponent? uiComponent) || !World.TryGet(entity, out PBUiTransform? pbUiTransform ))
                    return;

                uiComponent!.Transform.style.borderTopColor = pbUiTransform!.GetBorderTopColor();
                uiComponent.Transform.style.borderRightColor = pbUiTransform!.GetBorderRightColor();
                uiComponent.Transform.style.borderBottomColor = pbUiTransform!.GetBorderBottomColor();
                uiComponent.Transform.style.borderLeftColor = pbUiTransform!.GetBorderLeftColor();

                if (!World.TryGet(entity, out PBUiBackground? pbUiBackground))
                    return;
                uiComponent.Transform.style.backgroundColor = pbUiBackground!.GetColor();
            });
        }

        [Query]
        private void UpdateUIDropdown(ref UIDropdownComponent uiDropdownComponent, ref PBUiDropdown sdkModel)
        {
            if (!sdkModel.IsDirty) return;

            UiElementUtils.SetupUIDropdownComponent(ref uiDropdownComponent, ref sdkModel);
            sdkModel.IsDirty = false;
        }

        [Query]
        private void TriggerDropdownResults(ref UIDropdownComponent uiDropdownComponent, ref CRDTEntity sdkEntity)
        {
            if (!uiDropdownComponent.IsOnValueChangedTriggered)
                return;

            PutMessage(ref sdkEntity, uiDropdownComponent.DropdownField.index);
            uiDropdownComponent.IsOnValueChangedTriggered = false;
        }

        private void PutMessage(ref CRDTEntity sdkEntity, int index)
        {
            ecsToCRDTWriter.PutMessage<PBUiDropdownResult, int>(static (component, data) =>
            {
                component.Value = data;
            }, sdkEntity, index);
        }
    }
}
