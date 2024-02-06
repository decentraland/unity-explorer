using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Systems.UIText
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UITextInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIText";

        private readonly IComponentPool<Label> labelsPool;

        public UITextInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            labelsPool = poolsRegistry.GetReferenceTypePool<Label>();
        }

        protected override void Update(float t)
        {
            InstantiateUITextQuery(World);
            UpdateUITextQuery(World);
        }

        [Query]
        [All(typeof(PBUiText), typeof(UITransformComponent))]
        [None(typeof(UITextComponent))]
        private void InstantiateUIText(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            var label = labelsPool.Get();
            label.name = UiElementUtils.BuildElementName(COMPONENT_NAME, entity);
            label.pickingMode = PickingMode.Ignore;
            UiElementUtils.SetElementDefaultStyle(label.style);
            uiTransformComponent.Transform.Add(label);
            var uiTextComponent = new UITextComponent();
            uiTextComponent.Label = label;
            World.Add(entity, uiTextComponent);
        }

        [Query]
        private void UpdateUIText(ref UITextComponent uiTextComponent, ref PBUiText sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupLabel(ref uiTextComponent.Label, ref sdkModel, ref uiTransformComponent);
            sdkModel.IsDirty = false;
        }
    }
}
