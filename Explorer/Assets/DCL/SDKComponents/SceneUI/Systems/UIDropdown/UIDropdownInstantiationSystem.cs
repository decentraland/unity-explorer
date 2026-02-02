using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
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

            // ApplyDefaultUiTransformValues(entity, uiTransformComponent.Transform);
            // ApplyDefaultUiBackgroundValues(entity, uiTransformComponent.Transform);

            World.Add(entity, newDropdown);
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

        private void ApplyDefaultUiTransformValues(Entity entity, in VisualElement uiTransform)
        {
            var pbUiTransform = World.Get<PBUiTransform>(entity);

            if (pbUiTransform is
                {
                    HasBorderBottomLeftRadius: false,
                    HasBorderBottomRightRadius: false,
                    HasBorderTopLeftRadius: false,
                    HasBorderTopRightRadius: false
                })
            {
                uiTransform.style.borderBottomLeftRadius = new StyleLength(25);
                uiTransform.style.borderBottomRightRadius = new StyleLength(25);
                uiTransform.style.borderTopLeftRadius = new StyleLength(25);
                uiTransform.style.borderTopRightRadius = new StyleLength(25);
            }

            if (pbUiTransform is
                {
                    HasBorderTopWidth: false,
                    HasBorderRightWidth: false,
                    HasBorderBottomWidth: false,
                    HasBorderLeftWidth: false
                })
            {
                uiTransform.style.borderTopWidth = new StyleFloat(1);
                uiTransform.style.borderRightWidth = new StyleFloat(1);
                uiTransform.style.borderBottomWidth = new StyleFloat(1);
                uiTransform.style.borderLeftWidth = new StyleFloat(1);
            }

            if (pbUiTransform is
                {
                    BorderTopColor: null,
                    BorderRightColor: null,
                    BorderBottomColor: null,
                    BorderLeftColor: null
                })
            {
                uiTransform.style.borderTopColor = new StyleColor(Color.gray);
                uiTransform.style.borderRightColor = new StyleColor(Color.gray);
                uiTransform.style.borderBottomColor = new StyleColor(Color.gray);
                uiTransform.style.borderLeftColor = new StyleColor(Color.gray);
            }
        }

        private void ApplyDefaultUiBackgroundValues(Entity entity, in VisualElement uiTransform)
        {
            if (World.Has<PBUiBackground>(entity)) return;

            uiTransform.style.backgroundColor = new StyleColor(Color.white);
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
