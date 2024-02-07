using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using UnityEngine;

namespace DCL.SDKComponents.SceneUI.Systems.UIInput
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIInputInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIInput";

        private readonly IComponentPool<DCLInputText> inputTextsPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public UIInputInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            inputTextsPool = poolsRegistry.GetReferenceTypePool<DCLInputText>();
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            InstantiateUIInputQuery(World);
            UpdateUIInputQuery(World);
        }

        [Query]
        [All(typeof(PBUiInput), typeof(UITransformComponent))]
        [None(typeof(UIInputComponent))]
        private void InstantiateUIInput(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            var inputText = inputTextsPool.Get();
            inputText.Initialize(UiElementUtils.BuildElementName(COMPONENT_NAME, entity), "dcl-input");
            inputText.UnregisterAllCallbacks();
            uiTransformComponent.Transform.Add(inputText.TextField);
            var uiInputComponent = new UIInputComponent();
            uiInputComponent.Input = inputText;
            World.Add(entity, uiInputComponent);
        }

        [Query]
        private void UpdateUIInput(UIInputComponent uiInputComponent, ref PBUiInput sdkModel, CRDTEntity sdkEntity)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupDCLInputText(ref uiInputComponent.Input, ref sdkModel);

            uiInputComponent.Input.RegisterOnChangeCallback(evt =>
            {
                evt.StopPropagation();

                ecsToCRDTWriter.PutMessage(
                    new PBUiInputResult
                    {
                        IsSubmit = false,
                        Value = uiInputComponent.Input.TextField.value,
                        IsDirty = false,
                    }, sdkEntity);
            });

            uiInputComponent.Input.RegisterOnKeyDownCallback(evt =>
            {
                evt.StopPropagation();

                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                ecsToCRDTWriter.PutMessage(
                    new PBUiInputResult
                    {
                        IsSubmit = true,
                        Value = uiInputComponent.Input.TextField.value,
                        IsDirty = false,
                    }, sdkEntity);

                uiInputComponent.Input.TextField.SetValueWithoutNotify(string.Empty);
            });

            sdkModel.IsDirty = false;
        }
    }
}
