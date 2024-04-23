using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIText;
using DCL.SDKComponents.SceneUI.Utils;
using Decentraland.Common;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITextInstantiationSystemShould : UnitySystemTestBase<UITextInstantiationSystem>
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
                    { typeof(Label), new ComponentPool.WithDefaultCtor<Label>() },
                }, null);

            system = new UITextInstantiationSystem(world, poolsRegistry);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
        }

        [Test]
        public void InstantiateUIText()
        {
            // Arrange
            var input = new PBUiText();

            // Act
            world.Add(entity, input);
            system.Update(0);

            // Assert
            ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
            Assert.IsNotNull(uiTextComponent.Label);
            Assert.AreEqual(UiElementUtils.BuildElementName("UIText", entity), uiTextComponent.Label.name);
            Assert.AreEqual(PickingMode.Ignore, uiTextComponent.Label.pickingMode);
            Assert.IsTrue(uiTransformComponent.Transform.Contains(uiTextComponent.Label));
        }

        [Test]
        public void UpdateUIText()
        {
            // Arrange
            var input = new PBUiText();
            world.Add(entity, input);
            system.Update(0);
            const int NUMBER_OF_UPDATES = 3;

            for (var i = 0; i < NUMBER_OF_UPDATES; i++)
            {
                // Act
                input.Value = $"Test text {i}";
                input.Color = new Color4 { R = i, G = 1, B = 1, A = 1 };
                input.FontSize = i + 1;
                input.TextAlign = (TextAlignMode) i;
                input.IsDirty = true;
                system.Update(0);

                // Assert
                ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
                Assert.AreEqual(input.Value, uiTextComponent.Label.text);
                Assert.IsTrue(input.GetColor() == uiTextComponent.Label.style.color);
                Assert.IsTrue(input.GetFontSize() == uiTextComponent.Label.style.fontSize);
                Assert.IsTrue(input.GetTextAlign() == uiTextComponent.Label.style.unityTextAlign);
            }
        }
    }
}
