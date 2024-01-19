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
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UIInput
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIInputInstantiationSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<TextField> textFieldsPool;

        private UIInputInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            textFieldsPool = poolsRegistry.GetReferenceTypePool<TextField>();
        }

        protected override void Update(float t)
        {
            InstantiateUIInputQuery(World);
            UpdateUIInputQuery(World);
        }

        [Query]
        [All(typeof(PBUiInput), typeof(PBUiTransform), typeof(UITransformComponent))]
        [None(typeof(UIInputComponent))]
        private void InstantiateUIInput(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            var textField = textFieldsPool.Get();
            textField.name = $"UIInput (Entity {entity.Id})";
            textField.AddToClassList("dcl-input");
            textField.pickingMode = PickingMode.Position;
            uiTransformComponent.Transform.Add(textField);
            var uiInputComponent = new UIInputComponent();
            uiInputComponent.TextField = textField;
            uiInputComponent.Placeholder = new TextFieldPlaceholder(textField);
            World.Add(entity, uiInputComponent);
        }

        [Query]
        [All(typeof(PBUiInput), typeof(UIInputComponent))]
        private void UpdateUIInput(ref UIInputComponent uiInputComponent, ref PBUiInput sdkModel)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupTextField(ref uiInputComponent.TextField, ref uiInputComponent.Placeholder, ref sdkModel);
            sdkModel.IsDirty = false;
        }
    }
}
