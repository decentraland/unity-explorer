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

            ApplyDefaultUiTransformValues(entity, uiTransformComponent.Transform);
            ApplyDefaultUiBackgroundValues(entity, uiTransformComponent.Transform);

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

        private void ApplyDefaultUiTransformValues(Entity entity, in VisualElement uiTransform)
        {
            var pbUiTransform = World.Get<PBUiTransform>(entity);

            uiTransform.style.overflow = new StyleEnum<Overflow>(Overflow.Hidden);

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
