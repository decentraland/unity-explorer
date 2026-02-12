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

namespace DCL.SDKComponents.SceneUI.Systems.UIDropdown
{
    /*
     * As defined in the SDK, UiDropdown entities composition breakdown:
     * https://github.com/decentraland/js-sdk-toolchain/blob/main/packages/@dcl/react-ecs/src/components/Dropdown/index.tsx#L41-L53
     * - PBUiDropdown
     * - (optional, but Explorer queries require it) PBUiTransform
     * - (optional) PBUiBackground
     * - (optional) PBPointerEvents
     */

    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIDropdownInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIDropdown";

        private readonly IComponentPool<UIDropdownComponent> dropdownsPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly StyleFontDefinition[] styleFontDefinitions;

        public UIDropdownInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry, IECSToCRDTWriter ecsToCRDTWriter, in StyleFontDefinition[] styleFontDefinitions) : base(world)
        {
            dropdownsPool = poolsRegistry.GetReferenceTypePool<UIDropdownComponent>();
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.styleFontDefinitions = styleFontDefinitions;
        }

        protected override void Update(float t)
        {
            UpdateUIDropdownTransformDefaultsQuery(World);

            InstantiateUIDropdownQuery(World);
            UpdateUIDropdownQuery(World);

            TriggerDropdownResultsQuery(World);
        }

        [Query]
        [All(typeof(PBUiDropdown))]
        [None(typeof(UIDropdownComponent))]
        private void InstantiateUIDropdown(in Entity entity, in PBUiTransform pbUiTransform, ref UITransformComponent uiTransformComponent)
        {
            var newDropdown = dropdownsPool.Get();
            newDropdown.Initialize(UiElementUtils.BuildElementName(COMPONENT_NAME, entity));
            uiTransformComponent.Transform.Add(newDropdown.DropdownField);

            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, uiTransformComponent.Transform);
            UiElementUtils.ApplyDefaultUiBackgroundValues(World, entity, uiTransformComponent.Transform);
            UiElementUtils.ConfigureHoverStylesBehaviour(World, entity, in uiTransformComponent, newDropdown.DropdownField, 0.22f, 0.1f);

            World.Add(entity, newDropdown);
        }

        [Query]
        private void UpdateUIDropdown(ref UIDropdownComponent uiDropdownComponent, ref PBUiDropdown sdkModel)
        {
            if (!sdkModel.IsDirty) return;

            UiElementUtils.SetupUIDropdownComponent(ref uiDropdownComponent, in sdkModel, in styleFontDefinitions);
            sdkModel.IsDirty = false;
        }

        [Query]
        [All(typeof(UIDropdownComponent))]
        private void UpdateUIDropdownTransformDefaults(in UITransformComponent uiTransformComponent, in PBUiTransform pbUiTransform)
        {
            if (!pbUiTransform.IsDirty) return;

            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, uiTransformComponent.Transform);
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
