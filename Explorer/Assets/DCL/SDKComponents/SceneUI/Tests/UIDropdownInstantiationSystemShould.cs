using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIDropdown;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIDropdownInstantiationSystemShould : UnitySystemTestBase<UIDropdownInstantiationSystem>
    {
        private IComponentPoolsRegistry poolsRegistry;
        private Entity entity;
        private UITransformComponent uiTransformComponent;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(DCLDropdown), new ComponentPool<DCLDropdown>() },
                }, null);

            system = new UIDropdownInstantiationSystem(world, poolsRegistry);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
        }

        [Test]
        public void InstantiateUIDropdown()
        {
            // Arrange
            var input = new PBUiDropdown();

            // Act
            world.Add(entity, input);
            system.Update(0);

            // Assert
            ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
            Assert.AreEqual(UiElementUtils.BuildElementName("UIDropdown", entity), uiDropdownComponent.Dropdown.DropdownField.name);
            Assert.IsTrue(uiDropdownComponent.Dropdown.DropdownField.ClassListContains("dcl-dropdown"));
            Assert.AreEqual(PickingMode.Position, uiDropdownComponent.Dropdown.DropdownField.pickingMode);
            Assert.IsTrue(uiTransformComponent.VisualElement.Contains(uiDropdownComponent.Dropdown.DropdownField));
            Assert.IsNotNull(uiDropdownComponent.Dropdown.DropdownField);
            Assert.IsNotNull(uiDropdownComponent.Dropdown.TextElement);
            Assert.IsTrue(uiDropdownComponent.Dropdown.TextElement.ClassListContains("unity-base-popup-field__text"));
        }

        [Test]
        public void UpdateUIDropdown()
        {
            // Arrange
            var input = new PBUiDropdown();
            world.Add(entity, input);
            system.Update(0);
            const int NUMBER_OF_UPDATES = 3;

            for (var i = 0; i < NUMBER_OF_UPDATES; i++)
            {
                // Act
                for (var j = 0; j < i+1; j++) input.Options.Add((j+1).ToString());
                input.FontSize = i + 1;
                input.TextAlign = (TextAlignMode) i;
                input.IsDirty = true;
                system.Update(0);

                // Assert
                ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
                Assert.AreEqual(input.Options.Count, uiDropdownComponent.Dropdown.DropdownField.choices.Count);
                Assert.IsTrue(input.GetFontSize() == uiDropdownComponent.Dropdown.DropdownField.style.fontSize);
                Assert.IsTrue(input.GetTextAlign() == uiDropdownComponent.Dropdown.TextElement.style.unityTextAlign);
            }
        }
    }
}
