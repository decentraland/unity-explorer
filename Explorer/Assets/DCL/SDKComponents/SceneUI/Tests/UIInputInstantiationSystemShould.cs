using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIInput;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIInputInstantiationSystemShould : UnitySystemTestBase<UIInputInstantiationSystem>
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
                    { typeof(DCLInputText), new ComponentPool<DCLInputText>() },
                }, null);

            system = new UIInputInstantiationSystem(world, poolsRegistry);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
        }

        [Test]
        public void InstantiateUIInput()
        {
            // Arrange
            var input = new PBUiInput();

            // Act
            world.Add(entity, input);
            system.Update(0);

            // Assert
            ref UIInputComponent uiInputComponent = ref world.Get<UIInputComponent>(entity);
            Assert.AreEqual(UiElementUtils.BuildElementName("UIInput", entity), uiInputComponent.Input.TextField.name);
            Assert.IsTrue(uiInputComponent.Input.TextField.ClassListContains("dcl-input"));
            Assert.AreEqual(PickingMode.Position, uiInputComponent.Input.TextField.pickingMode);
            Assert.IsTrue(uiTransformComponent.Transform.Contains(uiInputComponent.Input.TextField));
            Assert.IsNotNull(uiInputComponent.Input.TextField);
            Assert.IsNotNull(uiInputComponent.Input.Placeholder);
        }

        [Test]
        public void UpdateUIInput()
        {
            // Arrange
            var input = new PBUiInput();
            world.Add(entity, input);
            system.Update(0);
            const int NUMBER_OF_UPDATES = 3;

            for (var i = 0; i < NUMBER_OF_UPDATES; i++)
            {
                // Act
                input.Value = $"Test text {i}";
                input.FontSize = i + 1;
                input.TextAlign = (TextAlignMode) i;
                input.Disabled = i % 2 == 0;
                input.IsDirty = true;
                system.Update(0);

                // Assert
                ref UIInputComponent uiInputComponent = ref world.Get<UIInputComponent>(entity);
                Assert.AreEqual(input.Value, uiInputComponent.Input.TextField.value);
                Assert.IsTrue(input.GetFontSize() == uiInputComponent.Input.TextField.style.fontSize);
                Assert.IsTrue(input.GetTextAlign() == uiInputComponent.Input.TextField.style.unityTextAlign);
            }
        }
    }
}
