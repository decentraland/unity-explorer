using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIDropdown;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.TestSuite;
using NSubstitute;
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
        private IECSToCRDTWriter ecsToCRDTWriter;
        private Entity entity;
        private UITransformComponent uiTransformComponent;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(UIDropdownComponent), new ComponentPool<UIDropdownComponent>() },
                }, null);

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            system = new UIDropdownInstantiationSystem(world, poolsRegistry, ecsToCRDTWriter);
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
            Assert.AreEqual(UiElementUtils.BuildElementName("UIDropdown", entity), uiDropdownComponent.DropdownField.name);
            Assert.IsTrue(uiDropdownComponent.DropdownField.ClassListContains("dcl-dropdown"));
            Assert.AreEqual(PickingMode.Position, uiDropdownComponent.DropdownField.pickingMode);
            Assert.IsTrue(uiTransformComponent.Transform.Contains(uiDropdownComponent.DropdownField));
            Assert.IsNotNull(uiDropdownComponent.DropdownField);
            Assert.IsNotNull(uiDropdownComponent.TextElement);
            Assert.IsTrue(uiDropdownComponent.TextElement.ClassListContains("unity-base-popup-field__text"));
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
                Assert.AreEqual(input.Options.Count, uiDropdownComponent.DropdownField.choices.Count);
                Assert.IsTrue(input.GetFontSize() == uiDropdownComponent.DropdownField.style.fontSize);
                Assert.IsTrue(input.GetTextAlign() == uiDropdownComponent.TextElement.style.unityTextAlign);
            }
        }
    }
}
