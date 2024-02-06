using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;

namespace DCL.SDKComponents.SceneUI.Systems.UIInput
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIInputInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIInput";

        private readonly IComponentPool<DCLInputText> inputTextsPool;

        public UIInputInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            inputTextsPool = poolsRegistry.GetReferenceTypePool<DCLInputText>();
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
            uiTransformComponent.Transform.Add(inputText.TextField);
            var uiInputComponent = new UIInputComponent();
            uiInputComponent.Input = inputText;
            World.Add(entity, uiInputComponent);
        }

        [Query]
        private void UpdateUIInput(ref UIInputComponent uiInputComponent, ref PBUiInput sdkModel)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupDCLInputText(ref uiInputComponent.Input, ref sdkModel);
            sdkModel.IsDirty = false;
        }
    }
}
