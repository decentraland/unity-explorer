using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIInput;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIInputInstantiationSystemShould : UnitySystemTestBase<UIInputInstantiationSystem>
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
                    { typeof(UIInputComponent), new ComponentPool.WithDefaultCtor<UIInputComponent>() },
                }, null);

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            system = new UIInputInstantiationSystem(world, poolsRegistry, ecsToCRDTWriter, Substitute.For<IInputBlock>(), new []{new StyleFontDefinition()});
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, new CRDTEntity(500));
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
            Assert.AreEqual(UiElementUtils.BuildElementName("UIInput", entity), uiInputComponent.TextField.name);
            Assert.IsTrue(uiInputComponent.TextField.ClassListContains("dcl-input"));
            Assert.AreEqual(PickingMode.Position, uiInputComponent.TextField.pickingMode);
            Assert.IsTrue(uiTransformComponent.Transform.Contains(uiInputComponent.TextField));
            Assert.IsNotNull(uiInputComponent.TextField);
            Assert.IsNotNull(uiInputComponent.Placeholder);
            Assert.IsNotNull(uiInputComponent.TextElement);
        }

        [Test]
        public void ApplyDefaultUiTransformValuesOnInstantiation()
        {
            // Arrange
            var input = new PBUiInput();
            world.Add(entity, input);

            // Act
            system.Update(0);

            // Assert - default transform values should be applied on instantiation
            var transformStyle = uiTransformComponent.Transform.style;
            Assert.AreEqual(Overflow.Hidden, transformStyle.overflow.value);
            Assert.AreEqual(new StyleLength(10f), transformStyle.borderBottomLeftRadius);
            Assert.AreEqual(new StyleLength(10f), transformStyle.borderBottomRightRadius);
            Assert.AreEqual(new StyleLength(10f), transformStyle.borderTopLeftRadius);
            Assert.AreEqual(new StyleLength(10f), transformStyle.borderTopRightRadius);
            Assert.AreEqual(new StyleFloat(1f), transformStyle.borderTopWidth);
            Assert.AreEqual(new StyleFloat(1f), transformStyle.borderRightWidth);
            Assert.AreEqual(new StyleFloat(1f), transformStyle.borderBottomWidth);
            Assert.AreEqual(new StyleFloat(1f), transformStyle.borderLeftWidth);
            Assert.AreEqual(new StyleColor(Color.gray), transformStyle.borderTopColor);
            Assert.AreEqual(new StyleColor(Color.gray), transformStyle.borderRightColor);
            Assert.AreEqual(new StyleColor(Color.gray), transformStyle.borderBottomColor);
            Assert.AreEqual(new StyleColor(Color.gray), transformStyle.borderLeftColor);
        }

        [Test]
        public void ApplyDefaultBackgroundWhenNoPBUiBackground()
        {
            // Arrange
            var input = new PBUiInput();
            world.Add(entity, input);

            // Act
            system.Update(0);

            // Assert - white background applied when entity has no PBUiBackground
            Assert.AreEqual(new StyleColor(Color.white), uiTransformComponent.Transform.style.backgroundColor);
        }

        [Test]
        public void NotApplyDefaultBackgroundWhenPBUiBackgroundExists()
        {
            // Arrange - create a new entity with PBUiBackground
            var newEntity = world.Create();
            var newUiTransform = AddUITransformToEntity(newEntity);
            world.Add(newEntity, new CRDTEntity(501));
            world.Add(newEntity, new PBUiBackground());
            world.Add(newEntity, new PBUiInput());

            // Act
            system.Update(0);

            // Assert - background should NOT be overridden to white
            Assert.AreNotEqual(new StyleColor(Color.white), newUiTransform.Transform.style.backgroundColor);
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
                Assert.IsTrue(input.GetTextAlign() == uiInputComponent.TextElement.style.unityTextAlign);
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void UpdateUIInputDisabledState(bool disabled)
        {
            // Arrange
            var input = new PBUiInput { Disabled = false };
            world.Add(entity, input);
            system.Update(0);

            // Act
            input.Disabled = disabled;
            input.IsDirty = true;
            system.Update(0);

            // Assert
            ref UIInputComponent uiInputComponent = ref world.Get<UIInputComponent>(entity);
            Assert.AreEqual(disabled ? PickingMode.Ignore : PickingMode.Position, uiInputComponent.TextField.pickingMode);
            Assert.AreEqual(!disabled, uiInputComponent.TextField.enabledSelf);
        }

        [Test]
        public void UpdateUIInputTransformDefaultsWhenDirty()
        {
            // Arrange
            var input = new PBUiInput();
            world.Add(entity, input);
            system.Update(0);

            ref PBUiTransform pbUiTransform = ref world.Get<PBUiTransform>(entity);
            pbUiTransform.IsDirty = true;

            // Act
            system.Update(0);

            // Assert - defaults should be reapplied (overflow hidden is always set)
            Assert.AreEqual(Overflow.Hidden, uiTransformComponent.Transform.style.overflow.value);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TriggerInputResults(bool isSubmit)
        {
            // Arrange
            const string TEST_VALUE = "Test text";
            var input = new PBUiInput
            {
                Value = TEST_VALUE,
                IsDirty = true,
            };
            world.Add(entity, input);
            system.Update(0);

            ref UIInputComponent uiInputComponent = ref world.Get<UIInputComponent>(entity);
            uiInputComponent.IsOnValueChangedTriggered = !isSubmit;
            uiInputComponent.IsOnSubmitTriggered = isSubmit;

            // Act
            system.Update(0);

            // Assert
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBUiInputResult, (bool, string)>>(), Arg.Any<CRDTEntity>(), (isSubmit, TEST_VALUE));
            Assert.IsFalse(uiInputComponent.IsOnValueChangedTriggered);
            Assert.IsFalse(uiInputComponent.IsOnSubmitTriggered);
        }
    }
}
