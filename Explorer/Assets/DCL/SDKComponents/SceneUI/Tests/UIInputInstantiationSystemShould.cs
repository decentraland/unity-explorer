using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIInput;
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
                    { typeof(TextField), new ComponentPool<TextField>() },
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
            Assert.AreEqual($"UIInput (Entity {entity.Id})", uiInputComponent.TextField.name);
            Assert.IsTrue(uiInputComponent.TextField.ClassListContains("dcl-input"));
            Assert.AreEqual(PickingMode.Position, uiInputComponent.TextField.pickingMode);
            Assert.IsTrue(uiTransformComponent.Transform.Contains(uiInputComponent.TextField));
            Assert.IsNotNull(uiInputComponent.TextField);
            Assert.IsNotNull(uiInputComponent.Placeholder);
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
                Assert.AreEqual(input.Value, uiInputComponent.TextField.value);
                Assert.IsTrue(input.GetFontSize() == uiInputComponent.TextField.style.fontSize);
                Assert.IsTrue(input.GetTextAlign() == uiInputComponent.TextField.style.unityTextAlign);
            }
        }
    }
}
