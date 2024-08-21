using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Input.UnityInputSystem.Blocks;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using DCL.Utilities.Extensions;
using ECS.Abstract;

namespace DCL.SDKComponents.SceneUI.Systems.UIInput
{
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
        private void InstantiateUIInput(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            var newUIInputComponent = inputTextsPool.Get()!;
            newUIInputComponent.Initialize(inputBlock, UiElementUtils.BuildElementName(COMPONENT_NAME, entity), "dcl-input");
            uiTransformComponent.Transform.Add(newUIInputComponent.TextField);
            World!.Add(entity, newUIInputComponent);
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
