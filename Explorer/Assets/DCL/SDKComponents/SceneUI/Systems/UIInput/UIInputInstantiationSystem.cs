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
using ECS.LifeCycle.Components;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UIInput
{
    /*
     * As defined in the SDK, UiInput entities composition breakdown:
     * https://github.com/decentraland/js-sdk-toolchain/blob/main/packages/@dcl/react-ecs/src/components/Input/index.tsx#L43-L55
     * - PBUiInput
     * - (optional, but Explorer queries require it) PBUiTransform
     * - (optional) PBUiBackground
     * - (optional) PBPointerEvents
     */

    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIInputInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIInput";
        private const float HOVER_BORDER_DARKEN_FACTOR = 0.3f;
        private const float HOVER_BACKGROUND_DARKEN_FACTOR = 0f;

        private readonly IComponentPool<UIInputComponent> inputTextsPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IInputBlock inputBlock;
        private readonly StyleFontDefinition[] styleFontDefinitions;

        public UIInputInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry, IECSToCRDTWriter ecsToCRDTWriter, IInputBlock inputBlock, in StyleFontDefinition[] styleFontDefinitions) : base(world)
        {
            inputTextsPool = poolsRegistry.GetReferenceTypePool<UIInputComponent>().EnsureNotNull();
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.inputBlock = inputBlock;
            this.styleFontDefinitions = styleFontDefinitions;
        }

        protected override void Update(float t)
        {
            UpdateUIInputTransformDefaultsQuery(World!);

            InstantiateUIInputQuery(World!);
            UpdateUIInputQuery(World!);

            TriggerInputResultsQuery(World!);
        }

        [Query]
        [All(typeof(PBUiInput))]
        [None(typeof(UIInputComponent))]
        private void InstantiateUIInput(in Entity entity, in PBUiInput sdkModel, in PBUiTransform pbUiTransform, ref UITransformComponent uiTransformComponent)
        {
            var newUIInputComponent = inputTextsPool.Get()!;
            newUIInputComponent.Initialize(inputBlock,
                UiElementUtils.BuildElementName(COMPONENT_NAME, entity),
                sdkModel.Value,
                sdkModel.Placeholder,
                sdkModel.GetPlaceholderColor());
            uiTransformComponent.ContentContainer.Add(newUIInputComponent.TextField);

            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, uiTransformComponent.Transform);
            UiElementUtils.ApplyDefaultUiBackgroundValues(World, entity, uiTransformComponent.Transform);
            UiElementUtils.ConfigureHoverStylesBehaviour(World, entity, in uiTransformComponent, newUIInputComponent.TextField, HOVER_BORDER_DARKEN_FACTOR, HOVER_BACKGROUND_DARKEN_FACTOR);

            World!.Add(entity, newUIInputComponent);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateUIInput(ref UIInputComponent uiInputComponent, ref PBUiInput sdkModel)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupUIInputComponent(ref uiInputComponent, in sdkModel, in styleFontDefinitions);
            sdkModel.IsDirty = false;
        }

        [Query]
        [All(typeof(UIInputComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateUIInputTransformDefaults(in Entity entity, in UITransformComponent uiTransformComponent, in PBUiTransform pbUiTransform, ref UIInputComponent uiInputComponent)
        {
            if (!pbUiTransform.IsDirty) return;

            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, uiTransformComponent.Transform);
            // Re-configure hover so the stored border colors match the updated transform (e.g. defaults applied above); avoids wrong restore on hover leave when PBUiTransform changed while hovered.
            UiElementUtils.ConfigureHoverStylesBehaviour(World!, entity, in uiTransformComponent, uiInputComponent.TextField, HOVER_BORDER_DARKEN_FACTOR, HOVER_BACKGROUND_DARKEN_FACTOR);
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
