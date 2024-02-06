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

namespace DCL.SDKComponents.SceneUI.Systems.UIDropdown
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIDropdownInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIDropdown";

        private readonly IComponentPool<DCLDropdown> dropdownsPool;

        public UIDropdownInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            dropdownsPool = poolsRegistry.GetReferenceTypePool<DCLDropdown>();
        }

        protected override void Update(float t)
        {
            InstantiateUIDropdownQuery(World);
            UpdateUIDropdownQuery(World);
        }

        [Query]
        [All(typeof(PBUiDropdown), typeof(UITransformComponent))]
        [None(typeof(UIDropdownComponent))]
        private void InstantiateUIDropdown(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            var dropdown = dropdownsPool.Get();
            dropdown.Initialize(UiElementUtils.BuildElementName(COMPONENT_NAME, entity), "dcl-dropdown", "unity-base-popup-field__text");
            uiTransformComponent.VisualElement.Add(dropdown.DropdownField);
            var uiDropdownComponent = new UIDropdownComponent();
            uiDropdownComponent.Dropdown = dropdown;
            World.Add(entity, uiDropdownComponent);
        }

        [Query]
        private void UpdateUIDropdown(ref UIDropdownComponent uiDropdownComponent, ref PBUiDropdown sdkModel)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupDCLDropdown(ref uiDropdownComponent.Dropdown, ref sdkModel);
            sdkModel.IsDirty = false;
        }
    }
}
