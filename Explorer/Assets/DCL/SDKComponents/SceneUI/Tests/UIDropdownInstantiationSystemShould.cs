using CRDT;
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
using UnityEngine;
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
        private PBUiDropdown input;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(UIDropdownComponent), new ComponentPool.WithDefaultCtor<UIDropdownComponent>() },
                }, null);

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            system = new UIDropdownInstantiationSystem(world, poolsRegistry, ecsToCRDTWriter, new []{new StyleFontDefinition()});
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, new CRDTEntity(500));

            input = new PBUiDropdown();
            input.Options.Add("TestOption1");
            input.Options.Add("TestOption2");
            input.Options.Add("TestOption3");
            world.Add(entity, input);
            system.Update(0);
        }

        [Test]
        public void InstantiateUIDropdown()
        {
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
        public void ApplyDefaultUiTransformValuesOnInstantiation()
        {
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

            var newInput = new PBUiDropdown();
            newInput.Options.Add("Option1");
            world.Add(newEntity, newInput);

            // Act
            system.Update(0);

            // Assert - background should NOT be overridden to white
            Assert.AreNotEqual(new StyleColor(Color.white), newUiTransform.Transform.style.backgroundColor);
        }

        [Test]
        public void UpdateUIDropdown()
        {
            // Arrange
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

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void UpdateUIDropdownDisabledState(bool disabled)
        {
            // Arrange
            input.Disabled = disabled;
            input.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
            Assert.AreEqual(disabled ? PickingMode.Ignore : PickingMode.Position, uiDropdownComponent.DropdownField.pickingMode);
            Assert.AreEqual(!disabled, uiDropdownComponent.DropdownField.enabledSelf);
        }

        [Test]
        public void UpdateUIDropdownSelectedIndex()
        {
            // Arrange
            ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
            input.SelectedIndex = 1;
            input.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(1, uiDropdownComponent.LastIndexSetByScene);
            Assert.AreEqual("TestOption2", uiDropdownComponent.DropdownField.value);
        }

        [Test]
        public void NotReapplySelectedIndexWhenUnchanged()
        {
            // Arrange - first update sets the index
            ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
            input.SelectedIndex = 2;
            input.IsDirty = true;
            system.Update(0);

            // Act - second update with same index should not change value
            input.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.AreEqual(2, uiDropdownComponent.LastIndexSetByScene);
            Assert.AreEqual("TestOption3", uiDropdownComponent.DropdownField.value);
        }

        [Test]
        public void UpdateUIDropdownTransformDefaultsWhenDirty()
        {
            // Arrange - modify PBUiTransform to be dirty
            ref PBUiTransform pbUiTransform = ref world.Get<PBUiTransform>(entity);
            pbUiTransform.IsDirty = true;

            // Act
            system.Update(0);

            // Assert - defaults should be reapplied (overflow hidden is always set)
            Assert.AreEqual(Overflow.Hidden, uiTransformComponent.Transform.style.overflow.value);
        }

        [Test]
        public void TriggerDropdownResults()
        {
            // Arrange
            input.IsDirty = true;
            system.Update(0);
            const int TEST_INDEX = 1;
            ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
            uiDropdownComponent.DropdownField.index = TEST_INDEX;
            uiDropdownComponent.IsOnValueChangedTriggered = true;
            system.Update(0);

            // Act
            system.Update(0);

            // Assert
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBUiDropdownResult, int>>(), Arg.Any<CRDTEntity>(), TEST_INDEX);
            Assert.IsFalse(uiDropdownComponent.IsOnValueChangedTriggered);
        }
    }
}
