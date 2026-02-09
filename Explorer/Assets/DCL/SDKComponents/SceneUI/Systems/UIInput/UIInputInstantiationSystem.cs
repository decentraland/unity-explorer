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
                sdkModel.Value,
                sdkModel.Placeholder,
                sdkModel.GetPlaceholderColor());
            uiTransformComponent.Transform.Add(newUIInputComponent.TextField);

            UiElementUtils.ConfigureHoverStylesBehaviour(World, entity, newUIInputComponent.TextField, 0.22f, 0f);

            World!.Add(entity, newUIInputComponent);
        }

        [Query]
        private void UpdateUIInput(ref UIInputComponent uiInputComponent, ref PBUiInput sdkModel)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupUIInputComponent(ref uiInputComponent, in sdkModel, in styleFontDefinitions);
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
