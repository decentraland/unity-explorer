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

            ApplyDefaultUiTransformValues(entity, in newDropdown.DropdownField);
            ApplyDefaultUiBackgroundValues(entity, in newDropdown.DropdownField);

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

        private void ApplyDefaultUiTransformValues(Entity entity, in DropdownField dropdownUiElement)
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
                dropdownUiElement.parent.style.borderBottomLeftRadius = new StyleLength(25);
                dropdownUiElement.parent.style.borderBottomRightRadius = new StyleLength(25);
                dropdownUiElement.parent.style.borderTopLeftRadius = new StyleLength(25);
                dropdownUiElement.parent.style.borderTopRightRadius = new StyleLength(25);
            }

            if (pbUiTransform is
                {
                    HasBorderTopWidth: false,
                    HasBorderRightWidth: false,
                    HasBorderBottomWidth: false,
                    HasBorderLeftWidth: false
                })
            {
                dropdownUiElement.parent.style.borderTopWidth = new StyleFloat(1);
                dropdownUiElement.parent.style.borderRightWidth = new StyleFloat(1);
                dropdownUiElement.parent.style.borderBottomWidth = new StyleFloat(1);
                dropdownUiElement.parent.style.borderLeftWidth = new StyleFloat(1);
            }

            if (pbUiTransform is
                {
                    BorderTopColor: null,
                    BorderRightColor: null,
                    BorderBottomColor: null,
                    BorderLeftColor: null
                })
            {
                dropdownUiElement.parent.style.borderTopColor = new StyleColor(Color.gray);
                dropdownUiElement.parent.style.borderRightColor = new StyleColor(Color.gray);
                dropdownUiElement.parent.style.borderBottomColor = new StyleColor(Color.gray);
                dropdownUiElement.parent.style.borderLeftColor = new StyleColor(Color.gray);
            }
        }

        private void ApplyDefaultUiBackgroundValues(Entity entity, in DropdownField dropdownUiElement)
        {
            if (World.Has<PBUiBackground>(entity)) return;

            dropdownUiElement.parent.style.backgroundColor = new StyleColor(Color.white);
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
